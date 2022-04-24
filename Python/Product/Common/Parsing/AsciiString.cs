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

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PythonTools.Common.Parsing {
    public sealed class AsciiString: IEquatable<AsciiString> {
        public AsciiString(byte[] bytes, string str) {
            Bytes = bytes;
            String = str;
        }

        public IReadOnlyList<byte> Bytes { get; }

        public string String { get; }

        public override string ToString() => String;

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }
            if (ReferenceEquals(this, obj)) {
                return true;
            }
            return obj is AsciiString other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                return ((Bytes != null ? Bytes.GetHashCode() : 0) * 397) ^ (String != null ? String.GetHashCode() : 0);
            }
        }

        public bool Equals(AsciiString other) {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Bytes.SequenceEqual(other.Bytes) && string.Equals(String, other.String);
        }
    }
}
