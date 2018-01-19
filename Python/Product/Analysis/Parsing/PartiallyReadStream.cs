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
using System.IO;
using System.Linq;

namespace Microsoft.PythonTools.Parsing {
    /// <summary>
    /// Creates a stream out of a some bytes we've read and the stream we read them from.  This allows us to
    /// not require a seekable stream for our parser.
    /// </summary>
    class PartiallyReadStream : Stream {
        private readonly byte[] _readBytes;
        private readonly Stream _stream;
        private long _position;

        public PartiallyReadStream(IEnumerable<byte> readBytes, Stream stream) {
            _readBytes = readBytes?.ToArray() ?? throw new ArgumentNullException(nameof(readBytes));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            if (disposing) {
                _stream.Dispose();
            }
        }

        public override bool CanRead {
            get { return true; }
        }

        public override bool CanSeek {
            get { return false; }
        }

        public override bool CanWrite {
            get { return false; }
        }

        public override void Flush() {
            throw new InvalidOperationException();
        }

        public override long Length {
            get { return _stream.Length;  }
        }

        public override long Position {
            get {
                if (_position == -1) {
                    return _stream.Position;
                }
                return _position;
            }
            set {
                throw new InvalidOperationException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count) {
            if (_position == -1) {
                return _stream.Read(buffer, offset, count);
            } else {
                int bytesRead = 0;
                for (int i = 0; i < count && _position < _readBytes.Length; i++) {
                    buffer[i + offset] = _readBytes[(int)_position];
                    _position++;
                    bytesRead++;
                }

                if (_position == _readBytes.LongLength) {
                    _position = -1;
                    if (bytesRead != count) {
                        var res = _stream.Read(buffer, offset + bytesRead, count - bytesRead);
                        if (res == -1) {
                            return bytesRead;
                        }
                        return res + bytesRead;
                    }
                }
                return bytesRead;
            }
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value) {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new InvalidOperationException();
        }
    }
}
