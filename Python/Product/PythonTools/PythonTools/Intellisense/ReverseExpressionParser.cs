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

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Parses an expression in reverse to get the experssion we need to
    /// analyze for completion, quick info, or signature help.
    /// </summary>
    class ReverseExpressionParser : IEnumerable<ClassificationSpan> {
        private readonly ITextSnapshot _snapshot;
        private readonly ITextBuffer _buffer;
        private readonly ITrackingSpan _span;
        private ITextSnapshotLine _curLine;
        private readonly PythonClassifier _classifier;
        private static readonly HashSet<string> _assignOperators = new HashSet<string> {
            "=" ,  "+=" ,  "-=" ,  "/=" ,  "%=" ,  "^=" ,  "*=" ,  "//=" ,  "&=" ,  "|=" ,  ">>=" ,  "<<=" ,  "**=", "@="
        };


        public ReverseExpressionParser(ITextSnapshot snapshot, ITextBuffer buffer, ITrackingSpan span) {
            _snapshot = snapshot;
            _buffer = buffer;
            _span = span;

            var loc = span.GetSpan(snapshot);
            var line = _curLine = snapshot.GetLineFromPosition(loc.Start);

            var targetSpan = new Span(line.Start.Position, span.GetEndPoint(snapshot).Position - line.Start.Position);

            _classifier = _buffer.GetPythonClassifier();
            if (_classifier == null) {
                throw new ArgumentException(Strings.ReverseExpressionParserFailedToGetClassifierFromBufferException);
            }
        }

        internal static IEnumerator<ClassificationSpan> ForwardClassificationSpanEnumerator(PythonClassifier classifier, SnapshotPoint startPoint) {
            var startLine = startPoint.GetContainingLine();
            int curLine = startLine.LineNumber;
            if (startPoint > startLine.End) {
                // May occur if startPoint is between \r and \n
                startPoint = startLine.End;
            }
            var tokens = classifier.GetClassificationSpans(new SnapshotSpan(startPoint, startLine.End));

            for (; ; )
            {
                for (int i = 0; i < tokens.Count; ++i) {
                    yield return tokens[i];
                }

                // indicate the line break
                yield return null;

                ++curLine;
                if (curLine < startPoint.Snapshot.LineCount) {
                    var nextLine = startPoint.Snapshot.GetLineFromLineNumber(curLine);
                    tokens = classifier.GetClassificationSpans(nextLine.Extent);
                } else {
                    break;
                }
            }
        }

        internal static IEnumerator<ClassificationSpan> ReverseClassificationSpanEnumerator(PythonClassifier classifier, SnapshotPoint startPoint) {
            var startLine = startPoint.GetContainingLine();
            int curLine = startLine.LineNumber;
            var tokens = classifier.GetClassificationSpans(new SnapshotSpan(startLine.Start, startPoint));

            for (; ; )
            {
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
        /// Walks backwards to figure out if we're a parameter name which comes after a (     
        /// </summary>
        private bool IsParameterNameOpenParen(IEnumerator<ClassificationSpan> enumerator) {
            if (MoveNextSkipExplicitNewLines(enumerator)) {
                if (enumerator.Current.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier)) {
                    if (MoveNextSkipExplicitNewLines(enumerator) &&
                        enumerator.Current.ClassificationType == Classifier.Provider.Keyword &&
                        enumerator.Current.Span.GetText() == "def") {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Walks backwards to figure out if we're a parameter name which comes after a comma.
        /// </summary>
        private bool IsParameterNameComma(IEnumerator<ClassificationSpan> enumerator) {
            int groupingLevel = 1;

            while (MoveNextSkipExplicitNewLines(enumerator)) {
                if (enumerator.Current.ClassificationType == Classifier.Provider.Keyword) {
                    if (enumerator.Current.Span.GetText() == "def" && groupingLevel == 0) {
                        return true;
                    }

                    if (PythonKeywords.IsOnlyStatementKeyword(enumerator.Current.Span.GetText())) {
                        return false;
                    }
                }
                if (enumerator.Current.IsOpenGrouping()) {
                    groupingLevel--;
                    if (groupingLevel == 0) {
                        return IsParameterNameOpenParen(enumerator);
                    }
                } else if (enumerator.Current.IsCloseGrouping()) {
                    groupingLevel++;
                }
            }

            return false;
        }

        private bool MoveNextSkipExplicitNewLines(IEnumerator<ClassificationSpan> enumerator) {
            while (enumerator.MoveNext()) {
                if (enumerator.Current == null) {
                    while (enumerator.Current == null) {
                        if (!enumerator.MoveNext()) {
                            return false;
                        }
                    }
                    if (!IsExplicitLineJoin(enumerator.Current)) {
                        return true;
                    }
                } else {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the range of the expression to the left of our starting span.  
        /// </summary>
        /// <param name="nesting">1 if we have an opening parenthesis for sig completion</param>
        /// <param name="paramIndex">The current parameter index.</param>
        /// <returns></returns>
        [Obsolete("Use GetExpressionAtPoint instead")]
        public SnapshotSpan? GetExpressionRange(int nesting, out int paramIndex, out SnapshotPoint? sigStart, out string lastKeywordArg, out bool isParameterName, bool forCompletion = true) {
            SnapshotSpan? start = null;
            paramIndex = 0;
            sigStart = null;
            bool nestingChanged = false, lastTokenWasCommaOrOperator = true, lastTokenWasKeywordArgAssignment = false;
            int otherNesting = 0;
            bool isSigHelp = nesting != 0;
            isParameterName = false;
            lastKeywordArg = null;

            ClassificationSpan lastToken = null;
            // Walks backwards over all the lines
            var enumerator = ReverseClassificationSpanEnumerator(Classifier, _span.GetSpan(_snapshot).End);
            if (enumerator.MoveNext()) {
                if (enumerator.Current != null && enumerator.Current.ClassificationType == this.Classifier.Provider.StringLiteral) {
                    return enumerator.Current.Span;
                }

                lastToken = enumerator.Current;
                while (ShouldSkipAsLastToken(lastToken, forCompletion) && enumerator.MoveNext()) {
                    // skip trailing new line if the user is hovering at the end of the line
                    if (lastToken == null && (nesting + otherNesting == 0)) {
                        // new line out of a grouping...
                        return _span.GetSpan(_snapshot);
                    }
                    lastToken = enumerator.Current;
                }

                int currentParamAtLastColon = -1;   // used to track the current param index at this last colon, before we hit a lambda.
                SnapshotSpan? startAtLastToken = null;
                // Walk backwards over the tokens in the current line
                do {
                    var token = enumerator.Current;

                    if (token == null) {
                        // new line
                        if (nesting != 0 || otherNesting != 0 || (enumerator.MoveNext() && IsExplicitLineJoin(enumerator.Current))) {
                            // we're in a grouping, or the previous token is an explicit line join, we'll keep going.
                            continue;
                        } else {
                            break;
                        }
                    }

                    var text = token.Span.GetText();
                    if (text == "(") {
                        if (nesting != 0) {
                            nesting--;
                            nestingChanged = true;
                            if (nesting == 0) {
                                if (sigStart == null) {
                                    sigStart = token.Span.Start;
                                }
                            }
                        } else {
                            if (start == null && !forCompletion) {
                                // hovering directly over an open paren, don't provide a tooltip
                                return null;
                            }

                            // figure out if we're a parameter definition
                            isParameterName = IsParameterNameOpenParen(enumerator);
                            break;
                        }
                        lastTokenWasCommaOrOperator = true;
                        lastTokenWasKeywordArgAssignment = false;
                    } else if (token.IsOpenGrouping()) {
                        if (otherNesting != 0) {
                            otherNesting--;
                        } else {
                            if (nesting == 0) {
                                if (start == null) {
                                    return null;
                                }
                                break;
                            }
                            paramIndex = 0;
                        }
                        nestingChanged = true;
                        lastTokenWasCommaOrOperator = true;
                        lastTokenWasKeywordArgAssignment = false;
                    } else if (text == ")") {
                        nesting++;
                        nestingChanged = true;
                        lastTokenWasCommaOrOperator = true;
                        lastTokenWasKeywordArgAssignment = false;
                    } else if (token.IsCloseGrouping()) {
                        otherNesting++;
                        nestingChanged = true;
                        lastTokenWasCommaOrOperator = true;
                        lastTokenWasKeywordArgAssignment = false;
                    } else if (token.ClassificationType == Classifier.Provider.Keyword ||
                               token.ClassificationType == Classifier.Provider.Operator) {
                        lastTokenWasKeywordArgAssignment = false;

                        if (token.ClassificationType == Classifier.Provider.Keyword && text == "lambda") {
                            if (currentParamAtLastColon != -1) {
                                paramIndex = currentParamAtLastColon;
                                currentParamAtLastColon = -1;
                            } else {
                                // fabcd(lambda a, b, c[PARAMINFO]
                                // We have to be the 1st param.
                                paramIndex = 0;
                            }
                        }

                        if (text == ":") {
                            startAtLastToken = start;
                            currentParamAtLastColon = paramIndex;
                        }

                        if (nesting == 0 && otherNesting == 0) {
                            if (start == null) {
                                // http://pytools.codeplex.com/workitem/560
                                // yield_value = 42
                                // def f():
                                //     yield<ctrl-space>
                                //     yield <ctrl-space>
                                // 
                                // If we're next to the keyword, just return the keyword.
                                // If we're after the keyword, return the span of the text proceeding
                                //  the keyword so we can complete after it.
                                // 
                                // Also repros with "return <ctrl-space>" or "print <ctrl-space>" both
                                // of which we weren't reporting completions for before
                                if (forCompletion) {
                                    if (token.Span.IntersectsWith(_span.GetSpan(_snapshot))) {
                                        return token.Span;
                                    } else {
                                        return _span.GetSpan(_snapshot);
                                    }
                                }

                                // hovering directly over a keyword, don't provide a tooltip
                                return null;
                            } else if ((nestingChanged || forCompletion) && token.ClassificationType == Classifier.Provider.Keyword && (text == "def" || text == "class")) {
                                return null;
                            }
                            if (text == "*" || text == "**") {
                                if (MoveNextSkipExplicitNewLines(enumerator)) {
                                    if (enumerator.Current.ClassificationType == Classifier.Provider.CommaClassification) {
                                        isParameterName = IsParameterNameComma(enumerator);
                                    } else if (enumerator.Current.IsOpenGrouping() && enumerator.Current.Span.GetText() == "(") {
                                        isParameterName = IsParameterNameOpenParen(enumerator);
                                    }
                                }
                            }
                            break;
                        } else if ((token.ClassificationType == Classifier.Provider.Keyword &&
                            PythonKeywords.IsOnlyStatementKeyword(text)) ||
                            (token.ClassificationType == Classifier.Provider.Operator && IsAssignmentOperator(text))) {
                            if (nesting != 0 && text == "=") {
                                // keyword argument allowed in signatures
                                lastTokenWasKeywordArgAssignment = lastTokenWasCommaOrOperator = true;
                            } else if (start == null || (nestingChanged && nesting != 0)) {
                                return null;
                            } else {
                                break;
                            }
                        } else if (token.ClassificationType == Classifier.Provider.Keyword &&
                            (text == "if" || text == "else")) {
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
                    } else if (token.ClassificationType == Classifier.Provider.DotClassification) {
                        lastTokenWasCommaOrOperator = true;
                        lastTokenWasKeywordArgAssignment = false;
                    } else if (token.ClassificationType == Classifier.Provider.CommaClassification) {
                        lastTokenWasCommaOrOperator = true;
                        lastTokenWasKeywordArgAssignment = false;
                        if (nesting == 0 && otherNesting == 0) {
                            if (start == null && !forCompletion) {
                                return null;
                            }
                            isParameterName = IsParameterNameComma(enumerator);
                            break;
                        } else if (nesting == 1 && otherNesting == 0 && sigStart == null) {
                            paramIndex++;
                        }
                    } else if (token.ClassificationType == Classifier.Provider.Comment) {
                        return null;
                    } else if (!lastTokenWasCommaOrOperator) {
                        if (nesting == 0 && otherNesting == 0) {
                            break;
                        }
                    } else {
                        if (lastTokenWasKeywordArgAssignment &&
                            token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier) &&
                            lastKeywordArg == null) {
                            if (paramIndex == 0) {
                                lastKeywordArg = text;
                            } else {
                                lastKeywordArg = "";
                            }
                        }
                        lastTokenWasCommaOrOperator = false;
                    }

                    start = token.Span;
                } while (enumerator.MoveNext());
            }

            if (start.HasValue && lastToken != null && (lastToken.Span.End.Position - start.Value.Start.Position) >= 0) {
                var spanToReturn = new SnapshotSpan(
                    Snapshot,
                    new Span(
                        start.Value.Start.Position,
                        lastToken.Span.End.Position - start.Value.Start.Position
                    )
                );
                // To handle a case where a space is returned for displaying the type signature.
                if (string.IsNullOrWhiteSpace(spanToReturn.GetText())) {
                    return null;
                }
                return spanToReturn;
            }

            return _span.GetSpan(_snapshot);
        }

        private static bool IsAssignmentOperator(string text) {
            return _assignOperators.Contains(text);
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
        private bool ShouldSkipAsLastToken(ClassificationSpan lastToken, bool forCompletion) {
            return lastToken == null || (
                (lastToken.ClassificationType.Classification == PredefinedClassificationTypeNames.WhiteSpace &&
                    (lastToken.Span.GetText() == "\r\n" || lastToken.Span.GetText() == "\n" || lastToken.Span.GetText() == "\r")) ||
                    (lastToken.ClassificationType == Classifier.Provider.DotClassification && !forCompletion));
        }

        public PythonClassifier Classifier {
            get { return _classifier; }
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

        public IEnumerator<ClassificationSpan> GetEnumerator() {
            return ReverseClassificationSpanEnumerator(Classifier, _span.GetSpan(_snapshot).End);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return ReverseClassificationSpanEnumerator(Classifier, _span.GetSpan(_snapshot).End);
        }

        internal static bool IsInGrouping(ITextSnapshot snapshot, IEnumerable<TrackingTokenInfo> tokensInReverse) {
            int nesting = 0;
            foreach (var token in tokensInReverse) {
                if (token.Category == Parsing.TokenCategory.Grouping) {
                    var t = token.GetText(snapshot);
                    if (t.IsCloseGrouping()) {
                        nesting++;
                    } else if (t.IsOpenGrouping()) {
                        if (nesting-- == 0) {
                            return true;
                        }
                    }
                } else if (token.Category == Parsing.TokenCategory.Delimiter) {
                    if (nesting == 0 && token.GetText(snapshot) == ",") {
                        return true;
                    }
                } else if (token.Category == Parsing.TokenCategory.Keyword) {
                    if (PythonKeywords.IsOnlyStatementKeyword(token.GetText(snapshot))) {
                        return false;
                    }
                }
            }
            return false;
        }

        internal bool IsInGrouping() {
            // We assume that groupings are correctly matched and keep a simple
            // nesting count.
            int nesting = 0;
            foreach (var token in this) {
                if (token == null) {
                    continue;
                }

                if (token.IsCloseGrouping()) {
                    nesting++;
                } else if (token.IsOpenGrouping()) {
                    if (nesting-- == 0) {
                        return true;
                    }
                } else if (token.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Comma)) {
                    if (nesting == 0) {
                        // A preceding comma at our level is only valid in a
                        // grouping.
                        return true;
                    }
                } else if (token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Keyword) &&
                    PythonKeywords.IsOnlyStatementKeyword(token.Span.GetText())) {
                    return false;
                }
            }
            return false;
        }

        internal SnapshotSpan? GetStatementRange() {
            var tokenStack = new Stack<ClassificationSpan>();
            bool eol = false, finishLine = false;

            // Collect all the tokens until we know we're not in any groupings
            foreach (var token in this) {
                if (eol) {
                    eol = false;
                    if (!IsExplicitLineJoin(token)) {
                        tokenStack.Push(null);
                        if (finishLine) {
                            break;
                        }
                    }
                }

                if (token == null) {
                    eol = true;
                    continue;
                }

                tokenStack.Push(token);
                if (token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Keyword) &&
                    PythonKeywords.IsOnlyStatementKeyword(token.Span.GetText())) {
                    finishLine = true;
                }
            }

            if (tokenStack.Count == 0) {
                return null;
            }

            // Now scan forward through the tokens setting our current statement
            // start point.
            SnapshotPoint start = new SnapshotPoint(_snapshot, 0);
            SnapshotPoint end = start;
            bool setStart = true;
            int nesting = 0;
            foreach (var token in tokenStack) {
                if (setStart && token != null) {
                    start = token.Span.Start;
                    setStart = false;
                }
                if (token == null) {
                    if (nesting == 0) {
                        setStart = true;
                    }
                } else {
                    end = token.Span.End;
                    if (token.IsOpenGrouping()) {
                        ++nesting;
                    } else if (token.IsCloseGrouping()) {
                        --nesting;
                    }
                }
            }

            // Keep going to find the end of the statement
            using (var e = ForwardClassificationSpanEnumerator(Classifier, Span.GetStartPoint(Snapshot))) {
                eol = false;
                while (e.MoveNext()) {
                    if (e.Current == null) {
                        if (nesting == 0) {
                            break;
                        }
                        eol = true;
                    } else {
                        eol = false;
                        if (setStart) {
                            // Final token was EOL, so our start is the first
                            // token moving forwards
                            start = e.Current.Span.Start;
                            setStart = false;
                        }
                        end = e.Current.Span.End;
                    }
                }
                if (setStart) {
                    start = Span.GetStartPoint(Snapshot);
                }
                if (eol) {
                    end = Span.GetEndPoint(Snapshot);
                }
            }

            if (end <= start) {
                // No statement here
                return null;
            }

            return new SnapshotSpan(start, end);
        }
    }
}
