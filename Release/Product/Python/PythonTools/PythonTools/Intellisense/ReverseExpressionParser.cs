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
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Parses an expression in reverse to get the experssion we need to
    /// analyze for completion, quick info, or signature help.
    /// </summary>
    class ReverseExpressionParser {
        private readonly ITextSnapshot _snapshot;
        private readonly ITextBuffer _buffer;
        private readonly ITrackingSpan _span;
        private IList<ClassificationSpan> _tokens;
        private ITextSnapshotLine _curLine;
        private IPythonClassifier _classifier;

        public ReverseExpressionParser(ITextSnapshot snapshot, ITextBuffer buffer, ITrackingSpan span) {
            _snapshot = snapshot;
            _buffer = buffer;
            _span = span;

            var loc = span.GetSpan(snapshot);
            var line = _curLine = snapshot.GetLineFromPosition(loc.Start);
            
            var targetSpan = new Span(line.Start.Position, span.GetEndPoint(snapshot).Position - line.Start.Position);
            _tokens = Classifier.GetClassificationSpans(new SnapshotSpan(snapshot, targetSpan));
        }

        public SnapshotSpan? GetExpressionRange(bool forCompletion = true) {
            int dummy;
            SnapshotPoint? dummyPoint;
            return GetExpressionRange(0, out dummy, out dummyPoint, forCompletion);
        }

        /// <summary>
        /// Gets the range of the expression to the left of our starting span.  
        /// </summary>
        /// <param name="nesting">1 if we have an opening parenthesis for sig completion</param>
        /// <param name="paramIndex">The current parameter index.</param>
        /// <returns></returns>
        public SnapshotSpan? GetExpressionRange(int nesting, out int paramIndex, out SnapshotPoint? sigStart, bool forCompletion = true) {
            SnapshotSpan? start = null;
            var endText = String.Empty;
            paramIndex = 0;
            sigStart = null;
            bool nestingChanged = false;

            ClassificationSpan lastToken = null;
            // Walks backwards over all the lines
            if (Tokens.Count > 0) {
                lastToken = Tokens[Tokens.Count - 1];
                if (!forCompletion && Tokens.Count > 1 && ShouldSkipAsLastToken(lastToken)) {
                    // skip trailing new line if the user is hovering at the end of the line
                    lastToken = Tokens[Tokens.Count - 2];
                }

                while (true) {
                    // Walk backwards over the tokens in the current line
                    for (int t = Tokens.Count - 1; t >= 0; t--) {
                        var token = Tokens[t];
                        var text = token.Span.GetText();

                        if (token.ClassificationType == Classifier.Provider.Keyword ||
                            (token.ClassificationType == Classifier.Provider.Operator &&
                            text != "[" && text != "]" && text != "}" && text != "{")) {
                            if (nesting == 0) {
                                if (start == null) {
                                    // hovering directly over a keyword, don't provide a tooltip
                                    return null;
                                } else if ((nestingChanged || forCompletion) && token.ClassificationType == Classifier.Provider.Keyword && text == "def") {
                                    return null;
                                }
                                break;
                            }
                        } else if (token.ClassificationType == Classifier.Provider.OpenGroupingClassification || text == "[" || text == "{") {
                            if (nesting != 0) {
                                nesting--;
                                nestingChanged = true;
                                if (nesting == 0 && sigStart == null) {
                                    sigStart = token.Span.Start;
                                }
                            } else {
                                if (start == null) {
                                    // hovering directly over an open paren, don't provide a tooltip
                                    return null;
                                }
                                break;
                            }
                        } else if (token.ClassificationType == Classifier.Provider.CloseGroupingClassification ||
                            text == "]" || text == "}") {
                            nesting++;
                            nestingChanged = true;
                        } else if (token.ClassificationType == Classifier.Provider.CommaClassification) {
                            if (nesting == 0) {
                                if (start == null) {
                                    return null;
                                }
                                break;
                            } else if (nesting == 1 && sigStart == null) {
                                paramIndex++;
                            }
                        } else if (token.ClassificationType == Classifier.Provider.Comment) {
                            return null;
                        }

                        start = token.Span;
                    }

                    if (nesting == 0 || CurrentLine.LineNumber == 0) {
                        break;
                    }

                    // We're in a nested paren context, continue to the next line
                    // to capture the entire expression
                    endText = CurrentLine.GetText() + endText;
                    CurrentLine = Snapshot.GetLineFromLineNumber(CurrentLine.LineNumber - 1);

                    var classSpan = new SnapshotSpan(Snapshot, CurrentLine.Start, CurrentLine.Length);
                    Tokens = Classifier.GetClassificationSpans(classSpan);
                }
            }

            if (start.HasValue) {
                return new SnapshotSpan(
                    Snapshot,
                    new Span(
                        start.Value.Start.Position,
                        //_span.GetEndPoint(_snapshot).Position - start.Value.Start.Position
                        lastToken.Span.End.Position - start.Value.Start.Position
                    )
                );
            }

            return _span.GetSpan(_snapshot);
        }

        /// <summary>
        /// Returns true if we should skip this token when it's the last token that the user hovers over.  Currently true
        /// for new lines and dot classifications.  
        /// </summary>
        private bool ShouldSkipAsLastToken(ClassificationSpan lastToken) {
            return (lastToken.ClassificationType.Classification == PredefinedClassificationTypeNames.WhiteSpace &&
                    (lastToken.Span.GetText() == "\r\n" || lastToken.Span.GetText() == "\n" || lastToken.Span.GetText() == "\r")) ||
                    (lastToken.ClassificationType == Classifier.Provider.DotClassification);
        }

        public IPythonClassifier Classifier {
            get { return _classifier ?? (_classifier = (IPythonClassifier)_buffer.Properties.GetProperty(typeof(IPythonClassifier))); }
        }

        public ITextSnapshot Snapshot {
            get { return _snapshot; }
        }

        public ITextBuffer Buffer {
            get { return _buffer; }
        }

        public ITrackingSpan Span {
            get { return _span; }
        }

        /// <summary>
        /// Tokens for the current line
        /// </summary>
        public IList<ClassificationSpan> Tokens {
            get { return _tokens; }
            set { _tokens = value; }
        }

        public ITextSnapshotLine CurrentLine {
            get { return _curLine; }
            set { _curLine = value; }
        }
    }
}
