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
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Provides classification based upon the DLR TokenCategory enum.
    /// </summary>
    internal class PythonClassifier : IClassifier {
        private readonly TokenCache _tokenCache;
        private readonly PythonClassifierProvider _provider;
        private readonly ITextBuffer _buffer;
        
        [ThreadStatic]
        private static Dictionary<PythonLanguageVersion, Tokenizer> _tokenizers;    // tokenizer for each version, shared between all buffers
        
        internal PythonClassifier(PythonClassifierProvider provider, ITextBuffer buffer) {
            buffer.Changed += BufferChanged;
            buffer.ContentTypeChanged += BufferContentTypeChanged;
            
            _tokenCache = new TokenCache();
            _provider = provider;
            _buffer = buffer;
        }

        internal void NewVersion() {
            _tokenCache.Clear();
            var changed = ClassificationChanged;
            if (changed != null) {
                var snapshot = _buffer.CurrentSnapshot;

                changed(this, new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
            }
        }

        #region IDlrClassifier

        // This event gets raised if the classification of existing test changes.
        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        /// <summary>
        /// This method classifies the given snapshot span.
        /// </summary>
        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
            var classifications = new List<ClassificationSpan>();
            var snapshot = span.Snapshot;

            
            if (span.Length > 0) {
                // don't add classifications for REPL commands.
                if (!span.Snapshot.IsReplBufferWithCommand()) {
                    AddClassifications(GetTokenizer(), classifications, span, 0, 0);
                }
            }

            return classifications;
        }

        private Tokenizer GetTokenizer() {
            if (_tokenizers == null) {
                _tokenizers = new Dictionary<PythonLanguageVersion, Tokenizer>();
            }
            var langVersion = _buffer.GetAnalyzer().InterpreterFactory.GetLanguageVersion();
            Tokenizer res;
            if (!_tokenizers.TryGetValue(langVersion, out res)) {
                _tokenizers[langVersion] = res = new Tokenizer(langVersion, verbatim: true);
            }
            return res;
        }

        public PythonClassifierProvider Provider {
            get {
                return _provider;
            }
        }

        #endregion

        #region Private Members

        private Dictionary<TokenCategory, IClassificationType> CategoryMap {
            get {
                return _provider.CategoryMap;
            }
        }

        private void BufferContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
            _tokenCache.Clear();
            _buffer.Changed -= BufferChanged;
            _buffer.ContentTypeChanged -= BufferContentTypeChanged;
            _buffer.Properties.RemoveProperty(typeof(PythonClassifier));
        }

        private void BufferChanged(object sender, TextContentChangedEventArgs e) {
            var snapshot = e.After;

            _tokenCache.EnsureCapacity(snapshot.LineCount);

            var tokenizer = GetTokenizer();
            foreach (var change in e.Changes) {
                if (change.LineCountDelta > 0) {
                    _tokenCache.InsertLines(snapshot.GetLineNumberFromPosition(change.NewEnd) + 1 - change.LineCountDelta, change.LineCountDelta);
                } else if (change.LineCountDelta < 0) {
                    _tokenCache.DeleteLines(snapshot.GetLineNumberFromPosition(change.NewEnd) + 1, -change.LineCountDelta);
                }

                ApplyChange(tokenizer, snapshot, change.NewSpan, 0, 0);
            }
        }

        /// <summary>
        /// Adds classification spans to the given collection.
        /// Scans a contiguous sub-<paramref name="span"/> of a larger code span which starts at <paramref name="codeStartLine"/>.
        /// </summary>
        private void AddClassifications(Tokenizer tokenizer, List<ClassificationSpan> classifications, SnapshotSpan span, int codeStartLine, int codeStartLineOffset) {
            Debug.Assert(span.Length > 0);

            var snapshot = span.Snapshot;            
            int firstLine = snapshot.GetLineNumberFromPosition(span.Start);
            int lastLine = snapshot.GetLineNumberFromPosition(span.End - 1);

            Contract.Assert(codeStartLineOffset >= 0);
            Contract.Assert(firstLine >= codeStartLine);

            _tokenCache.EnsureCapacity(snapshot.LineCount);

            // find the closest line preceding firstLine for which we know categorizer state, stop at the codeStartLine:
            LineTokenization lineTokenization;
            int currentLine = _tokenCache.IndexOfPreviousTokenization(firstLine, codeStartLine, out lineTokenization) + 1;
            object state = lineTokenization.State;

            while (currentLine <= lastLine) {
                if (!_tokenCache.TryGetTokenization(currentLine, out lineTokenization)) {
                    lineTokenization = TokenizeLine(tokenizer, snapshot, state, currentLine, (currentLine == codeStartLine) ? codeStartLineOffset : 0);
                    _tokenCache[currentLine] = lineTokenization;
                }

                state = lineTokenization.State;

                classifications.AddRange(
                    from token in lineTokenization.Tokens
                    let classification = ClassifyToken(span, token, currentLine)
                    where classification != null
                    select classification
                );

                currentLine++;
            }
        }

        /// <summary>
        /// Rescans the part of the buffer affected by a change. 
        /// Scans a contiguous sub-<paramref name="span"/> of a larger code span which starts at <paramref name="codeStartLine"/>.
        /// </summary>
        private void ApplyChange(Tokenizer tokenizer, ITextSnapshot snapshot, Span span, int codeStartLine, int codeStartLineOffset) {
            int firstLine = snapshot.GetLineNumberFromPosition(span.Start);
            int lastLine = snapshot.GetLineNumberFromPosition(span.Length > 0 ? span.End - 1 : span.End);

            Contract.Assert(codeStartLineOffset >= 0);
            Contract.Assert(firstLine >= codeStartLine);

            // find the closest line preceding firstLine for which we know categorizer state, stop at the codeStartLine:
            LineTokenization lineTokenization;
            firstLine = _tokenCache.IndexOfPreviousTokenization(firstLine, codeStartLine, out lineTokenization) + 1;
            object state = lineTokenization.State;

            int currentLine = firstLine;
            object previousState;
            while (currentLine < snapshot.LineCount) {
                previousState = _tokenCache.TryGetTokenization(currentLine, out lineTokenization) ? lineTokenization.State : null;
                _tokenCache[currentLine] = lineTokenization = TokenizeLine(tokenizer, snapshot, state, currentLine, (currentLine == codeStartLine) ? codeStartLineOffset : 0);
                state = lineTokenization.State;

                // stop if we visted all affected lines and the current line has no tokenization state or its previous state is the same as the new state:
                if (currentLine > lastLine && (previousState == null || previousState.Equals(state))) {
                    break;
                }

                currentLine++;
            }

            // classification spans might have changed between the start of the first and end of the last visited line:
            int changeStart = snapshot.GetLineFromLineNumber(firstLine).Start;
            int changeEnd = (currentLine < snapshot.LineCount) ? snapshot.GetLineFromLineNumber(currentLine).End : snapshot.Length;
            if (changeStart < changeEnd) {
                var classificationChanged = ClassificationChanged;
                if (classificationChanged != null) {
                    var args = new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, new Span(changeStart, changeEnd - changeStart)));
                    classificationChanged(this, args);
                }
            }
        }

        private LineTokenization TokenizeLine(Tokenizer tokenizer, ITextSnapshot snapshot, object previousLineState, int lineNo, int lineOffset) {
            ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNo);
            SnapshotSpan lineSpan = new SnapshotSpan(snapshot, line.Start + lineOffset, line.LengthIncludingLineBreak - lineOffset);

            var tcp = new SnapshotSpanSourceCodeReader(lineSpan);

            tokenizer.Initialize(previousLineState, tcp, new SourceLocation(lineOffset, lineNo + 1, lineOffset + 1));
            var tokens = new List<TokenInfo>(tokenizer.ReadTokens(lineSpan.Length)).ToArray();
            return new LineTokenization(tokens, tokenizer.CurrentState);
        }

        private ClassificationSpan ClassifyToken(SnapshotSpan span, TokenInfo token, int lineNumber) {
            IClassificationType classification = null;

            if (token.Category == TokenCategory.Operator) {
                if (token.Trigger == TokenTriggers.MemberSelect) {
                    classification = _provider.DotClassification;
                }
            } else if (token.Category == TokenCategory.Grouping) {
                if ((token.Trigger & TokenTriggers.MatchBraces) != 0) {
                    classification = _provider.GroupingClassification;
                }
            } else if (token.Category == TokenCategory.Delimiter) {
                if (token.Trigger == TokenTriggers.ParameterNext) {
                    classification = _provider.CommaClassification;
                }
            }

            if (classification == null) {
                CategoryMap.TryGetValue(token.Category, out classification);
            }

            if (classification != null) {
                var line = span.Snapshot.GetLineFromLineNumber(lineNumber);
                var index = line.Start.Position + token.SourceSpan.Start.Column - 1;
                var tokenSpan = new Span(index, token.SourceSpan.Length);
                var intersection = span.Intersection(tokenSpan);
                if (intersection != null && intersection.Value.Length > 0) {
                    return new ClassificationSpan(new SnapshotSpan(span.Snapshot, tokenSpan), classification);
                }
            }

            return null;
        }

        #endregion
    }

    internal static class ClassifierExtensions {
        public static PythonClassifier GetPythonClassifier(this ITextBuffer buffer) {
            PythonClassifier res;
            if (buffer.Properties.TryGetProperty<PythonClassifier>(typeof(PythonClassifier), out res)) {
                return res;
            }
            return null;
        }
    }
}
