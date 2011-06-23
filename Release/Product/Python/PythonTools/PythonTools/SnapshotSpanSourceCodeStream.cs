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
using System.IO;
using System.Text;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools {
    internal partial class SnapshotSpanSourceCodeStream : Stream, ISnapshotTextReader {
        private readonly SnapshotSpan _span;
        private readonly Encoding _encoding;
        private ITextSnapshot _snapshot;
        private const int BufferSizeInChars = 256;
        private readonly byte[] _buffer;
        private int _bufferLength, _bufferPos;
        private int _curSnapshotPos;
        private int _pos;

        internal SnapshotSpanSourceCodeStream(SnapshotSpan span) {
            ITextDocument doc;
            if (span.Snapshot.TextBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out doc)) {
                _encoding = doc.Encoding;
            } else {
                _encoding = Parser.DefaultEncoding;
            }

            _buffer = new byte[_encoding.GetMaxByteCount(BufferSizeInChars)];

            _span = span;
            _snapshot = span.Snapshot;
            _curSnapshotPos = span.Start.Position;
        }

        #region ISnapshotTextReader Members

        public ITextSnapshot Snapshot {
            get { return _snapshot; }
        }

        #endregion

        #region Stream Members

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
            throw new NotSupportedException();
        }

        public override long Length {
            get { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count) {
            for (int i = 0; i < count; i++) {
                if (_bufferPos == _bufferLength) {
                    int toRead = Math.Min(BufferSizeInChars, _span.End.Position - _curSnapshotPos);
                    var text = _snapshot.GetText(_curSnapshotPos, toRead);

                    _curSnapshotPos += toRead;
                    _bufferLength = _encoding.GetBytes(text, 0, text.Length, _buffer, 0);
                    _bufferPos = 0;

                    if (_bufferLength == 0) {
                        return i;
                    }
                }
                buffer[i + offset] = _buffer[_bufferPos++];
                _pos++;
            }
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }

        public override long Position {
            get {
                return _pos;
            }
            set {
                throw new NotSupportedException();
            }
        }

        #endregion

    }
}
