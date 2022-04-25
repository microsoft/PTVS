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
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.PythonTools.Common.Core.Text {
    /// <summary>
    /// This type represents the span over the string
    /// It is a temporary solution until .net standard 2.1 with ReadOnlySpan is published
    /// </summary>
    public struct StringSpan : IEquatable<StringSpan> {
        public string Source { get; }
        public int Start { get; }
        public int Length { get; }
        public int End => Start + Length;
        public bool IsValid => Source != null && Start >= 0 && Length >= 0 && Source.Length >= End;

        public StringSpan(string source) : this(source, 0, source.Length) { }

        public StringSpan(string source, int start, int length) {
            Source = source;
            Start = start;
            Length = length;
        }

        public static bool operator ==(StringSpan left, StringSpan right) => left.Equals(right);
        public static bool operator !=(StringSpan left, StringSpan right) => !left.Equals(right);

        public char this[int index] => Source[index + Start];
        public override bool Equals(object obj) 
            => obj is StringSpan other && Equals(other);

        public bool Equals(StringSpan other) 
            => Length == other.Length && string.CompareOrdinal(Source, Start, other.Source, other.Start, Length) == 0;

        public override int GetHashCode() => throw new NotSupportedException();

        public void Deconstruct(out string source, out int start, out int length) {
            source = Source;
            start = Start;
            length = Length;
        }
    }

    public readonly struct StringSpanSplitSequence : IEnumerable<StringSpan> {
        private readonly string _source;
        private readonly int _start;
        private readonly int _length;
        private readonly char _separator;
        
        public StringSpanSplitSequence(string source, int start, int length, char separator) {
            _source = source;
            _start = start;
            _length = length;
            _separator = separator;
        }

        public StringSpanSplitEnumerator GetEnumerator() => new StringSpanSplitEnumerator(_source, _start, _length, _separator);

        IEnumerator<StringSpan> IEnumerable<StringSpan>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public struct StringSpanSplitEnumerator : IEnumerator<StringSpan> {
        private readonly string _source;
        private readonly int _start;
        private readonly int _length;
        private readonly char _separator;

        public StringSpan Current { get; private set; }

        public StringSpanSplitEnumerator(string source, int start, int length, char separator) {
            _source = source;
            _start = start;
            _length = length;
            _separator = separator;
            Current = new StringSpan(source, 0, start);
        }

        public bool MoveNext() {
            if (!Current.IsValid) {
                return false;
            }

            var end = _start + _length;
            var (source, start, length) = Current;
            var nextStart = start + length;
            while (nextStart < end && source[nextStart] == _separator) {
                nextStart++;
            }

            if (nextStart == end) {
                Current = new StringSpan(source, -1, 0);
                return false;
            }

            var nextSeparatorIndex = source.IndexOf(_separator, nextStart);
            var nextLength = nextSeparatorIndex == -1 || nextSeparatorIndex >= end ? end - nextStart : nextSeparatorIndex - nextStart;
            Current = new StringSpan(source, nextStart, nextLength);
            return true;
        }

        public void Dispose() { }
        object IEnumerator.Current => Current;
        public void Reset() => Current = new StringSpan(_source, 0, _start);
    }
}
