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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace TestUtilities.Mocks {
    class MockUIThread : MockUIThreadBase {
        public MockUIThread() {
        }

        public override void Invoke(Action action) {
            action();
        }

        public override T Invoke<T>(Func<T> func) {
            return func();
        }

        public override Task InvokeAsync(Action action) {
            var tcs = new TaskCompletionSource<object>();
            InvokeAsyncHelper(action, tcs);
            return tcs.Task;
        }

        public override Task<T> InvokeAsync<T>(Func<T> func) {
            var tcs = new TaskCompletionSource<T>();
            InvokeAsyncHelper<T>(func, tcs);
            return tcs.Task;
        }

        public override Task InvokeAsync(Action action, CancellationToken cancellationToken) {
            var tcs = new TaskCompletionSource<object>();
            if (cancellationToken.CanBeCanceled) {
                cancellationToken.Register(() => tcs.TrySetCanceled());
            }
            InvokeAsyncHelper(action, tcs);
            return tcs.Task;
        }

        public override Task<T> InvokeAsync<T>(Func<T> func, CancellationToken cancellationToken) {
            var tcs = new TaskCompletionSource<T>();
            if (cancellationToken.CanBeCanceled) {
                cancellationToken.Register(() => tcs.TrySetCanceled());
            }
            InvokeAsyncHelper<T>(func, tcs);
            return tcs.Task;
        }

        public override Task InvokeTask(Func<Task> func) {
            var tcs = new TaskCompletionSource<object>();
            InvokeTaskHelper(func, tcs);
            return tcs.Task;
        }

        public override Task<T> InvokeTask<T>(Func<Task<T>> func) {
            var tcs = new TaskCompletionSource<T>();
            InvokeTaskHelper<T>(func, tcs);
            return tcs.Task;
        }

        public override void MustBeCalledFromUIThreadOrThrow() {
        }

        public override bool InvokeRequired {
            get { return false; }
        }

        internal static void InvokeAsyncHelper(Action action, TaskCompletionSource<object> tcs) {
            try {
                action();
                tcs.TrySetResult(null);
            } catch (OperationCanceledException) {
                tcs.TrySetCanceled();
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                tcs.TrySetException(ex);
            }
        }

        internal static void InvokeAsyncHelper<T>(Func<T> func, TaskCompletionSource<T> tcs) {
            try {
                tcs.TrySetResult(func());
            } catch (OperationCanceledException) {
                tcs.TrySetCanceled();
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                tcs.TrySetException(ex);
            }
        }

        internal static async void InvokeTaskHelper(Func<Task> func, TaskCompletionSource<object> tcs) {
            try {
                await func();
                tcs.TrySetResult(null);
            } catch (OperationCanceledException) {
                tcs.TrySetCanceled();
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                tcs.TrySetException(ex);
            }
        }

        internal static async void InvokeTaskHelper<T>(Func<Task<T>> func, TaskCompletionSource<T> tcs) {
            try {
                tcs.TrySetResult(await func());
            } catch (OperationCanceledException) {
                tcs.TrySetCanceled();
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                tcs.TrySetException(ex);
            }
        }

        public override void InvokeTaskSync(Func<Task> func, CancellationToken cancellationToken) {
            func().WaitAndUnwrapExceptions();
        }

        public override T InvokeTaskSync<T>(Func<Task<T>> func, CancellationToken cancellationToken) {
            return func().WaitAndUnwrapExceptions();
        }
    }
}
