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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.PythonTools.Interpreter {
    class StringListReader : TextReader {
        private readonly IEnumerator<string> _strings;
        private StringReader _current;

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
            }
        }

        private bool Next() {
            if (_current != null) {
                if (_strings.MoveNext()) {
                    _current = new StringReader(_strings.Current);
                    return true;
                } else {
                    _current = null;
                }
            }
            return false;
        }

        public override string ReadLine() {
            var r = _current?.ReadLine();
            if (r == null && Next()) {
                return _current.ReadLine();
            }
            return r;
        }

        public override int Read() {
            if (_current == null) {
                return -1;
            }

            var i = _current.Read();
            if (i < 0 && Next()) {
                return _current.Read();
            }
            return i;
        }

        public override int Read(char[] buffer, int index, int count) {
            if (_current == null) {
                return 0;
            }
            var i = _current.Read(buffer, index, count);
            if (i == 0 && Next()) {
                i = _current.Read(buffer, index, count);
            }
            return i;
        }

        public override int ReadBlock(char[] buffer, int index, int count) {
            return Read(buffer, index, count);
        }

        public override string ReadToEnd() {
            Debug.Fail("Please don't do this - it's why I wrote this reader in the first place!");
            var sb = new StringBuilder(_current.ReadToEnd());
            while (Next()) {
                sb.Append(_current.ReadToEnd());
            }
            return sb.ToString();
        }
    }
}
