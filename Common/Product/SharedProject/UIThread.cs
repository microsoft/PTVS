/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudioTools {
    /// <summary>
    /// Provides helper functions to execute code on the UI thread.
    /// </summary>
    /// <remarks>
    /// Because the UI thread is determined from the global service provider,
    /// in non-VS tests, all invocations will occur on the current thread.
    /// To specify a UI thread, call <see cref="MakeCurrentThreadUIThread"/>.
    /// </remarks>
    internal static class UIThread {
        private static ThreadHelper _invoker;
        private static readonly object _invokerLock = new object();
        private static bool _invokerSet;

        private static bool CanInvoke {
            get {
                if (!_invokerSet) {
                    lock (_invokerLock) {
                        if (!_invokerSet) {
                            _invokerSet = true;
                            try {
                                _invoker = ThreadHelper.Generic;
                                // Test the invoker to make sure it is available
                                _invoker.Invoke(() => { });
                            } catch (InvalidOperationException) {
                                // Not running within VS
                                _invoker = null;
                            }
                        }
                    }
                }

                return _invokerSet && _invoker != null;
            }
        }

        internal static bool InvokeRequired {
            get {
                // Must check CanInvoke() first or we may crash in unit tests
                return CanInvoke && !ThreadHelper.CheckAccess();
            }
        }


        public static void MustBeCalledFromUIThread(bool throwInRelease = false) {
            if (InvokeRequired) {
                Debug.Fail("Invalid cross-thread call", new StackTrace().ToString());

                if (throwInRelease) {
                    throw new COMException("Invalid cross-thread call", VSConstants.RPC_E_WRONG_THREAD);
                }
            }
        }

        /// <summary>
        /// Executes the specified action on the UI thread. Returns once the
        /// action has been completed.
        /// </summary>
        /// <remarks>
        /// If called from the UI thread, the action is executed synchronously.
        /// </remarks>
        public static void Invoke(Action action) {
            if (InvokeRequired) {
                _invoker.Invoke(action);
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
        public static T Invoke<T>(Func<T> func) {
            if (InvokeRequired) {
                return _invoker.Invoke(func);
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
        public static Task InvokeAsync(Action action) {
            var tcs = new TaskCompletionSource<object>();
            if (InvokeRequired) {
#if DEV10
                // VS 2010 does not have BeginInvoke, so use the thread pool
                // to run it asynchronously.
                ThreadPool.QueueUserWorkItem(() => {
                    _invoker.Invoke(() => InvokeAsyncHelper(action, tcs));
                });
#else
                _invoker.BeginInvoke(() => InvokeAsyncHelper(action, tcs));
#endif
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
        public static Task<T> InvokeAsync<T>(Func<T> func) {
            var tcs = new TaskCompletionSource<T>();
            if (InvokeRequired) {
#if DEV10
                // VS 2010 does not have BeginInvoke, so use the thread pool
                // to run it asynchronously.
                ThreadPool.QueueUserWorkItem(() => {
                    _invoker.Invoke(() => InvokeAsyncHelper(func, tcs));
                });
#else
                _invoker.BeginInvoke(() => InvokeAsyncHelper(func, tcs));
#endif
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
        public static Task InvokeTask(Func<Task> func) {
            var tcs = new TaskCompletionSource<object>();
            if (InvokeRequired) {
#if DEV10
                // VS 2010 does not have BeginInvoke, so use the thread pool
                // to run it asynchronously.
                ThreadPool.QueueUserWorkItem(() => {
                    _invoker.Invoke(() => InvokeTaskHelper(func, tcs));
                });
#else
                _invoker.BeginInvoke(() => InvokeTaskHelper(func, tcs));
#endif
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
        public static Task<T> InvokeTask<T>(Func<Task<T>> func) {
            var tcs = new TaskCompletionSource<T>();
            if (InvokeRequired) {
#if DEV10
                // VS 2010 does not have BeginInvoke, so use the thread pool
                // to run it asynchronously.
                ThreadPool.QueueUserWorkItem(() => {
                    _invoker.Invoke(() => InvokeTaskHelper(func, tcs));
                });
#else
                _invoker.BeginInvoke(() => InvokeTaskHelper(func, tcs));
#endif
            } else {
                // Function is run synchronously, but we still return the task.
                InvokeTaskHelper(func, tcs);
            }
            return tcs.Task;
        }

        #region Helper Functions

        private static void InvokeAsyncHelper(Action action, TaskCompletionSource<object> tcs) {
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

        private static void InvokeAsyncHelper<T>(Func<T> func, TaskCompletionSource<T> tcs) {
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

        private static async void InvokeTaskHelper(Func<Task> func, TaskCompletionSource<object> tcs) {
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

        private static async void InvokeTaskHelper<T>(Func<Task<T>> func, TaskCompletionSource<T> tcs) {
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

        #region Test Helpers

        /// <summary>
        /// Makes the current thread the UI thread. This is done by setting
        /// <see cref="ServiceProvider.GlobalProvider"/>.
        /// </summary>
        /// <param name="provider">
        /// A service provider that may be used to fulfil queries made through
        /// the global provider.
        /// </param>
        /// <remarks>
        /// This function is intended solely for testing purposes and should
        /// only be called from outside Visual Studio.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// A UI thread already exists. This may indicate that the function
        /// has been called within Visual Studio.
        /// </exception>
        internal static void MakeCurrentThreadUIThread(ServiceProvider provider = null) {
            if (ServiceProvider.GlobalProvider != null) {
                throw new InvalidOperationException("UI thread already exists");
            }
            ServiceProvider.CreateFromSetSite(new DummyOleServiceProvider(provider));
        }

        class DummyOleServiceProvider : IOleServiceProvider {
            private readonly ServiceProvider _provider;

            public DummyOleServiceProvider(ServiceProvider provider) {
                _provider = provider;
            }

            public int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject) {
                if (_provider == null) {
                    ppvObject = IntPtr.Zero;
                    return VSConstants.E_FAIL;
                }

                object service;
                int hr = _provider.QueryService(guidService, out service);
                if (ErrorHandler.Failed(hr)) {
                    ppvObject = IntPtr.Zero;
                    return hr;
                }

                ppvObject = Marshal.GetIUnknownForObject(service);
                if (riid != VSConstants.IID_IUnknown) {
                    var punk = ppvObject;
                    try {
                        Marshal.QueryInterface(punk, ref riid, out ppvObject);
                    } finally {
                        Marshal.Release(punk);
                    }
                }
                return VSConstants.S_OK;
            }
        }

        #endregion
    }
}