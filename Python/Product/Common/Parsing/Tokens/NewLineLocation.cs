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
using System.Diagnostics;
using Microsoft.PythonTools.Common.Core.Text;

namespace Microsoft.PythonTools.Common.Parsing {
    [DebuggerDisplay("NewLineLocation({EndIndex}, {Kind})")]
    public struct NewLineLocation : IComparable<NewLineLocation> {
        public NewLineLocation(int lineEnd, NewLineKind kind) {
            EndIndex = lineEnd;
            Kind = kind;
        }

        /// <summary>
        /// The end of of the line, including the line break.
        /// </summary>
        public int EndIndex { get; }

        /// <summary>
        /// The type of new line which terminated the line.
        /// </summary>
        public NewLineKind Kind { get; }

        public int CompareTo(NewLineLocation other) => EndIndex - other.EndIndex;

        public static SourceLocation IndexToLocation(NewLineLocation[] lineLocations, int index) {
            if (lineLocations == null || index == 0) {
                return new SourceLocation(index, 1, 1);
            }

            var match = Array.BinarySearch(lineLocations, new NewLineLocation(index, NewLineKind.None));
            if (match < 0) {
                // If our index = -1, it means we're on the first line.
                if (match == -1) {
                    return new SourceLocation(index, 1, checked(index + 1));
                }
                // If we couldn't find an exact match for this line number, get the nearest
                // matching line number less than this one
                match = ~match - 1;
            }

            while (match >= 0 && index == lineLocations[match].EndIndex && lineLocations[match].Kind == NewLineKind.None) {
                match -= 1;
            }
            if (match < 0) {
                return new SourceLocation(index, 1, checked(index + 1));
            }

            var line = match + 2;
            var col = index - lineLocations[match].EndIndex + 1;
            return new SourceLocation(index, line, col);
        }

        public static int LocationToIndex(NewLineLocation[] lineLocations, SourceLocation location, int endIndex) {
            if (lineLocations == null) {
                return 0;
            }
            var index = 0;
            if (lineLocations.Length == 0) {
                // We have a single line, so the column is the index
                index = location.Column - 1;
                return endIndex >= 0 ? Math.Min(index, endIndex) : index;
            }
            var line = location.Line - 1;

            if (line > lineLocations.Length) {
                index = lineLocations[lineLocations.Length - 1].EndIndex;
                return endIndex >= 0 ? Math.Min(index, endIndex) : index;
            }

            if (line > 0) {
                index = lineLocations[line - 1].EndIndex;
            }

            if (line < lineLocations.Length && location.Column > (lineLocations[line].EndIndex - index)) {
                index = lineLocations[line].EndIndex;
                return endIndex >= 0 ? Math.Min(index, endIndex) : index;
            }

            if (endIndex < 0) {
                endIndex = lineLocations[lineLocations.Length - 1].EndIndex;
            }

            return (int)Math.Min((long)index + location.Column - 1, endIndex);
        }

        private static readonly char[] _lineSeparators = new[] { '\r', '\n' };

        public static NewLineLocation FindNewLine(string text, int start) {
            var i = text.IndexOfAny(_lineSeparators, start);
            if (i < start) {
                return new NewLineLocation(text.Length, NewLineKind.None);
            }
            if (text[i] == '\n') {
                return new NewLineLocation(i + 1, NewLineKind.LineFeed);
            }
            if (text.Length > i + 1 && text[i + 1] == '\n') {
                return new NewLineLocation(i + 2, NewLineKind.CarriageReturnLineFeed);
            }
            return new NewLineLocation(i + 1, NewLineKind.CarriageReturn);
        }

        public override string ToString() => $"<NewLineLocation({EndIndex}, NewLineKind.{Kind})>";
    }
}
