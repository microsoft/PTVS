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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.Cdp {
    public class Connection : IDisposable {
        private readonly object _cacheLock = new object();
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1);
        private readonly Dictionary<int, RequestInfo> _requestCache;
        private readonly Dictionary<string, Type> _types = new Dictionary<string, Type>();
        private readonly Func<Request, Task<Response>> _requestHandler;
        private readonly Stream _writer, _reader;
        private int _seq;
        private static char[] _headerSeperator = new[] { ':' };

        /// <summary>
        /// Creates a new connection object for doing client/server communication.  
        /// </summary>
        /// <param name="writer">The stream that we write to for sending requests and responses.</param>
        /// <param name="reader">The stream that we read from for reading responses and events.</param>
        /// <param name="requestHandler">The callback that is invoked when a request is received.</param>
        /// <param name="types">A set of registered types for receiving requests and events.  The key is "request.command_name" or "event.event_name"
        /// where command_name and event_name correspond to the fields in the Request and Event objects.  The type is the type of object
        /// which will be deserialized and instantiated.  If a type is not registered a GenericRequest or GenericEvent object will be created
        /// which will include the complete body of the request as a dictionary.</param>
        public Connection(Stream writer, Stream reader, Func<Request, Task<Response>> requestHandler = null,
            Dictionary<string, Type> types = null) {
            _requestCache = new Dictionary<int, RequestInfo>();
            _requestHandler = requestHandler;
            _types = types;
            _writer = writer;
            _reader = reader;
            Task.Run(() => ProcessMessages());
        }

        /// <summary>
        /// When a fire and forget notifcation is received from the other side this event is raised.
        /// </summary>
        public event EventHandler<EventReceivedEventArgs> EventReceived;

        /// <summary>
        /// Sends a request from the client to the listening server.
        /// </summary>
        public async Task<RequestInfo<T>> SendRequestAsync<T>(Request<T> request, CancellationToken cancellationToken = default(CancellationToken))
            where T : Response, new() {
            int seq = Interlocked.Increment(ref _seq);

            var r = new RequestInfo<T>(this, request, seq);
            if (cancellationToken.CanBeCanceled) {
                cancellationToken.Register(() => r._task.TrySetCanceled());
            }

            lock (_cacheLock) {
                _requestCache[seq] = r;
            }

            await SendMessage(new Packet() { body = r.Request, seq = seq, type = PacketType.Request });
            return await r._task.Task;
        }

        /// <summary>
        /// Send a fire and forget event to the other side.
        /// </summary>
        /// <param name="eventValue">The event value to be sent.</param>
        public Task SendEventAsync(Event eventValue) {
            int seq = Interlocked.Increment(ref _seq);

            return SendMessage(new Packet() { body = eventValue, seq = seq, type = PacketType.Event });
        }

        /// <summary>
        /// Reads a single message from the protocol buffer.  First reads in any headers until a blank
        /// line is received.  Then reads in the body of the message.  The headers must include a Content-Length
        /// header specifying the length of the body.
        /// </summary>
        private async Task<string> ReadPacket(StreamReader reader) {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string line;
            while ((line = await reader.ReadLineAsync()) != null) {
                if (String.IsNullOrEmpty(line)) {
                    // end of headers for this request...
                    break;
                }
                var split = line.Split(_headerSeperator, 2);
                if (split.Length != 2) {
                    await WriteError("Malformed header, expected 'name: value'");
                }
                headers[split[0]] = split[1];
            }

            if (line == null) {
                return null;
            }

            string contentLengthStr;
            int contentLength;

            if (!headers.TryGetValue(Headers.ContentLength, out contentLengthStr)) {
                await WriteError("Content-Length not specified on request");
            }

            if (!Int32.TryParse(contentLengthStr, out contentLength) || contentLength < 0) {
                await WriteError("Invalid Content-Length: " + contentLengthStr);
            }

            char[] buffer = new char[contentLength];
            await reader.ReadAsync(buffer, 0, contentLength).ConfigureAwait(false);

            return new string(buffer);
        }

        private async void ProcessMessages() {
            try {
                var reader = new StreamReader(_reader, Encoding.UTF8);
                string line;
                while ((line = await ReadPacket(reader)) != null) {
                    var packet = JsonConvert.DeserializeObject<JObject>(line);

                    var body = packet["body"];
                    var type = packet["type"].ToObject<string>();
                    var seq = packet["seq"].ToObject<int?>();
                    if (seq == null) {
                        await WriteError("Missing sequence number").ConfigureAwait(false);
                    }

                    switch (type) {
                        case PacketType.Request:
                            var command = body["command"].ToObject<string>();
                            Request request;
                            Type requestType;
                            if (command != null &&
                                _types != null &&
                                _types.TryGetValue("request." + command, out requestType)) {
                                // We have a strongly typed request type registered, use that...
                                request = body.ToObject(requestType) as Request;
                            } else {
                                // There's no strogly typed request type, give the user a generic
                                // request object and they can look through the dictionary.
                                request = new GenericRequest() {
                                    command = command,
                                    body = body.ToObject<Dictionary<string, object>>()
                                };
                            }

                            await SendResponseAsync(seq.Value, await _requestHandler(request));
                            break;
                        case PacketType.Response:
                            RequestInfo r;

                            lock (_cacheLock) {
                                _requestCache.TryGetValue(seq.Value, out r);
                                r.SetResponse(body);
                            }
                            break;
                        case PacketType.Event:
                            var name = body["name"].ToObject<string>();
                            Event eventObj;
                            if (name != null &&
                                _types != null &&
                                _types.TryGetValue("event." + name, out requestType)) {
                                // We have a strongly typed event type registered, use that.
                                eventObj = body.ToObject(requestType) as Event;
                            } else {
                                // We have no strongly typed event type, so give the user a 
                                // GenericEvent and they can look through the body manually.
                                eventObj = new GenericEvent() {
                                    name = name,
                                    body = body.ToObject<Dictionary<string, object>>()
                                };
                            }

                            EventReceived?.Invoke(this, new EventReceivedEventArgs(eventObj));
                            break;
                        default:
                            await WriteError("Bad packet type: " + type ?? "<null>").ConfigureAwait(false);
                            break;
                    }

                }
            } catch (ObjectDisposedException) {
            }
        }

        private async Task SendResponseAsync(int sequence, Response response, CancellationToken cancellationToken = default(CancellationToken)) {
            var task = new TaskCompletionSource<Response>();
            if (cancellationToken.CanBeCanceled) {
                cancellationToken.Register(() => task.TrySetCanceled());
            }

            await SendMessage(new Packet() { body = response, seq = sequence, type = PacketType.Response });
        }


        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing) {
            if (disposing) {
                _writer.Dispose();
                _reader.Dispose();
                lock (_cacheLock) {
                    foreach (var r in _requestCache.Values) {
                        r.Cancel();
                    }
                }
            }
        }

        /// <summary>
        /// Sends a single message across the wire.
        /// </summary>
        private async Task SendMessage(Packet packet) {
            var str = JsonConvert.SerializeObject(packet);
            var bytes = Encoding.UTF8.GetBytes(str);
            await _writeLock.WaitAsync();
            try {
                var contentLengthStr = "Content-Length: " + bytes.Length + "\n\n";
                var contentLength = Encoding.UTF8.GetBytes(contentLengthStr);

                await _writer.WriteAsync(contentLength, 0, contentLength.Length).ConfigureAwait(false);
                await _writer.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                await _writer.FlushAsync().ConfigureAwait(false);
            } finally {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Writes an error on a malformed request.
        /// </summary>
        private async Task WriteError(string message) {
            await SendMessage(
                new Packet() {
                    seq = Interlocked.Increment(ref _seq),
                    type = PacketType.Error,
                    body = new Dictionary<string, object>() { { "message", message } }
                }
            );
        }

        internal void ClearSequence(int seq) {
            lock (_cacheLock) {
                RequestInfo r;
                if (_requestCache.TryGetValue(seq, out r)) {
                    _requestCache.Remove(seq);
                }
            }
        }

        class Packet {
            public string type;
            public int seq;
            public object body;
        }

        private class PacketType {
            public const string Request = "request";
            public const string Response = "response";
            public const string Event = "event";
            public const string Error = "error";
        }

        private class Headers {
            public const string ContentLength = "Content-Length";
        }
    }
}

