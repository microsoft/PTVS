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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools {
    /// <summary>
    /// Provides the ability to run code on the VS UI thread.
    /// 
    /// UIThreadBase must be an abstract class rather than an interface because the CLR
    /// doesn't take assembly names into account when generating an interfaces GUID, resulting 
    /// in resolution issues when we reference the interface from multiple assemblies.
    /// </summary>
    public abstract class UIThreadBase {
        public abstract void Invoke(Action action);
        public abstract T Invoke<T>(Func<T> func);
        public abstract Task InvokeAsync(Action action);
        public abstract Task<T> InvokeAsync<T>(Func<T> func);
        public abstract Task InvokeAsync(Action action, CancellationToken cancellationToken);
        public abstract Task<T> InvokeAsync<T>(Func<T> func, CancellationToken cancellationToken);
        public abstract Task InvokeTask(Func<Task> func);
        public abstract Task<T> InvokeTask<T>(Func<Task<T>> func);
        public abstract void InvokeTaskSync(Func<Task> func, CancellationToken cancellationToken);
        public abstract T InvokeTaskSync<T>(Func<Task<T>> func, CancellationToken cancellationToken);
        public abstract void MustBeCalledFromUIThreadOrThrow();

        public abstract bool InvokeRequired {
            get;
        }
    }

    /// <summary>
    /// Identifies mock implementations of IUIThread.
    /// </summary>
    abstract class MockUIThreadBase : UIThreadBase {
        public override void Invoke(Action action) {
            throw new NotImplementedException();
        }

        public override T Invoke<T>(Func<T> func) {
            throw new NotImplementedException();
        }

        public override Task InvokeAsync(Action action) {
            throw new NotImplementedException();
        }

        public override Task<T> InvokeAsync<T>(Func<T> func) {
            throw new NotImplementedException();
        }

        public override Task InvokeTask(Func<Task> func) {
            throw new NotImplementedException();
        }

        public override Task<T> InvokeTask<T>(Func<Task<T>> func) {
            throw new NotImplementedException();
        }

        public override void MustBeCalledFromUIThreadOrThrow() {
            throw new NotImplementedException();
        }

        public override bool InvokeRequired {
            get { throw new NotImplementedException(); }
        }
    }

    /// <summary>
    /// Provides a no-op implementation of <see cref="UIThreadBase"/> that will
    /// not execute any tasks.
    /// </summary>
    sealed class NoOpUIThread : MockUIThreadBase {
        public override void Invoke(Action action) { }

        public override T Invoke<T>(Func<T> func) {
            return default(T);
        }

        public override Task InvokeAsync(Action action) {
            return Task.FromResult<object>(null);
        }

        public override Task<T> InvokeAsync<T>(Func<T> func) {
            return Task.FromResult<T>(default(T));
        }

        public override Task InvokeAsync(Action action, CancellationToken cancellationToken) {
            return Task.FromResult<object>(null);
        }

        public override Task<T> InvokeAsync<T>(Func<T> func, CancellationToken cancellationToken) {
            return Task.FromResult<T>(default(T));
        }

        public override Task InvokeTask(Func<Task> func) {
            return Task.FromResult<object>(null);
        }

        public override Task<T> InvokeTask<T>(Func<Task<T>> func) {
            return Task.FromResult<T>(default(T));
        }

        public override void MustBeCalledFromUIThreadOrThrow() { }

        public override void InvokeTaskSync(Func<Task> func, CancellationToken cancellationToken) {
        }

        public override T InvokeTaskSync<T>(Func<Task<T>> func, CancellationToken cancellationToken) {
            return default(T);
        }

        public override bool InvokeRequired {
            get { return false; }
        }
    }

    /// <summary>
    /// Provides extension methods useful for using with UIThread.
    /// </summary>
    static class UIThreadExtensions {
        /// <summary>
        /// Returns the <see cref="UIThreadBase"/> instance associated with
        /// the service provider. This is guaranteed to return an instance,
        /// though if no UI thread is available, the instance may simply
        /// drop all calls without executing the code.
        /// </summary>
        public static UIThreadBase GetUIThread(this IServiceProvider serviceProvider) {
            var uiThread = (UIThreadBase)serviceProvider.GetService(typeof(UIThreadBase));
            if (uiThread == null) {
                Trace.TraceWarning("Returning NoOpUIThread instance from GetUIThread");
#if DEBUG
                var shell = (IVsShell)serviceProvider.GetService(typeof(SVsShell));
                object shutdownStarted;
                if (shell != null) {
                    int hr = shell.GetProperty((int)__VSSPROPID6.VSSPROPID_ShutdownStarted, out shutdownStarted);
                    if (hr >= 0 && !(bool)shutdownStarted) {
                        Debug.Fail("No UIThread service but shell is not shutting down");

                    }
                }
#endif
                return new NoOpUIThread();
            }
            return uiThread;
        }

        [Conditional("DEBUG")]
        // Available on serviceProvider so we can avoid the GetUIThread call on release builds
        public static void MustBeCalledFromUIThread(this IServiceProvider serviceProvider, string message = null) {
            serviceProvider.GetUIThread().MustBeCalledFromUIThread(message);
        }

        [Conditional("DEBUG")]
        // Available on serviceProvider so we can avoid the GetUIThread call on release builds
        public static void MustNotBeCalledFromUIThread(this IServiceProvider serviceProvider, string message = null) {
            serviceProvider.GetUIThread().MustNotBeCalledFromUIThread(message);
        }

        [Conditional("DEBUG")]
        public static void MustBeCalledFromUIThread(this UIThreadBase self, string message = null) {
            if (self is MockUIThreadBase || !self.InvokeRequired) {
                return;
            }

            Debug.Fail(
                message ?? string.Format("Invalid cross-thread call from thread {0}", Thread.CurrentThread.ManagedThreadId),
                new StackTrace().ToString()
            );
        }

        [Conditional("DEBUG")]
        public static void MustNotBeCalledFromUIThread(this UIThreadBase self, string message = null) {
            if (self is MockUIThreadBase || self.InvokeRequired) {
                return;
            }
            
            Debug.Fail(
                message ?? string.Format("Invalid cross-thread call from thread {0}", Thread.CurrentThread.ManagedThreadId),
                new StackTrace().ToString()
            );
        }

    }
}
