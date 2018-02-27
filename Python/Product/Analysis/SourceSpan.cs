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
using System.Globalization;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools {

    /// <summary>
    /// Stores the location of a span of text in a source file.
    /// </summary>
    [Serializable]
    [DebuggerDisplay("({_start._line}, {_start._column})-({_end._line}, {_end._column})")]
    public struct SourceSpan {
        private readonly SourceLocation _start;
        private readonly SourceLocation _end;

        /// <summary>
        /// Constructs a new span with a specific start and end location.
        /// </summary>
        /// <param name="start">The beginning of the span.</param>
        /// <param name="end">The end of the span.</param>
        public SourceSpan(SourceLocation start, SourceLocation end) {
            ValidateLocations(start, end);
            this._start = start;
            this._end = end;
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
        public SourceLocation Start {
            get { return _start; }
        }

        /// <summary>
        /// The end location of the span. Location of the first character behind the span.
        /// </summary>
        public SourceLocation End {
            get { return _end; }
        }

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
        public bool IsValid {
            get { return _start.IsValid && _end.IsValid; }
        }

        /// <summary>
        /// Compares two specified Span values to see if they are equal.
        /// </summary>
        /// <param name="left">One span to compare.</param>
        /// <param name="right">The other span to compare.</param>
        /// <returns>True if the spans are the same, False otherwise.</returns>
        public static bool operator ==(SourceSpan left, SourceSpan right) {
            return left._start == right._start && left._end == right._end;
        }

        /// <summary>
        /// Compares two specified Span values to see if they are not equal.
        /// </summary>
        /// <param name="left">One span to compare.</param>
        /// <param name="right">The other span to compare.</param>
        /// <returns>True if the spans are not the same, False otherwise.</returns>
        public static bool operator !=(SourceSpan left, SourceSpan right) {
            return left._start != right._start || left._end != right._end;
        }

        public override bool Equals(object obj) {
            if (!(obj is SourceSpan)) return false;

            SourceSpan other = (SourceSpan)obj;
            return _start == other._start && _end == other._end;
        }

        public override string ToString() {
            return _start.ToString() + " - " + _end.ToString();
        }

        public override int GetHashCode() {
            // 7 bits for each column (0-128), 9 bits for each row (0-512), xor helps if
            // we have a bigger file.
            return (_start.Column) ^ (_end.Column << 7) ^ (_start.Line << 14) ^ (_end.Line << 23);
        }

        internal string ToDebugString() {
            return "{0}-{1}".FormatInvariant(_start.ToDebugString(), _end.ToDebugString());
        }
    }
}
