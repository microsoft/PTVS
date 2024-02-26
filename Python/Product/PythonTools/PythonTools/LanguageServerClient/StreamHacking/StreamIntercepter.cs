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
        private Func<StreamData, int> readHandler;

        public StreamIntercepter(Stream stream, Func<StreamData, Tuple<StreamData, bool>> writeHandler, Func<StreamData, int> readHandler) {
            this.baseStream = stream;
            this.readHandler = readHandler;
            this.writeHandler = writeHandler;
        }

        public override bool CanRead => baseStream.CanRead;

        public override bool CanSeek => baseStream.CanSeek;

        public override bool CanWrite => baseStream.CanWrite;

        public override long Length => baseStream.Length;

        public override long Position { get => baseStream.Position; set => baseStream.Position = value; }

        public override void Flush() => baseStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) {
            var bytesRead = baseStream.Read(buffer, offset, count);
            var args = new StreamData { bytes = buffer, offset = offset, count = bytesRead };
            var newBytesRead = readHandler.Invoke(args);

            if (newBytesRead != bytesRead) {
                byte[] newBuffer = new byte[newBytesRead];
                Array.Copy(args.bytes, args.offset, newBuffer, 0, newBytesRead);
                buffer = newBuffer;
            }

            return newBytesRead;
        }
        public override long Seek(long offset, SeekOrigin origin) => baseStream.Seek(offset, origin);
        public override void SetLength(long value) => baseStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) {
            if (writeHandler != null) {
                var writeHandlerResult = writeHandler.Invoke(new StreamData { bytes = buffer, offset = offset, count = count });
                baseStream.Write(writeHandlerResult.Item1.bytes, writeHandlerResult.Item1.offset, writeHandlerResult.Item1.count);
                if (!writeHandlerResult.Item2) {
                    writeHandler = null;
                }
            } else {
                baseStream.Write(buffer, offset, count);
            }

        }
    }
}
