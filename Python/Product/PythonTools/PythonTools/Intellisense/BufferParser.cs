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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    sealed class BufferParser : IPythonTextBufferInfoEventSink, IDisposable {
        private readonly Timer _timer;
        internal readonly PythonEditorServices _services;
        private readonly VsProjectAnalyzer _analyzer;

        private PythonTextBufferInfoWithRefCount[] _buffers;
        private bool _parsing, _requeue, _textChange, _parseImmediately;

        private const int ReparseDelay = 1000;      // delay in MS before we re-parse a buffer w/ non-line changes.

        public static readonly object DoNotParse = new object();
        public static readonly object ParseImmediately = new object();

        public BufferParser(PythonEditorServices services, VsProjectAnalyzer analyzer, string filePath) {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            FilePath = filePath;
            _buffers = Array.Empty<PythonTextBufferInfoWithRefCount>();
            _timer = new Timer(ReparseTimer, null, Timeout.Infinite, Timeout.Infinite);
        }

        public bool IsDisposed { get; private set; }

        public string FilePath { get; }
        public bool IsTemporaryFile { get; set; }
        public bool SuppressErrorList { get; set; }

        public PythonTextBufferInfo GetBuffer(ITextBuffer buffer) {
            return buffer == null ? null : _services.GetBufferInfo(buffer);
        }

        // UNDONE: This is a temporary workaround while we migrate
        // from multiple buffers in a single entry to chained entries
        public PythonTextBufferInfo DefaultBufferInfo { get; private set; }
        public ITextBuffer[] AllBuffers => _buffers.Select(x => x.Buffer.Buffer).ToArray();
        public ITextBuffer[] Buffers => _buffers.Where(x => !x.Buffer.DoNotParse).Select(x => x.Buffer.Buffer).ToArray();

        internal void AddBuffer(ITextBuffer textBuffer) {
            int newId;
            var bi = _services.GetBufferInfo(textBuffer);

            var entry = bi.AnalysisEntry;

            if (entry == null) {
                throw new InvalidOperationException("buffer must have a project entry before parsing");
            }

            lock (this) {
                if (DefaultBufferInfo == null) {
                    DefaultBufferInfo = bi;
                }

                var existing = _buffers.FirstOrDefault(b => b.Buffer == bi);
                if (existing != null) {
                    existing.AddRef();
                    return;
                }

                _buffers = _buffers.Concat(Enumerable.Repeat(new PythonTextBufferInfoWithRefCount(bi), 1)).ToArray();
                newId = _buffers.Length - 1;
            }

            if (bi.ParseImmediately) {
                // Any buffer requesting immediate parsing enables it for
                // the whole file.
                _parseImmediately = true;
            }

            bi.AddSink(this, this);
            VsProjectAnalyzer.ConnectErrorList(bi);
        }

        internal void ClearBuffers() {
            lock (this) {
                DefaultBufferInfo = null;
                foreach (var bi in _buffers) {
                    bi.Buffer.ClearAnalysisEntry();
                    bi.Buffer.RemoveSink(this);
                    VsProjectAnalyzer.DisconnectErrorList(bi.Buffer);
                }
                _buffers = Array.Empty<PythonTextBufferInfoWithRefCount>();
            }
        }

        internal int RemoveBuffer(ITextBuffer subjectBuffer) {
            int result;
            var bi = PythonTextBufferInfo.TryGetForBuffer(subjectBuffer);

            lock (this) {
                if (bi != null) {
                    var existing = _buffers.FirstOrDefault(b => b.Buffer == bi);
                    if (existing != null && existing.Release()) {
                        if (DefaultBufferInfo == bi) {
                            DefaultBufferInfo = null;
                        }

                        _buffers = _buffers.Where(b => b != existing).ToArray();

                        bi.ClearAnalysisEntry();
                        bi.RemoveSink(this);
                        VsProjectAnalyzer.DisconnectErrorList(bi);

                        bi.Buffer.Properties.RemoveProperty(typeof(PythonTextBufferInfo));
                    }
                }
                result = _buffers.Length;
            }

            return result;
        }

        internal void ReparseTimer(object unused) {
            RequeueWorker();
        }

        internal void ReparseWorker(object unused) {
            ITextSnapshot[] snapshots;
            lock (this) {
                if (_parsing) {
                    return;
                }

                _parsing = true;
                snapshots = _buffers
                    .Where(b => !b.Buffer.DoNotParse)
                    .Select(b => b.Buffer.CurrentSnapshot).ToArray();
            }

            ParseBuffers(snapshots).WaitAndHandleAllExceptions(_services.Site);

            lock (this) {
                _parsing = false;
                if (_requeue) {
                    RequeueWorker();
                }
                _requeue = false;
            }
        }

        public Task EnsureCodeSyncedAsync(ITextBuffer buffer) => EnsureCodeSyncedAsync(buffer, false);

        public async Task EnsureCodeSyncedAsync(ITextBuffer buffer, bool force) {
            var lastSent = force ? null : _services.GetBufferInfo(buffer).LastSentSnapshot;
            var snapshot = buffer.CurrentSnapshot;
            if (force || lastSent != buffer.CurrentSnapshot) {
                await ParseBuffers(Enumerable.Repeat(snapshot, 1)).ConfigureAwait(false);
            }
        }

        private Task ParseBuffers(IEnumerable<ITextSnapshot> snapshots) {
            return ParseBuffersAsync(_services, _analyzer, snapshots, true);
        }

        private static IEnumerable<ITextVersion> GetVersions(ITextVersion from, ITextVersion to) {
            for (var v = from; v != null && v != to; v = v.Next) {
                yield return v;
            }
        }

        internal static IEnumerable<AP.FileUpdate> GetUpdatesForSnapshot(PythonTextBufferInfo buffer, ITextSnapshot snapshot) {
            if (buffer.DoNotParse || snapshot.IsReplBufferWithCommand()) {
                yield break;
            }

            var lastSent = buffer.AddSentSnapshot(snapshot);

            // Update last sent snapshot and the analysis cookie to our
            // current snapshot.
            var entry = buffer.AnalysisEntry;
            if (entry != null) {
                entry.AnalysisCookie = new SnapshotCookie(snapshot);
            }

            if (lastSent == null || lastSent == snapshot || lastSent.TextBuffer != buffer.Buffer) {
                // First time parsing from a live buffer, send the entire
                // file and set our initial snapshot.  We'll roll forward
                // to new snapshots when we receive the errors event.  This
                // just makes sure that the content is in sync.
                yield return new AP.FileUpdate {
                    content = snapshot.GetText(),
                    version = snapshot.Version.VersionNumber,
                    kind = AP.FileUpdateKind.reset
                };
                yield break;
            }

            foreach (var v in GetVersions(lastSent.Version, snapshot.Version)) {
                yield return new AP.FileUpdate {
                    version = v.VersionNumber + 1,
                    changes = GetChanges(buffer, v).Reverse().ToArray(),
                    kind = AP.FileUpdateKind.changes
                };
            }
        }

        internal static async Task ParseBuffersAsync(
            PythonEditorServices services,
            VsProjectAnalyzer analyzer,
            IEnumerable<ITextSnapshot> snapshots,
            bool retryOnFailure
        ) {
            var tasks = new List<Tuple<ITextSnapshot[], Task<AP.FileUpdateResponse>>>();

            foreach (var snapshotGroup in snapshots.GroupBy(s => PythonTextBufferInfo.TryGetForBuffer(s.TextBuffer))) {
                var entry = snapshotGroup.Key?.AnalysisEntry;
                if (entry == null) {
                    continue;
                }

                var updates = snapshotGroup.SelectMany(s => GetUpdatesForSnapshot(snapshotGroup.Key, s)).Where(u => u != null).ToArray();
                if (!updates.Any()) {
                    continue;
                }

                analyzer._analysisComplete = false;
                Interlocked.Increment(ref analyzer._parsePending);

                tasks.Add(Tuple.Create(snapshotGroup.ToArray(), analyzer.SendRequestAsync(
                    new AP.FileUpdateRequest {
                        documentUri = entry.DocumentUri,
                        updates = updates
                    }
                )));
            }

            var needRetry = new List<ITextSnapshot>();
            foreach (var task in tasks) {
                var res = await task.Item2;

                if (res?.failed ?? false) {
                    Interlocked.Decrement(ref analyzer._parsePending);
                    if (res != null) {
                        needRetry.AddRange(task.Item1);
                    }
                } else {
                    analyzer.OnAnalysisStarted();
                }
            }

            if (retryOnFailure && needRetry.Any()) {
                foreach (var bi in needRetry.Select(s => PythonTextBufferInfo.TryGetForBuffer(s.TextBuffer))) {
                    bi.ClearSentSnapshot();
                }

                await ParseBuffersAsync(services, analyzer, needRetry, false);
            }
        }

        internal static AP.ChangeInfo[] GetChanges(PythonTextBufferInfo buffer, ITextVersion curVersion) {
            var changes = new List<AP.ChangeInfo>();
            if (curVersion.Changes != null) {
                foreach (var change in curVersion.Changes) {
                    var oldPos = buffer.LocationTracker.GetSourceLocation(change.OldPosition, curVersion.VersionNumber);
                    var oldEnd = buffer.LocationTracker.GetSourceLocation(change.OldEnd, curVersion.VersionNumber);
                    changes.Add(new AP.ChangeInfo {
                        startLine = oldPos.Line,
                        startColumn = oldPos.Column,
                        endLine = oldEnd.Line,
                        endColumn = oldEnd.Column,
                        newText = change.NewText,
                    });
                }
            }

#if DEBUG
            Debug.WriteLine("Getting changes for version {0}", curVersion.VersionNumber);
            foreach (var c in changes) {
                Debug.WriteLine($" - ({c.startLine}, {c.startColumn})-({c.endLine}, {c.endColumn}): \"{c.newText}\"");
            }
#endif
            return changes.ToArray();
        }

        internal void Requeue() {
            RequeueWorker();
            ReparseNever();
        }

        private void ReparseNever() {
            ReparseSoon(Timeout.Infinite);
        }

        private void ReparseSoon(int delay = ReparseDelay) {
            try {
                _timer.Change(delay, Timeout.Infinite);
            } catch (ObjectDisposedException) {
            }
        }

        private void RequeueWorker() {
            ThreadPool.QueueUserWorkItem(ReparseWorker);
        }

        /// <summary>
        /// Used to track if we have line + text changes, just text changes, or just line changes.
        /// 
        /// If we have text changes followed by a line change we want to immediately reparse.
        /// If we have just text changes we want to reparse in ReparseDelay ms from the last change.
        /// If we have just repeated line changes (e.g. someone's holding down enter) we don't want to
        ///     repeatedly reparse, instead we want to wait ReparseDelay ms.
        /// </summary>
        private bool LineAndTextChanges(TextContentChangedEventArgs e) {
            if (_textChange) {
                _textChange = false;
                return e.Changes.IncludesLineChanges;
            }

            bool mixedChanges = false;
            if (e.Changes.IncludesLineChanges) {
                mixedChanges = IncludesTextChanges(e);
            }

            return mixedChanges;
        }

        /// <summary>
        /// Returns true if the change incldues text changes (not just line changes).
        /// </summary>
        private static bool IncludesTextChanges(TextContentChangedEventArgs e) {
            bool mixedChanges = false;
            foreach (var change in e.Changes) {
                if (!string.IsNullOrEmpty(change.OldText) || change.NewText != Environment.NewLine) {
                    mixedChanges = true;
                    break;
                }
            }
            return mixedChanges;
        }

        public void Dispose() {
            if (!IsDisposed) {
                IsDisposed = true;
                ClearBuffers();
                _timer.Dispose();
            }
        }

        Task IPythonTextBufferInfoEventSink.PythonTextBufferEventAsync(PythonTextBufferInfo sender, PythonTextBufferInfoEventArgs e) {
            switch (e.Event) {
                case PythonTextBufferInfoEvents.TextContentChangedLowPriority:
                    lock (this) {
                        // only immediately re-parse on line changes after we've seen a text change.
                        var ne = (e as PythonTextBufferInfoNestedEventArgs)?.NestedEventArgs as TextContentChangedEventArgs;

                        if (_parsing) {
                            // we are currently parsing, just reque when we complete
                            _requeue = true;
                            ReparseNever();
                        } else if (_parseImmediately) {
                            // we are a test buffer, we should requeue immediately
                            Requeue();
                        } else if (ne == null) {
                            // failed to get correct type for this event
                            Debug.Fail("Failed to get correct event type");
                        } else if (LineAndTextChanges(ne)) {
                            // user pressed enter, we should requeue immediately
                            Requeue();
                        } else {
                            // parse if the user doesn't do anything for a while.
                            _textChange = IncludesTextChanges(ne);
                            ReparseSoon();
                        }
                    }
                    break;

                case PythonTextBufferInfoEvents.DocumentEncodingChanged:
                    lock (this) {
                        if (_parsing) {
                            // we are currently parsing, just reque when we complete
                            _requeue = true;
                            ReparseNever();
                        } else {
                            Requeue();
                        }
                    }
                    break;
            }
            return Task.CompletedTask;
        }

        private class PythonTextBufferInfoWithRefCount {
            public readonly PythonTextBufferInfo Buffer;
            private int _refCount;

            public PythonTextBufferInfoWithRefCount(PythonTextBufferInfo buffer) {
                Buffer = buffer;
                _refCount = 1;
            }

            public void AddRef() {
                Interlocked.Increment(ref _refCount);
            }

            public bool Release() {
                return Interlocked.Decrement(ref _refCount) == 0;
            }
        }
    }
}
