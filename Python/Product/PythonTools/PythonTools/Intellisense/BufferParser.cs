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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    sealed class BufferParser : IPythonTextBufferInfoEventSink, IDisposable {
        private readonly Timer _timer;
        internal readonly PythonEditorServices _services;
        private readonly VsProjectAnalyzer _analyzer;

        private PythonTextBufferInfoWithRefCount[] _buffers;
        private bool _parsing, _requeue, _textChange, _parseImmediately;

        /// <summary>
        /// Maps between buffer ID and buffer info.
        /// </summary>
        private Dictionary<int, PythonTextBufferInfo> _bufferIdMapping = new Dictionary<int, PythonTextBufferInfo>();

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

        public string FilePath { get; }
        public bool IsTemporaryFile { get; set; }
        public bool SuppressErrorList { get; set; }

        public PythonTextBufferInfo GetBuffer(ITextBuffer buffer) {
            return buffer == null ? null : _services.GetBufferInfo(buffer);
        }

        public PythonTextBufferInfo GetBuffer(int bufferId) {
            lock (this) {
                PythonTextBufferInfo res;
                _bufferIdMapping.TryGetValue(bufferId, out res);
                return res;
            }
        }

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
                var existing = _buffers.FirstOrDefault(b => b.Buffer == bi);
                if (existing != null) {
                    existing.AddRef();
                    return;
                }

                _buffers = _buffers.Concat(Enumerable.Repeat(new PythonTextBufferInfoWithRefCount(bi), 1)).ToArray();
                newId = _buffers.Length - 1;

                if (!bi.SetAnalysisBufferId(newId)) {
                    // Raced, and now the buffer belongs somewhere else.
                    Debug.Fail("Race condition adding the buffer to a parser");
                    _buffers[newId] = null;
                    return;
                }
                _bufferIdMapping[newId] = bi;
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
                _bufferIdMapping.Clear();
                foreach (var bi in _buffers) {
                    bi.Buffer.SetAnalysisBufferId(-1);
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
                        _buffers = _buffers.Where(b => b != existing).ToArray();

                        bi.RemoveSink(this);

                        VsProjectAnalyzer.DisconnectErrorList(bi);
                        _bufferIdMapping.Remove(bi.AnalysisBufferId);
                        bi.SetAnalysisBufferId(-1);

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
            return ParseBuffersAsync(_services, _analyzer, snapshots);
        }

        private static IEnumerable<ITextVersion> GetVersions(ITextVersion from, ITextVersion to) {
            for (var v = from; v != null && v != to; v = v.Next) {
                yield return v;
            }
        }

        private static AP.FileUpdate GetUpdateForSnapshot(PythonTextBufferInfo buffer, ITextSnapshot snapshot) {
            if (buffer.DoNotParse || snapshot.IsReplBufferWithCommand() || buffer.AnalysisBufferId < 0) {
                return null;
            }

            var lastSent = buffer.AddSentSnapshot(snapshot);
            if (lastSent == snapshot) {
                // this snapshot is up to date...
                return null;
            }

            // Update last sent snapshot and the analysis cookie to our
            // current snapshot.
            var entry = buffer.AnalysisEntry;
            if (entry != null) {
                entry.AnalysisCookie = new SnapshotCookie(snapshot);
            }

            if (lastSent == null || lastSent.TextBuffer != buffer.Buffer) {
                // First time parsing from a live buffer, send the entire
                // file and set our initial snapshot.  We'll roll forward
                // to new snapshots when we receive the errors event.  This
                // just makes sure that the content is in sync.
                return new AP.FileUpdate {
                    content = snapshot.GetText(),
                    version = snapshot.Version.VersionNumber,
                    bufferId = buffer.AnalysisBufferId,
                    kind = AP.FileUpdateKind.reset
                };
            }

            var versions = GetVersions(lastSent.Version, snapshot.Version).Select(v => new AP.VersionChanges {
                changes = GetChanges(buffer, v)
            }).ToArray();

            return new AP.FileUpdate {
                versions = versions,
                version = snapshot.Version.VersionNumber,
                bufferId = buffer.AnalysisBufferId,
                kind = AP.FileUpdateKind.changes
            };
        }

        [Conditional("DEBUG")]
        private static void ValidateBufferContents(IEnumerable<ITextSnapshot> snapshots, AP.FileUpdateResponse response) {
#if DEBUG
            if (response.newCode == null) {
                return;
            }

            foreach (var snapshot in snapshots) {
                var bi = PythonTextBufferInfo.TryGetForBuffer(snapshot.TextBuffer);
                if (bi == null) {
                    continue;
                }

                string newCode;
                if (!response.newCode.TryGetValue(bi.AnalysisBufferId, out newCode)) {
                    continue;
                }

                if (newCode.TrimEnd() != snapshot.GetText().TrimEnd()) {
                    Console.Error.WriteLine($"New Code: [{newCode}]");
                    Console.Error.WriteLine($"Snapshot: [{snapshot.GetText()}]");
                    Debug.Fail("Buffer content mismatch");
                }
            }
#endif
        }

        internal static async Task ParseBuffersAsync(
            PythonEditorServices services,
            VsProjectAnalyzer analyzer,
            IEnumerable<ITextSnapshot> snapshots
        ) {
            var tasks = new List<Tuple<ITextSnapshot[], Task<AP.FileUpdateResponse>>>();

            foreach (var snapshotGroup in snapshots.GroupBy(s => PythonTextBufferInfo.TryGetForBuffer(s.TextBuffer))) {
                var entry = snapshotGroup.Key?.AnalysisEntry;
                if (entry == null) {
                    continue;
                }

                var updates = snapshotGroup.Select(s => GetUpdateForSnapshot(snapshotGroup.Key, s)).Where(u => u != null).ToArray();
                if (!updates.Any()) {
                    continue;
                }

                analyzer._analysisComplete = false;
                Interlocked.Increment(ref analyzer._parsePending);

                tasks.Add(Tuple.Create(snapshotGroup.ToArray(), analyzer.SendRequestAsync(
                    new AP.FileUpdateRequest {
                        fileId = entry.FileId,
                        updates = updates
                    }
                )));
            }

            foreach (var task in tasks) {
                var res = await task.Item2;

                if (res != null) {
                    Debug.Assert(res.failed != true);
                    analyzer.OnAnalysisStarted();
                    ValidateBufferContents(task.Item1, res);
                } else {
                    Interlocked.Decrement(ref analyzer._parsePending);
                }
            }
        }

        private static bool CanMerge(AP.ChangeInfo prevChange, ITextChange change, LocationTracker tracker, int version) {
            if (prevChange == null || tracker == null || change == null) {
                return false;
            }
            if (string.IsNullOrEmpty(prevChange.newText)) {
                return false;
            }

            if (change.OldLength != 0 || prevChange.startLine != prevChange.endLine || prevChange.startColumn != prevChange.endColumn) {
                return false;
            }

            var prevEnd = new SourceLocation(prevChange.startLine, prevChange.startColumn).AddColumns(prevChange.newText?.Length ?? 0);
            if (tracker.GetIndex(prevEnd, version) != change.OldPosition) {
                return false;
            }

            return true;
        }

        private static AP.ChangeInfo[] GetChanges(PythonTextBufferInfo buffer, ITextVersion curVersion) {
            Debug.WriteLine("Changes for version {0}", curVersion.VersionNumber);
            var changes = new List<AP.ChangeInfo>();
            if (curVersion.Changes != null) {
                AP.ChangeInfo prev = null;
                foreach (var change in curVersion.Changes) {
                    Debug.WriteLine("Changes for version {0} {1} {2}", change.OldPosition, change.OldLength, change.NewText);

                    if (CanMerge(prev, change, buffer.LocationTracker, curVersion.VersionNumber)) {
                        // we can merge the two changes together
                        prev.newText += change.NewText;
                        continue;
                    }

                    var oldPos = buffer.LocationTracker.GetSourceLocation(change.OldPosition, curVersion.VersionNumber);
                    var oldEnd = buffer.LocationTracker.GetSourceLocation(change.OldEnd, curVersion.VersionNumber);
                    prev = new AP.ChangeInfo {
                        startLine = oldPos.Line,
                        startColumn = oldPos.Column,
                        endLine = oldEnd.Line,
                        endColumn = oldEnd.Column,
                        newText = change.NewText
                    };
                    changes.Add(prev);
                }
            }
            return changes.ToArray();
        }

        internal void Requeue() {
            RequeueWorker();
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
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
            ClearBuffers();
            _timer.Dispose();
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
                            _timer.Change(Timeout.Infinite, Timeout.Infinite);
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
                            _timer.Change(ReparseDelay, Timeout.Infinite);
                        }
                    }
                    break;

                case PythonTextBufferInfoEvents.DocumentEncodingChanged:
                    lock (this) {
                        if (_parsing) {
                            // we are currently parsing, just reque when we complete
                            _requeue = true;
                            _timer.Change(Timeout.Infinite, Timeout.Infinite);
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
