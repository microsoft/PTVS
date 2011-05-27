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
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Library.Intellisense {
    /// <summary>
    /// Provides a single threaded analysis queue.  Items can be enqueued into the
    /// analysis at various priorities.  
    /// </summary>
    sealed class AnalysisQueue : IDisposable {
        private readonly Thread _thread;
        private readonly AutoResetEvent _event;
        private readonly ProjectAnalyzer _analyzer;
        private readonly object _queueLock = new object();
        private readonly List<IAnalyzable>[] _queue;
        private readonly HashSet<IGroupableAnalysisProject> _enqueuedGroups = new HashSet<IGroupableAnalysisProject>();
        private volatile bool _unload;
        private bool _isAnalyzing;
        private int _analysisPending;

        private const int PriorityCount = (int)AnalysisPriority.High + 1;

        internal AnalysisQueue(ProjectAnalyzer analyzer) {
            _event = new AutoResetEvent(false);
            _analyzer = analyzer;

            _queue = new List<IAnalyzable>[PriorityCount];
            for (int i = 0; i < PriorityCount; i++) {
                _queue[i] = new List<IAnalyzable>();
            }

            _thread = new Thread(Worker);
            _thread.Name = "Python Analysis Queue";
            _thread.Priority = ThreadPriority.BelowNormal;
            _thread.IsBackground = true;
            _thread.Start();
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
                        _analyzer.QueueActivityEvent.Set();

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
                _analyzer.QueueActivityEvent.Set();
                if (priority == AnalysisPriority.High) {
                    // always try and process high pri items immediately
                    _queue[iPri].Insert(0, item);
                } else {
                    _queue[iPri].Add(item);
                }
                _event.Set();
            }
        }

        public void Stop() {
            if (_thread != null) {
                _unload = true;
                _event.Set();
            }
        }

        public bool IsAnalyzing {
            get {
                return _isAnalyzing;
            }
        }

        public int AnalysisPending {
            get {
                return _analysisPending;
            }
        }

        #region IDisposable Members

        void IDisposable.Dispose() {
            Stop();
        }

        #endregion

        private IAnalyzable GetNextItem(out AnalysisPriority priority) {
            for (int i = PriorityCount - 1; i >= 0; i--) {
                if (_queue[i].Count > 0) {
                    var res = _queue[i][0];
                    _queue[i].RemoveAt(0);
                    Interlocked.Decrement(ref _analysisPending);
                    _analyzer.QueueActivityEvent.Set();
                    priority = (AnalysisPriority)i;
                    return res;
                }
            }
            priority = AnalysisPriority.None;
            return null;
        }

        private void Worker() {
            while (!_unload) {
                IAnalyzable workItem;

                AnalysisPriority pri;
                lock (_queueLock) {
                    workItem = GetNextItem(out pri);
                }
                _isAnalyzing = true;
                if (workItem != null) {
                    var groupable = workItem as IGroupableAnalysisProjectEntry;
                    if (groupable != null) {
                        bool added = _enqueuedGroups.Add(groupable.AnalysisGroup);
                        if (added) {
                            Enqueue(new GroupAnalysis(groupable.AnalysisGroup, this), pri);
                        }

                        groupable.Analyze(true);
                    } else {
                        workItem.Analyze();
                    }
                    _isAnalyzing = false;
                } else {
                    _isAnalyzing = false;
                    _event.WaitOne();
                }
            }
        }

        sealed class GroupAnalysis : IAnalyzable {
            private readonly IGroupableAnalysisProject _project;
            private readonly AnalysisQueue _queue;

            public GroupAnalysis(IGroupableAnalysisProject project, AnalysisQueue queue) {
                _project = project;
                _queue = queue;
            }

            #region IAnalyzable Members

            public void Analyze() {
                _queue._enqueuedGroups.Remove(_project);
                _project.AnalyzeQueuedEntries();
            }

            #endregion
        }
    }
}
