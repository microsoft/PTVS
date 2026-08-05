// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION, ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for the specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.LanguageServerClient.StreamHacking {
    /// <summary>
    /// Normalizes Pylance's numeric text synchronization capability for affected Dev18 LSP clients.
    /// All server output is passed through unchanged after the initialize response.
    /// </summary>
    internal sealed class PylanceInitializeResponseStream : Stream {
        private const string ContentLengthHeader = "Content-Length:";
        private const int MaxHeaderLength = 16 * 1024;

        private readonly Stream _baseStream;
        private readonly List<byte> _inputBuffer = new List<byte>();
        private readonly SemaphoreSlim _readLock = new SemaphoreSlim(1, 1);
        private byte[] _pendingOutput = Array.Empty<byte>();
        private int _pendingOutputOffset;
        private int _disposed;
        private bool _initializeResponseProcessed;

        public PylanceInitializeResponseStream(Stream baseStream) {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) {
            ValidateReadArguments(buffer, offset, count);
            if (count == 0) {
                return 0;
            }
            ThrowIfDisposed();

            await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                ThrowIfDisposed();
                if (_pendingOutputOffset >= _pendingOutput.Length) {
                    if (_initializeResponseProcessed && _inputBuffer.Count == 0) {
                        return await _baseStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                    }

                    _pendingOutput = await ReadNextFrameAsync(cancellationToken).ConfigureAwait(false);
                    _pendingOutputOffset = 0;
                    if (!_initializeResponseProcessed) {
                        _pendingOutput = NormalizeInitializeResponse(_pendingOutput);
                    }
                }

                if (_pendingOutput.Length == 0) {
                    return 0;
                }

                var bytesToCopy = Math.Min(count, _pendingOutput.Length - _pendingOutputOffset);
                Buffer.BlockCopy(_pendingOutput, _pendingOutputOffset, buffer, offset, bytesToCopy);
                _pendingOutputOffset += bytesToCopy;
                return bytesToCopy;
            } finally {
                _readLock.Release();
            }
        }

        private async Task<byte[]> ReadNextFrameAsync(CancellationToken cancellationToken) {
            while (true) {
                if (TryTakeFrame(out var frame)) {
                    return frame;
                }
                if (_inputBuffer.Count > MaxHeaderLength && FindHeaderEnd(_inputBuffer) < 0) {
                    throw new InvalidDataException("Pylance returned an LSP header that exceeds the maximum supported length.");
                }

                var buffer = new byte[8192];
                var count = await _baseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (count == 0) {
                    if (_inputBuffer.Count == 0) {
                        return Array.Empty<byte>();
                    }

                    throw new EndOfStreamException("Pylance output ended in the middle of an LSP message.");
                }

                for (var i = 0; i < count; i++) {
                    _inputBuffer.Add(buffer[i]);
                }
            }
        }

        private bool TryTakeFrame(out byte[] frame) {
            frame = null;
            var headerEnd = FindHeaderEnd(_inputBuffer);
            if (headerEnd < 0) {
                return false;
            }
            if (headerEnd > MaxHeaderLength) {
                throw new InvalidDataException("Pylance returned an LSP header that exceeds the maximum supported length.");
            }

            var contentLength = ParseContentLength(_inputBuffer, headerEnd);
            var frameLengthLong = (long)headerEnd + 4 + contentLength;
            if (frameLengthLong > int.MaxValue) {
                throw new InvalidDataException("Pylance returned an LSP message that is too large.");
            }

            var frameLength = (int)frameLengthLong;
            if (_inputBuffer.Count < frameLength) {
                return false;
            }

            frame = _inputBuffer.GetRange(0, frameLength).ToArray();
            _inputBuffer.RemoveRange(0, frameLength);
            return true;
        }

        private byte[] NormalizeInitializeResponse(byte[] frame) {
            var headerEnd = FindHeaderEnd(frame);
            var contentLength = ParseContentLength(frame, headerEnd);
            var messageJson = Encoding.UTF8.GetString(frame, headerEnd + 4, contentLength);
            var message = JObject.Parse(messageJson);
            var capabilities = message["result"]?["capabilities"] as JObject;
            if (message["id"] == null || capabilities == null) {
                return frame;
            }

            _initializeResponseProcessed = true;
            var textDocumentSync = capabilities["textDocumentSync"];
            if (textDocumentSync?.Type != JTokenType.Integer) {
                return frame;
            }

            var syncKind = textDocumentSync.Value<int>();
            textDocumentSync.Replace(new JObject {
                ["openClose"] = true,
                ["change"] = syncKind,
                ["save"] = new JObject {
                    ["includeText"] = false
                }
            });
            return MessageParser.Serialize(message).bytes;
        }

        private void ThrowIfDisposed() {
            if (Volatile.Read(ref _disposed) != 0) {
                throw new ObjectDisposedException(nameof(PylanceInitializeResponseStream));
            }
        }

        private static int FindHeaderEnd(IList<byte> buffer) {
            for (var i = 3; i < buffer.Count; i++) {
                if (buffer[i - 3] == '\r' &&
                    buffer[i - 2] == '\n' &&
                    buffer[i - 1] == '\r' &&
                    buffer[i] == '\n') {
                    return i - 3;
                }
            }

            return -1;
        }

        private static int ParseContentLength(IList<byte> frame, int headerEnd) {
            if (headerEnd < 0) {
                throw new InvalidDataException("Pylance returned an invalid LSP header.");
            }

            var headerBytes = new byte[headerEnd];
            for (var i = 0; i < headerEnd; i++) {
                headerBytes[i] = frame[i];
            }

            var header = Encoding.ASCII.GetString(headerBytes);
            foreach (var line in header.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)) {
                if (line.StartsWith(ContentLengthHeader, StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(line.Substring(ContentLengthHeader.Length).Trim(), out var contentLength) &&
                    contentLength >= 0) {
                    return contentLength;
                }
            }

            throw new InvalidDataException("Pylance returned an LSP message without a valid Content-Length header.");
        }

        private static void ValidateReadArguments(byte[] buffer, int offset, int count) {
            if (buffer == null) {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset < 0 || count < 0 || buffer.Length - offset < count) {
                throw new ArgumentOutOfRangeException();
            }
        }

        public override void Flush() {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0) {
                _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
