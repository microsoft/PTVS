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

using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools {
    internal class SnapshotMultipleSpanSourceCodeReader : TextReader, ISnapshotTextReader {
        private readonly NormalizedSnapshotSpanCollection _spans;
        private readonly SnapshotSpanSourceCodeReader[] _readers;
        private int _index;

        internal SnapshotMultipleSpanSourceCodeReader(NormalizedSnapshotSpanCollection spans) {
            _spans = spans;
            _readers = spans.Select(s => new SnapshotSpanSourceCodeReader(s)).ToArray();
            _index = 0;
        }

        #region TextReader

        public override void Close() {
            foreach (var r in _readers) {
                r.Close();
            }
            base.Close();
        }

        public int EditorPosition {
            get { return _readers[_index].Position; }
        }

        public void MoveTo(int line, int column) {
            Reset();

            for (int i = 0; i < line; i++) {
                ReadLine();
            }
            for (int i = 0; i < column; i++) {
                Read();
            }
        }

        public override int Peek() {
            while (_index < _readers.Length) {
                var c = _readers[_index].Peek();
                if (c != -1) {
                    return c;
                }
                _index++;
            }
            return -1;
        }

        public override int Read() {
            while (_index < _readers.Length) {
                var c = _readers[_index].Read();
                if (c != -1) {
                    return c;
                }
                _index++;
            }
            return -1;
        }

        public override int Read(char[] buffer, int index, int count) {
            int read = 0;
            while (_index < _readers.Length && count > 0) {
                read += _readers[_index].Read(buffer, index + read, count - read);
                _index++;
            }
            return read;
        }

        public override string ReadLine() {
            var text = new StringBuilder();
            while (_index <= _readers.Length) {
                var c = Read();
                if (c == -1) {
                    break;
                }
                if (c == '\r' || c == '\n') {
                    if (Peek() == '\n') {
                        Read();
                    }
                    break;
                }
                text.Append(c);
            }
            if (_index == _readers.Length && text.Length == 0) {
                return null;
            }
            return text.ToString();
        }

        public override string ReadToEnd() {
            var result = new StringBuilder();
            foreach (var reader in _readers) {
                result.Append(reader.ReadToEnd());
            }
            _index = _readers.Length - 1;
            return result.ToString();
        }

        #endregion

        internal void Reset() {
            foreach (var reader in _readers) {
                reader.Reset();
            }
            _index = 0;
        }

        #region ISnapshotTextReader Members

        public ITextSnapshot Snapshot {
            get { return _spans.First().Snapshot; }
        }

        #endregion
    }
}
