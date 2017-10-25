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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Provides classification based upon the DLR TokenCategory enum.
    /// </summary>
    internal sealed class PythonClassifier : IClassifier, IPythonTextBufferInfoEventSink {
        private readonly TokenCache _tokenCache;
        private readonly PythonClassifierProvider _provider;

        [ThreadStatic]
        private static Dictionary<PythonLanguageVersion, Tokenizer> _tokenizers;    // tokenizer for each version, shared between all buffers

        internal PythonClassifier(PythonClassifierProvider provider) {
            _tokenCache = new TokenCache();
            _provider = provider;
        }

        private Task OnNewAnalysisEntryAsync(PythonTextBufferInfo sender, AnalysisEntry entry) {
            var analyzer = entry?.Analyzer;
            if (analyzer == null) {
                Debug.Assert(entry == null, "Should not have new analysis entry without an analyzer");
                return Task.CompletedTask;
            }
            _tokenCache.Clear();

            var snapshot = sender.CurrentSnapshot;
            ClassificationChanged?.Invoke(
                this,
                new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length))
            );

            return Task.CompletedTask;
        }

        #region IDlrClassifier

        // This event gets raised if the classification of existing test changes.
        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        /// <summary>
        /// This method classifies the given snapshot span.
        /// </summary>
        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
            var classifications = new List<ClassificationSpan>();

            if (span.Length > 0) {
                // don't add classifications for REPL commands.
                var snapshot = span.Snapshot;
                if (!snapshot.IsReplBufferWithCommand()) {
                    AddClassifications(GetTokenizer(snapshot), classifications, span);
                }
            }

            return classifications;
        }

        private Tokenizer GetTokenizer(ITextSnapshot snapshot) {
            if (_tokenizers == null) {
                _tokenizers = new Dictionary<PythonLanguageVersion, Tokenizer>();
            }
            Tokenizer res;
            var bi = PythonTextBufferInfo.TryGetForBuffer(snapshot.TextBuffer);
            var version = bi?.LanguageVersion ?? PythonLanguageVersion.None;
            if (!_tokenizers.TryGetValue(version, out res)) {
                _tokenizers[version] = res = new Tokenizer(version, options: TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins);
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

        private Task OnTextContentChangedAsync(PythonTextBufferInfo sender, TextContentChangedEventArgs e) {
            if (e == null) {
                Debug.Fail("Invalid type passed to event");
            }

            var snapshot = e.After;

            if (!snapshot.IsReplBufferWithCommand()) {
                _tokenCache.EnsureCapacity(snapshot.LineCount);

                var tokenizer = GetTokenizer(snapshot);
                foreach (var change in e.Changes) {
                    var endLine = snapshot.GetLineNumberFromPosition(change.NewEnd) + 1;
                    if (change.LineCountDelta > 0) {
                        _tokenCache.InsertLines(endLine - change.LineCountDelta, change.LineCountDelta);
                    } else if (change.LineCountDelta < 0) {
                        _tokenCache.DeleteLines(endLine, Math.Min(-change.LineCountDelta, snapshot.LineCount - endLine));
                    }

                    ApplyChange(tokenizer, snapshot, change.NewSpan);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Adds classification spans to the given collection.
        /// Scans a contiguous sub-<paramref name="span"/> of a larger code span which starts at <paramref name="codeStartLine"/>.
        /// </summary>
        private void AddClassifications(Tokenizer tokenizer, List<ClassificationSpan> classifications, SnapshotSpan span) {
            Debug.Assert(span.Length > 0);

            var snapshot = span.Snapshot;
            int firstLine = snapshot.GetLineNumberFromPosition(span.Start);
            int lastLine = snapshot.GetLineNumberFromPosition(span.End - 1);

            Contract.Assert(firstLine >= 0);

            var strSpan = default(SourceSpan);

            _tokenCache.EnsureCapacity(snapshot.LineCount);

            for (int line = firstLine; line <= lastLine; ++line) {
                var lineTokenization = GetTokenizationOfKnownState(tokenizer, snapshot, _tokenCache, line);

                for (int i = 0; i < lineTokenization.Tokens.Length; i++) {
                    var token = lineTokenization.Tokens[i];
                    if (token.Category == TokenCategory.IncompleteMultiLineStringLiteral || token.Category == TokenCategory.StringLiteral) {
                        if (token.SourceSpan.Start >= strSpan.Start && token.SourceSpan.End <= strSpan.End) {
                            // We are already in the current string span, so do not add any
                            // more classifications.
                            continue;
                        }

                        // Walk both directions to get the full span
                        if (token.Category == TokenCategory.StringLiteral) {
                            strSpan = token.SourceSpan;
                        } else {
                            strSpan = GetMultiLineString(tokenizer, snapshot, token);
                        }

                        classifications.Add(new ClassificationSpan(
                            GetTokenSnapshotSpan(snapshot, strSpan.Start, strSpan.End),
                            _provider.StringLiteral
                        ));
                    } else {
                        var classification = ClassifyToken(span, token, line);

                        if (classification != null) {
                            classifications.Add(classification);
                        }
                    }
                }
            }
        }

        private static LineTokenization GetTokenizationOfKnownState(Tokenizer tokenizer, ITextSnapshot snapshot, TokenCache cache, int lineNo) {
            LineTokenization line;
            int lastLine = cache.IndexOfPreviousTokenization(lineNo + 1, 0, out line);
            if (lastLine == lineNo) {
                return line;
            }

            for (int i = lastLine + 1; i <= lineNo; ++i) {
                var lineState = line.State;
                if (!cache.TryGetTokenization(i, out line)) {
                    cache[i] = line = TokenizeLine(tokenizer, snapshot, lineState, i);
                }
            }

            return line;
        }

        private SourceSpan GetMultiLineString(Tokenizer tokenizer, ITextSnapshot snapshot, TokenInfo initial) {
            var bestStart = initial.SourceSpan.Start;
            var bestEnd = initial.SourceSpan.End;

            for (int line = initial.SourceSpan.Start.Line - 1; line >= 0; --line) {
                var tokens = GetTokenizationOfKnownState(tokenizer, snapshot, _tokenCache, line);

                if (tokens.Tokens.Length == 0) {
                    continue;
                }

                int lastIndex = tokens.Tokens.Length - 1;
                // For the first line, only look up to the start location
                if (line == initial.SourceSpan.Start.Line - 1) {
                    lastIndex = tokens.Tokens.Count(t => t.SourceSpan.Start < initial.SourceSpan.Start);
                    if (lastIndex >= tokens.Tokens.Length) {
                        lastIndex = tokens.Tokens.Length - 1;
                    }
                }

                // If the last token is not a string, the previous best was the start
                var tok = tokens.Tokens[lastIndex];
                if (tok.Category != TokenCategory.IncompleteMultiLineStringLiteral) {
                    break;
                }

                // We have a new best start point
                bestStart = tok.SourceSpan.Start;

                // If there are other tokens on this line, we have the start point
                if (lastIndex > 0) {
                    break;
                }
            }

            for (int line = initial.SourceSpan.End.Line - 1; line < snapshot.LineCount; ++line) {
                var tokens = GetTokenizationOfKnownState(tokenizer, snapshot, _tokenCache, line);

                if (tokens.Tokens.Length == 0) {
                    continue;
                }

                int firstIndex = 0;
                if (line == initial.SourceSpan.End.Line - 1) {
                    firstIndex = tokens.Tokens.Count(t => t.SourceSpan.Start < initial.SourceSpan.Start);
                    if (firstIndex >= tokens.Tokens.Length) {
                        continue;
                    }
                }

                // If the first token is not a string, the previous best was the end
                var tok = tokens.Tokens[firstIndex];
                if (tok.Category == TokenCategory.StringLiteral) {
                    bestEnd = tok.SourceSpan.End;
                    break;
                }
                if (tok.Category != TokenCategory.IncompleteMultiLineStringLiteral) {
                    break;
                }

                // We have a new best end point
                bestEnd = tok.SourceSpan.End;

                // If there are other tokens on this line, we have the end point
                if (tokens.Tokens.Length - 1> firstIndex) {
                    break;
                }
            }

            return new SourceSpan(bestStart, bestEnd);
        }

        /// <summary>
        /// Rescans the part of the buffer affected by a change. 
        /// Scans a contiguous sub-<paramref name="span"/> of a larger code span which starts at <paramref name="codeStartLine"/>.
        /// </summary>
        private void ApplyChange(Tokenizer tokenizer, ITextSnapshot snapshot, Span span) {
            int firstLine = snapshot.GetLineNumberFromPosition(span.Start);
            int lastLine = snapshot.GetLineNumberFromPosition(span.Length > 0 ? span.End - 1 : span.End);

            Contract.Assert(firstLine >= 0);

            // find the closest line preceding firstLine for which we know categorizer state, stop at the codeStartLine:
            LineTokenization lineTokenization;
            firstLine = _tokenCache.IndexOfPreviousTokenization(firstLine, 0, out lineTokenization) + 1;
            object state = lineTokenization.State;

            int currentLine = firstLine;
            object previousState;
            while (currentLine < snapshot.LineCount) {
                previousState = _tokenCache.TryGetTokenization(currentLine, out lineTokenization) ? lineTokenization.State : null;
                _tokenCache[currentLine] = lineTokenization = TokenizeLine(tokenizer, snapshot, state, currentLine);
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

        private static LineTokenization TokenizeLine(Tokenizer tokenizer, ITextSnapshot snapshot, object previousLineState, int lineNo) {
            ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNo);
            SnapshotSpan lineSpan = new SnapshotSpan(snapshot, line.Start, line.LengthIncludingLineBreak);

            var tcp = new SnapshotSpanSourceCodeReader(lineSpan);

            tokenizer.Initialize(previousLineState, tcp, new SourceLocation(line.Start.Position, lineNo + 1, 1));
            try {
                var tokens = tokenizer.ReadTokens(lineSpan.Length).ToArray();
                return new LineTokenization(tokens, tokenizer.CurrentState);
            } finally {
                tokenizer.Uninitialize();
            }
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
                _provider.CategoryMap.TryGetValue(token.Category, out classification);
            }

            if (classification != null) {
                var tokenSpan = GetTokenSnapshotSpan(span.Snapshot, token.SourceSpan.Start, token.SourceSpan.End);
                var intersection = span.Intersection(tokenSpan);

                if (intersection != null && intersection.Value.Length > 0 ||
                    (span.Length == 0 && tokenSpan.Contains(span.Start))) { // handle zero-length spans which Intersect and Overlap won't return true on ever.
                    return new ClassificationSpan(new SnapshotSpan(span.Snapshot, tokenSpan), classification);
                }
            }

            return null;
        }

        private static SnapshotSpan GetTokenSnapshotSpan(ITextSnapshot snapshot, SourceLocation tokenStart, SourceLocation tokenEnd) {
            ITextSnapshotLine startLine, endLine;
            if (tokenStart.Line <= 0) {
                startLine = snapshot.GetLineFromLineNumber(0);
            } else if (tokenStart.Line <= snapshot.LineCount) {
                startLine = snapshot.GetLineFromLineNumber(tokenStart.Line - 1);
            } else {
                return new SnapshotSpan(snapshot, snapshot.Length, 0);
            }

            var startCol = tokenStart.Column - 1;
            var start = (startLine.Start.Position + startCol >= startLine.EndIncludingLineBreak) ?
                startLine.EndIncludingLineBreak:
                (startLine.Start + startCol);

            if (tokenEnd.Line == tokenStart.Line) {
                endLine = startLine;
            } else if (tokenEnd.Line <= 0) {
                endLine = snapshot.GetLineFromLineNumber(0);
            } else if (tokenEnd.Line <= snapshot.LineCount) {
                endLine = snapshot.GetLineFromLineNumber(tokenEnd.Line - 1);
            } else {
                return new SnapshotSpan(snapshot, snapshot.Length, 0);
            }

            var endCol = tokenEnd.Column - 1;
            var end = (endLine.Start.Position + endCol >= endLine.EndIncludingLineBreak) ?
                endLine.EndIncludingLineBreak :
                (endLine.Start + endCol);

            return new SnapshotSpan(start, end);
        }

        Task IPythonTextBufferInfoEventSink.PythonTextBufferEventAsync(PythonTextBufferInfo sender, PythonTextBufferInfoEventArgs e) {
            if (e.Event == PythonTextBufferInfoEvents.NewAnalysisEntry) {
                return OnNewAnalysisEntryAsync(sender, e.AnalysisEntry);
            } else if (e.Event == PythonTextBufferInfoEvents.ContentTypeChanged) {
                _tokenCache.Clear();
            } else if (e.Event == PythonTextBufferInfoEvents.TextContentChanged) {
                return OnTextContentChangedAsync(sender, (e as PythonTextBufferInfoNestedEventArgs)?.NestedEventArgs as TextContentChangedEventArgs);
            }
            return Task.CompletedTask;
        }

        #endregion
    }

    internal static partial class ClassifierExtensions {
        public static PythonClassifier GetPythonClassifier(this ITextBuffer buffer) {
            var bi = PythonTextBufferInfo.TryGetForBuffer(buffer);
            if (bi == null) {
                return null;
            }

            var provider = bi.Services.ComponentModel.GetService<PythonClassifierProvider>();
            return provider.GetClassifier(buffer) as PythonClassifier;
        }
    }
}
