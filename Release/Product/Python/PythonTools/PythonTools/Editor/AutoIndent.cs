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
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

namespace Microsoft.PythonTools.Editor {
    internal static class AutoIndent {
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

        private static string CurrentLine(IWpfTextView buffer) {
            return buffer.TextSnapshot.GetLineFromPosition(buffer.Caret.Position.BufferPosition.Position).GetText();
        }

        private static string CurrentLine(IReplWindow buffer) {
            return CurrentLine(buffer.TextView);
        }

        private static bool EndsGrouping(ClassificationSpan token) {
            return token.ClassificationType.IsOfType("CloseGroupingClassification");
        }

        private static int CalculateIndentation(string baseline, ITextSnapshotLine line, IEditorOptions options, IClassifier classifier) {
            int indentation = GetIndentation(baseline, options.GetTabSize());
            int tabSize = options.GetIndentSize();
            var tokens = classifier.GetClassificationSpans(line.Extent);
            if (tokens.Count > 0 && !IsUnterminatedStringToken(tokens[tokens.Count - 1])) {
                int tokenIndex = tokens.Count - 1;

                while(tokenIndex >= 0 &&
                    (tokens[tokenIndex].ClassificationType.IsOfType(PredefinedClassificationTypeNames.Comment) ||
                    tokens[tokenIndex].ClassificationType.IsOfType(PredefinedClassificationTypeNames.WhiteSpace))) {
                    tokenIndex--;
                }

                if (tokenIndex < 0) {
                    return indentation;
                }

                if (ReverseExpressionParser.IsExplicitLineJoin(tokens[tokenIndex])) {
                    // explicit line continuation, we indent 1 level for the continued line unless
                    // we're already indented because of multiple line continuation characters.

                    indentation = GetIndentation(line.GetText(), options.GetTabSize());
                    var joinedLine = tokens[tokenIndex].Span.Start.GetContainingLine();
                    if (joinedLine.LineNumber > 0) {
                        var prevLineSpans = classifier.GetClassificationSpans(tokens[tokenIndex].Span.Snapshot.GetLineFromLineNumber(joinedLine.LineNumber - 1).Extent);
                        if (prevLineSpans.Count == 0 || !ReverseExpressionParser.IsExplicitLineJoin(prevLineSpans[prevLineSpans.Count - 1])) {
                            indentation += tabSize;
                        }
                    } else {
                        indentation += tabSize;
                    }

                    return indentation;
                }

                string sline = tokens[tokenIndex].Span.GetText();
                var lastChar = sline.Length == 0 ? '\0' : sline[sline.Length - 1];
                        
                // use the expression parser to figure out if we're in a grouping...
                var revParser = new ReverseExpressionParser(
                        line.Snapshot,
                        line.Snapshot.TextBuffer,
                        line.Snapshot.CreateTrackingSpan(
                            tokens[tokenIndex].Span.Span,
                            SpanTrackingMode.EdgePositive
                        )
                    );

                int paramIndex;
                SnapshotPoint? sigStart;
                var exprRangeNoImplicitOpen = revParser.GetExpressionRange(0, out paramIndex, out sigStart, false);
                var exprRangeImplicitOpen = revParser.GetExpressionRange(1, out paramIndex, out sigStart, false);

                if (exprRangeImplicitOpen != null && 
                    (exprRangeNoImplicitOpen == null || exprRangeImplicitOpen.Value.Span != exprRangeNoImplicitOpen.Value.Span) &&
                    IsOpenGrouping(exprRangeImplicitOpen.Value)) {
                    // we are in an open grouping, indent to the open grouping chars level + 1
                    return exprRangeImplicitOpen.Value.Start - exprRangeImplicitOpen.Value.Start.GetContainingLine().Start + 1;
                } else if (exprRangeNoImplicitOpen != null && IsProceededByKeywordWhichCausesDedent(classifier, exprRangeNoImplicitOpen.Value)) {
                    // return, break, continue, raise, etc... w/ an expression after it, dedent to the level of the line that contains the return, etc...
                    indentation = GetIndentation(exprRangeNoImplicitOpen.Value.Start.GetContainingLine().GetText(), options.GetTabSize()) - tabSize;
                } else if (indentation >= tabSize && ShouldDedentAfterKeyword(tokens[0])) {
                    // return, break, continue, raise, etc... no code generally executes after these, do a dedent.
                    indentation -= tabSize;
                } else if (exprRangeImplicitOpen != null && exprRangeNoImplicitOpen != null) {
                    // we have both a valid expression starting in a grouping and not starting in a grouping,
                    // we dedent to the same level as the line with the starting open paren.  An example of this:
                    // assert False, \
                    //      42
                    //
                    // Where exprRangeOpen parses back to the False where exprRangeNoOpen parses back to just the
                    // 42 because there are no implied groupings.  But we want to use the indentation level from
                    // the open one.
                    var endLine = exprRangeImplicitOpen.Value.End.GetContainingLine().LineNumber;
                    int minIndentation = Int32.MaxValue;
                    for (var line2 = exprRangeImplicitOpen.Value.Start.GetContainingLine();
                        line2.LineNumber <= endLine;
                        line2 = line2.Snapshot.GetLineFromLineNumber(line2.LineNumber + 1)) {
                        var tokens2 = classifier.GetClassificationSpans(line2.Extent);
                        minIndentation = Math.Min(minIndentation, GetIndentation(line2.GetText(), options.GetTabSize()));
                    }
                    indentation = minIndentation;
                } else if (exprRangeNoImplicitOpen != null) {
                    var line2 = exprRangeNoImplicitOpen.Value.Start.GetContainingLine();
                    var tokens2 = classifier.GetClassificationSpans(line2.Extent);
                    indentation = GetIndentation(line2.GetText(), options.GetTabSize());
                } else if (sigStart != null) {
                    return sigStart.Value - sigStart.Value.GetContainingLine().Start + 1;
                } else if (lastChar == ':') {
                    // not in an expression context, we have a function or class def which we should indent afterwards
                    indentation += tabSize;
                } 
            }

            return indentation;
        }

        private static bool IsProceededByKeywordWhichCausesDedent(IClassifier classifier, SnapshotSpan prevExpression) {
            bool dedentAfterKw = false;
            
            // find the token immediately before the expression and check if it's a return
            int start = prevExpression.Start.Position - 1;
            while (start > 0) {
                var prevSpans = classifier.GetClassificationSpans(new SnapshotSpan(prevExpression.Snapshot, Span.FromBounds(start, start + 1)));
                if (prevSpans.Count > 0) {
                    if (ShouldDedentAfterKeyword(prevSpans[0])) {
                        dedentAfterKw = true;
                    }
                    break;
                }
                start--;
            }
            
            return dedentAfterKw;
        }

        private static bool IsOpenGrouping(SnapshotSpan exprRangeOpen) {
            string text = exprRangeOpen.GetText();
            return text.StartsWith("(") || text.StartsWith("[") || text.StartsWith("{");
        }

        private static bool IsUnterminatedStringToken(ClassificationSpan lastToken) {
            if (lastToken.ClassificationType.IsOfType(PredefinedClassificationTypeNames.String)) {
                var text = lastToken.Span.GetText();
                if (text.EndsWith("\"") || text.EndsWith("'")) {
                    return false;
                }
                return true;
            }
            return false;
        }

        private static bool ShouldDedentAfterKeyword(ClassificationSpan span) {
            return span.ClassificationType.Classification == PredefinedClassificationTypeNames.Keyword && ShouldDedentAfterKeyword(span.Span.GetText());
        }

        private static bool ShouldDedentAfterKeyword(string keyword) {
            return keyword == "pass" || keyword == "return" || keyword == "break" || keyword == "continue" || keyword == "raise";
        }

        private static bool IsCaretInStringLiteral(IReplWindow buffer) {
            var caret = buffer.TextView.Caret;
            var spans = GetClassifier(buffer).GetClassificationSpans(buffer.TextView.GetTextElementSpan(caret.Position.BufferPosition));
            if (spans.Count > 0) {
                return spans[0].ClassificationType.IsOfType(PredefinedClassificationTypeNames.String);
            }
            return false;
        }

        internal static IClassifier GetClassifier(IReplWindow window) {
            var aggregator = PythonToolsPackage.ComponentModel.GetService<IClassifierAggregatorService>();
            return aggregator.GetClassifier(window.TextView.TextBuffer);
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

        internal static int GetLineIndentation(ITextSnapshotLine line, ITextView textView) {
            var options = textView.Options;

            ITextSnapshotLine baseline;
            string baselineText;
            SkipPreceedingBlankLines(line, out baselineText, out baseline);

            var classifier = line.Snapshot.TextBuffer.GetPythonClassifier();
            return CalculateIndentation(baselineText, baseline, options, classifier);
        }
    }
}
