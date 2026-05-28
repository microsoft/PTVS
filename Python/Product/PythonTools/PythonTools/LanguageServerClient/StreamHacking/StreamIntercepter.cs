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
using System.IO;

namespace Microsoft.PythonTools.LanguageServerClient.StreamHacking {
    /// <summary>
    /// This class wraps a stream so that writes to the stream can be changed and reads to the stream can be followed.
    /// </summary>
    class StreamIntercepter : Stream {
        private Stream baseStream;
        private Func<StreamData, Tuple<StreamData, bool>> writeHandler;
        private Action<StreamData> readHandler;

        public StreamIntercepter(Stream stream, Func<StreamData, Tuple<StreamData, bool>> writeHandler, Action<StreamData> readHandler) {
            this.baseStream = stream;
            this.readHandler = readHandler;
            this.writeHandler = writeHandler;
        }

        public override bool CanRead => baseStream.CanRead;

        public override bool CanSeek => baseStream.CanSeek;

        public override bool CanWrite => baseStream.CanWrite;

        public override long Length => baseStream.Length;

        public override long Position { get => baseStream.Position; set => baseStream.Position = value; }

        public override void Flush() {
            try {
                baseStream.Flush();
            } catch (IOException) {
                // Pipe to Pylance is broken (e.g. Pylance crashed).
                // Swallow so StreamJsonRpc can detect the disconnect via its own machinery.
            } catch (ObjectDisposedException) {
            }
        }
        public override int Read(byte[] buffer, int offset, int count) {
            int result;
            try {
                result = baseStream.Read(buffer, offset, count);
            } catch (IOException) {
                // Pipe to Pylance is broken. Returning 0 signals EOF to StreamJsonRpc
                // which will cleanly raise Disconnected.
                return 0;
            } catch (ObjectDisposedException) {
                return 0;
            }
            var args = new StreamData { bytes = buffer, offset = offset, count = result };
            readHandler.Invoke(args);
            return result;
        }
        public override long Seek(long offset, SeekOrigin origin) => baseStream.Seek(offset, origin);
        public override void SetLength(long value) => baseStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) {
            // Compute the handler result outside the try so that exceptions from
            // writeHandler (which is application logic, not pipe I/O) propagate
            // normally and never silently null out the handler.
            StreamData payload;
            bool keepHandler;
            if (writeHandler != null) {
                var writeHandlerResult = writeHandler.Invoke(new StreamData { bytes = buffer, offset = offset, count = count });
                payload = writeHandlerResult.Item1;
                keepHandler = writeHandlerResult.Item2;
            } else {
                payload = new StreamData { bytes = buffer, offset = offset, count = count };
                keepHandler = true;
            }
            try {
                baseStream.Write(payload.bytes, payload.offset, payload.count);
            } catch (IOException) {
                // Pipe to Pylance is broken (e.g. Pylance crashed - ERROR_BROKEN_PIPE 0x8007006d).
                // Swallow so StreamJsonRpc can detect the disconnect via its own machinery
                // rather than throwing on a long async chain that escapes to the dispatcher.
            } catch (ObjectDisposedException) {
            }
            if (writeHandler != null && !keepHandler) {
                writeHandler = null;
            }
        }
    }
}
