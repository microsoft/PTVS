// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.Ipc.Json {
    public sealed class Connection : IDisposable {
        private readonly object _cacheLock = new object();
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1);
        private readonly Dictionary<int, RequestInfo> _requestCache;
        private readonly Dictionary<string, Type> _types;
        private readonly Func<RequestArgs, Func<Response, Task>, Task> _requestHandler;
        private readonly bool _disposeWriter, _disposeReader;
        private readonly Stream _writer, _reader;
        private readonly TextWriter _basicLog;
        private readonly TextWriter _logFile;
        private readonly object _logFileLock;
        private int _seq;
        private static readonly char[] _headerSeparator = new[] { ':' };

        // Exposed settings for tests
        internal static bool AlwaysLog = false;
        internal static string LoggingBaseDirectory = Path.GetTempPath();

        private const string LoggingRegistrySubkey = @"Software\Microsoft\PythonTools\ConnectionLog";

        private static readonly Encoding TextEncoding = new UTF8Encoding(false);

        /// <summary>
        /// Creates a new connection object for doing client/server communication.  
        /// </summary>
        /// <param name="writer">The stream that we write to for sending requests and responses.</param>
        /// <param name="disposeWriter">The writer stream is disposed when this object is disposed.</param>
        /// <param name="reader">The stream that we read from for reading responses and events.</param>
        /// <param name="disposeReader">The reader stream is disposed when this object is disposed.</param>
        /// <param name="requestHandler">The callback that is invoked when a request is received.</param>
        /// <param name="types">A set of registered types for receiving requests and events.  The key is "request.command_name" or "event.event_name"
        /// where command_name and event_name correspond to the fields in the Request and Event objects.  The type is the type of object
        /// which will be deserialized and instantiated.  If a type is not registered a GenericRequest or GenericEvent object will be created
        /// which will include the complete body of the request as a dictionary.
        /// </param>
        /// <param name="connectionLogKey">Name of registry key used to determine if we should log messages and exceptions to disk.</param>
        /// <param name="basicLog">Text writer to use for basic logging (message ids only). If <c>null</c> then output will go to <see cref="Debug"/>.</param>
        public Connection(
            Stream writer,
            bool disposeWriter,
            Stream reader,
            bool disposeReader,
            Func<RequestArgs, Func<Response, Task>, Task> requestHandler = null,
            Dictionary<string, Type> types = null,
            string connectionLogKey = null,
            TextWriter basicLog = null
        ) {
            _requestCache = new Dictionary<int, RequestInfo>();
            _requestHandler = requestHandler;
            _types = types;
            _writer = writer;
            _disposeWriter = disposeWriter;
            _reader = reader;
            _disposeReader = disposeReader;
            _basicLog = basicLog ?? new DebugTextWriter();
            _logFile = OpenLogFile(connectionLogKey, out var filename);
            // FxCop won't let us lock a MarshalByRefObject, so we create
            // a plain old object that we can log against.
            if (_logFile != null) {
                _logFileLock = new object();
                LogFilename = filename;
            }
        }

        public string LogFilename { get; }

        /// <summary>
        /// Opens the log file for this connection. The log must be enabled in
        /// the registry under HKCU\Software\Microsoft\PythonTools\ConnectionLog
        /// with the connectionLogKey value set to a non-zero integer or
        /// non-empty string.
        /// </summary>
        private static TextWriter OpenLogFile(string connectionLogKey, out string filename) {
            filename = null;
            if (!AlwaysLog) {
                if (string.IsNullOrEmpty(connectionLogKey)) {
                    return null;
                }

                using (var root = Win32.Registry.CurrentUser.OpenSubKey(LoggingRegistrySubkey, false)) {
                    var value = root?.GetValue(connectionLogKey);
                    int? asInt = value as int?;
                    if (asInt.HasValue) {
                        if (asInt.GetValueOrDefault() == 0) {
                            // REG_DWORD but 0 means no logging
                            return null;
                        }
                    } else if (string.IsNullOrEmpty(value as string)) {
                        // Empty string or no value means no logging
                        return null;
                    }
                }
            }

            var filenameBase = Path.Combine(
                LoggingBaseDirectory,
                string.Format("PythonTools_{0}_{1}_{2:yyyyMMddHHmmss}", connectionLogKey, Process.GetCurrentProcess().Id.ToString(), DateTime.Now)
            );

            filename = filenameBase + ".log";
            for (int counter = 0; counter < int.MaxValue; ++counter) {
                try {
                    var file = new FileStream(filename, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
                    return new StreamWriter(file, TextEncoding);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
                filename = string.Format("{0}_{1}.log", filenameBase, ++counter);
            }
            return null;
        }

        private void LogToDisk(string message) {
            if (_logFile == null || _logFileLock == null) {
                return;
            }
            try {
                lock (_logFileLock) {
                    _logFile.WriteLine(message);
                    _logFile.Flush();
                }
            } catch (IOException) {
            } catch (ObjectDisposedException) {
            }
        }

        private void LogToDisk(Exception ex) {
            if (_logFile == null || _logFileLock == null) {
                return;
            }
            // Should have immediately logged a message before, so the exception
            // will have context automatically. Stack trace will not be
            // interesting because of the async-ness.
            LogToDisk(string.Format("{0}: {1}", ex.GetType().Name, ex.Message));
        }

        /// <summary>
        /// When a fire and forget notification is received from the other side this event is raised.
        /// </summary>
        public event EventHandler<EventReceivedEventArgs> EventReceived;

        /// <summary>
        /// When a fire and forget error notification is received from the other side this event is raised.
        /// </summary>
        public event EventHandler<ErrorReceivedEventArgs> ErrorReceived;

        /// <summary>
        /// Sends a request from the client to the listening server.
        /// 
        /// All request payloads inherit from Request&lt;TResponse&gt; where the TResponse generic parameter
        /// specifies the .NET type used for serializing the response.
        /// </summary>
        /// <param name="request">Request to send to the server.</param>
        /// <param name="onResponse">
        /// An action that you want to invoke after the SendRequestAsync task
        /// has completed, but before the message processing loop reads the
        /// next message from the read stream. This is currently used to cancel
        /// the message loop immediately on reception of a response. This is
        /// necessary when you want to stop processing messages without closing
        /// the read stream.
        /// </param>
        public async Task<T> SendRequestAsync<T>(Request<T> request, CancellationToken cancellationToken = default(CancellationToken), Action<T> onResponse = null)
            where T : Response, new() {
            int seq = Interlocked.Increment(ref _seq);

            var r = new RequestInfo<T>(request, seq, onResponse);
            if (cancellationToken.CanBeCanceled) {
                cancellationToken.Register(() => r._task.TrySetCanceled());
            }

            lock (_cacheLock) {
                _requestCache[seq] = r;
            }

            T res;
            try {
                _basicLog.WriteLine("Sending request {0}: {1}", seq, request.command);
                await SendMessage(
                    new RequestMessage() {
                        command = r.Request.command,
                        arguments = r.Request,
                        seq = seq,
                        type = PacketType.Request
                    },
                    cancellationToken
                ).ConfigureAwait(false);

                res = await r._task.Task.ConfigureAwait(false);
                if (r.success) {
                    return res;
                }

                throw new FailedRequestException(r.message, res);
            } finally {
                lock (_cacheLock) {
                    _requestCache.Remove(seq);
                }
            }
        }

        /// <summary>
        /// Send a fire and forget event to the other side.
        /// </summary>
        /// <param name="eventValue">The event value to be sent.</param>
        public async Task SendEventAsync(Event eventValue) {
            int seq = Interlocked.Increment(ref _seq);
            _basicLog.WriteLine("Sending event {0}: {1}", seq, eventValue.name);
            try {
                await SendMessage(
                    new EventMessage() {
                        @event = eventValue.name,
                        body = eventValue,
                        seq = seq,
                        type = PacketType.Event
                    },
                    CancellationToken.None
                ).ConfigureAwait(false);
            } catch (ObjectDisposedException) {
            }
        }

        /// <summary>
        /// Returns a task which will process incoming messages.  This can be started on another thread or
        /// in whatever form of synchronization context you like.  StartProcessing is a convenience helper
        /// for starting this running asynchronously using Task.Run.
        /// </summary>
        /// <returns></returns>
        public async Task ProcessMessages() {
            try {
                var reader = new ProtocolReader(_reader);
                while (true) {
                    var packet = await ReadPacketAsJObject(reader);
                    if (packet == null) {
                        break;
                    }

                    var type = packet["type"].ToObject<string>();
                    switch (type) {
                        case PacketType.Request: {
                                var seq = packet["seq"].ToObject<int?>();
                                if (seq == null) {
                                    throw new InvalidDataException("Request is missing seq attribute");
                                }
                                await ProcessRequest(packet, seq);
                            }
                            break;
                        case PacketType.Response:
                            ProcessResponse(packet);
                            break;
                        case PacketType.Event:
                            ProcessEvent(packet);
                            break;
                        case PacketType.Error:
                            ProcessError(packet);
                            break;
                        default:
                            throw new InvalidDataException("Bad packet type: " + type ?? "<null>");
                    }
                }
            } catch (InvalidDataException ex) {
                // UNDONE: Skipping assert to see if that fixes broken tests
                //Debug.Assert(false, "Terminating ProcessMessages loop due to InvalidDataException", ex.Message);
                // TODO: unsure that it makes sense to do this, but it maintains existing behavior
                await WriteError(ex.Message);
            } catch (OperationCanceledException) {
            } catch (ObjectDisposedException) {
            }

            _basicLog.WriteLine("ProcessMessages ended");
        }

        private void ProcessError(JObject packet) {
            var eventBody = packet["body"];
            string message;
            try {
                message = eventBody["message"].Value<string>();
            } catch (Exception e) {
                message = e.Message;
            }
            try {
                ErrorReceived?.Invoke(this, new ErrorReceivedEventArgs(message));
            } catch (Exception e) {
                // TODO: Report unhandled exception?
                Debug.Fail(e.Message);
            }
        }

        private void ProcessEvent(JObject packet) {
            Type requestType;
            var name = packet["event"].ToObject<string>();
            var eventBody = packet["body"];
            Event eventObj;
            try {
                if (name != null &&
                    _types != null &&
                    _types.TryGetValue("event." + name, out requestType)) {
                    // We have a strongly typed event type registered, use that.
                    eventObj = eventBody.ToObject(requestType) as Event;
                } else {
                    // We have no strongly typed event type, so give the user a 
                    // GenericEvent and they can look through the body manually.
                    eventObj = new GenericEvent() {
                        body = eventBody.ToObject<Dictionary<string, object>>()
                    };
                }
            } catch (Exception e) {
                // TODO: Notify receiver of invalid message
                Debug.Fail(e.Message);
                return;
            }
            try {
                EventReceived?.Invoke(this, new EventReceivedEventArgs(name, eventObj));
            } catch (Exception e) {
                // TODO: Report unhandled exception?
                Debug.Fail(e.Message);
            }
        }

        private void ProcessResponse(JObject packet) {
            var body = packet["body"];

            var reqSeq = packet["request_seq"].ToObject<int?>();

            _basicLog.WriteLine("Received response {0}", reqSeq);

            RequestInfo r;
            lock (_cacheLock) {
                // We might not find the entry in the request cache if the CancellationSource
                // passed into SendRequestAsync was signaled before the request
                // was completed.  That's okay, there's no one waiting on the 
                // response anymore.
                if (_requestCache.TryGetValue(reqSeq.Value, out r)) {
                    r.message = packet["message"]?.ToObject<string>() ?? string.Empty;
                    r.success = packet["success"]?.ToObject<bool>() ?? false;
                    r.SetResponse(body);
                }
            }
        }

        private async Task ProcessRequest(JObject packet, int? seq) {
            var command = packet["command"].ToObject<string>();
            var arguments = packet["arguments"];

            Request request;
            Type requestType;
            if (command != null &&
                _types != null &&
                _types.TryGetValue("request." + command, out requestType)) {
                // We have a strongly typed request type registered, use that...
                request = (Request)arguments.ToObject(requestType);
            } else {
                // There's no strongly typed request type, give the user a generic
                // request object and they can look through the dictionary.
                request = new GenericRequest() {
                    body = arguments.ToObject<Dictionary<string, object>>()
                };
            }

            bool success = true;
            string message = null;
            try {
                await _requestHandler(
                    new RequestArgs(command, request),
                    result => SendResponseAsync(seq.Value, command, success, message, result, CancellationToken.None)
                );
            } catch (OperationCanceledException) {
                throw;
            } catch (ObjectDisposedException) {
                throw;
            } catch (Exception e) {
                success = false;
                message = e.ToString();
                Trace.TraceError(message);
                await SendResponseAsync(seq.Value, command, success, message, null, CancellationToken.None).ConfigureAwait(false);
            }
        }

        public void Dispose() {
            if (_disposeWriter) {
                _writer.Dispose();
            }
            if (_disposeReader) {
                _reader.Dispose();
            }
            _writeLock.Dispose();
            lock (_cacheLock) {
                foreach (var r in _requestCache.Values) {
                    r.Cancel();
                }
            }
            try {
                _logFile?.Dispose();
            } catch (ObjectDisposedException) {
            }
        }

        internal static async Task<JObject> ReadPacketAsJObject(ProtocolReader reader) {
            var line = await ReadPacket(reader).ConfigureAwait(false);
            if (line == null) {
                return null;
            }

            string message = "";
            JObject packet = null;
            try {
                // JObject.Parse is more strict than JsonConvert.DeserializeObject<JObject>,
                // the latter happily deserializes malformed json.
                packet = JObject.Parse(line);
            } catch (JsonSerializationException ex) {
                message = ": " + ex.Message;
            } catch (JsonReaderException ex) {
                message = ": " + ex.Message;
            }

            if (packet == null) {
                Debug.WriteLine("Failed to parse {0}{1}", line, message);
                throw new InvalidDataException("Failed to parse packet" + message);
            }

            return packet;
        }

        /// <summary>
        /// Reads a single message from the protocol buffer.  First reads in any headers until a blank
        /// line is received.  Then reads in the body of the message.  The headers must include a Content-Length
        /// header specifying the length of the body.
        /// </summary>
        private static async Task<string> ReadPacket(ProtocolReader reader) {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = new List<string>();
            string line;
            while ((line = await reader.ReadHeaderLineAsync().ConfigureAwait(false)) != null) {
                lines.Add(line ?? "(null)");
                if (String.IsNullOrEmpty(line)) {
                    if (headers.Count == 0) {
                        continue;
                    }
                    // end of headers for this request...
                    break;
                }
                var split = line.Split(_headerSeparator, 2);
                if (split.Length != 2) {
                    // Probably getting an error message, so read all available text
                    var error = line;
                    try {
                        // Encoding is uncertain since this is malformed
                        error += TextEncoding.GetString(await reader.ReadToEndAsync());
                    } catch (ArgumentException) {
                    }
                    throw new InvalidDataException("Malformed header, expected 'name: value'" + Environment.NewLine + error);
                }
                headers[split[0]] = split[1];
            }

            if (line == null) {
                return null;
            }

            string contentLengthStr;
            int contentLength;

            if (!headers.TryGetValue(Headers.ContentLength, out contentLengthStr)) {
                // HACK: Attempting to find problem with message content
                Console.Error.WriteLine("Content-Length not specified on request. Lines follow:");
                foreach (var l in lines) {
                    Console.Error.WriteLine($"> {l}");
                }
                Console.Error.Flush();
                throw new InvalidDataException("Content-Length not specified on request");
            }

            if (!Int32.TryParse(contentLengthStr, out contentLength) || contentLength < 0) {
                throw new InvalidDataException("Invalid Content-Length: " + contentLengthStr);
            }

            var contentBinary = await reader.ReadContentAsync(contentLength);
            if (contentBinary.Length == 0 && contentLength > 0) {
                // The stream was closed, so let's abort safely
                return null;
            }
            if (contentBinary.Length != contentLength) {
                throw new InvalidDataException(string.Format("Content length does not match Content-Length header. Expected {0} bytes but read {1} bytes.", contentLength, contentBinary.Length));
            }

            try {
                var text = TextEncoding.GetString(contentBinary);
                return text;
            } catch (ArgumentException ex) {
                throw new InvalidDataException("Content is not valid UTF-8.", ex);
            }
        }

        private async Task SendResponseAsync(
            int sequence,
            string command,
            bool success,
            string message,
            Response response,
            CancellationToken cancel
        ) {
            int newSeq = Interlocked.Increment(ref _seq);
            _basicLog.WriteLine("Sending response {0}", newSeq);
            await SendMessage(
                new ResponseMessage() {
                    request_seq = sequence,
                    success = success,
                    message = message,
                    command = command,
                    body = response,
                    seq = newSeq,
                    type = PacketType.Response
                },
                cancel
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a single message across the wire.
        /// </summary>
        /// <remarks>
        /// Base protocol defined at https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#base-protocol
        /// </remarks>
        private async Task SendMessage(ProtocolMessage packet, CancellationToken cancel) {
            var str = JsonConvert.SerializeObject(packet, UriJsonConverter.Instance);

            try {
                try {
                    await _writeLock.WaitAsync(cancel).ConfigureAwait(false);
                } catch (ArgumentNullException) {
                    throw new ObjectDisposedException(nameof(_writeLock));
                } catch (ObjectDisposedException) {
                    throw new ObjectDisposedException(nameof(_writeLock));
                }
                try {
                    LogToDisk(str);

                    // The content part is encoded using the charset provided in the Content-Type field.
                    // It defaults to utf-8, which is the only encoding supported right now.
                    var contentBytes = TextEncoding.GetBytes(str);

                    // The header part is encoded using the 'ascii' encoding.
                    // This includes the '\r\n' separating the header and content part.
                    var header = "Content-Length: " + contentBytes.Length + "\r\n\r\n";
                    var headerBytes = Encoding.ASCII.GetBytes(header);

                    await _writer.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
                    await _writer.WriteAsync(contentBytes, 0, contentBytes.Length).ConfigureAwait(false);
                    await _writer.FlushAsync().ConfigureAwait(false);
                } finally {
                    _writeLock.Release();
                }
            } catch (Exception ex) {
                LogToDisk(ex);
                throw;
            }
        }

        /// <summary>
        /// Writes an error on a malformed request.
        /// </summary>
        private async Task WriteError(string message) {
            try {
                await SendMessage(
                    new ErrorMessage() {
                        seq = Interlocked.Increment(ref _seq),
                        type = PacketType.Error,
                        body = new Dictionary<string, object>() { { "message", message } }
                    },
                    CancellationToken.None
                );
                throw new InvalidOperationException(message);
            } catch (ObjectDisposedException) {
            } catch (IOException) {
            }
        }

        /// <summary>
        /// Class used for serializing each packet of information we send across.
        /// 
        /// Each packet consists of a packet type (defined in the PacketType class), a sequence
        /// number (requests and responses have linked sequence numbers), and a body which is 
        /// the rest of the JSON payload.
        /// </summary>
        private class ProtocolMessage {
            public string type;
            public int seq;
        }

        private class RequestMessage : ProtocolMessage {
            public string command;
            public object arguments;
        }

        private class ResponseMessage : ProtocolMessage {
            public int request_seq;
            public bool success;
            public string command;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string message;
            public object body;
        }

        private class EventMessage : ProtocolMessage {
            public string @event;
            public object body;
        }

        private class ErrorMessage : ProtocolMessage {
            public object body;
        }

        private class PacketType {
            public const string Request = "request";
            public const string Response = "response";
            public const string Event = "event";
            public const string Error = "error";
        }

        /// <summary>
        /// Provides constants for known header types, currently just includs the Content-Length
        /// header for specifying the length of the body of the request in bytes.
        /// </summary>
        private class Headers {
            public const string ContentLength = "Content-Length";
        }

        /// <summary>
        /// Base class for tracking state of our requests.  This is a non-generic class so we can have
        /// a dictionary of these and call the abstract methods which are actually implemented by the
        /// generic version.
        /// </summary>
        private abstract class RequestInfo {
            private readonly Request _request;
            private readonly int _sequence;
            public bool success;
            public string message;

            internal RequestInfo(Request request, int sequence) {
                _request = request;
                _sequence = sequence;
            }

            public Request Request => _request;

            internal abstract void SetResponse(JToken obj);
            internal abstract void Cancel();
        }

        /// <summary>
        /// Generic version of the request info type which includes the type information for
        /// the type of response we should return.  This response type is inferred from the
        /// TRespones type parameter of the Request&lt;TResponse&gt; generic type which is
        /// passed on a SendRequestAsync call.  The caller this receives the strongly typed
        /// response without any extra need to specify any information beyond the request
        /// payload.
        /// </summary>
        private sealed class RequestInfo<TResponse> : RequestInfo where TResponse : Response, new() {
            internal readonly TaskCompletionSource<TResponse> _task;
            private readonly Action<TResponse> _postResponseAction;

            internal RequestInfo(Request request, int sequence, Action<TResponse> postResponseAction) : base(request, sequence) {
                // Make it run continuation asynchronously so that the thread calling SetResponse
                // doesn't end up being hijacked to run the code after SendRequest, which would
                // prevent it from processing any more messages.
                _task = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                _postResponseAction = postResponseAction;
            }

            internal override void SetResponse(JToken obj) {
                var res = obj.ToObject<TResponse>();
                _task.TrySetResult(res);
                _postResponseAction?.Invoke(res);
            }

            internal override void Cancel() {
                _task.TrySetCanceled();
            }
        }
    }
}

