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
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

namespace Microsoft.PythonTools.Editor {
    internal static class AutoIndent {
        internal static int GetIndentation(string line, int tabSize) {
            int res = 0;
            for (int i = 0; i < line.Length; i++) {
                if (line[i] == ' ') {
                    res++;
                } else if (line[i] == '\t') {
                    res += tabSize;
                } else {
                    break;
                }
            }
            return res;
        }

        private struct LineInfo {
            public static readonly LineInfo Empty = new LineInfo();
            public bool NeedsUpdate;
            public int Indentation;
            public bool ShouldIndentAfter;
            public bool ShouldDedentAfter;
        }

        private static bool IsWhitespace(TokenCategory category) {
            return category == TokenCategory.Comment ||
                category == TokenCategory.DocComment ||
                category == TokenCategory.LineComment ||
                category == TokenCategory.WhiteSpace;
        }

        private static bool IsExplicitLineJoin(TokenInfo token, ITextSnapshot snapshot) => token.Category == TokenCategory.Operator && GetText(token, snapshot) == "\\";

        private static string GetText(TokenInfo token, ITextSnapshot snapshot) => token.SourceSpan.ToSnapshotSpan(snapshot).GetText();

        private static int CalculateIndentation(
            string baseline,
            ITextSnapshotLine line,
            IEditorOptions options,
            PythonTextBufferInfo buffer
        ) {
            var snapshot = line.Snapshot;
            if (snapshot.TextBuffer != buffer.Buffer) {
                throw new ArgumentException("buffer mismatch");
            }

            int indentation = GetIndentation(baseline, options.GetTabSize());
            int tabSize = options.GetIndentSize();
            var tokens = buffer.GetTokens(line).ToList();
            if (tokens.Count > 0 && !IsUnterminatedStringToken(tokens[tokens.Count - 1], line)) {
                int tokenIndex = tokens.Count - 1;

                while (tokenIndex >= 0 && IsWhitespace(tokens[tokenIndex].Category)) {
                    tokenIndex--;
                }

                if (tokenIndex < 0) {
                    return indentation;
                }

                var token = tokens[tokenIndex];

                if (IsExplicitLineJoin(token, snapshot)) {
                    // explicit line continuation, we indent 1 level for the continued line unless
                    // we're already indented because of multiple line continuation characters.

                    indentation = GetIndentation(line.GetText(), options.GetTabSize());
                    var joinedLine = token.SourceSpan.Start.Line - 1;
                    if (joinedLine > 0) {
                        var prevLineTokens = buffer.GetTokens(line.Snapshot.GetLineFromLineNumber(joinedLine - 1)).ToList();
                        if (prevLineTokens.Count == 0 || !IsExplicitLineJoin(prevLineTokens.Last(), snapshot)) {
                            indentation += tabSize;
                        }
                    } else {
                        indentation += tabSize;
                    }

                    return indentation;
                }

                var tokenStack = new Stack<TokenInfo?>();
                tokenStack.Push(null);  // end with an implicit newline
                int endAtLine = -1, currentLine = token.SourceSpan.Start.Line;

                foreach (var t in buffer.GetTokensInReverseFromPoint(token.SourceSpan.Start.ToSnapshotPoint(snapshot))) {
                    if (t.Category == TokenCategory.WhiteSpace && t.SourceSpan.Start.Line != currentLine) {
                        tokenStack.Push(null);
                        currentLine = t.SourceSpan.Start.Line;
                    } else {
                        tokenStack.Push(t);
                    }

                    if (t.SourceSpan.End.Line == endAtLine) {
                        break;
                    } else if (t.Category == TokenCategory.Keyword && PythonKeywords.IsOnlyStatementKeyword(GetText(t, snapshot), buffer.LanguageVersion)) {
                        endAtLine = t.SourceSpan.Start.Line - 1;
                    }
                }

                var indentStack = new Stack<LineInfo>();
                var current = LineInfo.Empty;

                while (tokenStack.Count > 0) {
                    var t = tokenStack.Pop();
                    if (t == null) {
                        current.NeedsUpdate = true;
                        continue;
                    }

                    var txt = GetText(t.Value, snapshot);
                    var tline = snapshot.GetLineFromLineNumber(t.Value.SourceSpan.Start.Line - 1);

                    if (t.Value.Category == TokenCategory.Grouping && txt.Length == 1 && "([{".Contains(txt)) {
                        indentStack.Push(current);
                        var next = tokenStack.Count > 0 ? tokenStack.Peek() : null;
                        if (next != null && next.Value.SourceSpan.End.Line == t.Value.SourceSpan.End.Line) {
                            // Put indent at same depth as grouping
                            current = new LineInfo {
                                Indentation = t.Value.SourceSpan.End.Column - 1
                            };
                        } else {
                            // Put indent at one indent deeper than this line
                            current = new LineInfo {
                                Indentation = GetIndentation(tline.GetText(), tabSize) + tabSize
                            };
                        }
                    } else if (t.Value.Category == TokenCategory.Grouping && txt.Length == 1 && ")]}".Contains(txt)) {
                        if (indentStack.Count > 0) {
                            current = indentStack.Pop();
                        } else {
                            current.NeedsUpdate = true;
                        }
                    } else if (IsExplicitLineJoin(t.Value, snapshot)) {
                        while (t != null && tokenStack.Count > 0) {
                            t = tokenStack.Pop();
                        }
                    } else if (current.NeedsUpdate == true) {
                        current = new LineInfo {
                            Indentation = GetIndentation(tline.GetText(), tabSize)
                        };
                    }

                    if (t.Value.Category == TokenCategory.Keyword &&
                        ShouldDedentAfterKeyword(txt)) {    // dedent after some statements
                        current.ShouldDedentAfter = true;
                    }

                    if (txt == ":" &&                       // indent after a colon
                        indentStack.Count == 0) {           // except in a grouping
                        current.ShouldIndentAfter = true;
                        // If the colon isn't at the end of the line, cancel it out.
                        // If the following is a ShouldDedentAfterKeyword, only one dedent will occur.
                        current.ShouldDedentAfter = (tokenStack.Count != 0 && tokenStack.Peek() != null);
                    }
                }

                indentation = current.Indentation +
                    (current.ShouldIndentAfter ? tabSize : 0) -
                    (current.ShouldDedentAfter ? tabSize : 0);
            }

            return indentation;
        }

        private static bool IsUnterminatedStringToken(TokenInfo token, ITextSnapshotLine line) {
            if (token.Category == TokenCategory.IncompleteMultiLineStringLiteral) {
                return true;
            }
            if (token.Category != TokenCategory.StringLiteral) {
                return false;
            }

            try {
                var span = new SnapshotSpan(line.Start + token.SourceSpan.End.Column - 2, 1);
                var c = span.GetText();
                return c == "\"" || c == "'";
            } catch (ArgumentException) {
                return false;
            }
        }

        private static bool ShouldDedentAfterKeyword(string keyword) {
            return keyword == "pass" || keyword == "return" || keyword == "break" || keyword == "continue" || keyword == "raise";
        }

        private static bool IsBlankLine(string lineText) {
            foreach (char c in lineText) {
                if (!Char.IsWhiteSpace(c)) {
                    return false;
                }
            }
            return true;
        }

        private static void SkipPreceedingBlankLines(ITextSnapshotLine line, out string baselineText, out ITextSnapshotLine baseline) {
            string text;
            while (line.LineNumber > 0) {
                line = line.Snapshot.GetLineFromLineNumber(line.LineNumber - 1);
                text = line.GetText();
                if (!IsBlankLine(text)) {
                    baseline = line;
                    baselineText = text;
                    return;
                }
            }
            baselineText = line.GetText();
            baseline = line;
        }

        private static bool PythonContentTypePrediciate(ITextSnapshot snapshot) {
            return snapshot.ContentType.IsOfType(PythonCoreConstants.ContentType);
        }

        internal static int? GetLineIndentation(PythonTextBufferInfo buffer, ITextSnapshotLine line, ITextView textView) {
            if (buffer == null) {
                return 0;
            }
            var options = textView.Options;

            ITextSnapshotLine baseline;
            string baselineText;
            SkipPreceedingBlankLines(line, out baselineText, out baseline);

            ITextBuffer targetBuffer = line.Snapshot.TextBuffer;
            if (!targetBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                var match = textView.BufferGraph.MapDownToFirstMatch(line.Start, PointTrackingMode.Positive, PythonContentTypePrediciate, PositionAffinity.Successor);
                if (match == null) {
                    return 0;
                }
                targetBuffer = match.Value.Snapshot.TextBuffer;
            }

            var desiredIndentation = CalculateIndentation(baselineText, baseline, options, buffer);
            if (desiredIndentation < 0) {
                desiredIndentation = 0;
            }

            // Map indentation back to the view's text buffer.
            if (textView.TextBuffer != targetBuffer) {
                var viewLineStart = textView.BufferGraph.MapUpToSnapshot(
                    baseline.Start,
                    PointTrackingMode.Positive,
                    PositionAffinity.Successor,
                    textView.TextSnapshot
                );
                if (viewLineStart.HasValue) {
                    desiredIndentation += viewLineStart.Value - viewLineStart.Value.GetContainingLine().Start;
                }
            }

            var caretLine = textView.Caret.Position.BufferPosition.GetContainingLine();
            // VS will get the white space when the user is moving the cursor or when the user is doing an edit which
            // introduces a new line.  When the user is moving the cursor the caret line differs from the line
            // we're querying.  When editing the lines are the same and so we want to account for the white space of
            // non-blank lines.  An alternate strategy here would be to watch for the edit and fix things up after
            // the fact which is what would happen pre-Dev10 when the language would not get queried for non-blank lines
            // (and is therefore what C# and other languages are doing).
            if (caretLine.LineNumber == line.LineNumber) {
                var lineText = caretLine.GetText();
                int indentationUpdate = 0;
                for (int i = textView.Caret.Position.BufferPosition.Position - caretLine.Start; i < lineText.Length; i++) {
                    if (lineText[i] == ' ') {
                        indentationUpdate++;
                    } else if (lineText[i] == '\t') {
                        indentationUpdate += textView.Options.GetIndentSize();
                    } else {
                        if (indentationUpdate > desiredIndentation) {
                            // we would dedent this line (e.g. there's a return on the previous line) but the user is
                            // hitting enter with a statement to the right of the caret and they're in the middle of white space.
                            // So we need to instead just maintain the existing indentation level.
                            desiredIndentation = Math.Max(GetIndentation(baselineText, options.GetTabSize()) - indentationUpdate, 0);
                        } else {
                            desiredIndentation -= indentationUpdate;
                        }
                        break;
                    }
                }
            }

            return desiredIndentation;
        }
    }
}
