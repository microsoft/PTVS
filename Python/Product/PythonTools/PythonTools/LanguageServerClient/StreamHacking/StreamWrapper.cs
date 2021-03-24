using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.LanguageServerClient.StreamHacking {
    class StreamWrapper : Stream {
        private Stream baseStream;
        private Func<StreamData, StreamData> writeHandler;
        private Action<StreamData> readHandler;

        public StreamWrapper(Stream stream, Func<StreamData, StreamData> writeHandler, Action<StreamData> readHandler) {
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
            var args = new StreamData { bytes = buffer, offset = offset, count = count };
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
