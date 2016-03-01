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
using Microsoft.PythonTools.Analysis.Communication;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Refactoring {
    using AP = AnalysisProtocol;

    /// <summary>
    /// Replaces a range of text with new text attempting only modifying lines which changed 
    /// and doing so in a single edit.
    /// </summary>
    class LinePreservingCodeReplacer {
        private readonly string _newLine;
        private readonly LineInfo[] _newLines, _oldLines;
        private static char[] _lineSeperators = new[] { '\r', '\n' };

        private LinePreservingCodeReplacer(string oldCode, string newCode, string newLine) {
            _newLine = newLine;
            _oldLines = SplitLines(oldCode);
            _newLines = SplitLines(newCode);
        }

        public static AP.ChangeInfo[] Replace(string oldCode, string newCode, string newLine = "\r\n") {
            return new LinePreservingCodeReplacer(oldCode, newCode, newLine).ReplaceCode();
        }

        public AP.ChangeInfo[] ReplaceCode() {
            List<AP.ChangeInfo> edits = new List<AP.ChangeInfo>();
            var oldLineMapping = new Dictionary<string, List<int>>();   // line to line #
            for (int i = 0; i < _oldLines.Length; i++) {
                List<int> lineInfo;
                if (!oldLineMapping.TryGetValue(_oldLines[i].Text, out lineInfo)) {
                    oldLineMapping[_oldLines[i].Text] = lineInfo = new List<int>();
                }
                lineInfo.Add(i);
            }

            int curOldLine = 0;
            for (int curNewLine = 0; curOldLine < _oldLines.Length && curNewLine < _newLines.Length; curOldLine++) {
                if (_oldLines[curOldLine] == _newLines[curNewLine]) {
                    curNewLine++;
                    continue;
                }

                bool replaced = false;
                // replace starts on this line, figure out where it ends...
                int startNewLine = curNewLine;
                for (curNewLine += 1; curNewLine < _newLines.Length; curNewLine++) {
                    List<int> lines;
                    if (oldLineMapping.TryGetValue(_newLines[curNewLine].Text, out lines)) {
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
                    ReplaceLines(edits, curOldLine, _oldLines.Length, startNewLine, _newLines.Length);
                    curOldLine = _oldLines.Length;
                    break;
                }
            }

            if (curOldLine < _oldLines.Length) {
                // remove the remaining new lines
                edits.Add(
                    AP.ChangeInfo.FromBounds(
                        "", 
                        _oldLines[curOldLine].Start, 
                        _oldLines[_oldLines.Length - 1].End
                    )
                );
            }
            return edits.ToArray();
        }

        private void ReplaceLines(List<AP.ChangeInfo> edits, int startOldLine, int endOldLine, int startNewLine, int endNewLine) {
            int oldLineCount = endOldLine - startOldLine;
            int newLineCount = endNewLine - startNewLine;

            // replace one line at a time instead of all of the lines at once so that we preserve breakpoints
            int excessNewLineStart = startNewLine - startOldLine;
            for (int i = startOldLine; i < endOldLine && i < (endNewLine - startNewLine + startOldLine); i++) {
                edits.Add(
                    AP.ChangeInfo.FromBounds(
                        _newLines[startNewLine + i - startOldLine].Text,
                        _oldLines[i].Start,
                        _oldLines[i].End
                    )
                );
                excessNewLineStart = startNewLine + i - startOldLine + 1;
            }

            if (oldLineCount > newLineCount) {
                // we end up w/ less lines, we need to delete some text
                edits.Add(
                    AP.ChangeInfo.FromBounds(
                        "",
                        _oldLines[endOldLine - (oldLineCount - newLineCount)].Start,
                        _oldLines[endOldLine - 1].End
                    )
                );
            } else if (oldLineCount < newLineCount) {
                // we end up w/ more lines, we need to insert some text
                edits.Add(
                    new AP.ChangeInfo() {
                        newText = string.Join(
                            _newLine,
                            _newLines,
                            excessNewLineStart,
                            endNewLine - excessNewLineStart
                        ) + _newLine,
                        start = _oldLines[endOldLine - 1].EndIncludingLineBreak,
                        length = 0,
                    }
                );
            }
        }

        class LineInfo {
            public readonly string Text;
            public int Start, Length;
            public string LineBreak;

            public LineInfo(string text, int start, int length, string lineBreak) {
                Text = text;
                Start = start;
                Length = length;
                LineBreak = lineBreak;
            }

            public int End {
                get {
                    return Start + Length;
                }
            }

            public int EndIncludingLineBreak {
                get {
                    return End + LineBreak.Length;
                }
            }
        }

        private static LineInfo[] SplitLines(string text) {
            int nextLine, offset = 0;
            List<LineInfo> lines = new List<LineInfo>();
            const string cr = "\r", lf = "\n", crLf = "\r\n";
            while ((nextLine = text.IndexOfAny(_lineSeperators, offset)) != -1) {
                string lineBreak;

                if (text[nextLine] == '\r' && nextLine + 1 < text.Length && text[nextLine + 1] == '\n') {
                    lineBreak = crLf;
                } else if (text[nextLine] == '\r') {
                    lineBreak = cr;
                } else {
                    lineBreak = lf;
                }
                
                lines.Add(
                    new LineInfo(
                        text.Substring(offset, nextLine - offset),
                        offset,
                        nextLine - offset,
                        lineBreak
                    )
                );

                offset = nextLine + lineBreak.Length;
            }

            if (offset != text.Length) {
                lines.Add(
                    new LineInfo(
                        text.Substring(offset, text.Length - offset),
                        offset,
                        text.Length - offset,
                        String.Empty
                    )
                );

            }
            return lines.ToArray();
        }

    }

    static class RefactoringTextViewExtensions {
        /// <summary>
        /// Replaces a range of text with new text attempting only modifying lines which changed 
        /// and doing so in a single edit.
        /// </summary>
        public static AP.ChangeInfo[] ReplaceByLines(this string oldCode, string newCode, string newLine = "\r\n") {
            return LinePreservingCodeReplacer.Replace(oldCode, newCode, newLine);
        }
    }
}
