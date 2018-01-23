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
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Replaces a range of text with new text attempting only modifying lines which changed 
    /// and doing so in a single edit.
    /// </summary>
    class LinePreservingCodeReplacer {
        private readonly string _newLine, _oldCode, _newCode;
        private readonly IReadOnlyList<LineInfo> _newLines, _oldLines;

        private LinePreservingCodeReplacer(string oldCode, string newCode, string newLine) {
            _newLine = newLine;
            _oldCode = oldCode;
            _newCode = newCode;
            _oldLines = LineInfo.SplitLines(oldCode).ToArray();
            _newLines = LineInfo.SplitLines(newCode).ToArray();
        }

        private string GetOldText(int line) {
            return _oldCode.Substring(_oldLines[line].Start, _oldLines[line].Length);
        }

        private string GetNewText(int line) {
            return _newCode.Substring(_newLines[line].Start, _newLines[line].Length);
        }

        public static IReadOnlyList<DocumentChange> Replace(string oldCode, string newCode, string newLine = "\r\n") {
            return new LinePreservingCodeReplacer(oldCode, newCode, newLine).ReplaceCode();
        }

        public IReadOnlyList<DocumentChange> ReplaceCode() {
            var edits = new List<DocumentChange>();
            var oldLineMapping = new Dictionary<string, List<int>>();   // line to line #
            for (int i = 0; i < _oldLines.Count; i++) {
                List<int> lineInfo;
                if (!oldLineMapping.TryGetValue(GetOldText(i), out lineInfo)) {
                    oldLineMapping[GetOldText(i)] = lineInfo = new List<int>();
                }
                lineInfo.Add(i);
            }

            int curOldLine = 0;
            for (int curNewLine = 0; curOldLine < _oldLines.Count && curNewLine < _newLines.Count; curOldLine++) {
                if (GetOldText(curOldLine) == GetNewText(curNewLine)) {
                    curNewLine++;
                    continue;
                }

                bool replaced = false;
                // replace starts on this line, figure out where it ends...
                int startNewLine = curNewLine;
                for (curNewLine += 1; curNewLine < _newLines.Count; curNewLine++) {
                    List<int> lines;
                    if (oldLineMapping.TryGetValue(GetNewText(curNewLine), out lines)) {
                        foreach (var matchingLineNo in lines) {
                            if (matchingLineNo > curOldLine) {
                                // Replace the lines from curOldLine to matchingLineNo-1 with the text
                                // from startNewLine - curNewLine - 1.
                                ReplaceLines(edits, curOldLine, matchingLineNo, startNewLine, curNewLine);
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
                    ReplaceLines(edits, curOldLine, _oldLines.Count, startNewLine, _newLines.Count);
                    curOldLine = _oldLines.Count;
                    break;
                }
            }

            if (curOldLine < _oldLines.Count) {
                // remove the remaining new lines
                edits.Add(
                    DocumentChange.Delete(new SourceSpan(
                        _oldLines[curOldLine].SourceStart,
                        _oldLines[_oldLines.Count - 1].SourceEnd
                    ))
                );
            }
            return edits.ToArray();
        }

        private void ReplaceLines(List<DocumentChange> edits, int startOldLine, int endOldLine, int startNewLine, int endNewLine) {
            int oldLineCount = endOldLine - startOldLine;
            int newLineCount = endNewLine - startNewLine;

            // replace one line at a time instead of all of the lines at once so that we preserve breakpoints
            int excessNewLineStart = startNewLine - startOldLine;
            for (int i = startOldLine; i < endOldLine && i < (endNewLine - startNewLine + startOldLine); i++) {
                edits.Add(
                    DocumentChange.Replace(
                        _oldLines[i].SourceExtent,
                        GetNewText(startNewLine + i - startOldLine)
                    )
                );
                excessNewLineStart = startNewLine + i - startOldLine + 1;
            }

            if (oldLineCount > newLineCount) {
                // we end up w/ less lines, we need to delete some text
                edits.Add(
                    DocumentChange.Delete(new SourceSpan(
                        _oldLines[endOldLine - (oldLineCount - newLineCount)].SourceStart,
                        _oldLines[endOldLine - 1].SourceEndIncludingLineBreak
                    ))
                );
            } else if (oldLineCount < newLineCount) {
                // we end up w/ more lines, we need to insert some text
                edits.Add(
                    DocumentChange.Insert(
                        string.Join(
                            _newLine,
                            _newLines.Skip(excessNewLineStart).Take(endNewLine - excessNewLineStart).Select(x => GetNewText(x.LineNo))
                        ) + _newLine,
                        _oldLines[endOldLine - 1].SourceEndIncludingLineBreak
                    )
                );
            }
        }
    }

    static class RefactoringTextViewExtensions {
        /// <summary>
        /// Replaces a range of text with new text attempting only modifying lines which changed 
        /// and doing so in a single edit.
        /// </summary>
        public static IReadOnlyList<DocumentChange> ReplaceByLines(this string oldCode, string newCode, string newLine = "\r\n") {
            return LinePreservingCodeReplacer.Replace(oldCode, newCode, newLine);
        }
    }
}
