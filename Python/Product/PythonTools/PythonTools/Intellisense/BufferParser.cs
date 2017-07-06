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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    sealed class BufferParser : IDisposable {
        private readonly Timer _timer;
        internal readonly AnalysisEntry AnalysisEntry;

        private readonly VsProjectAnalyzer _parser;
        private IList<ITextBuffer> _buffers;
        private bool _parsing, _requeue, _textChange;
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

        public static readonly object DoNotParse = new object();

        internal static async Task<BufferParser> CreateAsync(AnalysisEntry analysis, VsProjectAnalyzer parser, ITextBuffer buffer) {
            var res = new BufferParser(analysis, parser, buffer);

            using (new DebugTimer("BufferParser.ParseBuffers", 100)) {
                // lock not necessary for _bufferInfo, no one has access to us yet...
                await res.ParseBuffers(new[] { buffer.CurrentSnapshot }, new[] { res._bufferInfo[buffer] });
            }

            return res;
        }

        private BufferParser(AnalysisEntry analysis, VsProjectAnalyzer parser, ITextBuffer buffer) {
            Debug.Assert(analysis != null);

            _parser = parser;
            _timer = new Timer(ReparseTimer, null, Timeout.Infinite, Timeout.Infinite);
            _buffers = new[] { buffer };
            AnalysisEntry = analysis;

            InitBuffer(buffer, 0);
        }

        class BufferInfo {
            public readonly ITextBuffer Buffer;
            public readonly int Id;
            /// <summary>
            /// The last version analyzed.  This is the oldest version we expect to receive
            /// spans relative to.  We'll roll this forward whenever we receive a new analysis
            /// event.
            /// </summary>
            internal ITextVersion _analysisVersion;
            public ITextSnapshot LastSentSnapshot;
            internal int LastParsedVersion;

            public BufferInfo(ITextBuffer buffer, int id) {
                Buffer = buffer;
                Id = id;
                _analysisVersion = buffer.CurrentSnapshot.Version;
                LastParsedVersion = _analysisVersion.VersionNumber - 1;
            }
        }

        /// <summary>
        /// Gets the last version ID for which we've received an analysis
        /// for this buffer.
        /// </summary>
        public ITextVersion GetAnalysisVersion(ITextBuffer buffer) {
            lock (this) {
                BufferInfo info;
                if (_bufferInfo.TryGetValue(buffer, out info)) {
                    return info._analysisVersion;
                }

                return null;
            }
        }

        /// <summary>
        /// Indicates that the specified buffer ID has been analyzed with this version.
        /// </summary>
        public void Analyzed(int bufferId, int version) {
            lock (this) {
                var bufferInfo = _bufferIdMapping[bufferId];

                while (bufferInfo._analysisVersion.Next != null &&
                    bufferInfo._analysisVersion.VersionNumber < version) {

                    bufferInfo._analysisVersion = bufferInfo._analysisVersion.Next;
                }
            }
        }

        internal bool IsOldSnapshot(int bufferId, int version) {
            lock(this) {
                var bufferInfo = _bufferIdMapping[bufferId];

                var oldVersion = bufferInfo.LastParsedVersion;

                if (oldVersion < version) {
                    bufferInfo.LastParsedVersion = version;
                    return false;
                }
                return true;
            }
        }

        internal ITextSnapshot GetLastSentSnapshot(ITextBuffer buffer) {
            lock (this) {
                BufferInfo bi;
                if (buffer != null && _bufferInfo.TryGetValue(buffer, out bi) && bi != null) {
                    return bi.LastSentSnapshot;
                }
                return null;
            }
        }

        internal void SetLastSentSnapshot(ITextSnapshot snapshot) {
            lock (this) {
                BufferInfo bi;
                if (snapshot != null && _bufferInfo.TryGetValue(snapshot.TextBuffer, out bi) && bi != null) {
                    bi.LastSentSnapshot = snapshot;
                } else {
                    Debug.Fail("Unknown snapshot");
                }
            }
        }

        internal int? GetBufferId(ITextBuffer buffer) { 
            lock (this) {
                BufferInfo info;
                if (_bufferInfo.TryGetValue(buffer, out info)) {
                    return info.Id;
                }
                return null;
            }
        }

        internal ITextBuffer GetBuffer(int bufferId) {
            lock (this) {
                return _bufferIdMapping[bufferId].Buffer;
            }
        }

        private BufferInfo GetBufferInfo(ITextBuffer buffer) {
            lock (this) {
                return _bufferInfo[buffer];
            }
        }

        private Severity IndentationInconsistencySeverity {
            get {
                return _parser.PyService.GeneralOptions.IndentationInconsistencySeverity;
            }
        }

        public void StopMonitoring() {
            foreach (var buffer in _buffers) {
                UninitBuffer(buffer);
            }
            _timer.Dispose();
            AnalysisEntry.ClearBufferParser(this);
        }

        public ITextBuffer[] Buffers {
            get {
                return _buffers.Where(
                    x => !x.Properties.ContainsProperty(DoNotParse)
                ).ToArray();
            }
        }

        internal void AddBuffer(ITextBuffer textBuffer) {
            lock (this) {
                EnsureMutableBuffers();

                _buffers.Add(textBuffer);

                InitBuffer(textBuffer, _buffers.Count - 1);

                _parser.ConnectErrorList(AnalysisEntry, textBuffer);
            }
        }

        internal void RemoveBuffer(ITextBuffer subjectBuffer) {
            lock (this) {
                EnsureMutableBuffers();

                UninitBuffer(subjectBuffer);

                _buffers.Remove(subjectBuffer);

                _parser.DisconnectErrorList(AnalysisEntry, subjectBuffer);
            }
        }

        internal void UninitBuffer(ITextBuffer subjectBuffer) {
            subjectBuffer.UnregisterAllHandlers();
            if (_document != null) {
                _document.EncodingChanged -= EncodingChanged;
                _document = null;
            }
            subjectBuffer.ChangedLowPriority -= BufferChangedLowPriority;
        }

        private void InitBuffer(ITextBuffer buffer, int id = 0) {
            buffer.ChangedLowPriority += BufferChangedLowPriority;

            lock (this) {
                var bufferInfo = new BufferInfo(buffer, id);
                _bufferInfo[buffer] = bufferInfo;
                _bufferIdMapping[id] = bufferInfo;
            }

            if (_document != null) {
                _document.EncodingChanged -= EncodingChanged;
                _document = null;
            }
            if (buffer.Properties.TryGetProperty(typeof(ITextDocument), out _document) && _document != null) {
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

            ParseBuffers(snapshots, bufferInfos).WaitAndHandleAllExceptions(_parser._serviceProvider);

            lock (this) {
                _parsing = false;
                if (_requeue) {
                    RequeueWorker();
                }
                _requeue = false;
            }
        }

        public async Task EnsureCodeSyncedAsync(ITextBuffer buffer) {
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
            AnalysisEntry entry = AnalysisEntry;

            List<AP.FileUpdate> updates = new List<AP.FileUpdate>();
            lock (this) {
                for (int i = 0; i < snapshots.Length; i++) {
                    var snapshot = snapshots[i];
                    var bufferInfo = bufferInfos[i];

                    if (snapshot.TextBuffer.Properties.ContainsProperty(DoNotParse) ||
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
                        if (lastSent.Version == snapshot.Version) {
                            // this snapshot is up to date...
                            continue;
                        }

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
                }
            }

            if (updates.Count != 0) {
                _parser._analysisComplete = false;
                Interlocked.Increment(ref _parser._parsePending);

                var res = await _parser.SendRequestAsync(
                    new AP.FileUpdateRequest() {
                        fileId = entry.FileId,
                        updates = updates.ToArray()
                    }
                );

                if (res != null) {
                    Debug.Assert(res.failed != true);
                    _parser.OnAnalysisStarted();
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
                } else {
                    Interlocked.Decrement(ref _parser._parsePending);
                }
            }
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

        public void Dispose() {
            StopMonitoring();
        }

        internal ITextDocument Document {
            get {
                return _document;
            }
        }
    }

    static class BufferParserExtensions {
#if DEBUG
        private static object _newAnalysisKey = new { Name = "OnNewAnalysis" };
        private static object _newAnalysisEntryKey = new { Name = "OnNewAnalysisEntry" };
        private static object _newParseTreeKey = new { Name = "OnNewParseTree" };
#else
        private static object _newAnalysisKey = new object();
        private static object _newAnalysisEntryKey = new object();
        private static object _newParseTreeKey = new object();
#endif


        /// <summary>
        /// Registers for when a new analysis is available for the text buffer.  This mechanism
        /// is suitable for handlers which have no clear cut way to disconnect from the text
        /// buffer (e.g. classifiers).  This attachs the event to the active buffer so that the 
        /// classifier and buffer go away when the buffer is closed.  Hooking to the project
        /// entry would result in keeping the classifier alive, which will keep the buffer alive,
        /// and the classifier would continue to receive new analysis events.
        /// </summary>
        public static void RegisterForNewAnalysis(this ITextBuffer buffer, Action<AnalysisEntry> handler) {
            buffer.RegisterFor(_newAnalysisKey, handler);
        }

        public static void UnregisterForNewAnalysis(this ITextBuffer buffer, Action<AnalysisEntry> handler) {
            buffer.UnregisterFor(_newAnalysisKey, handler);
        }

        public static IEnumerable<Action<AnalysisEntry>> GetNewAnalysisRegistrations(this ITextBuffer buffer) {
            return buffer.GetRegistrations(_newAnalysisKey);
        }

        /// <summary>
        /// Registers for when a new analysis entry is available for the text buffer.  This mechanism
        /// is suitable for handlers which have no clear cut way to disconnect from the text
        /// buffer (e.g. classifiers).  This attachs the event to the active buffer so that the 
        /// classifier and buffer go away when the buffer is closed.  Hooking to the project
        /// entry would result in keeping the classifier alive, which will keep the buffer alive,
        /// and the classifier would continue to receive new analysis events.
        /// </summary>
        public static void RegisterForNewAnalysisEntry(this ITextBuffer buffer, Action<AnalysisEntry> handler) {
            buffer.RegisterFor(_newAnalysisEntryKey, handler);
        }

        public static void UnregisterForNewAnalysisEntry(this ITextBuffer buffer, Action<AnalysisEntry> handler) {
            buffer.UnregisterFor(_newAnalysisEntryKey, handler);
        }

        public static IEnumerable<Action<AnalysisEntry>> GetNewAnalysisEntryRegistrations(this ITextBuffer buffer) {
            return buffer.GetRegistrations(_newAnalysisEntryKey);
        }

        /// <summary>
        /// Registers for when a new parse tree is available for the text buffer.  This mechanism
        /// is suitable for handlers which have no clear cut way to disconnect from the text
        /// buffer (e.g. classifiers).  This attachs the event to the active buffer so that the 
        /// classifier and buffer go away when the buffer is closed.  Hooking to the project
        /// entry would result in keeping the classifier alive, which will keep the buffer alive,
        /// and the classifier would continue to receive new analysis events.
        /// </summary>
        public static void RegisterForParseTree(this ITextBuffer buffer, Action<AnalysisEntry> handler) {
            buffer.RegisterFor(_newParseTreeKey, handler);
        }

        public static void UnregisterForParseTree(this ITextBuffer buffer, Action<AnalysisEntry> handler) {
            buffer.UnregisterFor(_newParseTreeKey, handler);
        }

        public static IEnumerable<Action<AnalysisEntry>> GetParseTreeRegistrations(this ITextBuffer buffer) {
            return buffer.GetRegistrations(_newParseTreeKey);
        }

        private static void RegisterFor(this ITextBuffer buffer, object key, Action<AnalysisEntry> handler) {
            HashSet<Action<AnalysisEntry>> actions;
            if (!buffer.Properties.TryGetProperty(key, out actions)) {
                buffer.Properties[key] = actions = new HashSet<Action<AnalysisEntry>>();
            }
            actions.Add(handler);
        }

        private static void UnregisterFor(this ITextBuffer buffer, object key, Action<AnalysisEntry> handler) {
            HashSet<Action<AnalysisEntry>> actions;
            if (buffer.Properties.TryGetProperty(key, out actions)) {
                actions.Remove(handler);
            }
        }

        private static IEnumerable<Action<AnalysisEntry>> GetRegistrations(this ITextBuffer buffer, object key) {
            HashSet<Action<AnalysisEntry>> actions;
            if (buffer.Properties.TryGetProperty(key, out actions)) {
                return actions.ToArray();
            }
            return Array.Empty<Action<AnalysisEntry>>();
        }

        public static void UnregisterAllHandlers(this ITextBuffer buffer) {
            buffer.UnregisterAll(_newAnalysisKey);
            buffer.UnregisterAll(_newAnalysisEntryKey);
            buffer.UnregisterAll(_newParseTreeKey);
        }

        private static void UnregisterAll(this ITextBuffer buffer, object key) {
            buffer.Properties.RemoveProperty(key);
        }
    }
}
