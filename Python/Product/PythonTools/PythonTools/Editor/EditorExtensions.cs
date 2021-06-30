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
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Editor.Core {
    internal static class EditorExtensions {
        public static bool CommentOrUncommentBlock(this ITextView view, bool comment) {
            SnapshotPoint start, end;
            SnapshotPoint? mappedStart, mappedEnd;

            if (view.Selection.IsActive && !view.Selection.IsEmpty) {
                // comment every line in the selection
                start = view.Selection.Start.Position;
                end = view.Selection.End.Position;
                mappedStart = MapPoint(view, start);

                var endLine = end.GetContainingLine();
                if (endLine.Start == end) {
                    // http://pytools.codeplex.com/workitem/814
                    // User selected one extra line, but no text on that line.  So let's
                    // back it up to the previous line.  It's impossible that we're on the
                    // 1st line here because we have a selection, and we end at the start of
                    // a line.  In normal selection this is only possible if we wrapped onto the
                    // 2nd line, and it's impossible to have a box selection with a single line.
                    end = end.Snapshot.GetLineFromLineNumber(endLine.LineNumber - 1).End;
                }

                mappedEnd = MapPoint(view, end);
            } else {
                // comment the current line
                start = end = view.Caret.Position.BufferPosition;
                mappedStart = mappedEnd = MapPoint(view, start);
            }

            if (mappedStart != null && mappedEnd != null &&
                mappedStart.Value <= mappedEnd.Value) {
                if (comment) {
                    CommentRegion(view, mappedStart.Value, mappedEnd.Value);
                } else {
                    UncommentRegion(view, mappedStart.Value, mappedEnd.Value);
                }

                // TODO: select multiple spans?
                // Select the full region we just commented, do not select if in projection buffer 
                // (the selection might span non-language buffer regions)
                if (view.TextBuffer.IsPythonContent()) {
                    UpdateSelection(view, start, end);
                }
                return true;
            }

            return false;
        }

        internal static bool IsPythonContent(this ITextBuffer buffer) {
            return buffer.ContentType.IsOfType(PythonCoreConstants.ContentType);
        }

        internal static bool IsPythonContent(this ITextSnapshot buffer) {
            return buffer.ContentType.IsOfType(PythonCoreConstants.ContentType);
        }

        private static SnapshotPoint? MapPoint(ITextView view, SnapshotPoint point) {
            return view.BufferGraph.MapDownToFirstMatch(
               point,
               PointTrackingMode.Positive,
               IsPythonContent,
               PositionAffinity.Successor
            );
        }

        public static SnapshotPoint? MapDownToPythonBuffer(this ITextView view, SnapshotPoint point) => MapPoint(view, point);

        /// <summary>
        /// Maps down to the buffer using positive point tracking and successor position affinity
        /// </summary>
        public static SnapshotPoint? MapDownToBuffer(this ITextView textView, int position, ITextBuffer buffer) {
            if (textView.BufferGraph == null) {
                // Unit test case
                if (position <= buffer.CurrentSnapshot.Length) {
                    return new SnapshotPoint(buffer.CurrentSnapshot, position);
                }
                return null;
            }
            if (position <= textView.TextBuffer.CurrentSnapshot.Length) {
                return textView.BufferGraph.MapDownToBuffer(
                    new SnapshotPoint(textView.TextBuffer.CurrentSnapshot, position),
                    PointTrackingMode.Positive,
                    buffer,
                    PositionAffinity.Successor
                );
            }
            return null;
        }

        /// <summary>
        /// Adds comment characters (#) to the start of each line.  If there is a selection the comment is applied
        /// to each selected line.  Otherwise the comment is applied to the current line.
        /// </summary>
        /// <param name="view"></param>
        private static void CommentRegion(ITextView view, SnapshotPoint start, SnapshotPoint end) {
            Debug.Assert(start.Snapshot == end.Snapshot);
            var snapshot = start.Snapshot;

            using (var edit = snapshot.TextBuffer.CreateEdit()) {
                int minColumn = Int32.MaxValue;
                // first pass, determine the position to place the comment
                for (int i = start.GetContainingLine().LineNumber; i <= end.GetContainingLine().LineNumber; i++) {
                    var curLine = snapshot.GetLineFromLineNumber(i);
                    var text = curLine.GetText();

                    int firstNonWhitespace = IndexOfNonWhitespaceCharacter(text);
                    if (firstNonWhitespace >= 0 && firstNonWhitespace < minColumn) {
                        // ignore blank lines
                        minColumn = firstNonWhitespace;
                    }
                }

                // second pass, place the comment
                for (int i = start.GetContainingLine().LineNumber; i <= end.GetContainingLine().LineNumber; i++) {
                    var curLine = snapshot.GetLineFromLineNumber(i);
                    if (String.IsNullOrWhiteSpace(curLine.GetText())) {
                        continue;
                    }

                    Debug.Assert(curLine.Length >= minColumn);

                    edit.Insert(curLine.Start.Position + minColumn, "#");
                }

                edit.Apply();
            }
        }

        private static int IndexOfNonWhitespaceCharacter(string text) {
            for (int j = 0; j < text.Length; j++) {
                if (!Char.IsWhiteSpace(text[j])) {
                    return j;
                }
            }
            return -1;
        }

        /// <summary>
        /// Removes a comment character (#) from the start of each line.  If there is a selection the character is
        /// removed from each selected line.  Otherwise the character is removed from the current line.  Uncommented
        /// lines are ignored.
        /// </summary>
        private static void UncommentRegion(ITextView view, SnapshotPoint start, SnapshotPoint end) {
            Debug.Assert(start.Snapshot == end.Snapshot);
            var snapshot = start.Snapshot;

            using (var edit = snapshot.TextBuffer.CreateEdit()) {

                // first pass, determine the position to place the comment
                for (int i = start.GetContainingLine().LineNumber; i <= end.GetContainingLine().LineNumber; i++) {
                    var curLine = snapshot.GetLineFromLineNumber(i);

                    DeleteFirstCommentChar(edit, curLine);
                }

                edit.Apply();
            }
        }

        private static void UpdateSelection(ITextView view, SnapshotPoint start, SnapshotPoint end) {
            view.Selection.Select(
                new SnapshotSpan(
                    // translate to the new snapshot version:
                    start.GetContainingLine().Start.TranslateTo(view.TextBuffer.CurrentSnapshot, PointTrackingMode.Negative),
                    end.GetContainingLine().End.TranslateTo(view.TextBuffer.CurrentSnapshot, PointTrackingMode.Positive)
                ),
                false
            );
        }

        private static void DeleteFirstCommentChar(ITextEdit edit, ITextSnapshotLine curLine) {
            var text = curLine.GetText();
            for (int j = 0; j < text.Length; j++) {
                if (!Char.IsWhiteSpace(text[j])) {
                    if (text[j] == '#') {
                        edit.Delete(curLine.Start.Position + j, 1);
                    }
                    break;
                }
            }
        }

        public static SourceLocation ToSourceLocation(this SnapshotPoint point) {
            return new SourceLocation(
                point.Position,
                point.GetContainingLine().LineNumber + 1,
                point.Position - point.GetContainingLine().Start.Position + 1
            );
        }

        public static SourceSpan ToSourceSpan(this SnapshotSpan span) {
            return new SourceSpan(
                ToSourceLocation(span.Start),
                ToSourceLocation(span.End)
            );
        }

        public static SnapshotPoint ToSnapshotPoint(this SourceLocation location, ITextSnapshot snapshot) {
            ITextSnapshotLine line;

            if (location.Line < 1) {
                return new SnapshotPoint(snapshot, 0);
            }

            try {
                line = snapshot.GetLineFromLineNumber(location.Line - 1);
            } catch (ArgumentOutOfRangeException) {
                Debug.Assert(location.Line == snapshot.LineCount + 1 && location.Column == 1,
                    $"Out of range should only occur at end of snapshot ({snapshot.LineCount + 1}, 1), not at {location}");
                return new SnapshotPoint(snapshot, snapshot.Length);
            }

            if (location.Column > line.LengthIncludingLineBreak) {
                return line.EndIncludingLineBreak;
            }
            return line.Start + (location.Column - 1);
        }

        public static SnapshotSpan ToSnapshotSpan(this SourceSpan span, ITextSnapshot snapshot) {
            return new SnapshotSpan(
                ToSnapshotPoint(span.Start, snapshot),
                ToSnapshotPoint(span.End, snapshot)
            );
        }
    }
}
