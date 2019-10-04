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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Parsing;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.LanguageServerClient;
using Microsoft.PythonTools.Projects;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Editor {
    internal sealed class PythonTextBufferInfo {
        private static readonly object PythonTextBufferInfoKey = new { Id = "PythonTextBufferInfo" };

        private static readonly object _testFilename = new { Name = "TestFilename" };
        private static readonly object _testDocumentUri = new { Name = "DocumentUri" };

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
                bi.TraceWithStack("MarkForReplacement");
            }
        }

        public static IEnumerable<PythonTextBufferInfo> GetAllFromView(ITextView view) {
            return view.BufferGraph.GetTextBuffers(_ => true)
                .Select(b => TryGetForBuffer(b))
                .Where(b => b != null);
        }

        private readonly object _lock = new object();

        // LSC
        //private AnalysisEntry _analysisEntry;
        //private TaskCompletionSource<AnalysisEntry> _waitingForEntry;

        private readonly ConcurrentDictionary<object, IPythonTextBufferInfoEventSink> _eventSinks;

        private readonly Lazy<string> _filename;
        private readonly Lazy<Uri> _documentUri;
        private readonly TokenCache _tokenCache;
        // LSC
        //private readonly LocationTracker _locationTracker;

        private readonly bool _hasChangedOnBackground;
        private bool _replace;

        internal PythonLanguageVersion _defaultLanguageVersion;

        // LSC
        //private readonly AnalysisLogWriter _traceLog;

        private PythonTextBufferInfo(IServiceProvider site, ITextBuffer buffer) {
            Site = site;
            Buffer = buffer;
            _eventSinks = new ConcurrentDictionary<object, IPythonTextBufferInfoEventSink>();
            _filename = new Lazy<string>(GetOrCreateFilename);
            _documentUri = new Lazy<Uri>(GetOrCreateDocumentUri);
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

            // LSC
            //_locationTracker = new LocationTracker(Buffer.CurrentSnapshot);

            //_traceLog = OpenTraceLog();
        }

        private PythonTextBufferInfo ReplaceBufferInfo() {
            TraceWithStack("ReplaceBufferInfo");

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

            // LSC
            //Interlocked.Exchange(ref _waitingForEntry, null)?.TrySetResult(null);

            InvokeSinks(new PythonNewTextBufferInfoEventArgs(PythonTextBufferInfoEvents.NewTextBufferInfo, newInfo));

            //_traceLog?.Dispose();

            return newInfo;
        }

        private string GetOrCreateFilename() {
            string path;
            if (Buffer.Properties.TryGetProperty(_testFilename, out path)) {
                return path;
            }

            var replEval = Buffer.GetInteractiveWindow()?.Evaluator as IPythonInteractiveIntellisense;
            var docUri = replEval?.DocumentUri;
            if (docUri != null && docUri.IsFile) {
                return docUri.LocalPath;
            }

            if (Buffer.GetInteractiveWindow() != null) {
                return null;
            }

            if (Buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument doc) &&
                !string.IsNullOrEmpty(path = doc.FilePath)) {
                return PathUtils.NormalizePath(path);
            }

            return null;
        }

        private Uri GetOrCreateDocumentUri() {
            if (Buffer.Properties.TryGetProperty(_testDocumentUri, out Uri uri)) {
                return uri;
            }

            var path = Filename;
            if (!string.IsNullOrEmpty(path)) {
                try {
                    if (Path.IsPathRooted(path)) {
                        return new Uri(path);
                    }
                    // Make an opaque identifier for this file
                    var ub = new UriBuilder("python://", Guid.NewGuid().ToString("N")) { Path = path };
                    return ub.Uri;
                } catch (ArgumentException ex) {
                    Debug.Fail("{0} is not a valid path.{1}{2}".FormatInvariant(path, Environment.NewLine, ex.ToUnhandledExceptionMessage(GetType())));
                } catch (UriFormatException ex) {
                    Debug.Fail("{0} is not a valid URI.{1}{2}".FormatInvariant(path, Environment.NewLine, ex.ToUnhandledExceptionMessage(GetType())));
                }
                return null;
            }

            var replEval = Buffer.GetInteractiveWindow()?.Evaluator as IPythonInteractiveIntellisense;
            return replEval?.NextDocumentUri();
        }


        public ITextBuffer Buffer { get; }
        public ITextDocument Document { get; }
        public ITextSnapshot CurrentSnapshot => Buffer.CurrentSnapshot;
        public IContentType ContentType => Buffer.ContentType;
        public string Filename => _filename.Value;
        public Uri DocumentUri => _documentUri.Value;

        public IServiceProvider Site { get; }

        public PythonLanguageVersion LanguageVersion {
            get {
                var client = PythonLanguageClient.FindLanguageClient(Buffer);
                return client?.Factory?.Configuration.Version.ToLanguageVersion() ?? _defaultLanguageVersion;
            }
        }

        #region Events

        // LSC
        //private void OnNewAnalysisEntry(AnalysisEntry entry) {
        //    Trace("OnNewAnalysisEntry", entry?.Analyzer);
        //    ClearTokenCache();
        //    InvokeSinks(new PythonTextBufferInfoEventArgs(PythonTextBufferInfoEvents.NewAnalysisEntry, entry));
        //}

        //private void AnalysisEntry_ParseComplete(object sender, EventArgs e) {
        //    Trace("ParseComplete");
        //    InvokeSinks(new PythonTextBufferInfoEventArgs(PythonTextBufferInfoEvents.NewParseTree, (AnalysisEntry)sender));
        //}

        //private void AnalysisEntry_AnalysisComplete(object sender, EventArgs e) {
        //    Trace("AnalysisComplete");
        //    InvokeSinks(new PythonTextBufferInfoEventArgs(PythonTextBufferInfoEvents.NewAnalysis, (AnalysisEntry)sender));
        //}

        private void Buffer_TextContentChanged(object sender, TextContentChangedEventArgs e) {
            Trace("TextContentChanged");
            if (!_hasChangedOnBackground) {
                UpdateTokenCache(e);
            }
            InvokeSinks(new PythonTextBufferInfoNestedEventArgs(PythonTextBufferInfoEvents.TextContentChanged, e));
            if (!_hasChangedOnBackground) {
                InvokeSinks(new PythonTextBufferInfoNestedEventArgs(PythonTextBufferInfoEvents.TextContentChangedOnBackgroundThread, e));
            }
        }

        private void Buffer_TextContentChangedLowPriority(object sender, TextContentChangedEventArgs e) {
            Trace("TextContentChangedLowPriority");
            InvokeSinks(new PythonTextBufferInfoNestedEventArgs(PythonTextBufferInfoEvents.TextContentChangedLowPriority, e));
        }

        private void Buffer_ContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
            Trace("ContentTypeChanged", e.BeforeContentType, e.AfterContentType);
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
            Trace("EncodingChanged", e.OldEncoding, e.NewEncoding);
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
            TraceWithStack("AddSink", key, sink.GetType().FullName);
        }

        public T GetOrCreateSink<T>(object key, Func<PythonTextBufferInfo, T> creator) where T : class, IPythonTextBufferInfoEventSink {
            IPythonTextBufferInfoEventSink sink;
            if (_eventSinks.TryGetValue(key, out sink)) {
                Trace("GetOrCreateSink", typeof(T).FullName, "get", sink.GetType().FullName);
                return sink as T;
            }
            sink = creator(this);
            TraceWithStack("GetOrCreateSink", typeof(T).FullName, "create", sink.GetType().FullName);
            if (!_eventSinks.TryAdd(key, sink)) {
                sink = _eventSinks[key];
                Trace("GetOrCreateSink", typeof(T).FullName, "lost create race", sink.GetType().FullName);
            }
            return sink as T;
        }

        public IPythonTextBufferInfoEventSink TryGetSink(object key) {
            IPythonTextBufferInfoEventSink sink;
            return _eventSinks.TryGetValue(key, out sink) ? sink : null;
        }

        public bool RemoveSink(object key) {
            Trace("RemoveSink", key, _eventSinks.ContainsKey(key));
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

        #region Analysis Info

        // LSC
        //public AnalysisEntry AnalysisEntry {
        //    get {
        //        var entry = Volatile.Read(ref _analysisEntry);
        //        if (entry != null && (entry.Analyzer == null || !entry.Analyzer.IsActive)) {
        //            // Analyzer has closed, so clear it out from our info.
        //            TraceWithStack("AnalyzerExpired", entry.Analyzer);
        //            var previous = TrySetAnalysisEntry(null, entry);
        //            if (previous != entry) {
        //                // The entry has already been updated, so return the new one
        //                return previous;
        //            }
        //            InvokeSinks(new PythonTextBufferInfoEventArgs(PythonTextBufferInfoEvents.AnalyzerExpired));
        //            return null;
        //        }
        //        return entry;
        //    }
        //}

        ///// <summary>
        ///// Returns the current analysis entry if it is not null. Otherwise
        ///// waits for a non-null entry to be set and returns it. If cancelled,
        ///// return null.
        ///// </summary>
        //public Task<AnalysisEntry> GetAnalysisEntryAsync(CancellationToken cancellationToken) {
        //    var entry = AnalysisEntry;
        //    if (entry != null) {
        //        Trace("GetAnalysisEntryAsync", "completed synchronously");
        //        return Task.FromResult(entry);
        //    }
        //    TraceWithStack("GetAnalysisEntryAsync", "waiting");
        //    var tcs = Volatile.Read(ref _waitingForEntry);
        //    if (tcs != null) {
        //        return tcs.Task;
        //    }
        //    tcs = new TaskCompletionSource<AnalysisEntry>();
        //    tcs = Interlocked.CompareExchange(ref _waitingForEntry, tcs, null) ?? tcs;
        //    entry = AnalysisEntry;
        //    if (entry != null) {
        //        tcs.TrySetResult(entry);
        //    } else if (cancellationToken.CanBeCanceled) {
        //        cancellationToken.Register(() => tcs.TrySetResult(null));
        //    }
        //    return tcs.Task;
        //}

        ///// <summary>
        ///// Changes the analysis entry to <paramref name="entry"/> if the current
        ///// entry matches <paramref name="ifCurrent"/>. Returns the current analysis
        ///// entry, regardless of whether it changed or not.
        ///// </summary>
        //public AnalysisEntry TrySetAnalysisEntry(AnalysisEntry entry, AnalysisEntry ifCurrent) {
        //    var previous = Interlocked.CompareExchange(ref _analysisEntry, entry, ifCurrent);

        //    if (previous != ifCurrent) {
        //        TraceWithStack("FailedToSetAnalysisEntry", previous?.Analyzer, entry?.Analyzer, ifCurrent?.Analyzer);
        //        return previous;
        //    }

        //    if (previous != null) {
        //        previous.AnalysisComplete -= AnalysisEntry_AnalysisComplete;
        //        previous.ParseComplete -= AnalysisEntry_ParseComplete;
        //        previous.Analyzer.BufferDetached(previous, Buffer);
        //    }
        //    if (entry != null) {
        //        entry.AnalysisComplete += AnalysisEntry_AnalysisComplete;
        //        entry.ParseComplete += AnalysisEntry_ParseComplete;

        //        Interlocked.Exchange(ref _waitingForEntry, null)?.TrySetResult(entry);
        //    }

        //    TraceWithStack("TrySetAnalysisEntry", entry?.Analyzer, ifCurrent?.Analyzer);
        //    OnNewAnalysisEntry(entry);

        //    return entry;
        //}

        //public void ClearAnalysisEntry() {
        //    var previous = Interlocked.Exchange(ref _analysisEntry, null);
        //    TraceWithStack("ClearAnalysisEntry", previous?.Analyzer);

        //    if (previous != null) {
        //        previous.AnalysisComplete -= AnalysisEntry_AnalysisComplete;
        //        previous.ParseComplete -= AnalysisEntry_ParseComplete;
        //        previous.Analyzer.BufferDetached(previous, Buffer);

        //        OnNewAnalysisEntry(null);
        //    }
        //}

        //private readonly SortedDictionary<int, ITextSnapshot> _expectParse = new SortedDictionary<int, ITextSnapshot>();
        //private readonly SortedDictionary<int, ITextSnapshot> _expectAnalysis = new SortedDictionary<int, ITextSnapshot>();
        //private ITextSnapshot _lastSentSnapshot;

        //public ITextSnapshot LastAnalysisSnapshot { get; private set; }

        //public ITextSnapshot LastSentSnapshot {
        //    get {
        //        lock (_lock) {
        //            return _lastSentSnapshot;
        //        }
        //    }
        //}

        //public void ClearSentSnapshot() {
        //    lock (_lock) {
        //        _lastSentSnapshot = null;
        //        _expectAnalysis.Clear();
        //        _expectParse.Clear();
        //    }
        //}

        //public ITextSnapshot AddSentSnapshot(ITextSnapshot sent) {
        //    lock (_lock) {
        //        var prevSent = _lastSentSnapshot;
        //        Trace("AddSentSnapshot", prevSent?.Version?.VersionNumber, sent?.Version?.VersionNumber);
        //        if (prevSent != null && prevSent.Version.VersionNumber > sent.Version.VersionNumber) {
        //            return prevSent;
        //        }
        //        _lastSentSnapshot = sent;
        //        _expectAnalysis[sent.Version.VersionNumber] = sent;
        //        _expectParse[sent.Version.VersionNumber] = sent;
        //        return prevSent;
        //    }
        //}

        //public bool UpdateLastReceivedParse(int version) {
        //    lock (_lock) {
        //        Trace("UpdateLastReceivedParse", version, _expectParse.ContainsKey(version) ? "expected" : "unexpected");
        //        var toRemove = _expectParse.Keys.TakeWhile(k => k < version).ToArray();
        //        foreach (var i in toRemove) {
        //            Debug.WriteLine($"Skipped parse for version {i}");
        //            Trace("SkipParse", i);
        //            _expectParse.Remove(i);
        //        }
        //        return _expectParse.Remove(version);
        //    }
        //}

        //public bool UpdateLastReceivedAnalysis(int version) {
        //    lock (_lock) {
        //        Trace("UpdateLastReceivedAnalysis", version, _expectAnalysis.ContainsKey(version) ? "expected" : "unexpected");

        //        var toRemove = _expectAnalysis.Keys.TakeWhile(k => k < version).ToArray();
        //        foreach (var i in toRemove) {
        //            Debug.WriteLine($"Skipped analysis for version {i}");
        //            Trace("SkipAnalysis", i);
        //            _expectAnalysis.Remove(i);
        //        }
        //        if (_expectAnalysis.TryGetValue(version, out var snapshot)) {
        //            _expectAnalysis.Remove(version);
        //            if (snapshot.Version.VersionNumber > (LastAnalysisSnapshot?.Version.VersionNumber ?? -1)) {
        //                LastAnalysisSnapshot = snapshot;
        //                _locationTracker.UpdateBaseSnapshot(snapshot);
        //                return true;
        //            }
        //        }
        //        return false;
        //    }
        //}

        //public LocationTracker LocationTracker => _locationTracker;

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

        // LSC
        //public bool DoNotParse {
        //    get => Buffer.Properties.ContainsProperty(BufferParser.DoNotParse);
        //    set {
        //        if (value) {
        //            Buffer.Properties[BufferParser.DoNotParse] = BufferParser.DoNotParse;
        //        } else {
        //            Buffer.Properties.RemoveProperty(BufferParser.DoNotParse);
        //        }
        //    }
        //}

        //public bool ParseImmediately {
        //    get => Buffer.Properties.ContainsProperty(BufferParser.ParseImmediately);
        //    set {
        //        if (value) {
        //            Buffer.Properties[BufferParser.ParseImmediately] = BufferParser.ParseImmediately;
        //        } else {
        //            Buffer.Properties.RemoveProperty(BufferParser.ParseImmediately);
        //        }
        //    }
        //}

        #endregion

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

        #region Diagnostic Tracing

        // LSC
        //private static readonly Lazy<bool> _shouldUseTraceLog = new Lazy<bool>(GetShouldUseTraceLog);
        //private static bool GetShouldUseTraceLog() {
        //    using (var root = Win32.Registry.CurrentUser.OpenSubKey(PythonCoreConstants.LoggingRegistrySubkey, false)) {
        //        var value = root?.GetValue("BufferInfo", null);
        //        int? asInt = value as int?;
        //        if (asInt.HasValue) {
        //            if (asInt.GetValueOrDefault() == 0) {
        //                // REG_DWORD but 0 means no logging
        //                return false;
        //            }
        //        } else if (string.IsNullOrEmpty(value as string)) {
        //            // Empty string or no value means no logging
        //            return false;
        //        }
        //    }
        //    return true;
        //}

        //private AnalysisLogWriter OpenTraceLog() {
        //    if (!_shouldUseTraceLog.Value) {
        //        return null;
        //    }
        //    return new AnalysisLogWriter(
        //        PathUtils.GetAvailableFilename(Path.GetTempPath(), "PythonTools_Buffer_{0}_{1:yyyyMMddHHmmss}".FormatInvariant(PathUtils.GetFileOrDirectoryName(_filename.Value), DateTime.Now), ".log"),
        //        false,
        //        false,
        //        cacheSize: 1
        //    );
        //}

        private void Trace(string eventName, params object[] args) {
            //_traceLog?.Log(eventName, args);
        }

        private void TraceWithStack(string eventName, params object[] args) {
            //if (_traceLog != null) {
            //    var stack = new StackTrace(1, true).ToString().Replace("\r\n", "").Replace("\n", "");
            //    _traceLog.Log(eventName, args.Concat(Enumerable.Repeat(stack, 1)).ToArray());
            //    _traceLog.Flush();
            //}
        }

        #endregion
    }

    static class PythonTextBufferInfoExtensions {
        public static PythonTextBufferInfo TryGetInfo(this ITextBuffer buffer) => PythonTextBufferInfo.TryGetForBuffer(buffer);

        // LSC
        //public static AnalysisEntry TryGetAnalysisEntry(this ITextBuffer buffer) => PythonTextBufferInfo.TryGetForBuffer(buffer)?.AnalysisEntry;

        //public static Task<AnalysisEntry> GetAnalysisEntryAsync(this ITextBuffer buffer, PythonEditorServices services = null, CancellationToken cancellationToken = default(CancellationToken)) {
        //    var bi = services == null ? PythonTextBufferInfo.TryGetForBuffer(buffer) : services.GetBufferInfo(buffer);
        //    if (bi != null) {
        //        return bi.GetAnalysisEntryAsync(cancellationToken);
        //    }
        //    return Task.FromResult<AnalysisEntry>(null);
        //}

        //public static AnalysisEntry TryGetAnalysisEntry(this ITextView view, IServiceProvider site) {
        //    var entry = view.TextBuffer.TryGetAnalysisEntry();
        //    if (entry != null) {
        //        return entry;
        //    }

        //    var diffViewer = site.GetComponentModel().GetService<IWpfDifferenceViewerFactoryService>();
        //    var viewer = diffViewer?.TryGetViewerForTextView(view);
        //    if (viewer != null) {
        //        entry = viewer.DifferenceBuffer.RightBuffer.TryGetAnalysisEntry() ??
        //            viewer.DifferenceBuffer.LeftBuffer.TryGetAnalysisEntry();
        //        if (entry != null) {
        //            return entry;
        //        }
        //    }

        //    return null;
        //}

        //public static async Task<ProjectAnalyzer> FindAnalyzerAsync(this IServiceProvider site, ITextView view) {
        //    ProjectAnalyzer analyzer;

        //    var bi = view.TextBuffer.TryGetInfo();
        //    if (bi != null && (analyzer = await FindAnalyzerAsync(site, bi)) != null) {
        //        return analyzer;
        //    }

        //    site.MustBeCalledFromUIThread();
        //    var diffViewer = site.GetComponentModel().GetService<IWpfDifferenceViewerFactoryService>();
        //    var viewer = diffViewer?.TryGetViewerForTextView(view);
        //    if (viewer != null) {
        //        bi = viewer.DifferenceBuffer.RightBuffer.TryGetInfo();
        //        if (bi != null && (analyzer = await FindAnalyzerAsync(site, bi)) != null) {
        //            return analyzer;
        //        }
        //        bi = viewer.DifferenceBuffer.LeftBuffer.TryGetInfo();
        //        if (bi != null && (analyzer = await FindAnalyzerAsync(site, bi)) != null) {
        //            return analyzer;
        //        }
        //    }

        //    return null;
        //}

        //public static async Task<ProjectAnalyzer> FindAnalyzerAsync(this IServiceProvider site, PythonTextBufferInfo buffer) {
        //    ProjectAnalyzer analyzer;

        //    // If we have an analyzer in Properties, we will use that
        //    // NOTE: This should only be used for tests.
        //    if (buffer.Buffer.Properties.TryGetProperty(VsProjectAnalyzer._testAnalyzer, out analyzer)) {
        //        return analyzer;
        //    }

        //    // If we have a REPL evaluator we'll use its analyzer
        //    if (buffer.Buffer.GetInteractiveWindow()?.Evaluator is IPythonInteractiveIntellisense evaluator) {
        //        return await evaluator.GetAnalyzerAsync();
        //    }

        //    // If the file is associated with a project, use its analyzer
        //    analyzer = await site.GetUIThread().InvokeTask(() => {
        //        var p = site.GetProjectFromFile(buffer.Filename);
        //        if (p != null) {
        //            return p.GetAnalyzerAsync();
        //        }
        //        return Task.FromResult<VsProjectAnalyzer>(null);
        //    });
        //    if (analyzer != null) {
        //        return analyzer;
        //    }

        //    var workspaceAnalysis = site.GetComponentModel().GetService<WorkspaceAnalysis>();
        //    analyzer = await workspaceAnalysis.GetAnalyzerAsync();
        //    if (analyzer != null) {
        //        return analyzer;
        //    }

        //    return null;
        //}

        //public static async Task<ProjectAnalyzer> FindAnalyzerAsync(this IServiceProvider site, string filename) {
        //    return (await FindAllAnalyzersForFile(site, filename, true)).FirstOrDefault() ??
        //        (await site.GetPythonToolsService().GetSharedAnalyzerAsync());
        //}

        //public static Task<IReadOnlyList<ProjectAnalyzer>> FindAllAnalyzersForFile(this IServiceProvider site, string filename) {
        //    return FindAllAnalyzersForFile(site, filename, false);
        //}

        //private static async Task<IReadOnlyList<ProjectAnalyzer>> FindAllAnalyzersForFile(this IServiceProvider site, string filename, bool firstOnly) {
        //    if (string.IsNullOrEmpty(filename)) {
        //        throw new ArgumentNullException(nameof(filename));
        //    }

        //    var found = new HashSet<ProjectAnalyzer>();

        //    // If we have an open document, return that
        //    var buffer = site.GetTextBufferFromOpenFile(filename)?.TryGetInfo();
        //    if (buffer != null) {
        //        var analyzer = await site.FindAnalyzerAsync(buffer);
        //        if (analyzer != null) {
        //            found.Add(analyzer);
        //            if (firstOnly) {
        //                return found.ToArray();
        //            }
        //        }
        //    }

        //    var workspaceAnalysis = site.GetComponentModel().GetService<WorkspaceAnalysis>();
        //    var workspaceAnalyzer = workspaceAnalysis.TryGetWorkspaceAnalyzer();
        //    if (workspaceAnalyzer != null) {
        //        found.Add(workspaceAnalyzer);
        //        if (firstOnly) {
        //            return found.ToArray();
        //        }
        //    }

        //    // Yield all loaded projects containing the file
        //    var sln = (IVsSolution)site.GetService(typeof(SVsSolution));
        //    if (sln != null) {
        //        if (Path.IsPathRooted(filename)) {
        //            foreach (var project in sln.EnumerateLoadedPythonProjects()) {
        //                if (project.FindNodeByFullPath(filename) != null) {
        //                    var analyzer = project.TryGetAnalyzer();
        //                    if (analyzer != null) {
        //                        found.Add(analyzer);
        //                        if (firstOnly) {
        //                            return found.ToArray();
        //                        }
        //                    }
        //                }
        //            }
        //        } else {
        //            var withSlash = "\\" + filename;
        //            foreach (var project in sln.EnumerateLoadedPythonProjects()) {
        //                if (project.AllVisibleDescendants.Any(n => n.Url.Equals(filename, StringComparison.OrdinalIgnoreCase) ||
        //                    n.Url.EndsWithOrdinal(withSlash, ignoreCase: true))) {
        //                    var analyzer = project.TryGetAnalyzer();
        //                    if (analyzer != null) {
        //                        found.Add(analyzer);
        //                        if (firstOnly) {
        //                            return found.ToArray();
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    return found.ToArray();
        //}
    }
}
