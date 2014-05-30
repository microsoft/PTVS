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
#if DEV10
        private static Thread _uiThread;
#endif

        private static bool? _tryToInvoke;

        /// <summary>
        /// This function is called from the UI thread as early as possible. It
        /// will mark this class as active and future calls to the invoke
        /// methods will attempt to marshal to the UI thread.
        /// </summary>
        /// <remarks>
        /// If <see cref="InitializeAndNeverInvoke"/> has already been called,
        /// this method has no effect.
        /// 
        /// If neither Initialize method is called, attempting to call any other
        /// method on this class will terminate the process immediately.
        /// </remarks>
        public static void InitializeAndAlwaysInvokeToCurrentThread() {
            if (!_tryToInvoke.HasValue) {
                _invoker = ThreadHelper.Generic;
                _tryToInvoke = true;
#if DEV10
                Debug.Assert(_uiThread == null);
                _uiThread = Thread.CurrentThread;
#endif
            }
        }

        /// <summary>
        /// This function may be called from any thread and will prevent future
        /// calls to methods on this class from trying to marshal to another
        /// thread.
        /// </summary>
        /// <remarks>
        /// If neither Initialize method is called, attempting to call any other
        /// method on this class will terminate the process immediately.
        /// </remarks>
        public static void InitializeAndNeverInvoke() {
            _tryToInvoke = false;
        }

        internal static bool InvokeRequired {
            get {
                if (!_tryToInvoke.HasValue) {
                    // One of the initialize methods needs to be called before
                    // attempting to marshal to the UI thread.
                    Debug.Fail("Neither UIThread.Initialize method has been called");
                    throw new CriticalException("Neither UIThread.Initialize method has been called");
                }

                if (_tryToInvoke == false) {
                    return false;
                }
#if DEV10
                return Thread.CurrentThread != _uiThread;
#else
                return !ThreadHelper.CheckAccess();
#endif
            }
        }

        public static void MustBeCalledFromUIThreadOrThrow() {
            if (InvokeRequired) {
                const int RPC_E_WRONG_THREAD = unchecked((int)0x8001010E);
                throw new COMException("Invalid cross-thread call", RPC_E_WRONG_THREAD);
            }
        }

        [Conditional("DEBUG")]
        public static void MustBeCalledFromUIThread() {
            Debug.Assert(!InvokeRequired, "Invalid cross-thread call");
        }

        [Conditional("DEBUG")]
        public static void MustNotBeCalledFromUIThread() {
            if (_tryToInvoke != false) {
                Debug.Assert(InvokeRequired, "Invalid UI-thread call");
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
                ThreadPool.QueueUserWorkItem(_ => {
                    _invoker.Invoke(() => InvokeAsyncHelper(action, tcs));
                });
#elif DEV11
                _invoker.BeginInvoke(() => InvokeAsyncHelper(action, tcs));
#else
                _invoker.InvokeAsync(() => InvokeAsyncHelper(action, tcs));
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
                ThreadPool.QueueUserWorkItem(_ => {
                    _invoker.Invoke(() => InvokeAsyncHelper(func, tcs));
                });
#elif DEV11
                _invoker.BeginInvoke(() => InvokeAsyncHelper(func, tcs));
#else
                _invoker.InvokeAsync(() => InvokeAsyncHelper(func, tcs));
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
                ThreadPool.QueueUserWorkItem(_ => {
                    _invoker.Invoke(() => InvokeTaskHelper(func, tcs));
                });
#elif DEV11
                _invoker.BeginInvoke(() => InvokeTaskHelper(func, tcs));
#else
                _invoker.InvokeAsync(() => InvokeTaskHelper(func, tcs));
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
                ThreadPool.QueueUserWorkItem(_ => {
                    _invoker.Invoke(() => InvokeTaskHelper(func, tcs));
                });
#elif DEV11
                _invoker.BeginInvoke(() => InvokeTaskHelper(func, tcs));
#else
                _invoker.InvokeAsync(() => InvokeTaskHelper(func, tcs));
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

                object service = _provider.GetService(guidService);
                if (service == null) {
                    ppvObject = IntPtr.Zero;
                    return VSConstants.E_FAIL;
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