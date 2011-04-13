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

        private static int CalculateIndentation(string baseline, ITextSnapshotLine line, IEditorOptions options, IClassifier classifier) {
            int indentation = GetIndentation(baseline, options.GetTabSize());
            int tabSize = options.GetIndentSize();
            var tokens = classifier.GetClassificationSpans(line.Extent);
            if (tokens.Count > 0) {
                if (!tokens[tokens.Count - 1].ClassificationType.IsOfType(PredefinedClassificationTypeNames.String)) {
                    int tokenIndex = tokens.Count - 1;

                    while(tokenIndex >= 0 &&
                        (tokens[tokenIndex].ClassificationType.IsOfType(PredefinedClassificationTypeNames.Comment) ||
                        tokens[tokenIndex].ClassificationType.IsOfType(PredefinedClassificationTypeNames.WhiteSpace))) {
                        tokenIndex--;
                    }

                    if(tokenIndex >= 0) {
                        string sline = tokens[tokenIndex].Span.GetText();
                    
                        var lastChar = sline.Length == 0 ? '\0' : sline[sline.Length - 1];
                        if (lastChar == ':') {
                            indentation += tabSize;
                        } else if (IsGroupingChar(lastChar)) {
                            if (tokens != null) {
                                var groupings = new Stack<ClassificationSpan>();
                                foreach (var token in tokens) {
                                    if (token.IsOpenGrouping()) {
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
                            if (tokens.Count > 0 && 
                                tokens[0].ClassificationType.Classification == PredefinedClassificationTypeNames.Keyword && 
                                ShouldDedentAfterKeyword(tokens[0].Span.GetText())) {
                                indentation -= tabSize;
                            }
                        }
                    }
                }
            }
            return indentation;
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

        internal static int GetLineIndentation(ITextSnapshotLine line, IEditorOptions options) {
            ITextSnapshotLine baseline;
            string baselineText;
            SkipPreceedingBlankLines(line, out baselineText, out baseline);

            var classifier = line.Snapshot.TextBuffer.GetPythonClassifier();
            return CalculateIndentation(baselineText, baseline, options, classifier);
        }
    }
}
