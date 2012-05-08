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

        public override string ToString() {
            return String;
        }

        public override bool Equals(object obj) {
            AsciiString other = obj as AsciiString;
            if (other != null) {
                return _str == other._str;
            }
            return false;
        }

        public override int GetHashCode() {
            return _str.GetHashCode();
        }
    }
}
