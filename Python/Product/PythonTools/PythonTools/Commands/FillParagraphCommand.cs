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
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command to send selected text from a buffer to the remote REPL window.
    /// </summary>
    class FillParagraphCommand : Command {
        private readonly System.IServiceProvider _serviceProvider;

        private const string _sentenceTerminators = ".!?";
        private static Regex _startDocStringRegex = new Regex("^[\t ]*('''|\"\"\")[\t ]*$");
        private static Regex _endDocStringRegex = new Regex("('''|\"\"\")[\t ]*$");
        private static Regex _commentRegex = new Regex("^[\t ]*#+[\t ]*");
        private static Regex _whitespaceRegex = new Regex("^[\t ]+");

        public FillParagraphCommand(System.IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public override void DoCommand(object sender, EventArgs args) {
            FillCommentParagraph(CommonPackage.GetActiveTextView(_serviceProvider));
        }

        /// <summary>
        /// FillCommentParagraph fills the text in a contiguous block of comment lines,
        ///   each having the same [whitespace][commentchars][whitespace] prefix.  Each
        /// resulting line is as long as possible without exceeding the 
        /// Config.CodeWidth.Width column.  This function also works on paragraphs
        /// within doc strings, each having the same leading whitespace.  Leading
        /// whitespace must be space characters.        
        /// </summary>
        public void FillCommentParagraph(ITextView view) {
            var caret = view.Caret;
            var txtbuf = view.TextBuffer;
            // don't clone Caret, need point that works at buffer level not view.
            var bufpt = caret.Position.BufferPosition;  //txtbuf.GetTextPoint(caret.CurrentPosition);
            var fillPrefix = GetFillPrefix(view, bufpt);
            
            // TODO: Fix doc string parsing
            if (fillPrefix.Prefix == null || fillPrefix.Prefix.Length == 0 || fillPrefix.IsDocString) {
                System.Windows.MessageBox.Show(Strings.FillCommentSelectionError, Strings.ProductTitle);
                return;
            }

            var start = FindParagraphStart(bufpt, fillPrefix);
            var end = FindParagraphEnd(bufpt, fillPrefix);
            string newLine = view.Options.GetNewLineCharacter();
            using (var edit = view.TextBuffer.CreateEdit()) {
                int startLine = start.GetContainingLineNumber();
                string[] lines = new string[end.GetContainingLineNumber() - startLine + 1];
                for (int i = 0; i < lines.Length; i++) {
                    lines[i] = start.Snapshot.GetLineFromLineNumber(startLine + i).GetText();
                }

                int curLine = 0, curOffset = fillPrefix.Prefix.Length;

                int columnCutoff = 80 - fillPrefix.Prefix.Length;
                int defaultColumnCutoff = columnCutoff;
                StringBuilder newText = new StringBuilder(end.Position - start.Position);
                while (curLine < lines.Length) {
                    string curLineText = lines[curLine];
                    int lastSpace = curLineText.Length;
                    
                    // skip leading white space
                    while (curOffset < curLineText.Length && Char.IsWhiteSpace(curLineText[curOffset])) {
                        curOffset++;
                    }                    

                    // find next word
                    for (int i = curOffset; i < curLineText.Length; i++) {
                        if (Char.IsWhiteSpace(curLineText[i])) {
                            lastSpace = i;
                            break;
                        }
                    }

                    if (lastSpace - curOffset < columnCutoff || columnCutoff == defaultColumnCutoff) {
                        // we found a like break in the region and it's a reasonable size or
                        // we have a really long word that we need to append unbroken
                        if (columnCutoff == defaultColumnCutoff) {
                            // first time we're appending to this line
                            newText.Append(fillPrefix.Prefix);
                        }

                        newText.Append(curLineText, curOffset, lastSpace - curOffset);
                        
                        // append appropriate spacing
                        if (_sentenceTerminators.IndexOf(curLineText[lastSpace - 1]) != -1 ||   // we end in punctuation
                            ((lastSpace - curOffset) > 1 &&                                     // we close a paren that ends in punctuation
                            curLineText[lastSpace - curOffset] == ')' &&
                            _sentenceTerminators.IndexOf(curLineText[lastSpace - 2]) != -1)) {

                            newText.Append("  ");
                            columnCutoff -= lastSpace - curOffset + 2;                        
                        } else {
                            newText.Append(' ');
                            columnCutoff -= lastSpace - curOffset + 1;
                        }
                        curOffset = lastSpace + 1;
                    } else {
                        // current word is too long to append.  Start the next line.
                        while (newText.Length > 0 && newText[newText.Length - 1] == ' ') {
                            newText.Length = newText.Length - 1;
                        }
                        newText.Append(newLine);
                        columnCutoff = defaultColumnCutoff;
                    }
                    
                    if (curOffset >= lines[curLine].Length) {
                        // we're not reading from the next line
                        curLine++;
                        curOffset = fillPrefix.Prefix.Length;
                    }
                }
                while (newText.Length > 0 && newText[newText.Length - 1] == ' ') {
                    newText.Length = newText.Length - 1;
                }

                // commit the new text
                edit.Delete(start.Position, end.Position - start.Position);
                edit.Insert(start.Position, newText.ToString());
                edit.Apply();
            }

        }

        private static int GetFirstNonWhiteSpaceCharacterOnLine(ITextSnapshotLine line) {
            var text = line.GetText();
            for (int i = 0; i < text.Length; i++) {
                if (!Char.IsWhiteSpace(text[i])) {
                    return line.Start.Position + i;
                }
            }
            return line.End.Position;
        }

        private bool PrevNotParaStart(FillPrefix prefix, int prev_num, int ln_num, string prev_txt, Regex regexp) {
            var notBufFirstLn = prev_num != ln_num;
            var notFileDocStrAndHasPrefix = !string.IsNullOrEmpty(prefix.Prefix) && prev_txt.StartsWithOrdinal(prefix.Prefix);
            var isFileDocStrAndNotEmptyLine = string.IsNullOrEmpty(prefix.Prefix) && !string.IsNullOrEmpty(prev_txt);
            var notDocStringOrNoTripleQuotesYet = !(prefix.IsDocString && regexp.Match(prev_txt).Success);
            return (notBufFirstLn &&
                    (notFileDocStrAndHasPrefix || isFileDocStrAndNotEmptyLine) &&
                    notDocStringOrNoTripleQuotesYet);
        }

        // Finds first contiguous line that has the same prefix as the current line.
        // If the prefix is not a comment start prefix, then this function stops at
        // the first line with triple quotes or that is empty.  It returns the point
        // positioned at the start of the first line.
        private SnapshotPoint FindParagraphStart(SnapshotPoint point, FillPrefix prefix) {
            var buf = point.Snapshot.TextBuffer;

            Regex regexp = null;
            if (prefix.IsDocString) {
                regexp = new Regex(prefix + "('''|\"\"\")");
                // Check for edge case of being on first line of docstring with quotes.
                if (regexp.Match(point.GetContainingLine().GetText()).Success) {
                    return point.GetContainingLine().Start;
                }
            }

            var line = point.GetContainingLine();
            var ln_num = line.LineNumber;

            var prev_num = ln_num - 1;
            if (prev_num < 0) {
                return line.Start;
            }
            var prev_txt = point.Snapshot.GetLineFromLineNumber(prev_num).GetText();

            while (PrevNotParaStart(prefix, prev_num, ln_num, prev_txt, regexp)) {
                ln_num = prev_num;
                prev_num--;
                if (prev_num < 0) {
                    return new SnapshotPoint(point.Snapshot, 0);
                }
                prev_txt = point.Snapshot.GetLineFromLineNumber(prev_num).GetText();
            }

            SnapshotPoint res;
            if (!prefix.IsDocString ||
                string.IsNullOrEmpty(prev_txt) ||
                _startDocStringRegex.IsMatch(prev_txt)) {
                // Normally ln is the start for filling, prev line stopped the loop.
                res = point.Snapshot.GetLineFromLineNumber(ln_num).Start;
            } else {
                // If we're in a doc string, and prev is not just the triple quotes
                // line, and prev is not empty, then we use prev line because it
                // has text on it we need to fill.  Point is already on prev line.
                res = point.Snapshot.GetLineFromLineNumber(prev_num).Start;
            }
            return res;
        }

        private bool NextNotParaEnd(FillPrefix prefix, int next_num, int ln_num, string next_txt, Regex regexp) {
            var notBufLastLn = next_num != ln_num;
            var notFileDocStrAndHasPrefix = !string.IsNullOrEmpty(prefix.Prefix) && next_txt.StartsWithOrdinal(prefix.Prefix);
            var isFileDocStrAndNotEmptyLine = string.IsNullOrEmpty(prefix.Prefix) && string.IsNullOrEmpty(next_txt);
            var notDocStringOrNoTripleQuotesYet = !(prefix.IsDocString && regexp.Match(next_txt).Success);
            return (notBufLastLn &&
                    (notFileDocStrAndHasPrefix || isFileDocStrAndNotEmptyLine) &&
                    notDocStringOrNoTripleQuotesYet);
        }

        // Finds last contiguous line that has the same prefix as the current line. If
        // the prefix is not a comment start prefix, then this function stops at the
        // last line with triple quotes or that is empty.  It returns the point
        // positioned at the start of the last line.
        private SnapshotPoint FindParagraphEnd(SnapshotPoint point, FillPrefix prefix) {
            Regex regexp = null;
            if (prefix.IsDocString) {
                regexp = _endDocStringRegex;
                // Check for edge case of being on last line of doc string with quotes.
                if (regexp.Match(point.GetContainingLine().GetText()).Success) {
                    return point.GetContainingLine().Start;
                }
            }

            var line = point.GetContainingLine();
            var ln_num = line.LineNumber;

            var next_num = ln_num + 1;
            if (next_num >= point.Snapshot.LineCount) {
                return line.End;
            }

            var next_txt = point.Snapshot.GetLineFromLineNumber(next_num).GetText();

            while (NextNotParaEnd(prefix, next_num, ln_num, next_txt, regexp)) {
                ln_num = next_num;
                next_num++;
                if (next_num == point.Snapshot.LineCount) {
                    break;
                }
                next_txt = point.Snapshot.GetLineFromLineNumber(next_num).GetText();
            }

            SnapshotPoint res;
            if (!prefix.IsDocString ||
                string.IsNullOrEmpty(next_txt) ||
                _startDocStringRegex.IsMatch(next_txt)) {
                // Normally ln is the last line to fill, next line stopped the loop.
                res = point.Snapshot.GetLineFromLineNumber(ln_num).End;
            } else {
                // If we're in a doc string, and next is not just the triple quotes
                // line, and next is not empty, then we use next line because it has
                // text on it we need to fill.  Point is on next line.
                res = point.Snapshot.GetLineFromLineNumber(next_num).End;
            }
            return res;
        }

        struct FillPrefix {
            public readonly string Prefix;
            public readonly bool IsDocString;

            public FillPrefix(string prefix, bool isDocString) {
                Prefix = prefix;
                IsDocString = isDocString;
            }
        }

        /// <summary>
        ///  Returns the <whitespace><commentchars><whitespace> or the <whitespace>
        ///  prefix on point's line.  Returns None if not on a suitable line for filling.
        /// </summary>
        private FillPrefix GetFillPrefix(ITextView textWindow, SnapshotPoint point) {
            var regexp = _commentRegex;

            var line = point.GetContainingLine();
            var lntxt = point.GetContainingLine().GetText();
            var match = regexp.Match(lntxt);
            if (match.Success) {
                return new FillPrefix(lntxt.Substring(0, match.Length), false);
            } else if (string.IsNullOrEmpty(lntxt.Trim())) {
                return new FillPrefix(null, false);
            } else {
                regexp = _whitespaceRegex;
                match = regexp.Match(lntxt);
                if (match.Success && IsDocString(textWindow, point)) {
                    return new FillPrefix(lntxt.Substring(0, match.Length), true);
                } else if (/*GetFirstNonWhiteSpaceCharacterOnLine(line) == 0 && */IsDocString(textWindow, point)) {
                    return new FillPrefix("", true);
                } else {
                    return new FillPrefix(null, false);
                }
            }
        }

        private bool IsDocString(ITextView textWindow, SnapshotPoint point) {
            var aggregator = _serviceProvider.GetComponentModel().GetService<IClassifierAggregatorService>();
            IClassifier classifier = aggregator.GetClassifier(textWindow.TextBuffer);

            var curLine = point.GetContainingLine();
            var tokens = classifier.GetClassificationSpans(curLine.Extent);
            // TODO: Is null the right check for not having tokens?
            for (int i = curLine.LineNumber - 1; i >= 0 && tokens == null; i--) {
                tokens = classifier.GetClassificationSpans(curLine.Extent);
                if (tokens != null) {
                    break;
                }
                i = i - 1;
            }
            if (tokens == null) {
                return false;
            }
            // Tokens is NOT None here.
            // If first token found on a line is only token and is string literal,
            // we're in a doc string.  Because multiline, can't be "" or ''.
            return tokens.Count == 1 && tokens[0].ClassificationType.IsOfType(PredefinedClassificationTypeNames.String);
        }

        public override int? EditFilterQueryStatus(ref VisualStudio.OLE.Interop.OLECMD cmd, IntPtr pCmdText) {
            var activeView = CommonPackage.GetActiveTextView(_serviceProvider);
            if (activeView != null && activeView.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                cmd.cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
            } else {
                cmd.cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE);
            }

            return VSConstants.S_OK;
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    ((OleMenuCommand)sender).Visible = false;
                    ((OleMenuCommand)sender).Supported = false;
                };
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidFillParagraph; }
        }
    }
}
