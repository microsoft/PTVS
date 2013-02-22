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
using System.Collections.Generic;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Replaces a range of text with new text attempting only modifying lines which changed 
    /// and doing so in a single edit.
    /// </summary>
    class LinePreservingCodeReplacer {
        private readonly ITextView _view;
        private readonly ITextSnapshot _snapshot;
        private readonly string _newCode, _oldCode;
        private readonly string[] _newLines;
        private readonly int _startingReplacementLine;

        private LinePreservingCodeReplacer(ITextView view, string newCode, Span range) {
            _view = view;
            _snapshot = view.TextBuffer.CurrentSnapshot;
            _oldCode = _snapshot.GetText(range);
            _newCode = newCode;
            _newLines = newCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            _startingReplacementLine = _snapshot.GetLineFromPosition(range.Start).LineNumber;
        }

        public static void Replace(ITextView view, string newCode, Span range) {
            new LinePreservingCodeReplacer(view, newCode, range).ReplaceCode();
        }

        public void ReplaceCode() {
            using (var edit = _view.TextBuffer.CreateEdit()) {
                var oldLineMapping = new Dictionary<string, List<int>>();   // line to line #                
                var oldLines = _oldCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                for (int i = 0; i < oldLines.Length; i++) {
                    List<int> lineInfo;
                    if (!oldLineMapping.TryGetValue(oldLines[i], out lineInfo)) {
                        oldLineMapping[oldLines[i]] = lineInfo = new List<int>();
                    }
                    lineInfo.Add(i);
                }

                int curOldLine = 0;
                for (int curNewLine = 0; curOldLine < oldLines.Length && curNewLine < _newLines.Length; curOldLine++) {
                    if (oldLines[curOldLine] == _newLines[curNewLine]) {
                        curNewLine++;
                        continue;
                    }

                    bool replaced = false;
                    // replace starts on this line, figure out where it ends...
                    int startNewLine = curNewLine;
                    for (curNewLine += 1; curNewLine < _newLines.Length; curNewLine++) {
                        List<int> lines;
                        if (oldLineMapping.TryGetValue(_newLines[curNewLine], out lines)) {
                            foreach (var matchingLineNo in lines) {
                                if (matchingLineNo > curOldLine) {
                                    // Replace the lines from curOldLine to matchingLineNo-1 with the text
                                    // from startNewLine - curNewLine - 1.
                                    ReplaceLines(edit, curOldLine, matchingLineNo, startNewLine, curNewLine);
                                    replaced = true;
                                    curOldLine = matchingLineNo - 1;
                                    break;
                                }
                            }
                        }

                        if (replaced) {
                            break;
                        }
                    }

                    if (!replaced) {
                        ReplaceLines(edit, curOldLine, _snapshot.LineCount - _startingReplacementLine, startNewLine, _newLines.Length);
                        break;
                    }
                }

                if (curOldLine < oldLines.Length) {
                    // remove the remaining new lines
                    edit.Delete(
                        Span.FromBounds(
                            _snapshot.GetLineFromLineNumber(curOldLine).Start,
                            new SnapshotPoint(_snapshot, _snapshot.Length)
                        )
                    );
                }

                edit.Apply();
            }
        }

        private void ReplaceLines(ITextEdit edit, int startOldLine, int endOldLine, int startNewLine, int endNewLine) {
            edit.Replace(
                Span.FromBounds(
                    _snapshot.GetLineFromLineNumber(_startingReplacementLine + startOldLine).Start.Position,
                    _snapshot.GetLineFromLineNumber(_startingReplacementLine + endOldLine - 1).End.Position
                ),
                string.Join(
                    _view.Options.GetNewLineCharacter(),
                    _newLines,
                    startNewLine,
                    endNewLine - startNewLine
                )
            );
        }
    }

    static class RefactoringTextViewExtensions {
        /// <summary>
        /// Replaces a range of text with new text attempting only modifying lines which changed 
        /// and doing so in a single edit.
        /// </summary>
        public static void ReplaceByLines(this ITextView view, string newCode, Span range) {
            LinePreservingCodeReplacer.Replace(view, newCode, range);
        }
    }
}
