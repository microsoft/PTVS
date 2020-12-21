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
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    class TaskProviderItem {
        private readonly string _moniker;
        private readonly string _message;
        private readonly SourceSpan _rawSpan;
        private readonly VSTASKPRIORITY _priority;
        private readonly VSTASKCATEGORY _category;
        private readonly bool _squiggle;
        private readonly LocationTracker _spanTranslator;
        private readonly int _fromVersion;
        private readonly IServiceProvider _serviceProvider;

        internal TaskProviderItem(
            IServiceProvider serviceProvider,
            string moniker,
            string message,
            SourceSpan rawSpan,
            VSTASKPRIORITY priority,
            VSTASKCATEGORY category,
            bool squiggle,
            LocationTracker spanTranslator,
            int fromVersion
        ) {
            _serviceProvider = serviceProvider;
            _moniker = moniker;
            _message = message;
            _rawSpan = rawSpan;
            _spanTranslator = spanTranslator;
            _fromVersion = fromVersion;
            _rawSpan = rawSpan;
            _priority = priority;
            _category = category;
            _squiggle = squiggle;
        }

        private string ErrorType {
            get {
                switch (_priority) {
                    case VSTASKPRIORITY.TP_HIGH:
                        return PredefinedErrorTypeNames.SyntaxError;
                    case VSTASKPRIORITY.TP_LOW:
                        return PredefinedErrorTypeNames.OtherError;
                    case VSTASKPRIORITY.TP_NORMAL:
                        return PredefinedErrorTypeNames.Warning;
                    default:
                        return string.Empty;
                }
            }
        }

        #region Conversion Functions

        public bool ShowSquiggle => _squiggle && !string.IsNullOrEmpty(ErrorType);

        public void CreateSquiggleSpan(SimpleTagger<ErrorTag> tagger) {
            if (_rawSpan.Start >= _rawSpan.End || _spanTranslator == null) {
                return;
            }

            var snapshot = _spanTranslator.TextBuffer.CurrentSnapshot;
            var target = _spanTranslator.Translate(_rawSpan, _fromVersion, snapshot);

            if (target.Length <= 0) {
                return;
            }

            var tagSpan = snapshot.CreateTrackingSpan(
                target.Start,
                target.Length,
                SpanTrackingMode.EdgeInclusive
            );

            tagger.CreateTagSpan(tagSpan, new ErrorTagWithMoniker(ErrorType, _message, _moniker));
        }

        public ITextBuffer TextBuffer => _spanTranslator?.TextBuffer;

        public ErrorTaskItem ToErrorTaskItem(EntryKey key) {
            return new ErrorTaskItem(
                _serviceProvider,
                _rawSpan,
                _message,
                key.FilePath ?? string.Empty
            ) {
                Priority = _priority,
                Category = _category
            };
        }

        #endregion
    }

    sealed class ErrorTagWithMoniker : ErrorTag {
        public ErrorTagWithMoniker(string errorType, object toolTipContent, string moniker) : base(errorType, toolTipContent) {
            Moniker = moniker;
        }

        public string Moniker { get; }
    }

    sealed class TaskProviderItemFactory {
        private readonly LocationTracker _spanTranslator;
        private readonly int _fromVersion;

        public TaskProviderItemFactory(LocationTracker spanTranslator, int fromVersion) {
            _spanTranslator = spanTranslator;
            _fromVersion = fromVersion;
        }

        #region Factory Functions

        internal TaskProviderItem FromDiagnostic(
            IServiceProvider site,
            Diagnostic diagnostic,
            VSTASKCATEGORY category,
            bool squiggle
        ) {
            var priority = VSTASKPRIORITY.TP_LOW;
            switch (diagnostic.severity) {
                case DiagnosticSeverity.Error:
                    priority = VSTASKPRIORITY.TP_HIGH;
                    break;
                case DiagnosticSeverity.Warning:
                    priority = VSTASKPRIORITY.TP_NORMAL;
                    break;
            }

            return new TaskProviderItem(
                site,
                diagnostic.source,
                diagnostic.message,
                diagnostic.range,
                priority,
                category,
                squiggle,
                _spanTranslator,
                _fromVersion
            );
        }

        #endregion
    }

    struct EntryKey : IEquatable<EntryKey> {
        public readonly string FilePath;
        public readonly string Moniker;

        public static readonly EntryKey Empty = new EntryKey(null, null);

        public EntryKey(string filePath, string moniker) {
            FilePath = filePath;
            Moniker = moniker;
        }

        public override bool Equals(object obj) {
            return obj is EntryKey key && Equals(key);
        }

        public bool Equals(EntryKey other) {
            return FilePath == other.FilePath && Moniker == other.Moniker;
        }

        public override int GetHashCode() {
            return (FilePath?.GetHashCode() ?? 0) ^ (Moniker?.GetHashCode() ?? 0);
        }
    }

    abstract class WorkerMessage {
        private readonly EntryKey _key;
        private readonly List<TaskProviderItem> _items;

        protected WorkerMessage() {
            _key = EntryKey.Empty;
        }

        protected WorkerMessage(EntryKey key, List<TaskProviderItem> items) {
            _key = key;
            _items = items;
        }

        public abstract bool Apply(Dictionary<EntryKey, List<TaskProviderItem>> items, object itemsLock);

        // Factory methods
        public static WorkerMessage Clear() {
            return new ClearMessage(EntryKey.Empty);
        }

        public static WorkerMessage Clear(string filePath, string moniker) {
            return new ClearMessage(new EntryKey(filePath, moniker));
        }

        public static WorkerMessage Replace(string filePath, string moniker, List<TaskProviderItem> items) {
            return new ReplaceMessage(new EntryKey(filePath, moniker), items);
        }

        public static WorkerMessage Append(string filePath, string moniker, List<TaskProviderItem> items) {
            return new AppendMessage(new EntryKey(filePath, moniker), items);
        }

        public static WorkerMessage Flush(TaskCompletionSource<TimeSpan> taskSource) {
            return new FlushMessage(taskSource, DateTime.Now);
        }

        public static WorkerMessage Abort() {
            return new AbortMessage();
        }

        // Message implementations
        sealed class ReplaceMessage : WorkerMessage {
            public ReplaceMessage(EntryKey key, List<TaskProviderItem> items)
                : base(key, items) { }

            public override bool Apply(Dictionary<EntryKey, List<TaskProviderItem>> items, object itemsLock) {
                lock (itemsLock) {
                    items[_key] = _items;
                    return true;
                }
            }

            public override string ToString() {
                return $"Replace {_key.Moniker} {_items.Count} {_key.FilePath ?? "(null)"}";
            }
        }

        sealed class AppendMessage : WorkerMessage {
            public AppendMessage(EntryKey key, List<TaskProviderItem> items)
                : base(key, items) { }

            public override bool Apply(Dictionary<EntryKey, List<TaskProviderItem>> items, object itemsLock) {
                lock (itemsLock) {
                    List<TaskProviderItem> itemList;
                    if (items.TryGetValue(_key, out itemList)) {
                        itemList.AddRange(_items);
                    } else {
                        items[_key] = _items;
                    }
                    return true;
                }
            }

            public override string ToString() {
                return $"Append {_key.Moniker} {_key.FilePath ?? "(null)"}";
            }
        }

        sealed class ClearMessage : WorkerMessage {
            public ClearMessage(EntryKey key)
                : base(key, null) { }

            public override bool Apply(Dictionary<EntryKey, List<TaskProviderItem>> items, object itemsLock) {
                lock (itemsLock) {
                    if (_key.FilePath != null) {
                        items.Remove(_key);
                    } else {
                        items.Clear();
                    }
                    // Always return true to ensure the refresh occurs
                    return true;
                }
            }

            public override string ToString() {
                return $"Clear {_key.Moniker} {_key.FilePath ?? "(null)"}";
            }
        }

        internal sealed class FlushMessage : WorkerMessage {
            private readonly TaskCompletionSource<TimeSpan> _tcs;
            private readonly DateTime _start;

            public FlushMessage(TaskCompletionSource<TimeSpan> taskSource, DateTime start)
                : base(EntryKey.Empty, null) {
                _tcs = taskSource;
                _start = start;
            }

            public override bool Apply(Dictionary<EntryKey, List<TaskProviderItem>> items, object itemsLock) {
                _tcs.SetResult(DateTime.Now - _start);
                return false;
            }
        }

        internal sealed class AbortMessage : WorkerMessage {
            public AbortMessage() : base() { }

            public override bool Apply(Dictionary<EntryKey, List<TaskProviderItem>> items, object itemsLock) {
                // Indicates that we should stop work immediately and not try to recover
                throw new ObjectDisposedException(nameof(TaskProvider));
            }
        }
    }

    class TaskProvider : IVsTaskProvider, IDisposable {
        private readonly Dictionary<EntryKey, List<TaskProviderItem>> _items;
        private readonly Dictionary<EntryKey, HashSet<ITextBuffer>> _errorSources;
        private readonly HashSet<string> _monikers;
        private readonly object _itemsLock = new object();
        private uint _cookie;
        private readonly IVsTaskList _taskList;
        private readonly IErrorProviderFactory _errorProvider;
        protected readonly IServiceProvider _serviceProvider;

        private Thread _worker;
        private readonly Queue<WorkerMessage> _workerQueue = new Queue<WorkerMessage>();
        private readonly ManualResetEventSlim _workerQueueChanged = new ManualResetEventSlim();

        public TaskProvider(IServiceProvider serviceProvider, IVsTaskList taskList, IErrorProviderFactory errorProvider, IEnumerable<string> monikers) {
            _serviceProvider = serviceProvider;
            _items = new Dictionary<EntryKey, List<TaskProviderItem>>();
            _errorSources = new Dictionary<EntryKey, HashSet<ITextBuffer>>();
            _monikers = new HashSet<string>(monikers.MaybeEnumerate(), StringComparer.OrdinalIgnoreCase);

            _taskList = taskList;
            _errorProvider = errorProvider;
        }

#if DEBUG
        private static bool Quiet = false;
#endif

        [Conditional("DEBUG")]
        private static void Log(string fmt, params object[] args) {
#if DEBUG
            if (!Quiet) {
                Debug.WriteLine(args.Length > 0 ? fmt.FormatInvariant(args) : fmt);
            }
#endif
        }

        public virtual void Dispose() {
            try {
                // Disposing the main wait object will terminate the thread
                // as best as we can
                _workerQueueChanged.Dispose();
            } catch (ObjectDisposedException) {
            }

            lock (_itemsLock) {
                _items.Clear();
            }
            if (_taskList != null) {
                _taskList.UnregisterTaskProvider(_cookie);
            }
        }

        public uint Cookie {
            get {
                return _cookie;
            }
        }

        /// <summary>
        /// Replaces the items for the specified entry.
        /// </summary>
        public void ReplaceItems(string filePath, string moniker, List<TaskProviderItem> items) {
            SendMessage(WorkerMessage.Replace(filePath, moniker, items));
        }

        /// <summary>
        /// Adds items to the specified entry's existing items.
        /// </summary>
        public void AddItems(string filePath, string moniker, List<TaskProviderItem> items) {
            SendMessage(WorkerMessage.Append(filePath, moniker, items));
        }

        /// <summary>
        /// Removes all items from all entries.
        /// </summary>
        public void ClearAll() {
            SendMessage(WorkerMessage.Clear());
        }

        /// <summary>
        /// Removes all items for the specified entry.
        /// </summary>
        public void Clear(string filePath, string moniker) {
            SendMessage(WorkerMessage.Clear(filePath, moniker));
        }

        /// <summary>
        /// Waits for all messages to clear the queue. This typically takes at
        /// least one second, since that is the timeout on the worker thread.
        /// </summary>
        /// <returns>
        /// The time between when flush was called and the queue completed.
        /// </returns>
        public Task<TimeSpan> FlushAsync() {
            var tcs = new TaskCompletionSource<TimeSpan>();
            SendMessage(WorkerMessage.Flush(tcs));
            return tcs.Task;
        }

        /// <summary>
        /// Adds the buffer to be tracked for reporting squiggles and error list entries
        /// for the given project entry and moniker for the error source.
        /// </summary>
        public void AddBufferForErrorSource(string filePath, string moniker, ITextBuffer buffer) {
            lock (_errorSources) {
                var key = new EntryKey(filePath, moniker);
                HashSet<ITextBuffer> buffers;
                if (!_errorSources.TryGetValue(key, out buffers)) {
                    _errorSources[key] = buffers = new HashSet<ITextBuffer>();
                }
                buffers.Add(buffer);
            }
        }

        /// <summary>
        /// Removes the buffer from tracking for reporting squiggles and error list entries
        /// for the given project entry and moniker for the error source.
        /// </summary>
        public void RemoveBufferForErrorSource(string filePath, string moniker, ITextBuffer buffer) {
            lock (_errorSources) {
                if (_errorSources.TryGetValue(new EntryKey(filePath, moniker), out var buffers)) {
                    buffers.Remove(buffer);
                }
            }
        }

        /// <summary>
        /// Clears all tracked buffers for the given project entry and moniker for
        /// the error source.
        /// </summary>
        public void ClearErrorSource(string filePath, string moniker) {
            lock (_errorSources) {
                _errorSources.Remove(new EntryKey(filePath, moniker));
            }
        }

        #region Internal Worker Thread

        private void StartWorker() {
            if (_worker != null) {
                // Already running
                Debug.Assert(_worker.IsAlive, "Worker has died without clearing itself");
                return;
            }

            var t = new Thread(Worker);
            t.IsBackground = true;
            t.Name = GetType().Name + " Worker";
            t.Start(t);
        }

        private void Worker(object param) {
            for (; ; ) {
                var self = (Thread)param;
                if (Interlocked.CompareExchange(ref _worker, self, null) != null) {
                    // Not us, so abort
                    return;
                }

                try {
                    WorkerWorker();
                } catch (OperationCanceledException) {
                    Log("Operation canceled... {0}", DateTime.Now);

                } catch (ObjectDisposedException ex) {
                    Trace.TraceError(ex.ToString());
                    break;
                } catch (Exception ex) {
                    if (ex.IsCriticalException()) {
                        throw;
                    }
                    ex.ReportUnhandledException(_serviceProvider, GetType());
                } finally {
                    var oldWorker = Interlocked.CompareExchange(ref _worker, null, self);
                    Log("Checking worker... {0}", DateTime.Now);
                    Debug.Assert(oldWorker == self, "Worker was changed while running");
                    Log("Worker exiting... {0}", DateTime.Now);
                }

                // check for work after clearing out _worker so that we don't race with
                // StartWorker and end up with no worker.
                lock (_workerQueue) {
                    if (_workerQueue.Count == 0) {
                        break;
                    }
                    Log("Spinning to try and become worker again...");
                }
            }
        }

        private void WorkerWorker() {
            var flushMessages = new Queue<WorkerMessage>();
            bool changed = false;
            var lastUpdateTime = DateTime.Now;

            while (_workerQueueChanged.Wait(1000)) {
                // Reset event and handle all messages in the queue
                _workerQueueChanged.Reset();

                while (true) {
                    WorkerMessage msg;
                    lock (_workerQueue) {
                        if (_workerQueue.Count == 0) {
                            break;
                        }
                        msg = _workerQueue.Dequeue();
                        Log("{2} Processing msg... {0} {1}", DateTime.Now, msg, GetType());
                    }

                    if (msg is WorkerMessage.FlushMessage) {
                        // Keep flush messages until we've exited the loop
                        flushMessages.Enqueue(msg);
                    } else {
                        // Apply the message to our collection
                        changed |= msg.Apply(_items, _itemsLock);
                    }
                    Log("{2} Done processing msg... {0} {1}", DateTime.Now, msg, GetType());
                    // Every second, we want to force another update
                    if (changed) {
                        var currentTime = DateTime.Now;
                        if ((currentTime - lastUpdateTime).TotalMilliseconds > 1000) {
                            if (Refresh()) {
                                lastUpdateTime = currentTime;
                                changed = false;
                            }
                        }
                    }
                }
                Log("Looping to wait... {0}", DateTime.Now);
            }

            // Handle any changes that weren't handled in the loop
            if (changed) {
                Log("Refreshing... {0}", DateTime.Now);
                Refresh();
            }

            // Notify all the flush messages we received
            Log("Flushing... {0}", DateTime.Now);
            while (flushMessages.Any()) {
                var msg = flushMessages.Dequeue();
                msg.Apply(_items, _itemsLock);
            }
            Log("Done flushing... {0}", DateTime.Now);
        }

        private bool Refresh() {
            if (_taskList == null && _errorProvider == null) {
                return true;
            }
            _serviceProvider.MustNotBeCalledFromUIThread("TaskProvider.Refresh() called on UI thread");

            // Allow 1 second to get onto the UI thread for the update
            // Otherwise abort and we'll try again later
            var cts = new CancellationTokenSource(1000);
            try {
                try {
                    RefreshAsync(cts.Token).Wait(cts.Token);
                } catch (AggregateException ex) {
                    if (ex.InnerExceptions.Count == 1) {
                        ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    }
                    throw;
                }
            } catch (OperationCanceledException) {
                return false;
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                ex.ReportUnhandledException(_serviceProvider, GetType());
            } finally {
                cts.Dispose();
            }
            return true;
        }

        private async Task RefreshAsync(CancellationToken cancellationToken) {
            var buffers = new HashSet<ITextBuffer>();
            var bufferToErrorList = new Dictionary<ITextBuffer, List<TaskProviderItem>>();

            if (_errorProvider != null) {
                lock (_errorSources) {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var kv in _errorSources) {
                        buffers.UnionWith(kv.Value);

                        lock (_itemsLock) {
                            if (!_items.TryGetValue(kv.Key, out var items)) {
                                continue;
                            }

                            foreach (var item in items) {
                                if (item.ShowSquiggle && item.TextBuffer != null) {
                                    if (!bufferToErrorList.TryGetValue(item.TextBuffer, out var itemList)) {
                                        bufferToErrorList[item.TextBuffer] = itemList = new List<TaskProviderItem>();
                                    }

                                    itemList.Add(item);
                                }
                            }
                        }
                    }
                }
            }

            foreach (var kv in bufferToErrorList) {
                var tagger = _errorProvider.GetErrorTagger(kv.Key);
                if (tagger == null) {
                    continue;
                }

                using (tagger.Update()) {
                    if (buffers.Remove(kv.Key)) {
                        tagger.RemoveTagSpans(span =>
                            _monikers.Contains((span.Tag as ErrorTagWithMoniker)?.Moniker) &&
                            span.Span.TextBuffer == kv.Key
                        );
                    }

                    foreach (var taskProviderItem in kv.Value) {
                        taskProviderItem.CreateSquiggleSpan(tagger);
                    }
                }
            }

            if (buffers.Any()) {
                // Clear tags for any remaining buffers.
                foreach (var buffer in buffers) {
                    var tagger = _errorProvider.GetErrorTagger(buffer);
                    tagger.RemoveTagSpans(span =>
                        _monikers.Contains((span.Tag as ErrorTagWithMoniker)?.Moniker) &&
                        span.Span.TextBuffer == buffer
                    );
                }
            }

            await _serviceProvider.GetUIThread().InvokeAsync(() => {
                if (_taskList != null) {
                    if (_cookie == 0) {
                        ErrorHandler.ThrowOnFailure(_taskList.RegisterTaskProvider(this, out _cookie));
                    }
                    try {
                        _taskList.RefreshTasks(_cookie);
                    } catch (InvalidComObjectException) {
                        // DevDiv2 759317 - Watson bug, COM object can go away...
                    }
                }
            }, cancellationToken);
        }

        private void SendMessage(WorkerMessage message) {
            try {
                lock (_workerQueue) {
                    _workerQueue.Enqueue(message);
                    _workerQueueChanged.Set();
                }

                StartWorker();
            } catch (ObjectDisposedException) {
            }
        }

        #endregion

        #region IVsTaskProvider Members

        public int EnumTaskItems(out IVsEnumTaskItems ppenum) {
            lock (_itemsLock) {
                ppenum = new TaskEnum(_items
                    .Where(x => x.Key.FilePath != null)   // don't report REPL window errors in the error list, you can't naviagate to them
                    .SelectMany(kv => kv.Value.Select(i => i.ToErrorTaskItem(kv.Key)))
                    .ToArray()
                );
            }
            return VSConstants.S_OK;
        }

        public int ImageList(out IntPtr phImageList) {
            // not necessary if we report our category as build compile.
            phImageList = IntPtr.Zero;
            return VSConstants.E_NOTIMPL;
        }

        public int OnTaskListFinalRelease(IVsTaskList pTaskList) {
            return VSConstants.S_OK;
        }

        public int ReRegistrationKey(out string pbstrKey) {
            pbstrKey = null;
            return VSConstants.E_NOTIMPL;
        }

        public int SubcategoryList(uint cbstr, string[] rgbstr, out uint pcActual) {
            pcActual = 0;
            return VSConstants.S_OK;
        }

        #endregion
    }

    class TaskEnum : IVsEnumTaskItems {
        private readonly IEnumerable<ErrorTaskItem> _enumerable;
        private IEnumerator<ErrorTaskItem> _enumerator;

        public TaskEnum(IEnumerable<ErrorTaskItem> items) {
            _enumerable = items;
            _enumerator = _enumerable.GetEnumerator();
        }

        public int Clone(out IVsEnumTaskItems ppenum) {
            ppenum = new TaskEnum(_enumerable);
            return VSConstants.S_OK;
        }

        public int Next(uint celt, IVsTaskItem[] rgelt, uint[] pceltFetched = null) {
            bool fetchedAny = false;

            if (pceltFetched != null && pceltFetched.Length > 0) {
                pceltFetched[0] = 0;
            }

            for (int i = 0; i < celt && _enumerator.MoveNext(); i++) {
                if (pceltFetched != null && pceltFetched.Length > 0) {
                    pceltFetched[0] = (uint)i + 1;
                }
                rgelt[i] = _enumerator.Current;
                fetchedAny = true;
            }

            return fetchedAny ? VSConstants.S_OK : VSConstants.S_FALSE;
        }

        public int Reset() {
            _enumerator = _enumerable.GetEnumerator();
            return VSConstants.S_OK;
        }

        public int Skip(uint celt) {
            while (celt != 0 && _enumerator.MoveNext()) {
                celt--;
            }
            return VSConstants.S_OK;
        }
    }

    class ErrorTaskItem : IVsTaskItem, IVsErrorItem {
        private readonly IServiceProvider _serviceProvider;

        public ErrorTaskItem(
            IServiceProvider serviceProvider,
            SourceSpan span,
            string message,
            string sourceFile
        ) {
            _serviceProvider = serviceProvider;
            Span = span;
            Message = message;
            SourceFile = sourceFile;
            Category = VSTASKCATEGORY.CAT_BUILDCOMPILE;
            Priority = VSTASKPRIORITY.TP_NORMAL;

            MessageIsReadOnly = true;
            IsCheckedIsReadOnly = true;
            PriorityIsReadOnly = true;
        }

        public SourceSpan Span { get; private set; }
        public string Message { get; set; }
        public string SourceFile { get; }
        public VSTASKCATEGORY Category { get; set; }
        public VSTASKPRIORITY Priority { get; set; }
        public bool CanDelete { get; set; }
        public bool IsChecked { get; set; }
        public ProjectNode ProjectHierarchy { get; set; }
        private bool _projectHierarchyIsNull;

        public bool MessageIsReadOnly { get; set; }
        public bool IsCheckedIsReadOnly { get; set; }
        public bool PriorityIsReadOnly { get; set; }

        int IVsTaskItem.CanDelete(out int pfCanDelete) {
            pfCanDelete = CanDelete ? 1 : 0;
            return VSConstants.S_OK;
        }

        int IVsTaskItem.Category(VSTASKCATEGORY[] pCat) {
            pCat[0] = Category;
            return VSConstants.S_OK;
        }

        public int Column(out int piCol) {
            if (Span.Start.Line == 1 && Span.Start.Column == 1) {
                // we don't have the column number calculated
                piCol = 0;
                return VSConstants.E_FAIL;
            }
            piCol = Span.Start.Column - 1;
            return VSConstants.S_OK;
        }

        public int Document(out string pbstrMkDocument) {
            pbstrMkDocument = SourceFile;
            return VSConstants.S_OK;
        }

        public int HasHelp(out int pfHasHelp) {
            pfHasHelp = 0;
            return VSConstants.S_OK;
        }

        public int ImageListIndex(out int pIndex) {
            pIndex = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int IsReadOnly(VSTASKFIELD field, out int pfReadOnly) {
            switch (field) {
                case VSTASKFIELD.FLD_CHECKED:
                    pfReadOnly = IsCheckedIsReadOnly ? 1 : 0;
                    break;
                case VSTASKFIELD.FLD_DESCRIPTION:
                    pfReadOnly = MessageIsReadOnly ? 1 : 0;
                    break;
                case VSTASKFIELD.FLD_PRIORITY:
                    pfReadOnly = PriorityIsReadOnly ? 1 : 0;
                    break;
                case VSTASKFIELD.FLD_BITMAP:
                case VSTASKFIELD.FLD_CATEGORY:
                case VSTASKFIELD.FLD_COLUMN:
                case VSTASKFIELD.FLD_CUSTOM:
                case VSTASKFIELD.FLD_FILE:
                case VSTASKFIELD.FLD_LINE:
                case VSTASKFIELD.FLD_PROVIDERKNOWSORDER:
                case VSTASKFIELD.FLD_SUBCATEGORY:
                default:
                    pfReadOnly = 1;
                    break;
            }
            return VSConstants.S_OK;
        }

        public int Line(out int piLine) {
            if (Span.Start.Line == 1 && Span.Start.Column == 1) {
                // we don't have the line number calculated
                piLine = 0;
                return VSConstants.E_FAIL;
            }
            piLine = Span.Start.Line - 1;
            return VSConstants.S_OK;
        }

        public int NavigateTo() {
            try {
                PythonToolsPackage.NavigateTo(_serviceProvider, SourceFile, Guid.Empty, Span.Start.Line - 1, Span.Start.Column - 1);
                return VSConstants.S_OK;
            } catch (DirectoryNotFoundException) {
                // This may happen when the error was in a file that's located inside a .zip archive.
                // Let's walk the path and see if it is indeed the case.
                for (var path = SourceFile; PathUtils.IsValidPath(path); path = Path.GetDirectoryName(path)) {
                    if (!File.Exists(path)) {
                        continue;
                    }
                    var ext = Path.GetExtension(path);
                    if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ext, ".egg", StringComparison.OrdinalIgnoreCase)) {
                        MessageBox.Show(
                            Strings.ErrorTaskItemZipArchiveNotSupportedMessage,
                            Strings.ErrorTaskItemZipArchiveNotSupportedCaption,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                        return VSConstants.S_FALSE;
                    }
                }
                // If it failed for some other reason, let caller handle it.
                throw;
            }
        }

        public int NavigateToHelp() {
            return VSConstants.E_NOTIMPL;
        }

        public int OnDeleteTask() {
            return VSConstants.E_NOTIMPL;
        }

        public int OnFilterTask(int fVisible) {
            return VSConstants.E_NOTIMPL;
        }

        public int SubcategoryIndex(out int pIndex) {
            pIndex = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int get_Checked(out int pfChecked) {
            pfChecked = IsChecked ? 1 : 0;
            return VSConstants.S_OK;
        }

        public int get_Priority(VSTASKPRIORITY[] ptpPriority) {
            ptpPriority[0] = Priority;
            return VSConstants.S_OK;
        }

        public int get_Text(out string pbstrName) {
            pbstrName = Message;
            return VSConstants.S_OK;
        }

        public int put_Checked(int fChecked) {
            if (IsCheckedIsReadOnly) {
                return VSConstants.E_NOTIMPL;
            }
            IsChecked = (fChecked != 0);
            return VSConstants.S_OK;
        }

        public int put_Priority(VSTASKPRIORITY tpPriority) {
            if (PriorityIsReadOnly) {
                return VSConstants.E_NOTIMPL;
            }
            Priority = tpPriority;
            return VSConstants.S_OK;
        }

        public int put_Text(string bstrName) {
            if (MessageIsReadOnly) {
                return VSConstants.E_NOTIMPL;
            }
            Message = bstrName;
            return VSConstants.S_OK;
        }

        public int BrowseObject(out object ppObj) {
            ppObj = null;
            return VSConstants.E_NOTIMPL;
        }

        public int get_CustomColumnText(ref Guid guidView, uint iCustomColumnIndex, out string pbstrText) {
            pbstrText = $"{guidView};{iCustomColumnIndex}";
            return VSConstants.S_OK;
        }

        public int put_CustomColumnText(ref Guid guidView, uint iCustomColumnIndex, string bstrText) {
            return VSConstants.E_NOTIMPL;
        }

        public int IsCustomColumnReadOnly(ref Guid guidView, uint iCustomColumnIndex, out int pfReadOnly) {
            pfReadOnly = 1;
            return VSConstants.S_OK;
        }

        public int GetHierarchy(out IVsHierarchy ppProject) {
            if (_projectHierarchyIsNull || ProjectHierarchy != null) {
                ppProject = ProjectHierarchy;
            } else if (!string.IsNullOrEmpty(SourceFile)) {
                ppProject = ProjectHierarchy = _serviceProvider.GetProjectFromFile(SourceFile);
                _projectHierarchyIsNull = ProjectHierarchy == null;
            } else {
                ppProject = null;
            }
            return ppProject != null ? VSConstants.S_OK : VSConstants.E_NOTIMPL;
        }

        public int GetCategory(out uint pCategory) {
            switch (Priority) {
                case VSTASKPRIORITY.TP_HIGH:
                    pCategory = (uint)__VSERRORCATEGORY.EC_ERROR;
                    break;
                case VSTASKPRIORITY.TP_NORMAL:
                    pCategory = (uint)__VSERRORCATEGORY.EC_WARNING;
                    break;
                case VSTASKPRIORITY.TP_LOW:
                    pCategory = (uint)__VSERRORCATEGORY.EC_MESSAGE;
                    break;
                default:
                    pCategory = 0;
                    break;
            }
            return VSConstants.S_OK;
        }
    }

    sealed class ErrorTaskProvider : TaskProvider {
        internal ErrorTaskProvider(IServiceProvider serviceProvider, IVsTaskList taskList, IErrorProviderFactory errorProvider, IEnumerable<string> monikers)
            : base(serviceProvider, taskList, errorProvider, monikers) {
        }

        public static object CreateService(IServiceProvider container, Type serviceType) {
            if (serviceType.IsEquivalentTo(typeof(ErrorTaskProvider))) {
                var errorList = container.GetService(typeof(SVsErrorList)) as IVsTaskList;
                var model = container.GetComponentModel();
                var errorProvider = model != null ? model.GetService<IErrorProviderFactory>() : null;
                return new ErrorTaskProvider(container, errorList, errorProvider, new[] {
                    VsProjectAnalyzer.PythonMoniker,
                    VsProjectAnalyzer.InvalidEncodingMoniker,
                });
            }
            return null;
        }
    }

    sealed class CommentTaskProvider : TaskProvider, IVsTaskListEvents {
        private volatile Dictionary<string, VSTASKPRIORITY> _tokens;

        internal CommentTaskProvider(IServiceProvider serviceProvider, IVsTaskList taskList, IErrorProviderFactory errorProvider, IEnumerable<string> monikers)
            : base(serviceProvider, taskList, errorProvider, monikers) {
            RefreshTokens();
        }

        public static object CreateService(IServiceProvider container, Type serviceType) {
            if (serviceType.IsEquivalentTo(typeof(CommentTaskProvider))) {
                var errorList = container.GetService(typeof(SVsTaskList)) as IVsTaskList;
                var model = container.GetComponentModel();
                var errorProvider = model != null ? model.GetService<IErrorProviderFactory>() : null;
                return new CommentTaskProvider(container, errorList, errorProvider, new[] { VsProjectAnalyzer.TaskCommentMoniker });
            }
            return null;
        }

        public Dictionary<string, VSTASKPRIORITY> Tokens {
            get { return _tokens; }
        }

        public event EventHandler TokensChanged;

        public int OnCommentTaskInfoChanged() {
            RefreshTokens();
            return VSConstants.S_OK;
        }

        // Retrieves token settings as defined by user in Tools -> Options -> Environment -> Task List.
        private void RefreshTokens() {
            var taskInfo = (IVsCommentTaskInfo)_serviceProvider.GetService(typeof(SVsTaskList));
            if (taskInfo == null) {
                _tokens = new Dictionary<string, VSTASKPRIORITY>();
                return;
            }

            IVsEnumCommentTaskTokens enumTokens;
            ErrorHandler.ThrowOnFailure(taskInfo.EnumTokens(out enumTokens));

            var newTokens = new Dictionary<string, VSTASKPRIORITY>();

            var token = new IVsCommentTaskToken[1];
            uint fetched;
            string text;
            var priority = new VSTASKPRIORITY[1];

            // DevDiv bug 1135485: EnumCommentTaskTokens.Next returns E_FAIL instead of S_FALSE
            while (enumTokens.Next(1, token, out fetched) == VSConstants.S_OK && fetched > 0) {
                ErrorHandler.ThrowOnFailure(token[0].Text(out text));
                ErrorHandler.ThrowOnFailure(token[0].Priority(priority));
                newTokens[text] = priority[0];
            }

            _tokens = newTokens;

            TokensChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
