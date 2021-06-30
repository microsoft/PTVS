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

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    static class CodeCellAnalysis {
        internal static readonly Regex _codeCellRegex = new Regex(@"^\s*#(?:%%\s*|\s*In\s*(?=\[.+?\]:))(.*)$");

        public static bool IsCellMarker(string text) {
            return _codeCellRegex.IsMatch(text);
        }

        private static IEnumerable<ITextSnapshotLine> LinesBackward(ITextSnapshotLine start) {
            var snapshot = start.Snapshot;
            for (int lineNo = start.LineNumber; lineNo >= 0; --lineNo) {
                yield return snapshot.GetLineFromLineNumber(lineNo);
            }
        }

        private static IEnumerable<ITextSnapshotLine> LinesForward(ITextSnapshotLine start) {
            var snapshot = start.Snapshot;
            for (int lineNo = start.LineNumber; lineNo < snapshot.LineCount; ++lineNo) {
                yield return snapshot.GetLineFromLineNumber(lineNo);
            }
        }

        public static ITextSnapshotLine FindStartOfCell(ITextSnapshotLine line) {
            var snapshot = line.Snapshot;
            var start = line;

            // Search after the current line, as we may be in a comment
            // preceding a cell.
            bool seenMarker = false, seenComment = false;
            bool seenWhitespace = false;
            foreach (var current in LinesForward(line)) {
                var text = current.GetText();
                if (IsCellMarker(text)) {
                    // Certainly started in a comment preceding a cell
                    start = current;
                    seenMarker = true;
                    break;
                } else if (string.IsNullOrWhiteSpace(text)) {
                    seenWhitespace = true;
                } else if (text.TrimStart().StartsWithOrdinal("#")) {
                    // In a comment that may precede a cell, so keep looking
                    seenComment = true;
                } else {
                    // Certainly not in a comment preceding a cell
                    break;
                }
            }

            if (seenWhitespace && seenMarker && !seenComment) {
                // Need to go backwards and see whether we were on a blank line
                // following a comment or following code. If the latter, we are
                // part of the previous cell; otherwise, we are part of the
                // following cell.
                foreach (var current in LinesBackward(line)) {
                    var text = current.GetText();
                    if (IsCellMarker(text)) {
                        // In the cell we just found
                        start = current;
                        break;
                    } else if (string.IsNullOrWhiteSpace(text)) {
                        // Still not sure
                    } else if (text.TrimStart().StartsWithOrdinal("#")) {
                        // In the following cell
                        break;
                    } else {
                        // In the current cell
                        start = line;
                        break;
                    }
                }
            }

            bool lookingForCell = true;
            foreach (var current in LinesBackward(start)) {
                var text = current.GetText();
                if (IsCellMarker(text)) {
                    if (lookingForCell) {
                        // We're in a cell, so now look for the top of the
                        // preceding comment
                        start = current;
                        lookingForCell = false;
                    } else {
                        // We found the start of the next cell
                        break;
                    }
                } else if (string.IsNullOrWhiteSpace(text)) {
                    // Keep looking for top of the comment. If we don't find
                    // one, we won't want to have updated the start line.
                } else if (text.TrimStart().StartsWithOrdinal("#")) {
                    // Update the start to this line
                    start = current;
                } else {
                    // Not whitespace or comment, so we found the start already
                    if (!lookingForCell) {
                        break;
                    }
                }
            }

            if (lookingForCell) {
                // Didn't find a cell
                return null;
            }
            return start;
        }

        public static ITextSnapshotLine FindEndOfCell(ITextSnapshotLine cellStart, ITextSnapshotLine line, bool includeWhitespace = false) {
            if (cellStart == null) {
                return line;
            }
            line = cellStart;

            var snapshot = line.Snapshot;
            ITextSnapshotLine end = null, endInclWhitespace = null, endInclComment = null;
            foreach (var current in LinesForward(line)) {
                var text = current.GetText();
                if (IsCellMarker(text)) {
                    if (end == null) {
                        // Found the start of the current cell
                        end = current;
                    } else {
                        // Found the start of the next cell, so we're finished

                        // Don't want to include comments belonging to the next
                        // cell.
                        endInclComment = null;
                        break;
                    }
                } else if (string.IsNullOrWhiteSpace(text)) {
                    // Keep looking for the next cell marker. If we find it, we
                    // won't want to have updated the end line.
                    if (endInclComment != null) {
                        // Possibly within the next comment, so only update this
                        // ending.
                        endInclComment = current;
                    } else {
                        // Not inside the next comment yet, so keep the whitespace.
                        endInclWhitespace = current;
                    }
                } else if (text.TrimStart().StartsWithOrdinal("#")) {
                    // Keep looking for the next cell marker. If we find it, we
                    // won't want to have updated the end line.
                    endInclComment = current;
                } else {
                    end = current;
                    endInclComment = endInclWhitespace = null;
                }
            }

            return endInclComment ?? (includeWhitespace ? endInclWhitespace : null) ?? end;
        }

    }
}
