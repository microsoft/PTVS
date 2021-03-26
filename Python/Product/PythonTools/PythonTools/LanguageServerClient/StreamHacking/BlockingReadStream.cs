using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.PythonTools.LanguageServerClient.StreamHacking {
    // This stream blocks forever so that it can't be read from
    class BlockingReadStream : System.IO.Stream {
        private System.IO.MemoryStream _baseStream = new System.IO.MemoryStream();
        private AsyncManualResetEvent _event = new AsyncManualResetEvent();
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => 1000; // Static as we never really ready

        public override long Position { get => _baseStream.Position; set => _baseStream.Position = value; }

        public override void Flush() => _baseStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _baseStream.Read(buffer, offset, count);
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, System.AsyncCallback callback, object state) {
            // Never finish reading.
            return _event.WaitAsync();
        }
        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
        public override void SetLength(long value) => _baseStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    }
}
