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

using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Editor.Core {
    public static class EditorExtensions {
        /// <summary>
        /// Adds comment characters (#) to the start of each line.  If there is a selection the comment is applied
        /// to each selected line.  Otherwise the comment is applied to the current line.
        /// </summary>
        /// <param name="view"></param>
        public static void CommentBlock(this ITextView view) {
            if (view.Selection.IsActive) {
                // comment every line in the selection
                CommentRegion(view, view.Selection.Start.Position, view.Selection.End.Position);
            } else {
                // comment the current line
                CommentRegion(view, view.Caret.Position.BufferPosition, view.Caret.Position.BufferPosition);
            }
        }

        /// <summary>
        /// Removes a comment character (#) from the start of each line.  If there is a selection the character is
        /// removed from each selected line.  Otherwise the character is removed from the current line.  Uncommented
        /// lines are ignored.
        /// </summary>
        /// <param name="view"></param>
        public static void UncommentBlock(this ITextView view) {
            if (view.Selection.IsActive) {
                // uncomment every line in the selection
                UncommentRegion(view, view.Selection.Start.Position, view.Selection.End.Position);
            } else {
                // uncomment the current line
                UncommentRegion(view, view.Caret.Position.BufferPosition, view.Caret.Position.BufferPosition);
            }

        }

        private static void CommentRegion(ITextView view, SnapshotPoint start, SnapshotPoint end) {
            using (var edit = view.TextBuffer.CreateEdit()) {
                int minColumn = Int32.MaxValue;
                // first pass, determine the position to place the comment
                for (int i = start.GetContainingLine().LineNumber; i <= end.GetContainingLine().LineNumber; i++) {
                    var curLine = view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(i);
                    var text = curLine.GetText();

                    minColumn = Math.Min(GetMinimumColumn(text), minColumn);
                }

                // second pass, place the comment
                for (int i = start.GetContainingLine().LineNumber; i <= end.GetContainingLine().LineNumber; i++) {
                    var curLine = view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(i);
                    if (curLine.Length < minColumn) {
                        // need extra white space
                        edit.Insert(curLine.Start.Position + curLine.Length, new String(' ', minColumn - curLine.Length) + "#");
                    } else {
                        edit.Insert(curLine.Start.Position + minColumn, "#");
                    }
                }

                edit.Apply();
            }

            // select the full region we just commented
            UpdateSelection(view, start, end);
        }

        private static int GetMinimumColumn(string text) {
            for (int j = 0; j < text.Length; j++) {
                if (!Char.IsWhiteSpace(text[j])) {
                    return j;
                }
            }
            return Int32.MaxValue;
        }

        private static void UncommentRegion(ITextView view, SnapshotPoint start, SnapshotPoint end) {
            using (var edit = view.TextBuffer.CreateEdit()) {

                // first pass, determine the position to place the comment
                for (int i = start.GetContainingLine().LineNumber; i <= end.GetContainingLine().LineNumber; i++) {
                    var curLine = view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(i);

                    DeleteFirstCommentChar(edit, curLine);
                }

                edit.Apply();
            }
            
            // select the full region we just uncommented

            UpdateSelection(view, start, end);
        }

        private static void UpdateSelection(ITextView view, SnapshotPoint start, SnapshotPoint end) {
            view.Selection.Select(
                new SnapshotSpan(
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
    }
}
