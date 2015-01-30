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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides a single threaded analysis queue.  Items can be enqueued into the
    /// analysis at various priorities.  
    /// </summary>
    sealed class AnalysisQueue : IDisposable {
        private readonly Thread _workThread;
        private readonly AutoResetEvent _workEvent;
        private readonly VsProjectAnalyzer _analyzer;
        private readonly object _queueLock = new object();
        private readonly List<IAnalyzable>[] _queue;
        private readonly HashSet<IGroupableAnalysisProject> _enqueuedGroups = new HashSet<IGroupableAnalysisProject>();
        private TaskScheduler _scheduler;
        private CancellationTokenSource _cancel;
        private bool _isAnalyzing;
        private int _analysisPending;

        private const int PriorityCount = (int)AnalysisPriority.High + 1;

        internal AnalysisQueue(VsProjectAnalyzer analyzer) {
            _workEvent = new AutoResetEvent(false);
            _cancel = new CancellationTokenSource();
            _analyzer = analyzer;

            _queue = new List<IAnalyzable>[PriorityCount];
            for (int i = 0; i < PriorityCount; i++) {
                _queue[i] = new List<IAnalyzable>();
            }

            _workThread = new Thread(Worker);
            _workThread.Name = "Python Analysis Queue";
            _workThread.Priority = ThreadPriority.BelowNormal;
            _workThread.IsBackground = true;
            
            // start the thread, wait for our synchronization context to be created
            using (AutoResetEvent threadStarted = new AutoResetEvent(false)) {
                _workThread.Start(threadStarted);
                threadStarted.WaitOne();
            }
        }

        public TaskScheduler Scheduler {
            get {
                return _scheduler;
            }
        }

        public void Enqueue(IAnalyzable item, AnalysisPriority priority) {
            int iPri = (int)priority;

            if (iPri < 0 || iPri > _queue.Length) {
                throw new ArgumentException("priority");
            }

            lock (_queueLock) {
                // see if we have the item in the queue anywhere...
                for (int i = 0; i < _queue.Length; i++) {
                    if (_queue[i].Remove(item)) {
                        Interlocked.Decrement(ref _analysisPending);

                        AnalysisPriority oldPri = (AnalysisPriority)i;

                        if (oldPri > priority) {
                            // if it was at a higher priority then our current
                            // priority go ahead and raise the new entry to our
                            // old priority
                            priority = oldPri;
                        }

                        break;
                    }
                }

                // enqueue the work item
                Interlocked.Increment(ref _analysisPending);
                if (priority == AnalysisPriority.High) {
                    // always try and process high pri items immediately
                    _queue[iPri].Insert(0, item);
                } else {
                    _queue[iPri].Add(item);
                }
                try {
                    _workEvent.Set();
                } catch (ObjectDisposedException) {
                    // Queue was closed while we were running
                }
            }
        }

        public void Stop() {
            try {
                _cancel.Cancel();
            } catch (ObjectDisposedException) {
            }
            if (_workThread.IsAlive) {
                try {
                    _workEvent.Set();
                } catch (ObjectDisposedException) {
                }
                if (!_workThread.Join(TimeSpan.FromSeconds(5.0))) {
                    Trace.TraceWarning("Failed to wait for worker thread to terminate");
                }
            }
        }

        public bool IsAnalyzing {
            get {
                lock (_queueLock) {
                    return _isAnalyzing || _analysisPending > 0;
                }
            }
        }

        public int AnalysisPending {
            get {
                return _analysisPending;
            }
        }

        #region IDisposable Members

        public void Dispose() {
            Stop();
            _workEvent.Dispose();
            _cancel.Dispose();
        }

        #endregion

        private IAnalyzable GetNextItem(out AnalysisPriority priority) {
            for (int i = PriorityCount - 1; i >= 0; i--) {
                if (_queue[i].Count > 0) {
                    var res = _queue[i][0];
                    _queue[i].RemoveAt(0);
                    Interlocked.Decrement(ref _analysisPending);
                    priority = (AnalysisPriority)i;
                    return res;
                }
            }
            priority = AnalysisPriority.None;
            return null;
        }

        private void Worker(object threadStarted) {
            try {
                SynchronizationContext.SetSynchronizationContext(new AnalysisSynchronizationContext(this));
                _scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            } finally {
                ((AutoResetEvent)threadStarted).Set();
            }

            while (!_cancel.IsCancellationRequested) {
                IAnalyzable workItem;

                AnalysisPriority pri;
                lock (_queueLock) {
                    workItem = GetNextItem(out pri);
                    _isAnalyzing = true;
                }
                if (workItem != null) {
                    var groupable = workItem as IGroupableAnalysisProjectEntry;
                    if (groupable != null) {
                        bool added = _enqueuedGroups.Add(groupable.AnalysisGroup);
                        if (added) {
                            Enqueue(new GroupAnalysis(groupable.AnalysisGroup, this), pri);
                        }

                        groupable.Analyze(_cancel.Token, true);
                    } else {
                        workItem.Analyze(_cancel.Token);
                    }
                } else {
                    _isAnalyzing = false;
                    WaitHandle.SignalAndWait(
                        _analyzer.QueueActivityEvent,
                        _workEvent
                    );
                }   
            }
            _isAnalyzing = false;
        }

        sealed class GroupAnalysis : IAnalyzable {
            private readonly IGroupableAnalysisProject _project;
            private readonly AnalysisQueue _queue;

            public GroupAnalysis(IGroupableAnalysisProject project, AnalysisQueue queue) {
                _project = project;
                _queue = queue;
            }

            #region IAnalyzable Members

            public void Analyze(CancellationToken cancel) {
                _queue._enqueuedGroups.Remove(_project);
                _project.AnalyzeQueuedEntries(cancel);
            }

            #endregion
        }
    }
}
