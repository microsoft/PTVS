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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    sealed class VolatileCounter {
        private int _count;
        private TaskCompletionSource<int> _waitForChange;
        private TaskCompletionSource<object> _waitForZero;

        public int Count => Volatile.Read(ref _count);

        public bool IsZero => Count == 0;

        private sealed class DecrementOnDispose : IDisposable {
            public VolatileCounter _self;
            public void Dispose() => Interlocked.Exchange(ref _self, null)?.Decrement();
        }

        public IDisposable Incremented() {
            Increment();
            return new DecrementOnDispose { _self = this };
        }

        /// <summary>
        /// Increments the counter and returns true if it changes to non-zero.
        /// </summary>
        public bool Increment() {
            int newCount = Interlocked.Increment(ref _count);
            Interlocked.Exchange(ref _waitForChange, null)?.SetResult(newCount);
            return newCount == 1;
        }

        /// <summary>
        /// Decrements the counter and returns true if it changes to zero.
        /// </summary>
        public bool Decrement() {
            int origCount, newCount;
            do {
                origCount = Volatile.Read(ref _count);
                newCount = origCount - 1;
                if (newCount < 0) {
                    Debug.Fail("mismatched decrement");
                    return false;
                }
            } while (Interlocked.CompareExchange(ref _count, newCount, origCount) != origCount);

            Interlocked.Exchange(ref _waitForChange, null)?.SetResult(newCount);
            if (newCount > 0) {
                return false;
            }
            Interlocked.Exchange(ref _waitForZero, null)?.SetResult(null);
            return true;
        }

        public Task<int> WaitForChangeAsync() {
            var tcs = Volatile.Read(ref _waitForChange);
            if (tcs == null) {
                tcs = new TaskCompletionSource<int>();
                tcs = Interlocked.CompareExchange(ref _waitForChange, tcs, null) ?? tcs;
            }
            return tcs.Task;
        }

        public Task WaitForZeroAsync() {
            if (IsZero) {
                return Task.CompletedTask;
            }
            var tcs = Volatile.Read(ref _waitForZero);
            if (tcs == null) {
                tcs = new TaskCompletionSource<object>();
                tcs = Interlocked.CompareExchange(ref _waitForZero, tcs, null) ?? tcs;
            }
            if (IsZero) {
                tcs.TrySetResult(null);
            }
            return tcs.Task;
        }

        public async Task WaitForChangeToZeroAsync() {
            var tcs = Volatile.Read(ref _waitForZero);
            if (tcs == null) {
                tcs = new TaskCompletionSource<object>();
                tcs = Interlocked.CompareExchange(ref _waitForZero, tcs, null) ?? tcs;
            }
            await tcs.Task;
        }
    }
}
