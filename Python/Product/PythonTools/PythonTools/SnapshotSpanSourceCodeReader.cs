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

using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools {
    internal partial class SnapshotSpanSourceCodeReader : TextReader, ISnapshotTextReader {
        private readonly SnapshotSpan _span;
        private ITextSnapshot _snapshot;
        private int _position;
        private string _buffer;
        private Span? _bufferSpan;
        private const int BufferSize = 1024;

        internal SnapshotSpanSourceCodeReader(SnapshotSpan span) {
            _span = span;
            _snapshot = span.Snapshot;
            _position = span.Start.Position;
        }

        #region TextReader

        public override void Close() {
            Dispose(true);
        }

        protected override void Dispose(bool disposing) {
            _snapshot = null;
            _position = 0;
            base.Dispose(disposing);
        }

        public override int Peek() {
            CheckDisposed();
            if (_position == End) {
                return -1;
            }
            return _snapshot[_position];
        }

        public override int Read() {
            CheckDisposed();

            if (_position == End) {
                return -1;
            } else if (_bufferSpan == null || !(_position >= _bufferSpan.Value.Start && _position < _bufferSpan.Value.End)) {
                int bufferLength = Math.Min(BufferSize, _snapshot.Length - _position);
                _buffer = _snapshot.GetText(_position, bufferLength);
                _bufferSpan = new Span(_position, bufferLength);
            }

            return _buffer[_position++ - _bufferSpan.Value.Start];
        }

        public override int Read(char[] buffer, int index, int count) {
            int length = End - _position;
            if (length > 0) {
                length = System.Math.Min(length, count);
                _snapshot.CopyTo(_position, buffer, index, length);
                _position += length;
            }
            return length;
        }

        public override string ReadLine() {
            CheckDisposed();

            if (_position == _snapshot.Length) {
                return null;
            }

            var line = _snapshot.GetLineFromPosition(_position);
            _position = line.End.Position;

            return line.GetText();
        }

        public override string ReadToEnd() {
            CheckDisposed();
            int length = End - _position;
            var text = _snapshot.GetText(_position, length);
            _position = End;
            return text;
        }

        #endregion

        internal int Position {
            get { return _position; }
        }

        internal void Reset() {
            CheckDisposed();
            _position = _span.Start.Position;
        }

        private void CheckDisposed() {
            if (_snapshot == null) {
                throw new ObjectDisposedException("This SnapshotSpanSourceCodeReader has been closed");
            }
        }

        private int End {
            get { return _span.End.Position; }
        }

        #region ISnapshotTextReader Members

        public ITextSnapshot Snapshot {
            get { return _snapshot; }
        }

        #endregion
    }
}
