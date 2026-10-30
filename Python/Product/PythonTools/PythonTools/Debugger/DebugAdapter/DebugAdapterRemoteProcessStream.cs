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
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.Debugger {
    internal sealed class DebugRemoteAdapterProcessStream : Stream {
        private const int _maximumProtocolHeaderLength = 16 * 1024;
        private const int _maximumInspectedProtocolBodyLength = 64 * 1024;

        private readonly NetworkStream _networkStream;
        private readonly object _protocolLock = new object();
        private readonly MemoryStream _protocolBuffer = new MemoryStream();

        private int _disconnectedSignaled;
        private int _disposed;
        private int _protocolBodyBytesRead;
        private int _protocolBodyLength = -1;

        public DebugRemoteAdapterProcessStream(NetworkStream networkStream) {
            _networkStream = networkStream;
        }

        public override bool CanRead => _networkStream.CanRead;

        public override bool CanSeek => _networkStream.CanSeek;

        public override bool CanWrite => _networkStream.CanWrite;

        public override long Length => _networkStream.Length;

        public override long Position {
            get => _networkStream.Position;
            set => _networkStream.Position = value;
        }

        public override void Flush() => _networkStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) {
            try {
                var bytes = _networkStream.Read(buffer, offset, count);
                if (bytes == 0) {
                    SignalDisconnected();
                    return 0;
                }

                CheckForResponse(buffer, offset, bytes);
                return bytes;
            } catch (IOException ex) when (IsExpectedError(ex.InnerException as SocketException)) {
                // This is a case where the debuggee has exited, but the adapter host attempts to read remaining messages.
                // Returning 0 here will tell the debugger that the stream is empty.
                SignalDisconnected();
            } catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) != 0) {
                SignalDisconnected();
            }
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin) => _networkStream.Seek(offset, origin);

        public override void SetLength(long value) => _networkStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) {
            try {
                _networkStream.Write(buffer, offset, count);
            } catch (IOException ex) when (IsExpectedError(ex.InnerException as SocketException)) {
                // This is a case where the debuggee has exited, but the adapter host attempts to write to it.
                Debug.WriteLine($"Attempt to write after stream is closed.", nameof(DebugRemoteAdapterProcessStream));
                SignalDisconnected();
            } catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) != 0) {
                SignalDisconnected();
            }
        }

        protected override void Dispose(bool disposing) {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0) {
                try {
                    _networkStream.Dispose();
                } finally {
                    SignalDisconnected();
                }
            }

            base.Dispose(disposing);
        }

        private static bool IsExpectedError(SocketException ex) {
            switch (ex?.SocketErrorCode) {
                case SocketError.ConnectionReset:
                case SocketError.ConnectionAborted:
                case SocketError.Interrupted:
                case SocketError.OperationAborted:
                case SocketError.Shutdown:
                case SocketError.NotConnected:
                    return true;
                default:
                    return false;
            }
        }

        private void CheckForResponse(byte[] buffer, int offset, int count) {
            if (Disconnected == null && Initialized == null && LegacyDebugger == null) {
                return;
            }

            lock (_protocolLock) {
                var end = offset + count;
                while (offset < end) {
                    if (_protocolBodyLength < 0) {
                        _protocolBuffer.WriteByte(buffer[offset++]);
                        if (_protocolBuffer.Length > _maximumProtocolHeaderLength) {
                            ResetProtocolMessage();
                            continue;
                        }

                        if (HasProtocolHeaderTerminator()) {
                            var headerBuffer = _protocolBuffer.GetBuffer();
                            var headerLength = checked((int)_protocolBuffer.Length);
                            if (!TryGetContentLength(headerBuffer, headerLength, out _protocolBodyLength)) {
                                ResetProtocolMessage();
                                continue;
                            }

                            _protocolBodyBytesRead = 0;
                            _protocolBuffer.SetLength(0);
                            _protocolBuffer.Position = 0;
                            if (_protocolBodyLength == 0) {
                                ProcessProtocolBody(Array.Empty<byte>(), 0);
                                ResetProtocolMessage();
                            }
                        }

                        continue;
                    }

                    var bodyBytesRemaining = _protocolBodyLength - _protocolBodyBytesRead;
                    var bytesAvailable = end - offset;
                    var bytesToConsume = Math.Min(bodyBytesRemaining, bytesAvailable);
                    var inspectionCapacity = _maximumInspectedProtocolBodyLength - checked((int)_protocolBuffer.Length);
                    var bytesToInspect = Math.Min(bytesToConsume, Math.Max(inspectionCapacity, 0));
                    if (bytesToInspect > 0) {
                        _protocolBuffer.Write(buffer, offset, bytesToInspect);
                    }

                    offset += bytesToConsume;
                    _protocolBodyBytesRead += bytesToConsume;
                    if (_protocolBodyBytesRead == _protocolBodyLength) {
                        if (_protocolBodyLength <= _maximumInspectedProtocolBodyLength) {
                            ProcessProtocolBody(
                                _protocolBuffer.GetBuffer(),
                                checked((int)_protocolBuffer.Length)
                            );
                        }

                        ResetProtocolMessage();
                    }
                }
            }
        }

        private bool HasProtocolHeaderTerminator() {
            var length = checked((int)_protocolBuffer.Length);
            if (length < 4) {
                return false;
            }

            var buffer = _protocolBuffer.GetBuffer();
            return buffer[length - 4] == '\r' &&
                buffer[length - 3] == '\n' &&
                buffer[length - 2] == '\r' &&
                buffer[length - 1] == '\n';
        }

        private static bool TryGetContentLength(byte[] buffer, int count, out int contentLength) {
            var header = Encoding.ASCII.GetString(buffer, 0, count);
            foreach (var line in header.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)) {
                var separator = line.IndexOf(':');
                if (separator <= 0 ||
                    !line.Substring(0, separator).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                return int.TryParse(line.Substring(separator + 1).Trim(), out contentLength) &&
                    contentLength >= 0;
            }

            contentLength = 0;
            return false;
        }

        private void ProcessProtocolBody(byte[] buffer, int count) {
            try {
                var json = JObject.Parse(Encoding.UTF8.GetString(buffer, 0, count));
                if (!json.TryGetValue("command", out var tok)) {
                    json.TryGetValue("event", out tok);
                }

                var name = tok?.Value<string>();
                if (name == "disconnect") {
                    Task.Delay(50).ContinueWith(
                        _ => SignalDisconnected(),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default
                    );
                } else if (name == "initialized") {
                    Task.Run(() => Initialized?.Invoke(this, EventArgs.Empty));
                } else if (name?.StartsWith("legacy", StringComparison.Ordinal) == true) {
                    if (_legacyDebuggerEventCount == 0) {
                        Task.Run(() => LegacyDebugger?.Invoke(this, EventArgs.Empty));
                    }
                    _legacyDebuggerEventCount++;
                }
            } catch (Exception) {
                // Ignore malformed or unsupported protocol messages.
            }
        }

        private void ResetProtocolMessage() {
            _protocolBodyLength = -1;
            _protocolBodyBytesRead = 0;
            _protocolBuffer.SetLength(0);
            _protocolBuffer.Position = 0;
        }

        private void SignalDisconnected() {
            if (Interlocked.Exchange(ref _disconnectedSignaled, 1) == 0) {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler Initialized;
        public event EventHandler Disconnected;
        public event EventHandler LegacyDebugger;
        private int _legacyDebuggerEventCount;
    }
}
