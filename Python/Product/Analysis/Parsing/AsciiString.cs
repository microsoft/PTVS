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

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.PythonTools.Parsing {
    public sealed class AsciiString {
        private readonly byte[] _bytes;
        private string _str;

        public AsciiString(byte[] bytes, string str) {
            _bytes = bytes;
            _str = str;
        }

        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays",
            Justification = "breaking change")]
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
