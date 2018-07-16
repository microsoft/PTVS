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

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    internal sealed class PriorityProducerConsumer<T> : IDisposable {
        private readonly int _maxPriority;
        private readonly object _syncObj;
        private readonly List<T>[] _queues;
        private readonly Queue<Pending> _pendingTasks;
        private readonly DisposeToken _disposeToken;
        private readonly bool _excludeDuplicates;
        private readonly IEqualityComparer<T> _comparer;
        private int _firstAvailablePriority;

        public bool IsDisposed => _disposeToken.IsDisposed;
        public CancellationToken CancellationToken => _disposeToken.CancellationToken;

        public int Count {
            get {
                lock (_syncObj) {
                    return _queues.Sum(queue => queue.Count);
                }
            }
        }

        public PriorityProducerConsumer(int maxPriority = 1, bool excludeDuplicates = false, IEqualityComparer<T> comparer = null) {
            _maxPriority = maxPriority;
            _queues = new List<T>[maxPriority];
            for (var i = 0; i < _queues.Length; i++) {
                _queues[i] = new List<T>();
            }

            _syncObj = new object();
            _pendingTasks = new Queue<Pending>();
            _firstAvailablePriority = _maxPriority;
            _disposeToken = DisposeToken.Create<PriorityProducerConsumer<T>>();
            _excludeDuplicates = excludeDuplicates;
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public void Dispose() => _disposeToken.TryMarkDisposed();

        public void Produce(T value, int priority = 0) {
            if (priority < 0 || priority >= _maxPriority) throw new ArgumentOutOfRangeException(nameof(priority));
            _disposeToken.ThrowIfDisposed();

            TaskCompletionSource<T> pendingTcs = null;
            lock (_syncObj) {
                Debug.Assert(_pendingTasks.Count == 0 || _firstAvailablePriority == _maxPriority);
                if (_pendingTasks.Count > 0) {
                    pendingTcs = _pendingTasks.Dequeue().Release();
                } else {
                    if (_excludeDuplicates) {
                        RemoveExistingValue(value, ref priority);
                    }

                    _queues[priority].Add(value);
                    _firstAvailablePriority = Math.Min(_firstAvailablePriority, priority);
                }
            }

            if (pendingTcs != null) {
                ThreadPool.QueueUserWorkItem(ConsumeCallback, new ConsumeState(pendingTcs, value));
            }
        }

        private void RemoveExistingValue(T value, ref int priority) {
            lock (_syncObj) {
                for (var i = 0; i < _maxPriority; i++) {
                    var queue = _queues[i];
                    for (var j = 0; j < queue.Count; j++) {
                        // Check if value is scheduled already
                        // There can be no more than one
                        if (_comparer.Equals(queue[j], value)) {
                            priority = Math.Min(i, priority);
                            queue.RemoveAt(j);
                            return;
                        }
                    }
                }
            }
        }

        public Task<T> ConsumeAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            if (cancellationToken.IsCancellationRequested) {
                return Task.FromCanceled<T>(cancellationToken);
            }

            if (_disposeToken.IsDisposed) {
                return Task.FromCanceled<T>(_disposeToken.CancellationToken);
            }

            Pending pending;
            TaskCompletionSource<T> pendingTcs;
            lock (_syncObj) {
                Debug.Assert(_pendingTasks.Count == 0 || _firstAvailablePriority == _maxPriority);
                if (_firstAvailablePriority < _maxPriority) {
                    var queue = _queues[_firstAvailablePriority];
                    var result = queue[0];
                    queue.RemoveAt(0);
                    if (queue.Count == 0) {
                        do {
                            _firstAvailablePriority++;
                        } while (_firstAvailablePriority < _maxPriority && _queues[_firstAvailablePriority].Count == 0);
                    }

                    return Task.FromResult(result);
                }

                pendingTcs = new TaskCompletionSource<T>();
                pending = new Pending(pendingTcs, _syncObj);
                _pendingTasks.Enqueue(pending);
            }

            RegisterCancellation(_disposeToken.CancellationToken, pending, pendingTcs);
            if (cancellationToken.CanBeCanceled) {
                RegisterCancellation(cancellationToken, pending, pendingTcs);
            }

            return pendingTcs.Task;
        }

        private static void RegisterCancellation(CancellationToken cancellationToken, Pending pending, TaskCompletionSource<T> pendingTcs) => cancellationToken
            .Register(CancelCallback, new CancelState(pending, cancellationToken), useSynchronizationContext: false)
            .UnregisterOnCompletion(pendingTcs.Task);

        private static void CancelCallback(object state) {
            var cancelState = (CancelState) state;
            cancelState.Pending.Release()?.TrySetCanceled(cancelState.CancellationToken);
        }

        private static void ConsumeCallback(object state) {
            var consumeState = (ConsumeState) state;
            consumeState.Tcs.TrySetResult(consumeState.Value);
        }

        private class ConsumeState {
            public TaskCompletionSource<T> Tcs { get; }
            public T Value { get; }

            public ConsumeState(TaskCompletionSource<T> tcs, T value) {
                Tcs = tcs;
                Value = value;
            }
        }

        private class CancelState {
            public Pending Pending { get; }
            public CancellationToken CancellationToken { get; }

            public CancelState(Pending pending, CancellationToken cancellationToken) {
                Pending = pending;
                CancellationToken = cancellationToken;
            }
        }

        private class Pending {
            private TaskCompletionSource<T> _tcs;
            private readonly object _syncObj;

            public Pending(TaskCompletionSource<T> tcs, object syncObj) {
                _tcs = tcs;
                _syncObj = syncObj;
            }

            public TaskCompletionSource<T> Release() {
                lock (_syncObj) {
                    var tcs = _tcs;
                    _tcs = null;
                    return tcs;

                }
            }
        }
    }
}