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

using System;

namespace Microsoft.PythonTools.Parsing {
    /// <summary>
    /// This structure represents an immutable integer interval that describes a range of values, from Start to End. 
    /// 
    /// It is closed on the left and open on the right: [Start .. End). 
    /// </summary>
    internal struct IndexSpan : IEquatable<IndexSpan> {
        private readonly int _start, _length;

        public IndexSpan(int start, int length) {
            _start = start;
            _length = length;
        }

        public int Start {
            get {
                return _start;
            }
        }

        public int End {
            get {
                return _start + _length;
            }
        }

        public int Length {
            get {
                return _length;
            }
        }

        public override int GetHashCode() {
            return Length.GetHashCode() ^ Start.GetHashCode();
        }

        public override bool Equals(object obj) {
            if (obj is IndexSpan) {
                return Equals((IndexSpan)obj);
            }
            return false;
        }

        public static bool operator ==(IndexSpan self, IndexSpan other) {
            return self.Equals(other);
        }

        public static bool operator !=(IndexSpan self, IndexSpan other) {
            return !self.Equals(other);
        }

        #region IEquatable<IndexSpan> Members

        public bool Equals(IndexSpan other) {
            return _length == other._length && _start == other._start;
        }

        #endregion
    }
}
