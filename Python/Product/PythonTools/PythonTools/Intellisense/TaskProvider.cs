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
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    class TaskProviderItem {
        private readonly string _message;
        private readonly SourceSpan _rawSpan;
        private readonly VSTASKPRIORITY _priority;
        private readonly VSTASKCATEGORY _category;
        private readonly bool _squiggle;
        private readonly LocationTracker _spanTranslator;
        private readonly IServiceProvider _serviceProvider;

        internal TaskProviderItem(
            IServiceProvider serviceProvider,
            string message,
            SourceSpan rawSpan,
            VSTASKPRIORITY priority,
            VSTASKCATEGORY category,
            bool squiggle,
            LocationTracker spanTranslator
        ) {
            _serviceProvider = serviceProvider;
            _message = message;
            _rawSpan = rawSpan;
            _spanTranslator = spanTranslator;
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

        public bool IsValid => _squiggle && !string.IsNullOrEmpty(ErrorType);

        public void CreateSquiggleSpan(SimpleTagger<ErrorTag> tagger) {
            if (_rawSpan.Length <= 0 || _spanTranslator == null) {
                return;
            }

            SnapshotSpan target = _spanTranslator.TranslateForward(
                new Span(_rawSpan.Start.Index, _rawSpan.Length)
            );

            if (target.Length <= 0) {
                return;
            }

            var tagSpan = _spanTranslator.TextBuffer.CurrentSnapshot.CreateTrackingSpan(
                target.Start,
                target.Length,
                SpanTrackingMode.EdgeInclusive
            );

            tagger.CreateTagSpan(tagSpan, new ErrorTag(ErrorType, _message));
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

        private static ITrackingSpan CreateSpan(ITextSnapshot snapshot, SourceSpan span) {
            Debug.Assert(span.Start.Index >= 0);
            var res = new Span(
                span.Start.Index,
                Math.Min(span.End.Index - span.Start.Index, Math.Max(snapshot.Length - span.Start.Index, 0))
            );
            Debug.Assert(res.End <= snapshot.Length);
            return snapshot.CreateTrackingSpan(res, SpanTrackingMode.EdgeNegative);
        }
    }

    sealed class TaskProviderItemFactory {
        private readonly LocationTracker _spanTranslator;

        public TaskProviderItemFactory(LocationTracker spanTranslator) {
            _spanTranslator = spanTranslator;
        }

        #region Factory Functions

        public TaskProviderItem FromErrorResult(IServiceProvider serviceProvider, AP.Error result, VSTASKPRIORITY priority, VSTASKCATEGORY category) {
            return new TaskProviderItem(
                serviceProvider,
                result.message,
                GetSpan
                (result),
                priority,
                category,
                true,
                _spanTranslator
            );
        }

        internal static SourceSpan GetSpan(AP.Error result) {
            return new SourceSpan(
                new SourceLocation(result.startIndex, result.startLine, result.startColumn),
                new SourceLocation(result.startIndex + result.length, result.endLine, result.endColumn)
            );
        }

        internal TaskProviderItem FromUnresolvedImport(
            IServiceProvider serviceProvider, 
            IPythonInterpreterFactoryWithDatabase factory,
            string importName,
            SourceSpan span
        ) {
            string message;
            if (factory != null && !factory.IsCurrent) {
                message = Strings.UnresolvedModuleTooltipRefreshing.FormatUI(importName);
            } else {
                message = Strings.UnresolvedModuleTooltip.FormatUI(importName);
            }

            return new TaskProviderItem(
                serviceProvider,
                message,
                span,
                VSTASKPRIORITY.TP_NORMAL,
                VSTASKCATEGORY.CAT_BUILDCOMPILE,
                true,
                _spanTranslator
            );
        }

        #endregion
    }

    struct EntryKey : IEquatable<EntryKey> {
        public string FilePath;
        public string Moniker;

        public static readonly EntryKey Empty = new EntryKey(null, null);

        public EntryKey(string filePath, string moniker) {
            FilePath = filePath;
            Moniker = moniker;
        }

        public override bool Equals(object obj) {
            return obj is EntryKey && Equals((EntryKey)obj);
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
                throw new OperationCanceledException();
            }
        }
    }

    class TaskProvider : IVsTaskProvider, IDisposable {
        private readonly Dictionary<EntryKey, List<TaskProviderItem>> _items;
        private readonly Dictionary<EntryKey, HashSet<ITextBuffer>> _errorSources;
        private readonly object _itemsLock = new object();
        private uint _cookie;
        private readonly IVsTaskList _taskList;
        private readonly IErrorProviderFactory _errorProvider;
        protected readonly IServiceProvider _serviceProvider;

        private Thread _worker;
        private readonly Queue<WorkerMessage> _workerQueue = new Queue<WorkerMessage>();
        private readonly ManualResetEventSlim _workerQueueChanged = new ManualResetEventSlim();

        public TaskProvider(IServiceProvider serviceProvider, IVsTaskList taskList, IErrorProviderFactory errorProvider) {
            _serviceProvider = serviceProvider;
            _items = new Dictionary<EntryKey, List<TaskProviderItem>>();
            _errorSources = new Dictionary<EntryKey, HashSet<ITextBuffer>>();

            _taskList = taskList;
            _errorProvider = errorProvider;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

#if DEBUG
        private static bool Quiet = true;
#endif

        [Conditional("DEBUG")]
        private static void Log(string fmt, params object[] args) {
#if DEBUG
            if (!Quiet) {
                Debug.WriteLine(args.Length > 0 ? fmt.FormatInvariant(args) : fmt);
            }
#endif
        }

        private void Dispose(bool disposing) {
            if (disposing) {
                var worker = _worker;
                if (worker != null) {
                    Log("Sending abort... {0}", DateTime.Now);
                    lock (_workerQueue) {
                        _workerQueue.Clear();
                        _workerQueue.Enqueue(WorkerMessage.Abort());
                        _workerQueueChanged.Set();
                    }
                    Log("Waiting for abort... {0}", DateTime.Now);
                    bool stopped = worker.Join(10000);
                    Log("Done Waiting for abort... {0} {1}", DateTime.Now, stopped);
                    Debug.Assert(stopped, "Failed to terminate TaskProvider worker thread");
                }

                lock (_itemsLock) {
                    _items.Clear();
                }
                if (_taskList != null) {
                    _taskList.UnregisterTaskProvider(_cookie);
                }

                _workerQueueChanged.Dispose();
            }
        }

        ~TaskProvider() {
            Dispose(false);
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
            Clear(filePath, moniker);
            lock (_errorSources) {
                var key = new EntryKey(filePath, moniker);
                HashSet<ITextBuffer> buffers;
                if (_errorSources.TryGetValue(key, out buffers)) {
                    buffers.Remove(buffer);
                }
            }
        }

        /// <summary>
        /// Clears all tracked buffers for the given project entry and moniker for
        /// the error source.
        /// </summary>
        public void ClearErrorSource(string filePath, string moniker) {
            Clear(filePath, moniker);
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
            for (;;) {
                var self = (Thread)param;
                if (Interlocked.CompareExchange(ref _worker, self, null) != null) {
                    // Not us, so abort
                    return;
                }

                try {
                    WorkerWorker();
                } catch (OperationCanceledException) {
                    Log("Operation cancellled... {0}", DateTime.Now);

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
                        List<TaskProviderItem> items;
                        buffers.UnionWith(kv.Value);

                        lock (_itemsLock) {
                            if (!_items.TryGetValue(kv.Key, out items)) {
                                continue;
                            }

                            foreach (var item in items) {
                                if (item.IsValid && item.TextBuffer != null) {
                                    List<TaskProviderItem> itemList;
                                    if (!bufferToErrorList.TryGetValue(item.TextBuffer, out itemList)) {
                                        bufferToErrorList[item.TextBuffer] = itemList = new List<TaskProviderItem>();
                                    }

                                    itemList.Add(item);
                                }
                            }
                        }
                    }
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

                if (_errorProvider != null) {
                    foreach (var kv in bufferToErrorList) {
                        var tagger = _errorProvider.GetErrorTagger(kv.Key);
                        if (tagger == null) {
                            continue;
                        }

                        if (buffers.Remove(kv.Key)) {
                            tagger.RemoveTagSpans(span => span.Span.TextBuffer == kv.Key);
                        }

                        foreach (var taskProviderItem in kv.Value) {
                            taskProviderItem.CreateSquiggleSpan(tagger);
                        }
                    }

                    if (buffers.Any()) {
                        // Clear tags for any remaining buffers.
                        foreach (var buffer in buffers) {
                            var tagger = _errorProvider.GetErrorTagger(buffer);
                            tagger.RemoveTagSpans(span => span.Span.TextBuffer == buffer);
                        }
                    }
                }
            }, cancellationToken);
        }

        private void SendMessage(WorkerMessage message) {
            lock (_workerQueue) {
                _workerQueue.Enqueue(message);
                _workerQueueChanged.Set();
            }

            StartWorker();
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

    class ErrorTaskItem : IVsTaskItem {
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
        public string SourceFile { get; set; }
        public VSTASKCATEGORY Category { get; set; }
        public VSTASKPRIORITY Priority { get; set; }
        public bool CanDelete { get; set; }
        public bool IsChecked { get; set; }

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

        int IVsTaskItem.Column(out int piCol) {
            if (Span.Start.Line == 1 && Span.Start.Column == 1 && Span.Start.Index != 0) {
                // we don't have the column number calculated
                piCol = 0;
                return VSConstants.E_FAIL;
            }
            piCol = Span.Start.Column - 1;
            return VSConstants.S_OK;
        }

        int IVsTaskItem.Document(out string pbstrMkDocument) {
            pbstrMkDocument = SourceFile;
            return VSConstants.S_OK;
        }

        int IVsTaskItem.HasHelp(out int pfHasHelp) {
            pfHasHelp = 0;
            return VSConstants.S_OK;
        }

        int IVsTaskItem.ImageListIndex(out int pIndex) {
            pIndex = 0;
            return VSConstants.E_NOTIMPL;
        }

        int IVsTaskItem.IsReadOnly(VSTASKFIELD field, out int pfReadOnly) {
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

        int IVsTaskItem.Line(out int piLine) {
            if (Span.Start.Line == 1 && Span.Start.Column == 1 && Span.Start.Index != 0) {
                // we don't have the line number calculated
                piLine = 0;
                return VSConstants.E_FAIL;
            }
            piLine = Span.Start.Line - 1;
            return VSConstants.S_OK;
        }

        int IVsTaskItem.NavigateTo() {
            try {
                if (Span.Start.Line == 1 && Span.Start.Column == 1 && Span.Start.Index != 0) {
                    // we have just an absolute index, use that to naviagte
                    PythonToolsPackage.NavigateTo(_serviceProvider, SourceFile, Guid.Empty, Span.Start.Index);
                } else {
                    PythonToolsPackage.NavigateTo(_serviceProvider, SourceFile, Guid.Empty, Span.Start.Line - 1, Span.Start.Column - 1);
                }
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

        int IVsTaskItem.NavigateToHelp() {
            return VSConstants.E_NOTIMPL;
        }

        int IVsTaskItem.OnDeleteTask() {
            return VSConstants.E_NOTIMPL;
        }

        int IVsTaskItem.OnFilterTask(int fVisible) {
            return VSConstants.E_NOTIMPL;
        }

        int IVsTaskItem.SubcategoryIndex(out int pIndex) {
            pIndex = 0;
            return VSConstants.E_NOTIMPL;
        }

        int IVsTaskItem.get_Checked(out int pfChecked) {
            pfChecked = IsChecked ? 1 : 0;
            return VSConstants.S_OK;
        }

        int IVsTaskItem.get_Priority(VSTASKPRIORITY[] ptpPriority) {
            ptpPriority[0] = Priority;
            return VSConstants.S_OK;
        }

        int IVsTaskItem.get_Text(out string pbstrName) {
            pbstrName = Message;
            return VSConstants.S_OK;
        }

        int IVsTaskItem.put_Checked(int fChecked) {
            if (IsCheckedIsReadOnly) {
                return VSConstants.E_NOTIMPL;
            }
            IsChecked = (fChecked != 0);
            return VSConstants.S_OK;
        }

        int IVsTaskItem.put_Priority(VSTASKPRIORITY tpPriority) {
            if (PriorityIsReadOnly) {
                return VSConstants.E_NOTIMPL;
            }
            Priority = tpPriority;
            return VSConstants.S_OK;
        }

        int IVsTaskItem.put_Text(string bstrName) {
            if (MessageIsReadOnly) {
                return VSConstants.E_NOTIMPL;
            }
            Message = bstrName;
            return VSConstants.S_OK;
        }
    }

    sealed class ErrorTaskProvider : TaskProvider {
        internal ErrorTaskProvider(IServiceProvider serviceProvider, IVsTaskList taskList, IErrorProviderFactory errorProvider)
            : base(serviceProvider, taskList, errorProvider) {
        }

        public static object CreateService(IServiceProvider container, Type serviceType) {
            if (serviceType.IsEquivalentTo(typeof(ErrorTaskProvider))) {
                var errorList = container.GetService(typeof(SVsErrorList)) as IVsTaskList;
                var model = container.GetComponentModel();
                var errorProvider = model != null ? model.GetService<IErrorProviderFactory>() : null;
                return new ErrorTaskProvider(container, errorList, errorProvider);
            }
            return null;
        }
    }

    sealed class CommentTaskProvider : TaskProvider, IVsTaskListEvents {
        private volatile Dictionary<string, VSTASKPRIORITY> _tokens;

        internal CommentTaskProvider(IServiceProvider serviceProvider, IVsTaskList taskList, IErrorProviderFactory errorProvider)
            : base(serviceProvider, taskList, errorProvider) {
            RefreshTokens();
        }

        public static object CreateService(IServiceProvider container, Type serviceType) {
            if (serviceType.IsEquivalentTo(typeof(CommentTaskProvider))) {
                var errorList = container.GetService(typeof(SVsTaskList)) as IVsTaskList;
                var model = container.GetComponentModel();
                var errorProvider = model != null ? model.GetService<IErrorProviderFactory>() : null;
                return new CommentTaskProvider(container, errorList, errorProvider);
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

            var tokensChanged = TokensChanged;
            if (tokensChanged != null) {
                tokensChanged(this, EventArgs.Empty);
            }
        }
    }
}
