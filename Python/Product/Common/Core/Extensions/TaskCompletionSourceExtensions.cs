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

namespace Microsoft.PythonTools.Common.Core.Extensions {
    public static class TaskCompletionSourceExtensions {
        private const int no_delay = -1;

        public static CancellationTokenRegistration RegisterForCancellation<T>(this TaskCompletionSource<T> taskCompletionSource, CancellationToken cancellationToken) 
            => taskCompletionSource.RegisterForCancellation(millisecondsDelay: no_delay, cancellationToken);

        public static CancellationTokenRegistration RegisterForCancellation<T>(this TaskCompletionSource<T> taskCompletionSource, int millisecondsDelay, CancellationToken cancellationToken) {
            if (millisecondsDelay >= 0) {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(millisecondsDelay);
                cancellationToken = cts.Token;
            }

            var action = new CancelOnTokenAction<T>(taskCompletionSource, cancellationToken);
            return cancellationToken.Register(action.Invoke);
        }

        private struct CancelOnTokenAction<T> {
            private readonly TaskCompletionSource<T> _taskCompletionSource;
            private readonly CancellationToken _cancellationToken;

            public CancelOnTokenAction(TaskCompletionSource<T> taskCompletionSource, CancellationToken cancellationToken) {
                _taskCompletionSource = taskCompletionSource;
                _cancellationToken = cancellationToken;
            }

            public void Invoke() {
                if (!_taskCompletionSource.Task.IsCompleted) {
                    ThreadPool.QueueUserWorkItem(TryCancel);
                }
            }

            private void TryCancel(object state) => _taskCompletionSource.TrySetCanceled(_cancellationToken);
        }
    }
}
