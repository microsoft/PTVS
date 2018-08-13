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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides a single threaded analysis queue.  Items can be enqueued into the
    /// analysis at various priorities.  
    /// </summary>
    internal sealed class AnalysisQueue : IDisposable {
        private static readonly AsyncLocal<AnalysisQueue> _current = new AsyncLocal<AnalysisQueue>();
        public static AnalysisQueue Current => _current.Value;

        private readonly HashSet<IGroupableAnalysisProject> _enqueuedGroups;
        private readonly PriorityProducerConsumer<QueueItem> _ppc;
        private readonly Task _consumerTask;

        public event EventHandler AnalysisStarted;
        public event EventHandler AnalysisComplete;
        public event EventHandler AnalysisAborted;
        public event EventHandler<UnhandledExceptionEventArgs> UnhandledException;

        public int Count => _ppc.Count;

        internal AnalysisQueue() {
            _ppc = new PriorityProducerConsumer<QueueItem>(4, excludeDuplicates: true, comparer: QueueItemComparer.Instance);
            _enqueuedGroups = new HashSet<IGroupableAnalysisProject>();
            _consumerTask = Task.Run(ConsumerLoop);
        }

        private void Enqueue(object key, Func<CancellationToken, Task> handler, AnalysisPriority priority)
            => _ppc.Produce(new QueueItem(key, handler), (int)priority);

        private async Task ConsumerLoop() {
            RaiseEventOnThreadPool(AnalysisStarted);
            while (!_ppc.IsDisposed) {
                try {
                    var item = await ConsumeAsync();
                    _current.Value = this;
                    await item.Handler(_ppc.CancellationToken);
                    _current.Value = null;
                } catch (OperationCanceledException) when (_ppc.IsDisposed)  {
                    return;
                } catch (Exception ex) when (!ex.IsCriticalException())  {
                    UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(ex, false));
                    Dispose();
                }
            }
        }

        private async Task<QueueItem> ConsumeAsync() {
            var task = _ppc.ConsumeAsync(_ppc.CancellationToken);
            if (!task.IsCompleted) {
                await Task.WhenAny(task, Task.Delay(50));
                if (!task.IsCompleted) {
                    RaiseEventOnThreadPool(AnalysisComplete);
                    var result = await task;
                    RaiseEventOnThreadPool(AnalysisStarted);
                    return result;
                }
            }

            return await task;
        }

        private void RaiseEventOnThreadPool(EventHandler handler) {
            if (handler != null) {
                ThreadPool.QueueUserWorkItem(_ => handler(this, EventArgs.Empty));
            }
        }

        /// <summary>
        /// Queues the specified work to run in analysis queue.
        /// Exceptions are rethrown to the caller and don't affect queue processing loop.
        /// </summary>
        /// <param name="function">The work to execute</param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public Task ExecuteInQueueAsync(Func<CancellationToken, Task> function, AnalysisPriority priority) {
            // If we are inside the queue already, simply call the function
            if (Current == this) {
                return function(_ppc.CancellationToken);
            }

            var item = new ExecuteInQueueAsyncItem<bool>(function);
            Enqueue(item, item.Handler, priority);
            return item.Task;
        }

        /// <summary>
        /// Queues the specified work to run in analysis queue.
        /// Exceptions are rethrown to the caller and don't affect queue processing loop.
        /// </summary>
        /// <param name="function">The work to execute</param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public Task<T> ExecuteInQueueAsync<T>(Func<CancellationToken, Task<T>> function, AnalysisPriority priority) {
            // If we are inside the queue already, simply call the function
            if (Current == this) {
                return function(_ppc.CancellationToken);
            }

            var item = new ExecuteInQueueAsyncItem<T>(function);
            Enqueue(item, item.Handler, priority);
            return item.Task;
        }

        public Task WaitForCompleteAsync() => ExecuteInQueueAsync(ct => Task.CompletedTask, AnalysisPriority.None);

        public void Enqueue(IAnalyzable item, AnalysisPriority priority) {
            Enqueue(item, ct => HandleAnalyzable(item, priority, ct), priority);
        }

        private async Task HandleAnalyzable(IAnalyzable item, AnalysisPriority priority, CancellationToken cancellationToken) {
            if (item is IGroupableAnalysisProjectEntry groupable) {
                var added = _enqueuedGroups.Add(groupable.AnalysisGroup);
                if (added) {
                    Enqueue(new GroupAnalysis(groupable.AnalysisGroup, this), priority);
                }

                groupable.Analyze(cancellationToken, true);
            } else {
                item.Analyze(cancellationToken);
            }
        }

        public void Dispose() {
            if (!_ppc.IsDisposed) {
                _ppc.Dispose();
                RaiseEventOnThreadPool(AnalysisAborted);
            }

            if (!_consumerTask.IsCompleted) {
                if (!_consumerTask.Wait(TimeSpan.FromSeconds(5.0))) {
                    Trace.TraceWarning("Failed to wait for worker thread to terminate");
                }
            }
        }

        private struct QueueItem {
            public readonly object Key;
            public readonly Func<CancellationToken, Task> Handler;

            public QueueItem(object key, Func<CancellationToken, Task> handler) {
                Key = key;
                Handler = handler;
            }
        }

        private sealed class ExecuteInQueueAsyncItem<T> {
            private readonly Func<CancellationToken, Task> _handler;
            private readonly TaskCompletionSourceEx<T> _tcs;
            public Task<T> Task => _tcs.Task;

            public ExecuteInQueueAsyncItem(Func<CancellationToken, Task> handler) {
                _handler = handler;
                _tcs = new TaskCompletionSourceEx<T>();
            }

            public async Task Handler(CancellationToken cancellationToken) {
                try {
                    var task = _handler(cancellationToken);
                    await task;
                    var result = task is Task<T> typedTask ? typedTask.Result : default(T);
                    ThreadPool.QueueUserWorkItem(SetResult, result);
                } catch (OperationCanceledException oce) {
                    ThreadPool.QueueUserWorkItem(SetCanceled, oce);
                } catch (Exception ex) {
                    ThreadPool.QueueUserWorkItem(SetException, ex);
                }
            }

            private void SetResult(object state) => _tcs.TrySetResult((T)state);
            private void SetCanceled(object state) => _tcs.TrySetCanceled((OperationCanceledException)state);
            private void SetException(object state) => _tcs.TrySetException((Exception)state);
        }

        private sealed class QueueItemComparer : IEqualityComparer<QueueItem> {
            public static IEqualityComparer<QueueItem> Instance { get; } = new QueueItemComparer();

            private QueueItemComparer() {}
            public bool Equals(QueueItem x, QueueItem y) => Equals(x.Key, y.Key);
            public int GetHashCode(QueueItem obj) => obj.Key.GetHashCode();
        }

        private sealed class GroupAnalysis : IAnalyzable {
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
