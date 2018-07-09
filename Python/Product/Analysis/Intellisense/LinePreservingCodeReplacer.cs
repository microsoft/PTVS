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
using Microsoft.PythonTools.Analysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Replaces a range of text with new text attempting only modifying lines which changed 
    /// and doing so in a single edit.
    /// </summary>
    class LinePreservingCodeReplacer {
        private readonly string _newLine;
        private readonly Line[] _newLines, _oldLines;
        private readonly SourceLocation _start;
        
        private LinePreservingCodeReplacer(SourceLocation start, string oldCode, string newCode, string newLine) {
            _newLine = newLine;
            _start = start;
            _oldLines = GetLines(oldCode, start.Line);
            _newLines = GetLines(newCode, start.Line);
        }

        public static IReadOnlyList<DocumentChange> Replace(SourceLocation start, string oldCode, string newCode, string newLine = "\r\n") {
            return new LinePreservingCodeReplacer(start, oldCode, newCode, newLine).ReplaceCode();
        }

        private static Line[] GetLines(string source, int startLine) 
            => LineInfo.SplitLines(source, startLine - 1).Select(l => new Line(l, source)).ToArray();

        private static bool LcsEqualityComparer(Line column, Line row) 
            => column.Info.Length == row.Info.Length && 
               string.CompareOrdinal(column.Source, column.Info.Start, row.Source, row.Info.Start, column.Info.Length) == 0;


        public IReadOnlyList<DocumentChange> ReplaceCode() {
            var lcs = LongestCommonSequence<Line>.Find(_oldLines, _newLines, LcsEqualityComparer);
            var edits = new List<DocumentChange>();

            foreach (var diff in lcs) {
                if (diff.NewLength == 0) {
                    edits.Add(Delete(diff.OldStart, diff.OldEnd));
                } else if (diff.OldLength == 0) {
                    edits.Add(Insert(diff.OldStart, diff.NewStart, diff.NewEnd));
                } else {
                    var length = Math.Min(diff.NewLength, diff.OldLength);
                    for (var i = 0; i < length; i++) {
                        edits.Add(Replace(diff.OldStart + i, diff.NewStart + i));
                    }

                    if (diff.OldLength > length) {
                        edits.Add(Delete(diff.OldStart + length, diff.OldEnd));
                    } else if (diff.NewLength > length) {
                        edits.Add(Insert(diff.OldStart + length, diff.NewStart + length, diff.NewEnd));
                    }
                }
            }

            if (_start.Column > 1) {
                foreach (var e in edits) {
                    if (e.ReplacedSpan.End.Line == _start.Line) {
                        e.ReplacedSpan = new SourceSpan(e.ReplacedSpan.Start, e.ReplacedSpan.End.AddColumns(_start.Column - 1));
                    }
                    if (e.ReplacedSpan.Start.Line == _start.Line) {
                        e.ReplacedSpan = new SourceSpan(e.ReplacedSpan.Start.AddColumns(_start.Column - 1), e.ReplacedSpan.End);
                    }
                }
            }

            return edits.AsReadOnly();
        }

        private DocumentChange Replace(int oldLineIndex, int newLineIndex) {
            var oldLineInfo = _oldLines[oldLineIndex].Info;
            var newLineInfo = _newLines[newLineIndex].Info;
            var newSource = _newLines[newLineIndex].Source;
            var text = newSource.Substring(newLineInfo.Start, newLineInfo.Length);
            return DocumentChange.Replace(oldLineInfo.SourceStart, oldLineInfo.SourceEnd, text);
        }

        private DocumentChange Insert(int oldStart, int newStart, int newEnd) {
            var newSource = _newLines[newStart].Source;
            var insertText = new StringBuilder();
            for (var i = newStart; i < newEnd; i++) {
                insertText
                    .Append(newSource, _newLines[i].Info.Start, _newLines[i].Info.Length)
                    .Append(_newLine);
            }

            insertText.Append(newSource, _newLines[newEnd].Info.Start, _newLines[newEnd].Info.Length);
            if (oldStart < _oldLines.Length) {
                insertText.Append(_newLine);
                return DocumentChange.Insert(insertText.ToString(), _oldLines[oldStart].Info.SourceStart);
            }

            // If inserting after the last line, line break should be added before the inserted text
            insertText.Insert(0, _newLine);
            return DocumentChange.Insert(insertText.ToString(), _oldLines[_oldLines.Length - 1].Info.SourceEndIncludingLineBreak);
        }

        private DocumentChange Delete(int oldStart, int oldEnd) {
            return DocumentChange.Delete(new SourceSpan(
                _oldLines[oldStart].Info.SourceStart,
                _oldLines[oldEnd].Info.SourceEndIncludingLineBreak
            ));
        }

        private struct Line {
            public LineInfo Info { get; }
            public string Source { get; }

            public Line(LineInfo lineInfo, string source) {
                Info = lineInfo;
                Source = source;
            }
        }
    }

    static class RefactoringTextViewExtensions {
        /// <summary>
        /// Replaces a range of text with new text attempting only modifying lines which changed 
        /// and doing so in a single edit.
        /// </summary>
        public static IReadOnlyList<DocumentChange> ReplaceByLines(this string oldCode, SourceLocation start, string newCode, string newLine = "\r\n") {
            return LinePreservingCodeReplacer.Replace(start, oldCode, newCode, newLine);
        }
    }
}
