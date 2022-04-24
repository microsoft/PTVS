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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Common.Core.Threading {
    public class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable {
        private readonly ConcurrentQueue<(SendOrPostCallback callback, object state)> _queue = new ConcurrentQueue<(SendOrPostCallback, object)>();
        private readonly ManualResetEventSlim _workAvailable = new ManualResetEventSlim(false);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public SingleThreadSynchronizationContext() {
            Task.Run(() => QueueWorker());
        }

        public override void Post(SendOrPostCallback d, object state) {
            _queue.Enqueue((d, state));
            _workAvailable.Set();
        }

        public void Dispose() => _cts.Cancel();

        private void QueueWorker() {
            while (true) {
                _workAvailable.Wait(_cts.Token);
                if (_cts.IsCancellationRequested) {
                    break;
                }
                while (_queue.TryDequeue(out var entry)) {
                    entry.callback(entry.state);
                }

                _workAvailable.Reset();
            }
        }
    }
}
