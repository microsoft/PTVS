/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Microsoft.PythonTools.Parsing {
    /// <summary>
    /// Creates a stream out of a some bytes we've read and the stream we read them from.  This allows us to
    /// not require a seekable stream for our parser.
    /// </summary>
    class PartiallyReadStream : Stream {
        private readonly List<byte> _readBytes;
        private readonly Stream _stream;
        private long _position;

        public PartiallyReadStream(List<byte> readBytes, Stream stream) {
            _readBytes = readBytes;
            _stream = stream;
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
                for (int i = 0; i < count && _position < _readBytes.Count; i++) {
                    // _position is a long from a stream, but we're a 32-bit process and won't have a 2gb python file
                    buffer[i + offset] = _readBytes[(int)_position];
                    _position++;
                    bytesRead++;
                }

                if (_position == _readBytes.Count) {
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
