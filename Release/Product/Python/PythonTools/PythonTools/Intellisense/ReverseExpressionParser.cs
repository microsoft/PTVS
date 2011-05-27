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
        private PythonClassifier _classifier;
        private static readonly string[] _assignOperators = new[] {
            "=" ,  "+=" ,  "-=" ,  "/=" ,  "%=" ,  "^=" ,  "*=" ,  "//=" ,  "&=" ,  "|=" ,  ">>=" ,  "<<=" ,  "**="
        };
        private static readonly string[] _stmtKeywords = new[] {
            "assert", "print" , "break" ,  "del" ,  "except" ,  "finally" ,  "for" ,  "global" ,  
            "nonlocal" ,  "pass" ,  "raise" ,  "return" ,  "try" ,  "while" ,  "with" ,  "class" ,  
            "def"
        };


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

        internal static IEnumerator<ClassificationSpan> ReverseClassificationSpanEnumerator(PythonClassifier classifier, SnapshotPoint startPoint) {
            var startLine = startPoint.GetContainingLine();
            int curLine = startLine.LineNumber;
            var tokens = classifier.GetClassificationSpans(new SnapshotSpan(startLine.Start, startPoint));

            for (; ; ) {
                for (int i = tokens.Count - 1; i >= 0; i--) {
                    yield return tokens[i];
                }

                // indicate the line break
                yield return null;

                curLine--;
                if (curLine >= 0) {
                    var prevLine = startPoint.Snapshot.GetLineFromLineNumber(curLine);
                    tokens = classifier.GetClassificationSpans(prevLine.Extent);
                } else {
                    break;
                }
            }
        }


        /// <summary>
        /// Gets the range of the expression to the left of our starting span.  
        /// </summary>
        /// <param name="nesting">1 if we have an opening parenthesis for sig completion</param>
        /// <param name="paramIndex">The current parameter index.</param>
        /// <returns></returns>
        public SnapshotSpan? GetExpressionRange(int nesting, out int paramIndex, out SnapshotPoint? sigStart, bool forCompletion = true) {
            SnapshotSpan? start = null;
            paramIndex = 0;
            sigStart = null;
            bool nestingChanged = false, lastTokenWasCommaOrOperator = true;

            ClassificationSpan lastToken = null;
            // Walks backwards over all the lines
            var enumerator = ReverseClassificationSpanEnumerator(_classifier, _span.GetSpan(_snapshot).End);
            if (enumerator.MoveNext()) {
                lastToken = enumerator.Current;
                if (!forCompletion && ShouldSkipAsLastToken(lastToken) && enumerator.MoveNext()) {
                    // skip trailing new line if the user is hovering at the end of the line
                    lastToken = enumerator.Current;
                }

                int currentParamAtLastColon = -1;   // used to track the current param index at this last colon, before we hit a lambda.
                SnapshotSpan? startAtLastToken = null;
                // Walk backwards over the tokens in the current line
                do {
                    var token = enumerator.Current;
                    
                    if (token == null) {
                        // new line
                        if (nesting != 0 || (enumerator.MoveNext() && IsExplicitLineJoin(enumerator.Current))) {
                            // we're in a grouping, or the previous token is an explicit line join, we'll keep going.
                            continue;
                        } else {
                            break;
                        }
                    }

                    var text = token.Span.GetText();
                    if (token.IsOpenGrouping()) {
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
                        lastTokenWasCommaOrOperator = true;
                    } else if (token.IsCloseGrouping()) {
                        nesting++;
                        nestingChanged = true;
                    } else if (token.ClassificationType == Classifier.Provider.Keyword ||
                               token.ClassificationType == Classifier.Provider.Operator) {
                        if (token.ClassificationType == Classifier.Provider.Keyword && text == "lambda") {
                            if (currentParamAtLastColon != -1) {
                                paramIndex = currentParamAtLastColon;
                                currentParamAtLastColon = -1;
                            } else {
                                // fabcd(lambda a, b, c[PARAMINFO]
                                // We have to be the 1st param.
                                paramIndex = 0;
                            }
                        } else {
                            if (text == ":") {
                                startAtLastToken = start;
                                currentParamAtLastColon = paramIndex;
                            }
                            if (nesting == 0) {
                                if (start == null) {
                                    // hovering directly over a keyword, don't provide a tooltip
                                    return null;
                                } else if ((nestingChanged || forCompletion) && token.ClassificationType == Classifier.Provider.Keyword && text == "def") {
                                    return null;
                                }
                                break;
                            } else if ((token.ClassificationType == Classifier.Provider.Keyword && IsStmtKeyword(text)) ||
                                (token.ClassificationType == Classifier.Provider.Operator && IsAssignmentOperator(text))) {
                                    if (start == null) {
                                        return null;
                                    }
                                break;
                            } else if (token.ClassificationType == Classifier.Provider.Keyword && (text == "if" || text == "else")) {
                                // if and else can be used in an expression context or a statement context
                                if (currentParamAtLastColon != -1) {
                                    start = startAtLastToken;
                                    if (start == null) {
                                        return null;
                                    }
                                    break;
                                }
                            }
                            lastTokenWasCommaOrOperator = true;
                        }
                    } else if (token.ClassificationType == Classifier.Provider.DotClassification) {
                        lastTokenWasCommaOrOperator = true;
                    } else if (token.ClassificationType == Classifier.Provider.CommaClassification) {
                        lastTokenWasCommaOrOperator = true;
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
                    } else if (!lastTokenWasCommaOrOperator) {
                        break;
                    } else {
                        lastTokenWasCommaOrOperator = false;
                    }

                    start = token.Span;
                } while (enumerator.MoveNext());
            }

            if (start.HasValue && (lastToken.Span.End.Position - start.Value.Start.Position) >= 0) {
                return new SnapshotSpan(
                    Snapshot,
                    new Span(
                        start.Value.Start.Position,
                        lastToken.Span.End.Position - start.Value.Start.Position
                    )
                );
            }

            return _span.GetSpan(_snapshot);
        }

        private static bool IsAssignmentOperator(string text) {
            return ((IList<string>)_assignOperators).Contains(text);
        }

        private static bool IsStmtKeyword(string text) {
            return ((IList<string>)_stmtKeywords).Contains(text);
        }

        internal static bool IsExplicitLineJoin(ClassificationSpan cur) {
            if (cur != null && cur.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Operator)) {
                var text = cur.Span.GetText();
                return text == "\\\r\n" || text == "\\\r" || text == "\n";
            }
            return false;
        }

        /// <summary>
        /// Returns true if we should skip this token when it's the last token that the user hovers over.  Currently true
        /// for new lines and dot classifications.  
        /// </summary>
        private bool ShouldSkipAsLastToken(ClassificationSpan lastToken) {
            return lastToken == null || (
                (lastToken.ClassificationType.Classification == PredefinedClassificationTypeNames.WhiteSpace &&
                    (lastToken.Span.GetText() == "\r\n" || lastToken.Span.GetText() == "\n" || lastToken.Span.GetText() == "\r")) ||
                    (lastToken.ClassificationType == Classifier.Provider.DotClassification));
        }

        public PythonClassifier Classifier {
            get { return _classifier ?? (_classifier = (PythonClassifier)_buffer.Properties.GetProperty(typeof(PythonClassifier))); }
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
