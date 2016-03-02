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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Communication;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "ownership is unclear")]
    class BufferParser {
        internal VsProjectAnalyzer _parser;
        private readonly Timer _timer;
        private IList<ITextBuffer> _buffers;
        private bool _parsing, _requeue, _textChange;
        internal ProjectFileInfo _currentProjEntry;
        private ITextDocument _document;
        public int AttachedViews;
        /// <summary>
        /// Maps from a text buffer to a buffer info... Buffer info is used for tracking our state
        /// of requests sent to/from the remote analysis process.
        /// 
        /// Buffers have unique ids so that we can send multiple buffers to the remote analyzer for
        /// REPL scenarios.
        /// </summary>
        private Dictionary<ITextBuffer, BufferInfo> _bufferInfo = new Dictionary<ITextBuffer, BufferInfo>();
        /// <summary>
        /// Maps from a buffer ID back toa buffer info.
        /// </summary>
        private Dictionary<int, BufferInfo> _bufferIdMapping = new Dictionary<int, BufferInfo>();

        private const int ReparseDelay = 1000;      // delay in MS before we re-parse a buffer w/ non-line changes.

        public BufferParser(ProjectFileInfo initialProjectEntry, VsProjectAnalyzer parser, ITextBuffer buffer) {
            _parser = parser;
            _timer = new Timer(ReparseTimer, null, Timeout.Infinite, Timeout.Infinite);
            _buffers = new[] { buffer };
            _currentProjEntry = initialProjectEntry;
            AttachedViews = 1;

            InitBuffer(buffer, 0);

            // lock not necessary for _bufferInfo, no one has access to us yet...
            ParseBuffers(new[] { buffer.CurrentSnapshot }, new[] { _bufferInfo[buffer] }).DoNotWait();

            initialProjectEntry.BufferParser = this;
        }

        class BufferInfo {
            public readonly ITextBuffer Buffer;
            public readonly int Id;
            private readonly Dictionary<int, ITextSnapshot> _parsedSnapshots = new Dictionary<int, ITextSnapshot>();
            public ITextSnapshot LastSentSnapshot, LastParsedSnapshot;

            public BufferInfo(ITextBuffer buffer, int id) {
                Buffer = buffer;
                Id = id;
            }

            public void PushSnapshot(ITextSnapshot snapshot) {
                _parsedSnapshots[snapshot.Version.VersionNumber] = snapshot;
            }

            public ITextSnapshot PopSnapshot(int version) {
                var res = _parsedSnapshots[version];
                _parsedSnapshots.Remove(version);
                return res;
            }
        }

        internal void SentSnapshot(ITextSnapshot snapshot) {
            lock (this) {
                var bufferInfo = _bufferInfo[snapshot.TextBuffer];
                
                bufferInfo.PushSnapshot(snapshot);
                bufferInfo.LastSentSnapshot = snapshot;
            }
        }

        internal ITextSnapshot SnapshotParsed(int bufferId, int version) {
            lock (this) {
                return _bufferIdMapping[bufferId].PopSnapshot(version);
            }
        }

        internal ITextSnapshot GetLastSentSnapshot(ITextBuffer buffer) {
            lock (this) {
                return _bufferInfo[buffer].LastSentSnapshot;
            }
        }

        internal void SetLastSentSnapshot(ITextSnapshot snapshot) {
            lock (this) {
                _bufferInfo[snapshot.TextBuffer].LastSentSnapshot = snapshot;
            }
        }

        internal int GetBufferId(ITextBuffer buffer) { 
            lock (this) {
                return _bufferInfo[buffer].Id;
            }
        }

        private BufferInfo GetBufferInfo(ITextBuffer buffer) {
            lock (this) {
                return _bufferInfo[buffer];
            }
        }

        internal ITextSnapshot GetLastParsedSnapshot(ITextBuffer buffer) {
            lock (this) {
                return _bufferInfo[buffer].LastParsedSnapshot;
            }
        }

        internal ITextSnapshot GetLastParsedSnapshot(int buffer) {
            lock (this) {
                return _bufferIdMapping[buffer].LastParsedSnapshot;
            }
        }

        internal void SetLastParsedSnapshot(ITextSnapshot snapshot) {
            lock (this) {
                _bufferInfo[snapshot.TextBuffer].LastParsedSnapshot = snapshot;
            }
        }

        private Severity IndentationInconsistencySeverity {
            get {
                return _parser.PyService.GeneralOptions.IndentationInconsistencySeverity;
            }
        }

        public void StopMonitoring() {
            foreach (var buffer in _buffers) {
                buffer.ChangedLowPriority -= BufferChangedLowPriority;
                buffer.Properties.RemoveProperty(typeof(BufferParser));
                if (_document != null) {
                    _document.EncodingChanged -= EncodingChanged;
                    _document = null;
                }
            }
            _timer.Dispose();
        }

        public ITextBuffer[] Buffers {
            get {
                return _buffers.Where(
                    x => !x.Properties.ContainsProperty(PythonReplEvaluator.InputBeforeReset)
                ).ToArray();
            }
        }

        internal void AddBuffer(ITextBuffer textBuffer) {
            lock (this) {
                EnsureMutableBuffers();

                _buffers.Add(textBuffer);

                InitBuffer(textBuffer, _buffers.Count - 1);

                _parser.ConnectErrorList(_currentProjEntry, textBuffer);
            }
        }

        internal void RemoveBuffer(ITextBuffer subjectBuffer) {
            lock (this) {
                EnsureMutableBuffers();

                UninitBuffer(subjectBuffer);

                _buffers.Remove(subjectBuffer);

                _parser.DisconnectErrorList(_currentProjEntry, subjectBuffer);
            }
        }

        private void UninitBuffer(ITextBuffer subjectBuffer) {
            if (_document != null) {
                _document.EncodingChanged -= EncodingChanged;
                _document = null;
            }
            subjectBuffer.Properties.RemoveProperty(typeof(ProjectFileInfo));
            subjectBuffer.Properties.RemoveProperty(typeof(BufferParser));
            subjectBuffer.ChangedLowPriority -= BufferChangedLowPriority;
        }

        private void InitBuffer(ITextBuffer buffer, int id = 0) {
            buffer.Properties.AddProperty(typeof(BufferParser), this);
            buffer.ChangedLowPriority += BufferChangedLowPriority;
            buffer.Properties.AddProperty(typeof(ProjectFileInfo), _currentProjEntry);

            lock (this) {
                var bufferInfo = new BufferInfo(buffer, id);
                _bufferInfo[buffer] = bufferInfo;
                _bufferIdMapping[id] = bufferInfo;
            }

            if (_document != null) {
                _document.EncodingChanged -= EncodingChanged;
                _document = null;
            }
            if (buffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out _document) && _document != null) {
                _document.EncodingChanged += EncodingChanged;
            }
        }

        private void EnsureMutableBuffers() {
            if (_buffers.IsReadOnly) {
                _buffers = new List<ITextBuffer>(_buffers);
            }
        }

        internal void ReparseTimer(object unused) {
            RequeueWorker();
        }

        internal void ReparseWorker(object unused) {
            ITextSnapshot[] snapshots;
            BufferInfo[] bufferInfos;
            lock (this) {
                if (_parsing) {
                    return;
                }

                _parsing = true;
                var buffers = Buffers;
                snapshots = new ITextSnapshot[buffers.Length];
                bufferInfos = new BufferInfo[buffers.Length];
                for (int i = 0; i < buffers.Length; i++) {
                    snapshots[i] = buffers[i].CurrentSnapshot;
                    bufferInfos[i] = _bufferInfo[buffers[i]];
                }
            }

            ParseBuffers(snapshots, bufferInfos).Wait();

            lock (this) {
                _parsing = false;
                if (_requeue) {
                    RequeueWorker();
                }
                _requeue = false;
            }
        }

        public async Task EnsureCodeSynced(ITextBuffer buffer) {
            var lastSent = GetLastSentSnapshot(buffer);
            if (lastSent != buffer.CurrentSnapshot) {
                await ParseBuffers(
                    new[] { buffer.CurrentSnapshot },
                    new[] { GetBufferInfo(buffer) }
                );
            }
        }

        private async Task ParseBuffers(ITextSnapshot[] snapshots, BufferInfo[] bufferInfos) {
            var indentationSeverity = _parser.PyService.GeneralOptions.IndentationInconsistencySeverity;
            ProjectFileInfo entry = _currentProjEntry;

            List<AP.FileUpdate> updates = new List<AP.FileUpdate>();
            lock (this) {
                for (int i = 0; i < snapshots.Length; i++) {
                    var snapshot = snapshots[i];
                    var bufferInfo = bufferInfos[i];

                    if (snapshot.TextBuffer.Properties.ContainsProperty(PythonReplEvaluator.InputBeforeReset) ||
                        snapshot.IsReplBufferWithCommand()) {
                        continue;
                    }

                    var lastSent = GetLastSentSnapshot(bufferInfo.Buffer);
                    if (lastSent == null || lastSent.TextBuffer != snapshot.TextBuffer) {
                        // First time parsing from a live buffer, send the entire
                        // file and set our initial snapshot.  We'll roll forward
                        // to new snapshots when we receive the errors event.  This
                        // just makes sure that the content is in sync.
                        updates.Add(
                            new AP.FileUpdate() {
                                content = snapshot.GetText(),
                                version = snapshot.Version.VersionNumber,
                                bufferId = bufferInfo.Id,
                                kind = AP.FileUpdateKind.reset
                            }
                        );
                    } else {
                        List<AP.VersionChanges> versions = new List<AnalysisProtocol.VersionChanges>();
                        for (var curVersion = lastSent.Version;
                            curVersion != snapshot.Version;
                            curVersion = curVersion.Next) {
                            versions.Add(
                                new AP.VersionChanges() {
                                    changes = GetChanges(curVersion)
                                }
                            );
                        }

                        updates.Add(
                            new AP.FileUpdate() {
                                versions = versions.ToArray(),
                                version = snapshot.Version.VersionNumber,
                                bufferId = bufferInfo.Id,
                                kind = AP.FileUpdateKind.changes
                            }
                        );
                    }

                    Debug.WriteLine("Added parse request {0}", snapshot.Version.VersionNumber);
                    entry.AnalysisCookie = new SnapshotCookie(snapshot);  // TODO: What about multiple snapshots?
                    SetLastSentSnapshot(snapshot);
                    SentSnapshot(snapshot);
                }
            }

            _parser._analysisComplete = false;
            Interlocked.Increment(ref _parser._parsePending);

            var res = await _parser._conn.SendRequestAsync(
                new AP.FileUpdateRequest() {
                    fileId = entry.FileId,
                    updates = updates.ToArray()
                }
            );

#if DEBUG
            for (int i = 0; i < bufferInfos.Length; i++) {
                var snapshot = snapshots[i];
                var buffer = bufferInfos[i];

                string newCode;
                if (res.newCode.TryGetValue(buffer.Id, out newCode)) {
                    Debug.Assert(newCode == snapshot.GetText());
                }
            }
#endif
        }

        private static AP.ChangeInfo[] GetChanges(ITextVersion curVersion) {
            Debug.WriteLine("Changes for version {0}", curVersion.VersionNumber);
            var changes = new List<AP.ChangeInfo>();
            if (curVersion.Changes != null) {
                foreach (var change in curVersion.Changes) {
                    Debug.WriteLine("Changes for version {0} {1} {2}", change.OldPosition, change.OldLength, change.NewText);
                    
                    changes.Add(
                        new AP.ChangeInfo() {
                            start = change.OldPosition,
                            length = change.OldLength,
                            newText = change.NewText
                        }
                    );
                }
            }
            return changes.ToArray();
        }

        internal void EncodingChanged(object sender, EncodingChangedEventArgs e) {
            lock (this) {
                if (_parsing) {
                    // we are currently parsing, just reque when we complete
                    _requeue = true;
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                } else {
                    Requeue();
                }
            }
        }

        internal void BufferChangedLowPriority(object sender, TextContentChangedEventArgs e) {
            lock (this) {
                // only immediately re-parse on line changes after we've seen a text change.

                if (_parsing) {
                    // we are currently parsing, just reque when we complete
                    _requeue = true;
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                } else if (LineAndTextChanges(e)) {
                    // user pressed enter, we should reque immediately
                    Requeue();
                } else {
                    // parse if the user doesn't do anything for a while.
                    _textChange = IncludesTextChanges(e);
                    _timer.Change(ReparseDelay, Timeout.Infinite);
                }
            }
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


        internal ITextDocument Document {
            get {
                return _document;
            }
        }
    }
}
