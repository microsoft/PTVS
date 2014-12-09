/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudioTools;
using SR = Microsoft.PythonTools.Project.SR;

namespace Microsoft.PythonTools.Intellisense {
    class TaskProviderItem {
        private readonly string _message;
        private readonly ITrackingSpan _span;
        private readonly SourceSpan _rawSpan;
        private readonly VSTASKPRIORITY _priority;
        private readonly bool _squiggle;
        private readonly ITextSnapshot _snapshot;
        private readonly IServiceProvider _serviceProvider;

        internal TaskProviderItem(
            IServiceProvider serviceProvider,
            string message,
            SourceSpan rawSpan,
            VSTASKPRIORITY priority,
            bool squiggle,
            ITextSnapshot snapshot
        ) {
            _serviceProvider = serviceProvider;
            _message = message;
            _rawSpan = rawSpan;
            _snapshot = snapshot;
            _span = snapshot != null ? CreateSpan(snapshot, rawSpan) : null;
            _rawSpan = rawSpan;
            _priority = priority;
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

        
        public bool IsValid {
            get {
                if (!_squiggle || _snapshot == null || _span == null || string.IsNullOrEmpty(ErrorType)) {
                    return false;
                }
                return true;
            }
        }

        public void CreateSquiggleSpan(SimpleTagger<ErrorTag> tagger) {
            tagger.CreateTagSpan(_span, new ErrorTag(ErrorType, _message));
        }

        public ITextSnapshot Snapshot {
            get {
                return _snapshot;
            }
        }

        public ErrorTaskItem ToErrorTaskItem(EntryKey key) {
            return new ErrorTaskItem(
                _serviceProvider,
                _rawSpan,
                _message,
                (key.Entry != null ? key.Entry.FilePath : null) ?? string.Empty
            ) { Priority = _priority };
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
        private readonly ITextSnapshot _snapshot;

        public TaskProviderItemFactory(
            ITextSnapshot snapshot
        ) {
            _snapshot = snapshot;
        }

        #region Factory Functions

        public TaskProviderItem FromParseWarning(IServiceProvider serviceProvider, ErrorResult result) {
            return new TaskProviderItem(
                serviceProvider,
                result.Message,
                result.Span,
                VSTASKPRIORITY.TP_NORMAL,
                true,
                _snapshot
            );
        }

        public TaskProviderItem FromParseError(IServiceProvider serviceProvider, ErrorResult result) {
            return new TaskProviderItem(
                serviceProvider,
                result.Message,
                result.Span,
                VSTASKPRIORITY.TP_HIGH,
                true,
                _snapshot
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
                message = SR.GetString(SR.UnresolvedModuleTooltipRefreshing, importName);
            } else {
                message = SR.GetString(SR.UnresolvedModuleTooltip, importName);
            }

            return new TaskProviderItem(
                serviceProvider,
                message,
                span,
                VSTASKPRIORITY.TP_NORMAL,
                true,
                _snapshot
            );
        }

        #endregion
    }

    struct EntryKey : IEquatable<EntryKey> {
        public IProjectEntry Entry;
        public string Moniker;

        public static readonly EntryKey Empty = new EntryKey(null, null);

        public EntryKey(IProjectEntry entry, string moniker) {
            Entry = entry;
            Moniker = moniker;
        }

        public override bool Equals(object obj) {
            return obj is EntryKey && Equals((EntryKey)obj);
        }

        public bool Equals(EntryKey other) {
            return Entry == other.Entry && Moniker == other.Moniker;
        }

        public override int GetHashCode() {
            return (Entry == null ? 0 : Entry.GetHashCode()) ^ (Moniker ?? string.Empty).GetHashCode();
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

        public static WorkerMessage Clear(IProjectEntry entry, string moniker) {
            return new ClearMessage(new EntryKey(entry, moniker));
        }

        public static WorkerMessage Replace(IProjectEntry entry, string moniker, List<TaskProviderItem> items) {
            return new ReplaceMessage(new EntryKey(entry, moniker), items);
        }

        public static WorkerMessage Append(IProjectEntry entry, string moniker, List<TaskProviderItem> items) {
            return new AppendMessage(new EntryKey(entry, moniker), items);
        }

        public static WorkerMessage Flush(TaskCompletionSource<TimeSpan> taskSource) {
            return new FlushMessage(taskSource, DateTime.Now);
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
        }

        sealed class ClearMessage : WorkerMessage {
            public ClearMessage(EntryKey key)
                : base(key, null) { }

            public override bool Apply(Dictionary<EntryKey, List<TaskProviderItem>> items, object itemsLock) {
                lock (itemsLock) {
                    if (_key.Entry != null) {
                        items.Remove(_key);
                    } else {
                        items.Clear();
                    }
                    // Always return true to ensure the refresh occurs
                    return true;
                }
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
    }

    sealed class TaskProvider : IVsTaskProvider, IDisposable {
        private readonly Dictionary<EntryKey, List<TaskProviderItem>> _items;
        private readonly Dictionary<EntryKey, HashSet<ITextBuffer>> _errorSources;
        private readonly object _itemsLock = new object();
        private readonly uint _cookie;
        private readonly IVsTaskList _errorList;
        internal readonly IErrorProviderFactory _errorProvider;

        private bool _hasWorker;
        private readonly BlockingCollection<WorkerMessage> _workerQueue;

        public TaskProvider(IVsTaskList errorList, IErrorProviderFactory errorProvider) {
            _items = new Dictionary<EntryKey, List<TaskProviderItem>>();
            _errorSources = new Dictionary<EntryKey, HashSet<ITextBuffer>>();

            _errorList = errorList;
            if (_errorList != null) {
                ErrorHandler.ThrowOnFailure(_errorList.RegisterTaskProvider(this, out _cookie));
            }
            _errorProvider = errorProvider;
            _workerQueue = new BlockingCollection<WorkerMessage>();
        }

        public void Dispose() {
            lock (_workerQueue) {
                if (_hasWorker) {
                    _hasWorker = false;
                    _workerQueue.CompleteAdding();
                } else {
                    _workerQueue.Dispose();
                }
            }
            lock (_itemsLock) {
                _items.Clear();
            }
            RefreshAsync().DoNotWait();
            if (_errorList != null) {
                _errorList.UnregisterTaskProvider(_cookie);
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
        public void ReplaceItems(IProjectEntry entry, string moniker, List<TaskProviderItem> items) {
            SendMessage(WorkerMessage.Replace(entry, moniker, items));
        }

        /// <summary>
        /// Adds items to the specified entry's existing items.
        /// </summary>
        public void AddItems(IProjectEntry entry, string moniker, List<TaskProviderItem> items) {
            SendMessage(WorkerMessage.Append(entry, moniker, items));
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
        public void Clear(IProjectEntry entry, string moniker) {
            SendMessage(WorkerMessage.Clear(entry, moniker));
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
        public void AddBufferForErrorSource(IProjectEntry entry, string moniker, ITextBuffer buffer) {
            lock (_errorSources) {
                var key = new EntryKey(entry, moniker);
                HashSet<ITextBuffer> buffers;
                if (!_errorSources.TryGetValue(key, out buffers)) {
                    _errorSources[new EntryKey(entry, moniker)] = buffers = new HashSet<ITextBuffer>();
                }
                buffers.Add(buffer);
            }
        }

        /// <summary>
        /// Removes the buffer from tracking for reporting squiggles and error list entries
        /// for the given project entry and moniker for the error source.
        /// </summary>
        public void RemoveBufferForErrorSource(IProjectEntry entry, string moniker, ITextBuffer buffer) {
            lock (_errorSources) {
                var key = new EntryKey(entry, moniker);
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
        public void ClearErrorSource(IProjectEntry entry, string moniker) {
            lock (_errorSources) {
                _errorSources.Remove(new EntryKey(entry, moniker));
            }
        }

        #region Internal Worker Thread

        private void Worker() {
            var flushMessages = new Queue<WorkerMessage>();
            var cts = new CancellationTokenSource();
            bool changed = false;
            var lastUpdateTime = DateTime.Now;

            try {
                // First time through, we don't want to abort the queue. There
                // should be at least one message or the worker would not have
                // been started.

                foreach (var msg in _workerQueue.GetConsumingEnumerable(cts.Token)) {
                    // Prevent timeouts while processing the message
                    cts.CancelAfter(-1);

                    if (msg is WorkerMessage.FlushMessage) {
                        // Keep flush messages until we've exited the loop
                        flushMessages.Enqueue(msg);
                    } else {
                        // Apply the message to our collection
                        changed |= msg.Apply(_items, _itemsLock);
                    }

                    // Every second, we want to force another update
                    if (changed) {
                        var currentTime = DateTime.Now;
                        if ((currentTime - lastUpdateTime).TotalMilliseconds > 1000) {
                            Refresh();
                            lastUpdateTime = currentTime;
                            changed = false;
                        }
                    }

                    // Reset the timeout back to 1 second
                    cts.CancelAfter(1000);
                }
            } catch (OperationCanceledException) {
                // Expected when the timeout expires
            } catch (ObjectDisposedException ex) {
                // We have been disposed.
                Debug.Assert(
                    ex.ObjectName == "BlockingCollection",
                    "Handled ObjectDisposedException for the wrong type"
                );
                return;
            } finally {
                lock (_workerQueue) {
                    _hasWorker = false;
                }
            }

            // Handle any changes that weren't handled in the loop
            if (changed) {
                Refresh();
            }

            // Notify all the flush messages we received
            while (flushMessages.Any()) {
                var msg = flushMessages.Dequeue();
                msg.Apply(_items, _itemsLock);
            }

            if (_workerQueue.IsCompleted) {
                _workerQueue.Dispose();
            }
        }

        private void Refresh() {
            if (_errorList != null && _errorProvider != null) {
                UIThread.MustNotBeCalledFromUIThread();
                RefreshAsync().WaitAndHandleAllExceptions(SR.ProductName, GetType());
            }
        }

        private async Task RefreshAsync() {
            var buffers = new HashSet<ITextBuffer>();
            var bufferToErrorList = new Dictionary<ITextBuffer, List<TaskProviderItem>>();

            if (_errorProvider != null) {
                lock (_errorSources) {
                    foreach (var kv in _errorSources) {
                        List<TaskProviderItem> items;
                        buffers.UnionWith(kv.Value);

                        lock (_itemsLock) {
                            if (!_items.TryGetValue(kv.Key, out items)) {
                                continue;
                            }

                            foreach (var item in items) {
                                if (item.IsValid) {
                                    List<TaskProviderItem> itemList;
                                    if (!bufferToErrorList.TryGetValue(item.Snapshot.TextBuffer, out itemList)) {
                                        bufferToErrorList[item.Snapshot.TextBuffer] = itemList = new List<TaskProviderItem>();
                                    }

                                    itemList.Add(item);
                                }
                            }
                        }
                    }
                }
            }

            await UIThread.InvokeAsync(() => {
                if (_errorList != null) {
                    try {
                        _errorList.RefreshTasks(_cookie);
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
            });
        }

        private void SendMessage(WorkerMessage message) {
            lock (_workerQueue) {
                try {
                    _workerQueue.Add(message);
                } catch (ObjectDisposedException) {
                    return;
                }
                if (!_hasWorker) {
                    _hasWorker = true;
                    Task.Run(() => Worker())
                        .HandleAllExceptions(SR.ProductName, GetType())
                        .DoNotWait();
                }
            }
        }

        #endregion

        #region IVsTaskProvider Members

        public int EnumTaskItems(out IVsEnumTaskItems ppenum) {
            lock (_itemsLock) {
                ppenum = new TaskEnum(_items
                    .Where(x => x.Key.Entry.FilePath != null)   // don't report REPL window errors in the error list, you can't naviagate to them
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
                for (var path = SourceFile; CommonUtils.IsValidPath(path); path = Path.GetDirectoryName(path)) {
                    if (!File.Exists(path)) {
                        continue;
                    }
                    var ext = Path.GetExtension(path);
                    if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ext, ".egg", StringComparison.OrdinalIgnoreCase)) {
                        MessageBox.Show(
                            "Opening source files contained in .zip archives is not supported",
                            "Cannot open file",
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
}
