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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Core.Extensions;
using Microsoft.PythonTools.Common.Core.Threading;

namespace Microsoft.Python.Core {
    public sealed class AsyncAutoResetEvent {
        private readonly Queue<(CancellationTokenRegistration, TaskCompletionSource<bool>)> _waiters = new Queue<(CancellationTokenRegistration, TaskCompletionSource<bool>)>(); 
        private bool _isSignaled;

        public Task WaitAsync(in CancellationToken cancellationToken = default) {
            TaskCompletionSource<bool> tcs;
            
            lock (_waiters) { 
                if (_isSignaled) { 
                    _isSignaled = false; 
                    return Task.CompletedTask; 
                }
                
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var ctr = cancellationToken.CanBeCanceled ? tcs.RegisterForCancellation(cancellationToken) : default;
                _waiters.Enqueue((ctr, tcs)); 
            }

            if (cancellationToken.CanBeCanceled) {
                tcs.RegisterForCancellation(cancellationToken).UnregisterOnCompletion(tcs.Task);
            }

            return tcs.Task; 
        }

        public void Set() {
            var waiterToRelease = default(TaskCompletionSource<bool>);
            var ctr = default(CancellationTokenRegistration);
            lock (_waiters) {
                while (_waiters.Count > 0) {
                    (ctr, waiterToRelease) = _waiters.Dequeue();
                    ctr.Dispose();

                    if (!waiterToRelease.Task.IsCompleted) {
                        break;
                    }
                }

                if (!_isSignaled && (waiterToRelease == null || waiterToRelease.Task.IsCompleted)) {
                    _isSignaled = true;
                }
            }

            waiterToRelease?.TrySetResult(true);
        }
    }
}
