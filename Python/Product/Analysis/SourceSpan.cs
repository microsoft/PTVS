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
using System.Diagnostics;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools {

    /// <summary>
    /// Stores the location of a span of text in a source file.
    /// </summary>
    [Serializable]
    [DebuggerDisplay("({Start._line}, {Start._column})-({End._line}, {End._column})")]
    public struct SourceSpan {
        /// <summary>
        /// Constructs a new span with a specific start and end location.
        /// </summary>
        /// <param name="start">The beginning of the span.</param>
        /// <param name="end">The end of the span.</param>
        public SourceSpan(SourceLocation start, SourceLocation end) {
            ValidateLocations(start, end);
            Start = start;
            End = end;
        }

        public SourceSpan(int startLine, int startColumn, int endLine, int endColumn)
            : this(new SourceLocation(startLine, startColumn), new SourceLocation(endLine, endColumn)) { }

        private static void ValidateLocations(SourceLocation start, SourceLocation end) {
            if (start.IsValid && end.IsValid) {
                if (start > end) {
                    throw new ArgumentException("Start and End must be well ordered");
                }
            } else {
                if (start.IsValid || end.IsValid) {
                    throw new ArgumentException("Start and End must both be valid or both invalid");
                }
            }
        }

        /// <summary>
        /// The start location of the span.
        /// </summary>
        public SourceLocation Start { get; }

        /// <summary>
        /// The end location of the span. Location of the first character behind the span.
        /// </summary>
        public SourceLocation End { get; }

        /// <summary>
        /// A valid span that represents no location.
        /// </summary>
        public static readonly SourceSpan None = new SourceSpan(SourceLocation.None, SourceLocation.None);

        /// <summary>
        /// An invalid span.
        /// </summary>
        public static readonly SourceSpan Invalid = new SourceSpan(SourceLocation.Invalid, SourceLocation.Invalid);

        /// <summary>
        /// Whether the locations in the span are valid.
        /// </summary>
        public bool IsValid => Start.IsValid && End.IsValid;

        public SourceSpan Union(SourceSpan other) {
            var startLine = Math.Min(other.Start.Line, Start.Line);
            var startColumn = Math.Min(other.Start.Column, Start.Column);

            var endLine = Math.Max(other.End.Line, End.Line);
            var endColumn = Math.Max(other.End.Column, End.Column);

            return new SourceSpan(new SourceLocation(startLine, startColumn), new SourceLocation(endLine, endColumn));
        }

        /// <summary>
        /// Compares two specified Span values to see if they are equal.
        /// </summary>
        /// <param name="left">One span to compare.</param>
        /// <param name="right">The other span to compare.</param>
        /// <returns>True if the spans are the same, False otherwise.</returns>
        public static bool operator ==(SourceSpan left, SourceSpan right) 
            => left.Start == right.Start && left.End == right.End;

        /// <summary>
        /// Compares two specified Span values to see if they are not equal.
        /// </summary>
        /// <param name="left">One span to compare.</param>
        /// <param name="right">The other span to compare.</param>
        /// <returns>True if the spans are not the same, False otherwise.</returns>
        public static bool operator !=(SourceSpan left, SourceSpan right)
            => left.Start != right.Start || left.End != right.End;

        public override bool Equals(object obj) {
            if (!(obj is SourceSpan)) return false;

            var other = (SourceSpan)obj;
            return Start == other.Start && End == other.End;
        }

        public override string ToString() => "{0} - {1}".FormatInvariant(Start, End);

        public override int GetHashCode()
            // 7 bits for each column (0-128), 9 bits for each row (0-512), xor helps if
            // we have a bigger file.
            => (Start.Column) ^ (End.Column << 7) ^ (Start.Line << 14) ^ (End.Line << 23);

        internal string ToDebugString() =>
            "{0}-{1}".FormatInvariant(Start.ToDebugString(), End.ToDebugString());
    }
}
