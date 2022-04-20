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
using System.Threading;

namespace Microsoft.PythonTools.Common.Core.Threading {
    // https://stackoverflow.com/questions/1656404/c-sharp-producer-consumer
    // http://www.albahari.com/threading/part4.aspx#_Wait_and_Pulse
    public sealed class TaskQueue : IDisposable {
        private readonly object _lock = new object();
        private readonly Thread[] _workers;
        private readonly Queue<Action> _queue = new Queue<Action>();

        public TaskQueue(int maxWorkers) {
            _workers = new Thread[maxWorkers];
            // Create and start a separate thread for each worker
            for (var i = 0; i < _workers.Length; i++) {
                (_workers[i] = new Thread(Consume)).Start();
            }
        }

        public void Dispose() {
            // Enqueue one null task per worker to make each exit.
            foreach (var worker in _workers) {
                Enqueue(null);
            }
            foreach (var worker in _workers) {
                worker.Join();
            }
        }

        public void Enqueue(Action action, bool immediate = true) {
            lock (_lock) {
                _queue.Enqueue(action);
                if (immediate) {
                    Monitor.PulseAll(_lock);
                }
            }
        }

        public void ProcessQueue() {
            lock (_lock) {
                Monitor.PulseAll(_lock);
            }
        }

        private void Consume() {
            while (true) {
                Action action;
                lock (_lock) {
                    while (_queue.Count == 0) {
                        Monitor.Wait(_lock);
                    }
                    action = _queue.Dequeue();
                }
                if (action == null) {
                    break;
                }
                action();
            }
        }
    }
}
