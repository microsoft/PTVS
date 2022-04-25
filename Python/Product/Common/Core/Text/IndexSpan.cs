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

namespace Microsoft.PythonTools.Common.Core.Text {
    /// <summary>
    /// This structure represents an immutable integer interval that describes a range of values, from Start to End. 
    /// 
    /// It is closed on the left and open on the right: [Start .. End). 
    /// </summary>
    public struct IndexSpan : IEquatable<IndexSpan> {
        public IndexSpan(int start, int length) {
            Start = start;
            Length = length;
        }

        public int Start { get; }

        public int End => Start + Length;

        public int Length { get; }

        public override int GetHashCode() => Length.GetHashCode() ^ Start.GetHashCode();

        public override bool Equals(object obj) => obj is IndexSpan ? Equals((IndexSpan)obj) : false;

        public static bool operator ==(IndexSpan self, IndexSpan other) {
            return self.Equals(other);
        }

        public static bool operator !=(IndexSpan self, IndexSpan other) {
            return !self.Equals(other);
        }

        #region IEquatable<IndexSpan> Members
        public bool Equals(IndexSpan other) => Length == other.Length && Start == other.Start;
        #endregion

        public static IndexSpan FromBounds(int start, int end) => new IndexSpan(start, end - start);
    }
}
