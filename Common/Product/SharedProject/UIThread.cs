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

using Microsoft.VisualStudio.Threading;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudioTools
{
    class UIThread : UIThreadBase, IDisposable
    {
        private readonly JoinableTaskContext _context;
        private readonly JoinableTaskFactory _factory;
        private readonly bool _needDispose;

        public UIThread(JoinableTaskFactory joinableTaskFactory)
        {
            if (joinableTaskFactory != null)
            {
                _factory = joinableTaskFactory;
                _context = joinableTaskFactory.Context;
                Trace.TraceInformation("Using TID {0}:{1} as UI thread", _context.MainThread.ManagedThreadId, _context.MainThread.Name ?? "(null)");
            }
            else
            {
                _needDispose = true;
                _context = new JoinableTaskContext();
                Trace.TraceInformation("Setting TID {0}:{1} as UI thread", _context.MainThread.ManagedThreadId, _context.MainThread.Name ?? "(null)");
            }

            _factory = _context.Factory;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~UIThread()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_needDispose)
                {
                    _context.Dispose();
                }
            }
        }

        public override bool InvokeRequired
        {
            get
            {
                return !_context.IsOnMainThread;
            }
        }

        public override void MustBeCalledFromUIThreadOrThrow()
        {
            if (InvokeRequired)
            {
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
        public override void Invoke(Action action)
        {
            _factory.Run(async () =>
            {
                await _factory.SwitchToMainThreadAsync();
                action();
            });
        }

        /// <summary>
        /// Evaluates the specified function on the UI thread. Returns once the
        /// function has completed.
        /// </summary>
        /// <remarks>
        /// If called from the UI thread, the function is evaluated 
        /// synchronously.
        /// </remarks>
        public override T Invoke<T>(Func<T> func)
        {
            return _factory.Run(async () =>
            {
                await _factory.SwitchToMainThreadAsync();
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
        public override Task InvokeAsync(Action action)
        {
            return _factory.RunAsync(async () =>
            {
                await _factory.SwitchToMainThreadAsync();
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
        public override Task<T> InvokeAsync<T>(Func<T> func)
        {
            return _factory.RunAsync(async () =>
            {
                await _factory.SwitchToMainThreadAsync();
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
        public override Task InvokeAsync(Action action, CancellationToken cancellationToken)
        {
            return _factory.RunAsync(async () =>
            {
                await _factory.SwitchToMainThreadAsync();
                action();
            }).JoinAsync(cancellationToken);
        }

        /// <summary>
        /// Evaluates the specified function on the UI thread. The task is
        /// completed once the result is available.
        /// </summary>
        /// <remarks>
        /// If called from the UI thread, the function is evaluated 
        /// synchronously.
        /// </remarks>
        public override Task<T> InvokeAsync<T>(Func<T> func, CancellationToken cancellationToken)
        {
            return _factory.RunAsync(async () =>
            {
                await _factory.SwitchToMainThreadAsync();
                return func();
            }).JoinAsync(cancellationToken);
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
        public override Task InvokeTask(Func<Task> func)
        {
            return _factory.RunAsync(async () =>
            {
                await _factory.SwitchToMainThreadAsync();
                await func();
            }).Task;
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
        public override Task<T> InvokeTask<T>(Func<Task<T>> func)
        {
            return _factory.RunAsync(async () =>
            {
                await _factory.SwitchToMainThreadAsync();
                return await func();
            }).Task;
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
        public override void InvokeTaskSync(Func<Task> func, CancellationToken cancellationToken)
        {
            _factory.RunAsync(async () =>
            {
                // Convert assertions to exceptions while joining on a task
                // or the message box will deadlock.
                using (NoDeadlockAssertListener.Push())
                {
                    await _factory.SwitchToMainThreadAsync(cancellationToken);
                    await func();
                }
            }).Join(cancellationToken);
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
        public override T InvokeTaskSync<T>(Func<Task<T>> func, CancellationToken cancellationToken)
        {
            return _factory.RunAsync(async () =>
            {
                // Convert assertions to exceptions while joining on a task
                // or the message box will deadlock.
                using (NoDeadlockAssertListener.Push())
                {
                    await _factory.SwitchToMainThreadAsync(cancellationToken);
                    return await func();
                }
            }).Join(cancellationToken);
        }

        #region ThrowOnAssertListener class

#if DEBUG
        class NoDeadlockAssertListener : TraceListener {
            public static IDisposable Push() {
                var inner = new TraceListener[Trace.Listeners.Count];
                Trace.Listeners.CopyTo(inner, 0);
                Trace.Listeners.Clear();
                var res = new NoDeadlockAssertListener(inner);
                Trace.Listeners.Add(res);
                return res;
            }

            private readonly TraceListener[] _inner;

            protected NoDeadlockAssertListener(TraceListener[] inner) : base(nameof(NoDeadlockAssertListener)) {
                _inner = inner;
            }

            protected override void Dispose(bool disposing) {
                if (disposing) {
                    Trace.Listeners.Remove(this);
                    Trace.Listeners.AddRange(_inner);
                }
                base.Dispose(disposing);
            }

            public override bool IsThreadSafe => true;

            public override void Write(string message) {
                foreach (var listener in _inner) {
                    listener.Write(message);
                }
            }

            public override void WriteLine(string message) {
                foreach (var listener in _inner) {
                    listener.WriteLine(message);
                }
            }

            public override void Fail(string message) {
                var trace = new StackTrace(true).ToString();
                var fullMessage = string.IsNullOrEmpty(message) ? trace : (message + Environment.NewLine + trace);
                switch (MessageBox.Show(fullMessage, "Failed Assertion", MessageBoxButtons.AbortRetryIgnore)) {
                    case DialogResult.Abort:
                        Environment.FailFast(string.IsNullOrEmpty(message) ? fullMessage : message);
                        break;
                    case DialogResult.Retry:
                        Debugger.Launch();
                        break;
                    case DialogResult.Ignore:
                        break;
                }
            }

            public override void Fail(string message, string detailMessage) {
                Fail(message + Environment.NewLine + detailMessage);
            }
        }
#else
        class NoDeadlockAssertListener
        {
            public static IDisposable Push() => null;
        }
#endif

        #endregion
    }
}