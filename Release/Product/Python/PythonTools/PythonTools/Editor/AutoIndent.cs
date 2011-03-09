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
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

namespace Microsoft.PythonTools.Editor {
    internal static class AutoIndent {
        private static string _groupingChars = ",([{";

        private static int GetIndentation(string line, int tabSize) {
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

        private static bool StartsGrouping(ClassificationSpan token) {
            return token.ClassificationType.IsOfType("OpenGroupingClassification");
        }

        private static string CurrentLine(IWpfTextView buffer) {
            return buffer.TextSnapshot.GetLineFromPosition(buffer.Caret.Position.BufferPosition.Position).GetText();
        }

        private static string CurrentLine(IReplWindow buffer) {
            return CurrentLine(buffer.TextView);
        }

        private static bool EndsGrouping(ClassificationSpan token) {
            return token.ClassificationType.IsOfType("CloseGroupingClassification");
        }

        private static bool IsGroupingChar(char c) {
            return _groupingChars.IndexOf(c) >= 0;
        }

        private static int CalculateIndentation(string baseline, ITextSnapshotLine line, IWpfTextView view, IClassifier classifier) {
            int indentation = GetIndentation(baseline, view.Options.GetTabSize());
            var sline = baseline.Trim();
            int tabSize = view.Options.GetIndentSize();
            var tokens = classifier.GetClassificationSpans(line.Extent);
            if (tokens.Count > 0) {
                if (!tokens[tokens.Count - 1].ClassificationType.IsOfType(PredefinedClassificationTypeNames.Comment)) {
                    var lastChar = sline.Length == 0 ? '\0' : sline[sline.Length - 1];
                    if (lastChar == ':') {
                        indentation += tabSize;
                    } else if (IsGroupingChar(lastChar)) {
                        if (tokens != null) {
                            var groupings = new Stack<ClassificationSpan>();
                            foreach (var token in tokens) {
                                if (token.Span.Start.Position > view.Caret.Position.BufferPosition.Position) {
                                    break;
                                }
                                if (StartsGrouping(token)) {
                                    groupings.Push(token);
                                } else if (groupings.Count > 0 && EndsGrouping(token)) {
                                    groupings.Pop();
                                }
                            }
                            if (groupings.Count > 0) {
                                indentation = groupings.Peek().Span.End.Position - line.Extent.Start.Position;
                            }
                        }
                    } else if (indentation >= tabSize) {
                        if (tokens.Count > 0 && tokens[0].ClassificationType.Classification == PredefinedClassificationTypeNames.Keyword && tokens[0].Span.GetText() == "return") {
                            indentation -= tabSize;
                        }
                    }
                }
            }
            return indentation;
        }

        private static bool IsCaretInStringLiteral(IReplWindow buffer) {
            var caret = buffer.TextView.Caret;
            var spans = GetClassifier(buffer).GetClassificationSpans(buffer.TextView.GetTextElementSpan(caret.Position.BufferPosition));
            if (spans.Count > 0) {
                return spans[0].ClassificationType.IsOfType(PredefinedClassificationTypeNames.String);
            }
            return false;
        }

        private static bool IsExtendedLine(string line) {
            var sline = line.Trim();
            if (sline.Length == 0) {
                return false;
            }
            var lastChar = sline[sline.Length - 1];
            return IsGroupingChar(lastChar) || lastChar == '\\';
        }

        internal static IClassifier GetClassifier(IReplWindow window) {
            var aggregator = PythonToolsPackage.ComponentModel.GetService<IClassifierAggregatorService>();
            return aggregator.GetClassifier(window.TextView.TextBuffer);
        }

        public static void HandleReturn(IWpfTextView view) {
            var pt = view.BufferGraph.MapDownToFirstMatch(
                new SnapshotPoint(view.TextBuffer.CurrentSnapshot, view.Caret.Position.BufferPosition),
                PointTrackingMode.Positive,
                PythonCoreConstants.IsPythonContent,
                PositionAffinity.Successor
            );

            if (pt != null) {
                var point = pt.Value;
                
                int indentation = GetTargetIndentation(view, point);
                int existingWsChars;
                int existingWhiteSpace = GetExistingWhiteSpaceRightOfTheCaret(view, point, out existingWsChars);

                using (var edit = point.Snapshot.TextBuffer.CreateEdit()) {
                    if (view.Selection.IsActive) {
                        foreach (var span in view.Selection.SelectedSpans) {
                            foreach (var mappedSpan in view.BufferGraph.MapDownToBuffer(
                                span,
                                SpanTrackingMode.EdgeExclusive,
                                point.Snapshot.TextBuffer)) {
                                edit.Delete(mappedSpan);
                            }
                        }
                    }
                    edit.Insert(point.Position, view.Options.GetNewLineCharacter());
                    if (indentation != existingWhiteSpace) {
                        if (indentation > existingWhiteSpace) {
                            int spacesInserted = indentation - existingWhiteSpace;
                            if (view.Options.IsConvertTabsToSpacesEnabled()) {
                                edit.Insert(point.Position, new String(' ', spacesInserted));
                            } else {
                                edit.Insert(point.Position, new String('\t', spacesInserted / view.Options.GetTabSize()));
                            }
                        } else {
                            // too much white space, delete the old white space
                            edit.Delete(point.Position, existingWhiteSpace - indentation);
                        }
                    }

                    point += existingWsChars;
                    edit.Apply();
                }

                var caretDestPoint = view.BufferGraph.MapUpToBuffer(point, PointTrackingMode.Positive, PositionAffinity.Successor, view.TextBuffer);
                if (caretDestPoint != null) {
                    view.Caret.MoveTo(caretDestPoint.Value);
                }
                view.Caret.EnsureVisible();
            }
        }

        private static int GetExistingWhiteSpaceRightOfTheCaret(IWpfTextView view, SnapshotPoint point, out int existingChars) {
            var caretLine = point.GetContainingLine();
            var firstNonWhiteSpaceChar = point;
            int indentation = 0;
            existingChars = 0;
            int tabSize = view.Options.GetIndentSize();
            for (var curPoint = point; curPoint < caretLine.End; curPoint += 1) {
                if (!Char.IsWhiteSpace(curPoint.GetChar())) {
                    firstNonWhiteSpaceChar = curPoint;
                    break;
                } else if (curPoint.GetChar() == '\t') {
                    indentation += tabSize;
                } else {
                    indentation++;
                }
                existingChars++;
            }
            return indentation;
        }

        /// <summary>
        /// Gets the indentation level that we want to be at, in spaces.
        /// </summary>
        private static int GetTargetIndentation(IWpfTextView view, SnapshotPoint point) {
            var caretLine = point.GetContainingLine();
            int curLine = caretLine.LineNumber;
            var snapshot = point.Snapshot;

            int startLine = curLine;

            // skip blank lines as far as calculating indentation goes...
            bool hasNonWhiteSpace = false;
            string lineText;
            ITextSnapshotLine line;
            do {
                line = snapshot.GetLineFromLineNumber(curLine);
                if (curLine == startLine) {
                    // if we're in the middle of a line only consider text to the left for white space detection
                    lineText = line.GetText().Substring(0, point.Position - caretLine.Start);
                } else {
                    lineText = line.GetText();
                }
                foreach (char c in lineText) {
                    if (!Char.IsWhiteSpace(c)) {
                        hasNonWhiteSpace = true;
                        break;
                    }
                }
                if (!hasNonWhiteSpace) {
                    curLine--;
                }
            } while (!hasNonWhiteSpace && curLine > 0);

            var classifier = point.Snapshot.TextBuffer.GetPythonClassifier();
            int indentation = CalculateIndentation(lineText, line, view, classifier);
            if (curLine != startLine) {
                // enter on a blank line, don't re-indent instead just maintain the current indentation
                indentation = Math.Min(indentation, GetIndentation(caretLine.GetText(), view.Options.GetIndentSize()));
            }
            return indentation;
        }

        private static string GetPreviousLine(IWpfTextView buffer) {
            int lineno = buffer.Caret.Position.BufferPosition.GetContainingLine().LineNumber;
            if (lineno < 1) {
                return String.Empty;
            }
            return buffer.TextSnapshot.GetLineFromLineNumber(lineno - 1).GetText();
        }
    }
}
