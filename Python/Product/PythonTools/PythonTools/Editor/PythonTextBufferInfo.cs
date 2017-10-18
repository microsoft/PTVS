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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Editor {
    sealed class PythonTextBufferInfo {
        public static PythonTextBufferInfo ForBuffer(PythonEditorServices services, ITextBuffer buffer) {
            var bi = (buffer ?? throw new ArgumentNullException(nameof(buffer))).Properties.GetOrCreateSingletonProperty(
                typeof(PythonTextBufferInfo),
                () => new PythonTextBufferInfo(services, buffer)
            );
            if (bi._replace) {
                bi = bi.ReplaceBufferInfo();
                buffer.Properties[typeof(PythonTextBufferInfo)] = bi;
            }
            return bi;
        }

        public static PythonTextBufferInfo TryGetForBuffer(ITextBuffer buffer) {
            PythonTextBufferInfo bi;
            if (buffer == null) {
                return null;
            }
            if (!buffer.Properties.TryGetProperty(typeof(PythonTextBufferInfo), out bi) || bi == null) {
                return null;
            }
            if (bi._replace) {
                bi = bi.ReplaceBufferInfo();
                buffer.Properties[typeof(PythonTextBufferInfo)] = bi;
            }
            return bi;
        }

        /// <summary>
        /// Calling this function marks the buffer to be replaced next
        /// time it is retrieved.
        /// </summary>
        public static void MarkForReplacement(ITextBuffer buffer) {
            var bi = TryGetForBuffer(buffer);
            if (bi != null) {
                bi._replace = true;
            }
        }

        public static IEnumerable<PythonTextBufferInfo> GetAllFromView(ITextView view) {
            return view.BufferGraph.GetTextBuffers(_ => true)
                .Select(b => TryGetForBuffer(b))
                .Where(b => b != null);
        }

        private readonly object _lock = new object();

        private int _bufferId;
        private AnalysisEntry _analysisEntry;

        private readonly ConcurrentDictionary<object, IPythonTextBufferInfoEventSink> _eventSinks;

        private readonly Lazy<string> _filename;

        private bool _replace;

        private PythonTextBufferInfo(PythonEditorServices services, ITextBuffer buffer) {
            Services = services;
            Buffer = buffer;
            _eventSinks = new ConcurrentDictionary<object, IPythonTextBufferInfoEventSink>();
            _filename = new Lazy<string>(GetOrCreateFilename);
            _bufferId = -1;

            ITextDocument doc;
            if (Buffer.Properties.TryGetProperty(typeof(ITextDocument), out doc)) {
                Document = doc;
                Document.EncodingChanged += Document_EncodingChanged;
            }
            Buffer.ContentTypeChanged += Buffer_ContentTypeChanged;
            Buffer.Changed += Buffer_TextContentChanged;
            Buffer.ChangedLowPriority += Buffer_TextContentChangedLowPriority;
        }

        private T GetOrCreate<T>(ref T destination, Func<PythonTextBufferInfo, T> creator) where T : class {
            if (destination != null) {
                return destination;
            }
            var created = creator(this);
            lock (_lock) {
                if (destination == null) {
                    destination = created;
                } else {
                    created = destination;
                }
            }
            return created;
        }

        private PythonTextBufferInfo ReplaceBufferInfo() {
            var newInfo = new PythonTextBufferInfo(Services, Buffer);
            foreach (var sink in _eventSinks) {
                newInfo._eventSinks[sink.Key] = sink.Value;
            }

            Buffer.Properties[typeof(PythonTextBufferInfo)] = newInfo;

            Buffer.ContentTypeChanged -= Buffer_ContentTypeChanged;
            Buffer.Changed -= Buffer_TextContentChanged;
            Buffer.ChangedLowPriority -= Buffer_TextContentChangedLowPriority;

            InvokeSinks(new PythonNewTextBufferInfoEventArgs(PythonTextBufferInfoEvents.NewTextBufferInfo, newInfo));

            return newInfo;
        }

        private string GetOrCreateFilename() {
            var replEval = Buffer.GetInteractiveWindow()?.GetPythonEvaluator();
            if (replEval != null) {
                return replEval.AnalysisFilename;
            }

            ITextDocument doc;
            if (Buffer.Properties.TryGetProperty(typeof(ITextDocument), out doc)) {
                return doc.FilePath;
            }

            return "{0}.py".FormatInvariant(Guid.NewGuid());
        }



        public ITextBuffer Buffer { get; }
        public ITextDocument Document { get; }
        public ITextSnapshot CurrentSnapshot => Buffer.CurrentSnapshot;
        public IContentType ContentType => Buffer.ContentType;
        public string Filename => _filename.Value;

        public PythonEditorServices Services { get; }

        public PythonLanguageVersion LanguageVersion => AnalysisEntry?.Analyzer.LanguageVersion ?? PythonLanguageVersion.None;

        #region Events

        private void OnNewAnalysisEntry(AnalysisEntry entry) {
            InvokeSinks(new PythonTextBufferInfoEventArgs(PythonTextBufferInfoEvents.NewAnalysisEntry, entry));
        }

        private void AnalysisEntry_ParseComplete(object sender, EventArgs e) {
            InvokeSinks(new PythonTextBufferInfoEventArgs(PythonTextBufferInfoEvents.NewParseTree, (AnalysisEntry)sender));
        }

        private void AnalysisEntry_AnalysisComplete(object sender, EventArgs e) {
            InvokeSinks(new PythonTextBufferInfoEventArgs(PythonTextBufferInfoEvents.NewAnalysis, (AnalysisEntry)sender));
        }

        private void Buffer_TextContentChanged(object sender, TextContentChangedEventArgs e) {
            InvokeSinks(new PythonTextBufferInfoNestedEventArgs(PythonTextBufferInfoEvents.TextContentChanged, e));
        }

        private void Buffer_TextContentChangedLowPriority(object sender, TextContentChangedEventArgs e) {
            InvokeSinks(new PythonTextBufferInfoNestedEventArgs(PythonTextBufferInfoEvents.TextContentChangedLowPriority, e));
        }

        private void Buffer_ContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
            InvokeSinks(new PythonTextBufferInfoNestedEventArgs(PythonTextBufferInfoEvents.ContentTypeChanged, e));
        }

        private void Document_EncodingChanged(object sender, EncodingChangedEventArgs e) {
            InvokeSinks(new PythonTextBufferInfoNestedEventArgs(PythonTextBufferInfoEvents.DocumentEncodingChanged, e));
        }

        #endregion

        #region Sink management

        public void AddSink(object key, IPythonTextBufferInfoEventSink sink) {
            if (!_eventSinks.TryAdd(key, sink)) {
                if (_eventSinks[key] != sink) {
                    throw new InvalidOperationException("cannot replace existing sink");
                }
            }
        }

        public T GetOrCreateSink<T>(object key, Func<PythonTextBufferInfo, T> creator) where T : class, IPythonTextBufferInfoEventSink {
            IPythonTextBufferInfoEventSink sink;
            if (_eventSinks.TryGetValue(key, out sink)) {
                return sink as T;
            }
            sink = creator(this);
            if (!_eventSinks.TryAdd(key, sink)) {
                sink = _eventSinks[key];
            }
            return sink as T;
        }

        public IPythonTextBufferInfoEventSink TryGetSink(object key) {
            IPythonTextBufferInfoEventSink sink;
            return _eventSinks.TryGetValue(key, out sink) ? sink : null;
        }

        public bool RemoveSink(object key) => _eventSinks.TryRemove(key, out _);

        private void InvokeSinks(PythonTextBufferInfoEventArgs e) {
            foreach (var sink in _eventSinks.Values) {
                sink.PythonTextBufferEventAsync(this, e)
                    .HandleAllExceptions(Services.Site, GetType())
                    .DoNotWait();
            }
        }

        #endregion

        #region Analysis Info

        public AnalysisEntry AnalysisEntry => Volatile.Read(ref _analysisEntry);

        /// <summary>
        /// Changes the analysis entry to <paramref name="entry"/> if the current
        /// entry matches <paramref name="ifCurrent"/>. Returns the current analysis
        /// entry, regardless of whether it changed or not.
        /// </summary>
        public AnalysisEntry TrySetAnalysisEntry(AnalysisEntry entry, AnalysisEntry ifCurrent) {
            var previous = Interlocked.CompareExchange(ref _analysisEntry, entry, ifCurrent);

            if (previous != ifCurrent) {
                return previous;
            }

            if (previous != null) {
                previous.AnalysisComplete -= AnalysisEntry_AnalysisComplete;
                previous.ParseComplete -= AnalysisEntry_ParseComplete;
                previous.Analyzer.BufferDetached(previous, Buffer);
            }
            if (entry != null) {
                entry.AnalysisComplete += AnalysisEntry_AnalysisComplete;
                entry.ParseComplete += AnalysisEntry_ParseComplete;
            }

            OnNewAnalysisEntry(entry);

            return entry;
        }

        public void ClearAnalysisEntry() {
            var previous = Interlocked.Exchange(ref _analysisEntry, null);
            if (previous != null) {
                previous.AnalysisComplete -= AnalysisEntry_AnalysisComplete;
                previous.ParseComplete -= AnalysisEntry_ParseComplete;
                previous.Analyzer.BufferDetached(previous, Buffer);

                OnNewAnalysisEntry(null);
            }
        }

        public int AnalysisBufferId => Volatile.Read(ref _bufferId);

        public bool SetAnalysisBufferId(int id) {
            if (id < 0) {
                Volatile.Write(ref _bufferId, -1);
                return true;
            }
            return Interlocked.CompareExchange(ref _bufferId, id, -1) == -1;
        }

        public ITextSnapshot LastSentSnapshot { get; set; }
        public ITextVersion LastParseReceivedVersion { get; private set; }
        public ITextVersion LastAnalysisReceivedVersion { get; private set; }

        public ITextVersion UpdateLastReceivedParse(int version) {
            lock (_lock) {
                var ver = LastParseReceivedVersion ?? Buffer.CurrentSnapshot.Version;
                while (ver?.Next != null && ver.VersionNumber < version) {
                    ver = ver.Next;
                }
                var r = ver != null && ver.VersionNumber >= version;
                LastParseReceivedVersion = ver;
                return r ? ver : null;
            }
        }

        public ITextVersion UpdateLastReceivedAnalysis(int version) {
            lock (_lock) {
                var ver = LastAnalysisReceivedVersion ?? Buffer.CurrentSnapshot.Version;
                while (ver?.Next != null && ver.VersionNumber < version) {
                    ver = ver.Next;
                }
                var r = ver != null && ver.VersionNumber >= version;
                LastAnalysisReceivedVersion = ver;
                return r ? ver : null;
            }
        }

        /// <summary>
        /// Gets the smallest expression that fully contains the span.
        /// </summary>
        /// <remarks>
        /// When options specifies the member target, rather than the
        /// full expression, only the start of the span is used.
        /// </remarks>
        public SnapshotSpan? GetExpressionAtPoint(SnapshotSpan span, GetExpressionOptions options) {
            var timer = new Stopwatch();
            timer.Start();
            bool hasError = true, hasResult = false;
            try {
                var r = GetExpressionAtPoint(span, LanguageVersion, options);
                hasResult = r != null;
                hasError = false;
                return r;
            } finally {
                timer.Stop();
                try {
                    int elapsed = (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue);
                    if (elapsed > 10) {
                        Services.Python.Logger.LogEvent(Logging.PythonLogEvent.GetExpressionAtPoint, new Logging.GetExpressionAtPointInfo {
                            Milliseconds = elapsed,
                            PartialAstLength = span.End.Position,
                            ExpressionFound = hasResult,
                            Success = !hasError
                        });
                    }
                } catch (Exception ex) {
                    Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                }
            }
        }

        /// <summary>
        /// Returns the first token containing or adjacent to the specified point.
        /// </summary>
        public TokenInfo? GetTokenAtPoint(SnapshotPoint point) {
            var line = point.GetContainingLine();
            var pt = point.ToSourceLocation();
            return GetLineTokens(line, LanguageVersion)
                .TakeWhile(t => t.SourceSpan.End >= pt)
                .FirstOrDefault(t => t.SourceSpan.Start <= pt);
        }

        private static IEnumerable<TokenInfo> GetLineTokens(ITextSnapshotLine line, PythonLanguageVersion version) {
            var sourceSpan = new SnapshotSpanSourceCodeReader(line.Extent);
            var tokenizer = new Tokenizer(version, options: TokenizerOptions.VerbatimCommentsAndLineJoins);
            tokenizer.Initialize(sourceSpan);
            for (var t = tokenizer.ReadToken(); t.Category != TokenCategory.EndOfStream; t = tokenizer.ReadToken()) {
                yield return t;
            }
        }

        internal static bool IsPossibleExpressionAtPoint(SnapshotPoint point, PythonLanguageVersion version) {
            var line = point.GetContainingLine();
            int col = point - line.Start + 1;
            bool anyTokens = false;

            foreach (var t in GetLineTokens(line, version)) {
                anyTokens = true;
                if (t.SourceSpan.Start.Column >= col || t.SourceSpan.End.Column < col) {
                    continue;
                }

                if (t.Category == TokenCategory.LineComment || t.Category == TokenCategory.Comment) {
                    // We are in or at the end of a comment
                    return false;
                }

                // Tokens after this point are possible expressions if we are looking
                // at the very end of the token.
                if (t.SourceSpan.End.Column == col) {
                    continue;
                }

                if (t.Category == TokenCategory.StringLiteral) {
                    // We are in a string literal
                    return false;
                }
            }

            return anyTokens;
        }

        internal static SnapshotSpan? GetExpressionAtPoint(SnapshotSpan span, PythonLanguageVersion version, GetExpressionOptions options) {
            // First do some very quick tokenization to save a full analysis
            if (!IsPossibleExpressionAtPoint(span.Start, version)) {
                return null;
            }

            if (span.End.GetContainingLine() != span.Start.GetContainingLine() &&
                !IsPossibleExpressionAtPoint(span.End, version)) {
                return null;
            }

            var sourceSpan = new SnapshotSpanSourceCodeReader(
                new SnapshotSpan(span.Snapshot, 0, span.End.Position)
            );

            PythonAst ast;
            using (var parser = Parser.CreateParser(sourceSpan, version)) {
                ast = parser.ParseFile();
            }

            var finder = new ExpressionFinder(ast, options);
            var actualExpr = finder.GetExpressionSpan(span.ToSourceSpan());
            if (actualExpr == null) {
                // No expression
                return null;
            }

            return actualExpr.Value.ToSnapshotSpan(span.Snapshot);
        }


        public bool DoNotParse {
            get => Buffer.Properties.ContainsProperty(BufferParser.DoNotParse);
            set {
                if (value) {
                    Buffer.Properties[BufferParser.DoNotParse] = BufferParser.DoNotParse;
                } else {
                    Buffer.Properties.RemoveProperty(BufferParser.DoNotParse);
                }
            }
        }

        public bool ParseImmediately {
            get => Buffer.Properties.ContainsProperty(BufferParser.ParseImmediately);
            set {
                if (value) {
                    Buffer.Properties[BufferParser.ParseImmediately] = BufferParser.ParseImmediately;
                } else {
                    Buffer.Properties.RemoveProperty(BufferParser.ParseImmediately);
                }
            }
        }


        #endregion
    }
}
