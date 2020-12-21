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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.CookiecutterTools.Infrastructure {
    class UIThread : UIThreadBase {
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly JoinableTaskContext _joinableTaskContext;

        public UIThread(JoinableTaskFactory joinableTaskFactory) {
            _joinableTaskFactory = joinableTaskFactory;
            _joinableTaskContext = joinableTaskFactory.Context;
        }

        public override bool InvokeRequired => !_joinableTaskContext.IsOnMainThread;

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
            if (_joinableTaskContext.IsOnMainThread) {
                action();
            } else {
                _joinableTaskFactory.Run(async () => {
                    await _joinableTaskFactory.SwitchToMainThreadAsync();
                    action();
                });
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
            if (_joinableTaskContext.IsOnMainThread) {
                return func();
            }

            return _joinableTaskFactory.Run(async () => {
                await _joinableTaskFactory.SwitchToMainThreadAsync();
                return func();
            });

        }

        /// <summary>
        /// Executes the specified action on the UI thread. The task is
        /// completed once the action completes.
        /// </summary>
        /// <remarks>
        /// If called from the UI thread, the action is executed synchronously.
        /// </remarks>
        public override Task InvokeAsync(Action action) {
            if (_joinableTaskContext.IsOnMainThread) {
                // Action is run synchronously, but we still return the task.
                return Wrap(action);
            }

            return _joinableTaskFactory.RunAsync(async () => {
                await _joinableTaskFactory.SwitchToMainThreadAsync();
                action();
            }).Task;
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
            if (_joinableTaskContext.IsOnMainThread) {
                // Function is run synchronously, but we still return the task.
                return Wrap(func);
            }

            return _joinableTaskFactory.RunAsync(async () => {
                await _joinableTaskFactory.SwitchToMainThreadAsync();
                return func();
            }).Task;
        }

        /// <summary>
        /// Executes the specified action on the UI thread. The task is
        /// completed once the action completes.
        /// </summary>
        /// <remarks>
        /// If called from the UI thread, the action is executed synchronously.
        /// </remarks>
        public override Task InvokeAsync(Action action, CancellationToken cancellationToken) {
            if (!cancellationToken.CanBeCanceled) {
                // ReSharper disable once MethodSupportsCancellation
                return InvokeAsync(action);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (_joinableTaskContext.IsOnMainThread) {
                // Action is run synchronously, but we still return the task.
                return Wrap(action);
            }

            return RunAsyncOnMainThread<object>(_joinableTaskFactory, () => {
                action();
                return null;
            }, cancellationToken);
        }

        /// <summary>
        /// Evaluates the specified function on the UI thread. The task is
        /// completed once the result is available.
        /// </summary>
        /// <remarks>
        /// If called from the UI thread, the function is evaluated 
        /// synchronously.
        /// </remarks>
        public override Task<T> InvokeAsync<T>(Func<T> func, CancellationToken cancellationToken) {
            if (!cancellationToken.CanBeCanceled) {
                // ReSharper disable once MethodSupportsCancellation
                return InvokeAsync(func);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (_joinableTaskContext.IsOnMainThread) {
                // Action is run synchronously, but we still return the task.
                return Wrap(func);
            }

            return RunAsyncOnMainThread(_joinableTaskFactory, func, cancellationToken);
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
            if (InvokeRequired) {
                return _joinableTaskFactory.RunAsync(async () => {
                    await _joinableTaskFactory.SwitchToMainThreadAsync();
                    await func();
                }).Task;
            }

            // Function is run synchronously, but we still return the task.
            return func();
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
            if (InvokeRequired) {
                return _joinableTaskFactory.RunAsync(async () => {
                    await _joinableTaskFactory.SwitchToMainThreadAsync();
                    return await func();
                }).Task;
            }

            // Function is run synchronously, but we still return the task.
            return func();
        }

        #region Helper Functions

        private static Task Wrap(Action action) {
            try {
                action();
                return Task.CompletedTask;
            } catch (OperationCanceledException oce) {
                return Task.FromCanceled(oce.CancellationToken);
            } catch (Exception ex) {
                return Task.FromException(ex);
            }
        }

        private static Task<T> Wrap<T>(Func<T> func) {
            try {
                return Task.FromResult(func());
            } catch (OperationCanceledException oce) {
                return Task.FromCanceled<T>(oce.CancellationToken);
            } catch (Exception ex) {
                return Task.FromException<T>(ex);
            }
        }

        private static Task<T> RunAsyncOnMainThread<T>(JoinableTaskFactory joinableTaskFactory, Func<T> func, CancellationToken cancellationToken) {
            var tcs = new TaskCompletionSource<T>();
            tcs.RegisterForCancellation(cancellationToken).UnregisterOnCompletion(tcs.Task);

            joinableTaskFactory.RunAsync(async () => {
                try {
                    await joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                    tcs.TrySetResult(func());
                } catch (OperationCanceledException oce) {
                    tcs.TrySetCanceled(oce.CancellationToken);
                } catch (Exception ex) {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }

        #endregion

    }
}