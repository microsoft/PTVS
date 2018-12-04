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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.PythonTools.Infrastructure {
    sealed class StringListReader : TextReader {
        private readonly IEnumerator<string> _strings;
        private StringReader _current;
        private int _peekBuffer = -1;

        public StringListReader(IEnumerable<string> strings) {
            _strings = strings.GetEnumerator();
            if (_strings.MoveNext()) {
                _current = new StringReader(_strings.Current);
            }
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            if (disposing) {
                _strings.Dispose();
                if (_current != null) {
                    _current.Dispose();
                }
            }
        }

        private bool Next() {
            if (_current != null) {
                _current.Dispose();
                if (_strings.MoveNext()) {
                    _current = new StringReader(_strings.Current);
                    return true;
                } else {
                    _current = null;
                }
            }
            return false;
        }

        public override int Peek() {
            if (_peekBuffer >= 0) {
                return _peekBuffer;
            }
            _peekBuffer = Read();
            return _peekBuffer;
        }

        public override string ReadLine() {
            var r = _current?.ReadLine();
            if (r == null && Next()) {
                r = _current.ReadLine();
            }
            if (_peekBuffer >= 0) {
                r = $"{(char)_peekBuffer}{r ?? ""}";
                _peekBuffer = -1;
            }
            return r;
        }

        public override int Read() {
            if (_current == null) {
                return -1;
            }

            int i;
            if (_peekBuffer >= 0) {
                i = _peekBuffer;
                _peekBuffer = -1;
                return i;
            }

            i = _current.Read();
            if (i < 0 && Next()) {
                return _current.Read();
            }
            return i;
        }

        public override int Read(char[] buffer, int index, int count) {
            if (count == 0) {
                return 0;
            }

            bool readPeek = false;
            if (_peekBuffer >= 0) {
                readPeek = true;
                buffer[index] = (char)_peekBuffer;
                _peekBuffer = -1;
                index += 1;
                count -= 1;
            }

            if (_current == null) {
                return readPeek ? 1 : 0;
            }
            var i = _current.Read(buffer, index, count);
            if (i == 0 && Next()) {
                i = _current.Read(buffer, index, count);
            }
            return i + (readPeek ? 1 : 0);
        }

        public override int ReadBlock(char[] buffer, int index, int count) {
            int totalCount = 0;
            while (totalCount < count) {
                int newCount = Read(buffer, index, count);
                if (newCount == 0) {
                    break;
                }
                index += newCount;
                count -= newCount;
                totalCount += newCount;
            }
            return totalCount;
        }

        public override string ReadToEnd() {
            Debug.Fail("Please don't do this - it's why I wrote this reader in the first place!");
            return ReadToEndWithoutAssert();
        }

        internal string ReadToEndWithoutAssert() {
            var line = ReadLine();
            if (line == null) {
                return null;
            }

            var sb = new StringBuilder();
            while (line != null) {
                sb.Append(line);
                line = ReadLine();
            }
            return sb.ToString();
        }
    }
}
