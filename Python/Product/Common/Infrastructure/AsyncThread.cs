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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Common.Infrastructure {
    internal class AsyncThread {
        private Func<Task> _initialTask;
        private readonly Thread _thread;
        private ExceptionDispatchInfo _edi;

        public AsyncThread(Func<Task> initialTask) {
            _thread = new Thread(StartThread);
            _initialTask = initialTask;
        }

        public string Name { get => _thread.Name; set => _thread.Name = value; }

        public void Start() {
            var initialTask = _initialTask;
            _initialTask = null;
            if (initialTask == null) {
                throw new InvalidOperationException("Cannot start thread with no initial task");
            }
            _thread.Start(initialTask);
        }

        public void Join() {
            _thread.Join();
            _edi?.Throw();
        }

        public bool Join(int millisecondsTimeout) {
            if (_thread.Join(millisecondsTimeout)) {
                _edi?.Throw();
                return true;
            }
            return false;
        }

        private void StartThread(object o) {
            var ctxt = new AsyncThreadSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(ctxt);

            try {
                var t = ((Func<Task>)o)();
                while (!t.IsCanceled && !t.IsCompleted && !t.IsFaulted) {
                    ctxt._queueEvent.Wait();
                    ctxt._queueEvent.Reset();
                    while (ctxt._queue.TryDequeue(out var kv)) {
                        kv.Key(kv.Value);
                    }
                }
                if (t.Exception != null) {
                    _edi = ExceptionDispatchInfo.Capture(t.Exception);
                }
            } finally {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        class AsyncThreadSynchronizationContext : SynchronizationContext {
            public readonly ConcurrentQueue<KeyValuePair<SendOrPostCallback, object>> _queue;
            public readonly ManualResetEventSlim _queueEvent;

            public AsyncThreadSynchronizationContext() {
                _queue = new ConcurrentQueue<KeyValuePair<SendOrPostCallback, object>>();
                _queueEvent = new ManualResetEventSlim();
            }

            public override void Send(SendOrPostCallback d, object state) {
                if (Current == this) {
                    d(state);
                    return;
                }

                using (var e = new ManualResetEventSlim()) {
                    Post(_ => {
                        try {
                            d(state);
                        } finally {
                            try {
                                e.Set();
                            } catch (ObjectDisposedException) {
                            }
                        }
                    }, null);
                    e.Wait();
                }
            }

            public override void Post(SendOrPostCallback d, object state) {
                _queue.Enqueue(new KeyValuePair<SendOrPostCallback, object>(d, state));
                _queueEvent.Set();
            }
        }
    }
}
