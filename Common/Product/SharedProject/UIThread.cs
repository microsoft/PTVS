// Visual Studio Shared Project
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
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudioTools {
    class UIThread : UIThreadBase {
        private readonly TaskScheduler _scheduler;
        private readonly TaskFactory _factory;
        private readonly Thread _uiThread;

        private UIThread() {
            _scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            _factory = new TaskFactory(_scheduler);
            _uiThread = Thread.CurrentThread;
        }

        public static void EnsureService(IServiceContainer container) {
            if (container.GetService(typeof(UIThreadBase)) == null) {
                container.AddService(typeof(UIThreadBase), new UIThread(), true);
            }
        }

        public override bool InvokeRequired {
            get {
                return Thread.CurrentThread != _uiThread;
            }
        }

        public override void MustBeCalledFromUIThreadOrThrow() {
            if (InvokeRequired) {
                const int RPC_E_WRONG_THREAD = unchecked((int)0x8001010E);
                throw new COMException("Invalid cross-thread call", RPC_E_WRONG_THREAD);
            }
        }

        /// <summary>
        /// Executes the specified action on the UI thread. Returns once the
        /// action has been completed.
        /// </summary>
        /// <remarks>
        /// If called from the UI thread, the action is executed synchronously.
        /// </remarks>
        public override void Invoke(Action action) {
            if (InvokeRequired) {
                _factory.StartNew(action).GetAwaiter().GetResult();
            } else {
                action();
            }
        }

        /// <summary>
        /// Evaluates the specified function on the UI thread. Returns once the
        /// function has completed.
        /// </summary>
        /// <remarks>
        /// If called from the UI thread, the function is evaluated 
        /// synchronously.
        /// </remarks>
        public override T Invoke<T>(Func<T> func) {
            if (InvokeRequired) {
                return _factory.StartNew(func).GetAwaiter().GetResult();
            } else {
                return func();
            }
        }

        /// <summary>
        /// Executes the specified action on the UI thread. The task is
        /// completed once the action completes.
        /// </summary>
        /// <remarks>
        /// If called from the UI thread, the action is executed synchronously.
        /// </remarks>
        public override Task InvokeAsync(Action action) {
            var tcs = new TaskCompletionSource<object>();
            if (InvokeRequired) {
                return _factory.StartNew(action);
            } else {
                // Action is run synchronously, but we still return the task.
                InvokeAsyncHelper(action, tcs);
            }
            return tcs.Task;
        }

        /// <summary>
        /// Evaluates the specified function on the UI thread. The task is
        /// completed once the result is available.
        /// </summary>
        /// <remarks>
        /// If called from the UI thread, the function is evaluated 
        /// synchronously.
        /// </remarks>
        public override Task<T> InvokeAsync<T>(Func<T> func) {
            var tcs = new TaskCompletionSource<T>();
            if (InvokeRequired) {
                return _factory.StartNew(func);
            } else {
                // Function is run synchronously, but we still return the task.
                InvokeAsyncHelper(func, tcs);
            }
            return tcs.Task;
        }

        /// <summary>
        /// Awaits the provided task on the UI thread. The function will be
        /// invoked on the UI thread to ensure the correct context is captured
        /// for any await statements within the task.
        /// </summary>
        /// <remarks>
        /// If called from the UI thread, the function is evaluated 
        /// synchronously.
        /// </remarks>
        public override Task InvokeTask(Func<Task> func) {
            var tcs = new TaskCompletionSource<object>();
            if (InvokeRequired) {
                InvokeAsync(() => InvokeTaskHelper(func, tcs));
            } else {
                // Function is run synchronously, but we still return the task.
                InvokeTaskHelper(func, tcs);
            }
            return tcs.Task;
        }

        /// <summary>
        /// Awaits the provided task on the UI thread. The function will be
        /// invoked on the UI thread to ensure the correct context is captured
        /// for any await statements within the task.
        /// </summary>
        /// <remarks>
        /// If called from the UI thread, the function is evaluated 
        /// synchronously.
        /// </remarks>
        public override Task<T> InvokeTask<T>(Func<Task<T>> func) {
            var tcs = new TaskCompletionSource<T>();
            if (InvokeRequired) {
                InvokeAsync(() => InvokeTaskHelper(func, tcs));
            } else {
                // Function is run synchronously, but we still return the task.
                InvokeTaskHelper(func, tcs);
            }
            return tcs.Task;
        }

        #region Helper Functions

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

        #endregion

    }
}