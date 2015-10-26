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
                        ReplaceLines(edit, curOldLine, oldLines.Length, startNewLine, _newLines.Length);
                        curOldLine = oldLines.Length;
                        break;
                    }
                }

                if (curOldLine < oldLines.Length) {
                    // remove the remaining new lines
                    edit.Delete(
                        Span.FromBounds(
                            _snapshot.GetLineFromLineNumber(curOldLine + _startingReplacementLine + 1).Start,
                            _snapshot.GetLineFromLineNumber(oldLines.Length + _startingReplacementLine).End
                        )
                    );
                }

                edit.Apply();
            }
        }

        private void ReplaceLines(ITextEdit edit, int startOldLine, int endOldLine, int startNewLine, int endNewLine) {
            int oldLineCount = endOldLine - startOldLine;
            int newLineCount = endNewLine - startNewLine;

            // replace one line at a time instead of all of the lines at once so that we preserve breakpoints
            int excessNewLineStart = startNewLine - startOldLine;
            for (int i = startOldLine; i < endOldLine && i < (endNewLine - startNewLine + startOldLine); i++) {
                edit.Replace(
                    _snapshot.GetLineFromLineNumber(_startingReplacementLine + i).Extent,
                    _newLines[startNewLine + i - startOldLine]
                );
                excessNewLineStart = startNewLine + i - startOldLine + 1;
            }

            if (oldLineCount > newLineCount) {
                // we end up w/ less lines, we need to delete some text
                edit.Delete(
                    Span.FromBounds(
                        _snapshot.GetLineFromLineNumber(_startingReplacementLine + endOldLine - (oldLineCount - newLineCount)).Start.Position,
                        _snapshot.GetLineFromLineNumber(_startingReplacementLine + endOldLine - 1).EndIncludingLineBreak.Position
                    )
                );
            } else if (oldLineCount < newLineCount) {
                // we end up w/ more lines, we need to insert some text
                edit.Insert(
                    _snapshot.GetLineFromLineNumber(_startingReplacementLine + endOldLine - 1).EndIncludingLineBreak,
                    string.Join(
                        _view.Options.GetNewLineCharacter(),
                        _newLines,
                        excessNewLineStart,
                        endNewLine - excessNewLineStart
                    ) + _view.Options.GetNewLineCharacter()
                );
            }
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
