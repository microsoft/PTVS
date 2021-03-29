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
        private Func<StreamData, StreamData> writeHandler;
        private Action<StreamData> readHandler;

        public StreamIntercepter(Stream stream, Func<StreamData, StreamData> writeHandler, Action<StreamData> readHandler) {
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
            var result = baseStream.Read(buffer, offset, count);
            var args = new StreamData { bytes = buffer, offset = offset, count = result };
            readHandler.Invoke(args);
            return result;
        }
        public override long Seek(long offset, SeekOrigin origin) => baseStream.Seek(offset, origin);
        public override void SetLength(long value) => baseStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) {
            var writeHandlerResult = writeHandler.Invoke(new StreamData{ bytes = buffer, offset = offset, count = count });
            baseStream.Write(writeHandlerResult.bytes, writeHandlerResult.offset, writeHandlerResult.count);
        }
    }
}
