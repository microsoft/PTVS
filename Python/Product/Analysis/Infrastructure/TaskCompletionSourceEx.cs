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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    internal sealed class TaskCompletionSourceEx<TResult> {
        private readonly AsyncTaskMethodBuilder<TResult> _atmb;
        private int _completed;

        public Task<TResult> Task { get; }

        public TaskCompletionSourceEx() {
            _atmb = AsyncTaskMethodBuilder<TResult>.Create();
            Task = _atmb.Task;
        }

        public bool TrySetResult(TResult result) {
            if (Task.IsCompleted) {
                return false;
            }

            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0) {
                _atmb.SetResult(result);
                return true;
            }

            SpinUntilCompleted();
            return false;
        }

        public bool TrySetCanceled(OperationCanceledException exception = null, CancellationToken cancellationToken = default(CancellationToken)) {
            if (Task.IsCompleted) {
                return false;
            }

            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0) {
                exception = exception ?? new OperationCanceledException(cancellationToken);
                _atmb.SetException(exception);
                return true;
            }

            SpinUntilCompleted();
            return false;
        }

        public bool TrySetException(Exception exception) {
            if (exception == null) {
                throw new ArgumentNullException(nameof(exception));
            }

            if (exception is OperationCanceledException) {
                throw new ArgumentOutOfRangeException(nameof(exception), Invariant($"Use {nameof(TrySetCanceled)} to cancel task"));
            }

            if (Task.IsCompleted) {
                return false;
            }
            
            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0) {
                _atmb.SetException(exception);
                return true;
            }

            SpinUntilCompleted();
            return false;
        }

        private void SpinUntilCompleted() {
            if (Task.IsCompleted) {
                return;
            }

            var sw = new SpinWait();
            while (!Task.IsCompleted) {
                sw.SpinOnce();
            }
        }

        public void SetResult(TResult result) {
            if (!TrySetResult(result)) {
                throw new InvalidOperationException("Task already completed");
            }
        }

        public void SetCanceled(OperationCanceledException exception = null, CancellationToken cancellationToken = default(CancellationToken)) {
            if (!TrySetCanceled(exception, cancellationToken)) {
                throw new InvalidOperationException("Task already completed");
            }
        }

        public void SetException(Exception exception) {
            if (!TrySetException(exception)) {
                throw new InvalidOperationException("Task already completed");
            }
        }
    }
}