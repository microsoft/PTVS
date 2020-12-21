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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace TestUtilities
{
    [ExcludeFromCodeCoverage]
    internal sealed class TestMainThreadService
    {
        [DllImport("ole32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern int OleInitialize(IntPtr value);

        private static readonly Lazy<TestMainThreadService> LazyInstance = new Lazy<TestMainThreadService>(Create, LazyThreadSafetyMode.ExecutionAndPublication);

        private static TestMainThreadService Create()
        {
            var mainThreadService = new TestMainThreadService();
            var initialized = new ManualResetEventSlim();

            AppDomain.CurrentDomain.DomainUnload += mainThreadService.Destroy;
            AppDomain.CurrentDomain.ProcessExit += mainThreadService.Destroy;

            // We want to maintain an application on a single STA thread
            // set Background so that it won't block process exit.
            var thread = new Thread(mainThreadService.RunMainThread) { Name = "WPF Dispatcher Thread" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start(initialized);

            initialized.Wait();
            Dispatcher.FromThread(thread).Invoke(() =>
            {
                mainThreadService.Thread = thread;
                mainThreadService.SyncContext = SynchronizationContext.Current;
            });
            return mainThreadService;
        }

        public static TestMainThreadService Instance => LazyInstance.Value;

        private readonly AsyncLocal<TestMainThread> _testMainThread;
        private DispatcherFrame _frame;
        private Application _application;

        private TestMainThreadService()
        {
            _testMainThread = new AsyncLocal<TestMainThread>();
        }

        public Thread Thread { get; private set; }
        public SynchronizationContext SyncContext { get; private set; }

        internal TestMainThread CreateTestMainThread()
        {
            if (_testMainThread.Value != null)
            {
                throw new InvalidOperationException("AsyncLocal<TestMainThread> reentrancy");
            }

            var testMainThread = new TestMainThread(this, RemoveTestMainThread);
            _testMainThread.Value = testMainThread;
            return testMainThread;
        }

        private void RemoveTestMainThread() => _testMainThread.Value = null;

        public void Invoke(Action action)
        {
            ExceptionDispatchInfo exception = Thread == Thread.CurrentThread
               ? CallSafe(action)
               : _application.Dispatcher.Invoke(() => CallSafe(action));

            exception?.Throw();
        }

        public async Task InvokeAsync(Action action)
        {
            ExceptionDispatchInfo exception;
            if (Thread == Thread.CurrentThread)
            {
                exception = CallSafe(action);
            }
            else
            {
                exception = await _application.Dispatcher.InvokeAsync(() => CallSafe(action), DispatcherPriority.Normal);
            }
            exception?.Throw();
        }

        public T Invoke<T>(Func<T> action)
        {
            var result = Thread == Thread.CurrentThread
               ? CallSafe(action)
               : _application.Dispatcher.Invoke(() => CallSafe(action));

            result.Exception?.Throw();
            return result.Value;
        }

        public async Task<T> InvokeAsync<T>(Func<T> action)
        {
            CallSafeResult<T> result;
            if (Thread == Thread.CurrentThread)
            {
                result = CallSafe(action);
            }
            else
            {
                result = await _application.Dispatcher.InvokeAsync(() => CallSafe(action));
            }

            result.Exception?.Throw();
            return result.Value;
        }

        private void RunMainThread(object obj)
        {
            if (Application.Current != null)
            {
                // Need to be on our own sta thread
                Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);

                if (Application.Current != null)
                {
                    throw new InvalidOperationException("Unable to shut down existing application.");
                }
            }

            // Kick OLE so we can use the clipboard if necessary
            OleInitialize(IntPtr.Zero);

            _application = new Application
            {
                // Application should survive window closing events to be reusable
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            // Dispatcher.Run internally calls PushFrame(new DispatcherFrame()), so we need to call PushFrame ourselves
            _frame = new DispatcherFrame(exitWhenRequested: false);
            var exceptionInfos = new List<ExceptionDispatchInfo>();

            // Initialization completed
            ((ManualResetEventSlim)obj).Set();

            while (_frame.Continue)
            {
                var exception = CallSafe(() => Dispatcher.PushFrame(_frame));
                if (exception != null)
                {
                    exceptionInfos.Add(exception);
                }
            }

            var dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
            if (dispatcher != null && !dispatcher.HasShutdownStarted)
            {
                dispatcher.InvokeShutdown();
            }

            if (exceptionInfos.Any())
            {
                throw new AggregateException(exceptionInfos.Select(ce => ce.SourceException).ToArray());
            }
        }

        private void Destroy(object sender, EventArgs e)
        {
            AppDomain.CurrentDomain.DomainUnload -= Destroy;
            AppDomain.CurrentDomain.ProcessExit -= Destroy;

            var mainThread = Thread;
            Thread = null;
            _frame.Continue = false;

            // If the thread is still alive, allow it to exit normally so the dispatcher can continue to clear pending work items
            // 10 seconds should be enough
            mainThread.Join(10000);
        }

        private static ExceptionDispatchInfo CallSafe(Action action)
            => CallSafe<object>(() =>
            {
                action();
                return null;
            }).Exception;

        private static CallSafeResult<T> CallSafe<T>(Func<T> func)
        {
            try
            {
                return new CallSafeResult<T> { Value = func() };
            }
            catch (ThreadAbortException tae)
            {
                // Thread should be terminated anyway
                Thread.ResetAbort();
                return new CallSafeResult<T> { Exception = ExceptionDispatchInfo.Capture(tae) };
            }
            catch (Exception e)
            {
                return new CallSafeResult<T> { Exception = ExceptionDispatchInfo.Capture(e) };
            }
        }

        private class CallSafeResult<T>
        {
            public T Value { get; set; }
            public ExceptionDispatchInfo Exception { get; set; }
        }
    }
}
