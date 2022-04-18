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

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Core {
    public sealed class AsyncManualResetEvent {
        private TaskCompletionSource<bool> _tcs;
        public Task WaitAsync() => _tcs.Task;
        public void Set() => _tcs.TrySetResult(true);

        public Task WaitAsync(CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return Task.FromCanceled(cancellationToken);
            }

            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(CancelTcs, tcs);
            _tcs.Task.ContinueWith(WaitContinuation, tcs, TaskContinuationOptions.ExecuteSynchronously);
            return tcs.Task;
        }

        private void WaitContinuation(Task<bool> task, object state) {
            var tcs = (TaskCompletionSource<bool>)state;
            switch (task.Status) {
                case TaskStatus.Faulted:
                    tcs.TrySetException(task.Exception);
                    break;
                case TaskStatus.Canceled:
                    tcs.TrySetCanceled();
                    break;
                case TaskStatus.RanToCompletion:
                    tcs.TrySetResult(task.Result);
                    break;
            }
        }

        public AsyncManualResetEvent() {
            _tcs = new TaskCompletionSource<bool>();
        }

        public void Reset() {
            while (true) {
                var tcs = _tcs;
                if (!tcs.Task.IsCompleted) {
                    return;
                }

                if (Interlocked.CompareExchange(ref _tcs, new TaskCompletionSource<bool>(), tcs) == tcs) {
                    return;
                }
            }
        }

        private static void CancelTcs(object obj) {
            var tcs = (TaskCompletionSource<bool>)obj;
            tcs.TrySetCanceled();
        }
    }
}
