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

using Microsoft.PythonTools.Editor.Core;

namespace Microsoft.PythonTools.Editor
{
    internal static class AutoIndent
    {
        internal static int GetIndentation(string line, int tabSize)
        {
            int res = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == ' ')
                {
                    res++;
                }
                else if (line[i] == '\t')
                {
                    res += tabSize;
                }
                else
                {
                    break;
                }
            }
            return res;
        }

        private struct LineInfo
        {
            public static readonly LineInfo Empty = new LineInfo();
            public bool NeedsUpdate;
            public int Indentation;
            public bool ShouldIndentAfter;
            public bool ShouldDedentAfter;
        }

        private static int CalculateIndentation(
            string baseline,
            ITextSnapshotLine line,
            IEditorOptions options,
            PythonTextBufferInfo buffer
        )
        {
            var snapshot = line.Snapshot;
            if (snapshot.TextBuffer != buffer.Buffer)
            {
                throw new ArgumentException("buffer mismatch");
            }

            int indentation = GetIndentation(baseline, options.GetTabSize());
            int tabSize = options.GetIndentSize();
            var tokens = buffer.GetTokens(line).ToList();

            while (tokens.Count > 0 && IsWhitespace(tokens[tokens.Count - 1].Category))
            {
                tokens.RemoveAt(tokens.Count - 1);
            }

            if (tokens.Count == 0 || IsUnterminatedStringToken(tokens[tokens.Count - 1], snapshot))
            {
                return indentation;
            }

            if (HasExplicitLineJoin(tokens, snapshot))
            {
                // explicit line continuation, we indent 1 level for the continued line unless
                // we're already indented because of multiple line continuation characters.
                indentation = GetIndentation(line.GetText(), options.GetTabSize());
                var joinedLine = line.LineNumber - 1;
                if (joinedLine >= 0)
                {
                    var prevLineTokens = buffer.GetTokens(snapshot.GetLineFromLineNumber(joinedLine)).ToList();
                    if (prevLineTokens.Count == 0 || !HasExplicitLineJoin(prevLineTokens, snapshot))
                    {
                        indentation += tabSize;
                    }
                }
                else
                {
                    indentation += tabSize;
                }

                return indentation;
            }

            var tokenStack = new Stack<TrackingTokenInfo?>();
            tokenStack.Push(null);  // end with an implicit newline
            int endAtLine = -1, currentLine = tokens.Last().LineNumber;

            foreach (var t in buffer.GetTokensInReverseFromPoint(tokens.Last().ToSnapshotSpan(snapshot).Start))
            {
                if (t.LineNumber == currentLine)
                {
                    tokenStack.Push(t);
                }
                else
                {
                    tokenStack.Push(null);
                }

                if (t.LineNumber == endAtLine)
                {
                    break;
                }
                else if (t.Category == TokenCategory.Keyword && PythonKeywords.IsOnlyStatementKeyword(t.GetText(snapshot), buffer.LanguageVersion))
                {
                    endAtLine = t.LineNumber - 1;
                }

                if (t.LineNumber != currentLine)
                {
                    currentLine = t.LineNumber;
                    if (t.Category != TokenCategory.WhiteSpace && t.Category != TokenCategory.Comment && t.Category != TokenCategory.LineComment)
                    {
                        tokenStack.Push(t);
                    }
                }
            }

            var indentStack = new Stack<LineInfo>();
            var current = LineInfo.Empty;

            while (tokenStack.Count > 0)
            {
                var t = tokenStack.Pop();
                if (t == null)
                {
                    current.NeedsUpdate = true;
                    continue;
                }

                var tline = new Lazy<string>(() => snapshot.GetLineFromLineNumber(t.Value.LineNumber).GetText());

                if (IsOpenGrouping(t.Value, snapshot))
                {
                    indentStack.Push(current);
                    var next = tokenStack.Count > 0 ? tokenStack.Peek() : null;
                    if (next != null && next.Value.LineNumber == t.Value.LineNumber)
                    {
                        // Put indent at same depth as grouping
                        current = new LineInfo
                        {
                            Indentation = t.Value.ToSourceSpan().End.Column - 1
                        };
                    }
                    else
                    {
                        // Put indent at one indent deeper than this line
                        current = new LineInfo
                        {
                            Indentation = GetIndentation(tline.Value, tabSize) + tabSize
                        };
                    }
                }
                else if (IsCloseGrouping(t.Value, snapshot))
                {
                    if (indentStack.Count > 0)
                    {
                        current = indentStack.Pop();
                    }
                    else
                    {
                        current.NeedsUpdate = true;
                    }
                }
                else if (IsExplicitLineJoin(t.Value, snapshot))
                {
                    while (t != null && tokenStack.Count > 0)
                    {
                        t = tokenStack.Pop();
                    }
                    if (!t.HasValue)
                    {
                        continue;
                    }
                }
                else if (current.NeedsUpdate == true)
                {
                    current = new LineInfo
                    {
                        Indentation = GetIndentation(tline.Value, tabSize)
                    };
                }

                if (ShouldDedentAfterKeyword(t.Value, snapshot))
                {    // dedent after some statements
                    current.ShouldDedentAfter = true;
                }

                if (IsColon(t.Value, snapshot) &&       // indent after a colon
                    indentStack.Count == 0)
                {           // except in a grouping
                    current.ShouldIndentAfter = true;
                    // If the colon isn't at the end of the line, cancel it out.
                    // If the following is a ShouldDedentAfterKeyword, only one dedent will occur.
                    current.ShouldDedentAfter = (tokenStack.Count != 0 && tokenStack.Peek() != null);
                }
            }

            indentation = current.Indentation +
                (current.ShouldIndentAfter ? tabSize : 0) -
                (current.ShouldDedentAfter ? tabSize : 0);

            return indentation;
        }

        private static bool IsOpenGrouping(TrackingTokenInfo token, ITextSnapshot snapshot)
        {
            if (token.Category != TokenCategory.Grouping)
            {
                return false;
            }
            var span = token.ToSnapshotSpan(snapshot);
            return span.Length == 1 && "([{".Contains(span.GetText());
        }

        private static bool IsCloseGrouping(TrackingTokenInfo token, ITextSnapshot snapshot)
        {
            if (token.Category != TokenCategory.Grouping)
            {
                return false;
            }
            var span = token.ToSnapshotSpan(snapshot);
            return span.Length == 1 && ")]}".Contains(span.GetText());
        }

        private static bool IsUnterminatedStringToken(TrackingTokenInfo token, ITextSnapshot snapshot)
        {
            if (token.Category == TokenCategory.IncompleteMultiLineStringLiteral)
            {
                return true;
            }
            if (token.Category != TokenCategory.StringLiteral)
            {
                return false;
            }

            try
            {
                var c = token.GetText(snapshot);
                return c == "\"" || c == "'";
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool ShouldDedentAfterKeyword(TrackingTokenInfo token, ITextSnapshot snapshot)
        {
            if (token.Category != TokenCategory.Keyword)
            {
                return false;
            }
            var keyword = token.GetText(snapshot);
            return keyword == "pass" || keyword == "return" || keyword == "break" || keyword == "continue" || keyword == "raise";
        }

        private static bool IsColon(TrackingTokenInfo token, ITextSnapshot snapshot)
        {
            if (token.Category != TokenCategory.Delimiter)
            {
                return false;
            }
            var span = token.ToSnapshotSpan(snapshot);
            return span.Length == 1 && span.GetText() == ":";
        }

        private static bool IsBlankLine(string lineText)
        {
            foreach (char c in lineText)
            {
                if (!Char.IsWhiteSpace(c))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsWhitespace(TokenCategory category)
        {
            return category == TokenCategory.Comment ||
                category == TokenCategory.DocComment ||
                category == TokenCategory.LineComment ||
                category == TokenCategory.WhiteSpace;
        }

        private static bool IsExplicitLineJoin(TrackingTokenInfo token, ITextSnapshot snapshot)
        {
            if (token.Category != TokenCategory.Operator)
            {
                return false;
            }
            var t = token.GetText(snapshot);
            return t == "\\" || t.TrimEnd('\r', '\n') == "\\";
        }

        private static bool HasExplicitLineJoin(IReadOnlyList<TrackingTokenInfo> tokens, ITextSnapshot snapshot)
        {
            foreach (var t in tokens.Reverse())
            {
                if (IsExplicitLineJoin(t, snapshot))
                {
                    return true;
                }
                if (t.Category != TokenCategory.WhiteSpace)
                {
                    return false;
                }
            }
            return false;
        }

        private static void SkipPreceedingBlankLines(ITextSnapshotLine line, out string baselineText, out ITextSnapshotLine baseline)
        {
            string text;
            while (line.LineNumber > 0)
            {
                line = line.Snapshot.GetLineFromLineNumber(line.LineNumber - 1);
                text = line.GetText();
                if (!IsBlankLine(text))
                {
                    baseline = line;
                    baselineText = text;
                    return;
                }
            }
            baselineText = line.GetText();
            baseline = line;
        }

        internal static int? GetLineIndentation(PythonTextBufferInfo buffer, ITextSnapshotLine line, ITextView textView)
        {
            if (buffer == null)
            {
                return 0;
            }
            var options = textView.Options;

            ITextSnapshotLine baseline;
            string baselineText;
            SkipPreceedingBlankLines(line, out baselineText, out baseline);

            var lineStart = line.Start;
            if (!lineStart.Snapshot.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType))
            {
                var match = textView.MapDownToPythonBuffer(lineStart);
                if (match == null)
                {
                    return 0;
                }
                lineStart = match.Value;
            }

            var desiredIndentation = CalculateIndentation(baselineText, baseline, options, buffer);
            if (desiredIndentation < 0)
            {
                desiredIndentation = 0;
            }

            var caretPos = textView.MapDownToBuffer(textView.Caret.Position.BufferPosition, lineStart.Snapshot.TextBuffer);
            var caretLine = caretPos?.GetContainingLine();
            // VS will get the white space when the user is moving the cursor or when the user is doing an edit which
            // introduces a new line.  When the user is moving the cursor the caret line differs from the line
            // we're querying.  When editing the lines are the same and so we want to account for the white space of
            // non-blank lines.  An alternate strategy here would be to watch for the edit and fix things up after
            // the fact which is what would happen pre-Dev10 when the language would not get queried for non-blank lines
            // (and is therefore what C# and other languages are doing).
            if (caretLine != null && caretLine.LineNumber == line.LineNumber)
            {
                var lineText = caretLine.GetText();
                int indentationUpdate = 0;
                for (int i = caretPos.Value.Position - caretLine.Start; i < lineText.Length; i++)
                {
                    if (lineText[i] == ' ')
                    {
                        indentationUpdate++;
                    }
                    else if (lineText[i] == '\t')
                    {
                        indentationUpdate += textView.Options.GetTabSize();
                    }
                    else
                    {
                        if (indentationUpdate > desiredIndentation)
                        {
                            // we would dedent this line (e.g. there's a return on the previous line) but the user is
                            // hitting enter with a statement to the right of the caret and they're in the middle of white space.
                            // So we need to instead just maintain the existing indentation level.
                            desiredIndentation = Math.Max(GetIndentation(baselineText, options.GetTabSize()) - indentationUpdate, 0);
                        }
                        else
                        {
                            desiredIndentation -= indentationUpdate;
                        }
                        break;
                    }
                }
            }

            // Map indentation back to the view's text buffer.
            if (textView.TextBuffer != lineStart.Snapshot.TextBuffer)
            {
                var viewLineStart = textView.BufferGraph.MapUpToSnapshot(
                    lineStart,
                    PointTrackingMode.Positive,
                    PositionAffinity.Successor,
                    textView.TextSnapshot
                );
                if (viewLineStart.HasValue)
                {
                    desiredIndentation += viewLineStart.Value - viewLineStart.Value.GetContainingLine().Start;
                }
            }

            return desiredIndentation;
        }
    }
}
