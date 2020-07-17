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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Parsing;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.LanguageServerClient;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Editor {
    internal sealed class PythonTextBufferInfo {
        private static readonly object PythonTextBufferInfoKey = new { Id = "PythonTextBufferInfo" };

        public static PythonTextBufferInfo ForBuffer(IServiceProvider site, ITextBuffer buffer) {
            var bi = (buffer ?? throw new ArgumentNullException(nameof(buffer))).Properties.GetOrCreateSingletonProperty(
                PythonTextBufferInfoKey,
                () => new PythonTextBufferInfo(site, buffer)
            );
            if (bi._replace) {
                bi = bi.ReplaceBufferInfo();
            }
            return bi;
        }

        public static PythonTextBufferInfo TryGetForBuffer(ITextBuffer buffer) {
            PythonTextBufferInfo bi;
            if (buffer == null) {
                return null;
            }
            if (!buffer.Properties.TryGetProperty(PythonTextBufferInfoKey, out bi) || bi == null) {
                return null;
            }
            if (bi._replace) {
                bi = bi.ReplaceBufferInfo();
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

        private readonly ConcurrentDictionary<object, IPythonTextBufferInfoEventSink> _eventSinks;

        private readonly TokenCache _tokenCache;

        private readonly bool _hasChangedOnBackground;
        private bool _replace;

        internal PythonLanguageVersion _defaultLanguageVersion;

        private PythonTextBufferInfo(IServiceProvider site, ITextBuffer buffer) {
            Site = site;
            Buffer = buffer;
            _eventSinks = new ConcurrentDictionary<object, IPythonTextBufferInfoEventSink>();
            _tokenCache = new TokenCache();
            _defaultLanguageVersion = PythonLanguageVersion.None;

            ITextDocument doc;
            if (Buffer.Properties.TryGetProperty(typeof(ITextDocument), out doc)) {
                Document = doc;
                Document.EncodingChanged += Document_EncodingChanged;
            }
            Buffer.ContentTypeChanged += Buffer_ContentTypeChanged;
            Buffer.Changed += Buffer_TextContentChanged;
            Buffer.ChangedLowPriority += Buffer_TextContentChangedLowPriority;

            if (Buffer is ITextBuffer2 buffer2) {
                _hasChangedOnBackground = true;
                buffer2.ChangedOnBackground += Buffer_TextContentChangedOnBackground;
            }
        }

        private PythonTextBufferInfo ReplaceBufferInfo() {
            var newInfo = new PythonTextBufferInfo(Site, Buffer);
            foreach (var sink in _eventSinks) {
                newInfo._eventSinks[sink.Key] = sink.Value;
            }

            Buffer.Properties[PythonTextBufferInfoKey] = newInfo;

            Buffer.ContentTypeChanged -= Buffer_ContentTypeChanged;
            Buffer.Changed -= Buffer_TextContentChanged;
            Buffer.ChangedLowPriority -= Buffer_TextContentChangedLowPriority;

            if (Buffer is ITextBuffer2 buffer2) {
                buffer2.ChangedOnBackground -= Buffer_TextContentChangedOnBackground;
            }

            InvokeSinks(new PythonNewTextBufferInfoEventArgs(PythonTextBufferInfoEvents.NewTextBufferInfo, newInfo));

            return newInfo;
        }

        public ITextBuffer Buffer { get; }
        public ITextDocument Document { get; }
        public ITextSnapshot CurrentSnapshot => Buffer.CurrentSnapshot;
        public IContentType ContentType => Buffer.ContentType;

        public IServiceProvider Site { get; }

        public PythonLanguageVersion LanguageVersion {
            get {
                // TODO: Pylance
                return PythonLanguageVersion.V37;
                //var client = PythonLanguageClient.FindLanguageClient(Buffer);
                //return client?.Configuration.Version.ToLanguageVersion() ?? _defaultLanguageVersion;
            }
        }

        #region Events

        private void Buffer_TextContentChanged(object sender, TextContentChangedEventArgs e) {
            if (!_hasChangedOnBackground) {
                UpdateTokenCache(e);
            }
            InvokeSinks(new PythonTextBufferInfoNestedEventArgs(PythonTextBufferInfoEvents.TextContentChanged, e));
            if (!_hasChangedOnBackground) {
                InvokeSinks(new PythonTextBufferInfoNestedEventArgs(PythonTextBufferInfoEvents.TextContentChangedOnBackgroundThread, e));
            }
        }

        private void Buffer_TextContentChangedLowPriority(object sender, TextContentChangedEventArgs e) {
            InvokeSinks(new PythonTextBufferInfoNestedEventArgs(PythonTextBufferInfoEvents.TextContentChangedLowPriority, e));
        }

        private void Buffer_ContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
            ClearTokenCache();
            InvokeSinks(new PythonTextBufferInfoNestedEventArgs(PythonTextBufferInfoEvents.ContentTypeChanged, e));
        }

        private void Buffer_TextContentChangedOnBackground(object sender, TextContentChangedEventArgs e) {
            if (!_hasChangedOnBackground) {
                Debug.Fail("Received TextContentChangedOnBackground unexpectedly");
                return;
            }
            UpdateTokenCache(e);
            InvokeSinks(new PythonTextBufferInfoNestedEventArgs(PythonTextBufferInfoEvents.TextContentChangedOnBackgroundThread, e));
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

        public bool RemoveSink(object key) {
            return _eventSinks.TryRemove(key, out _);
        }

        private void InvokeSinks(PythonTextBufferInfoEventArgs e) {
            foreach (var sink in _eventSinks.Values) {
                sink.PythonTextBufferEventAsync(this, e)
                    .HandleAllExceptions(Site, GetType())
                    .DoNotWait();
            }
        }

        #endregion

        /// <summary>
        /// Returns the first token containing or adjacent to the specified point.
        /// </summary>
        public TrackingTokenInfo? GetTokenAtPoint(SnapshotPoint point) {
            return GetTrackingTokens(new SnapshotSpan(point, 0))
                .Cast<TrackingTokenInfo?>()
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns tokens for the specified line.
        /// </summary>
        public IEnumerable<TrackingTokenInfo> GetTokens(ITextSnapshotLine line) {
            using (var cacheSnapshot = _tokenCache.GetSnapshot()) {
                var lineTokenization = cacheSnapshot.GetLineTokenization(line, GetTokenizerLazy());
                var lineNumber = line.LineNumber;
                var lineSpan = line.Snapshot.CreateTrackingSpan(line.ExtentIncludingLineBreak, SpanTrackingMode.EdgeNegative);
                return lineTokenization.Tokens.Select(t => new TrackingTokenInfo(t, lineNumber, lineSpan));
            }
        }

        public IEnumerable<TrackingTokenInfo> GetTokens(SnapshotSpan span) {
            return GetTrackingTokens(span);
        }

        /// <summary>
        /// Iterates forwards through tokens starting from the token at or
        /// adjacent to the specified point.
        /// </summary>
        public IEnumerable<TrackingTokenInfo> GetTokensForwardFromPoint(SnapshotPoint point) {
            var line = point.GetContainingLine();

            foreach (var token in GetTrackingTokens(new SnapshotSpan(point, line.End))) {
                yield return token;
            }

            while (line.LineNumber < line.Snapshot.LineCount - 1) {
                line = line.Snapshot.GetLineFromLineNumber(line.LineNumber + 1);
                // Use line.Extent because GetLineTokens endpoints are inclusive - we
                // will get the line break token because it is adjacent, but no
                // other repetitions.
                foreach (var token in GetTrackingTokens(line.Extent)) {
                    yield return token;
                }
            }
        }

        /// <summary>
        /// Iterates backwards through tokens starting from the token at or
        /// adjacent to the specified point.
        /// </summary>
        public IEnumerable<TrackingTokenInfo> GetTokensInReverseFromPoint(SnapshotPoint point) {
            var line = point.GetContainingLine();

            foreach (var token in GetTrackingTokens(new SnapshotSpan(line.Start, point)).Reverse()) {
                yield return token;
            }

            while (line.LineNumber > 0) {
                line = line.Snapshot.GetLineFromLineNumber(line.LineNumber - 1);
                // Use line.Extent because GetLineTokens endpoints are inclusive - we
                // will get the line break token because it is adjacent, but no
                // other repetitions.
                foreach (var token in GetTrackingTokens(line.Extent).Reverse()) {
                    yield return token;
                }
            }
        }

        internal IEnumerable<TrackingTokenInfo> GetTrackingTokens(SnapshotSpan span) {
            int firstLine = span.Start.GetContainingLine().LineNumber;
            int lastLine = span.End.GetContainingLine().LineNumber;

            int startCol = span.Start - span.Start.GetContainingLine().Start;
            int endCol = span.End - span.End.GetContainingLine().Start;

            // We need current state of the cache since it can change from a background thread
            using (var cacheSnapshot = _tokenCache.GetSnapshot()) {
                var tokenizerLazy = GetTokenizerLazy();
                for (int line = firstLine; line <= lastLine; ++line) {
                    var lineTokenization = cacheSnapshot.GetLineTokenization(span.Snapshot.GetLineFromLineNumber(line), tokenizerLazy);

                    foreach (var token in lineTokenization.Tokens.MaybeEnumerate()) {
                        if (line == firstLine && token.Column + token.Length < startCol) {
                            continue;
                        }
                        if (line == lastLine && token.Column > endCol) {
                            continue;
                        }
                        yield return new TrackingTokenInfo(token, line, lineTokenization.Line);
                    }
                }
            }
        }

        #region Token Cache Management

        private Lazy<Tokenizer> GetTokenizerLazy()
            => new Lazy<Tokenizer>(() => new Tokenizer(LanguageVersion, options: TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins));

        private void ClearTokenCache() => _tokenCache.Clear();

        private void UpdateTokenCache(TextContentChangedEventArgs e) {
            // NOTE: Runs on background thread

            var snapshot = e.After;
            if (snapshot.TextBuffer != Buffer) {
                Debug.Fail("Mismatched buffer");
                return;
            }

            if (snapshot.IsReplBufferWithCommand()) {
                return;
            }

            // Prevent later updates overwriting our tokenization
            _tokenCache.Update(e, GetTokenizerLazy());
        }

        #endregion
    }
}
