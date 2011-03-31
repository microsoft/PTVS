using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.PythonTools.Parsing {
    public sealed class AsciiString {
        private readonly byte[] _bytes;
        private string _str;

        public AsciiString(byte[] bytes, string str) {
            _bytes = bytes;
            _str = str;
        }

        public byte[] Bytes {
            get {
                return _bytes;
            }
        }

        public string String {
            get {
                return _str;
            }
        }
    }
}
