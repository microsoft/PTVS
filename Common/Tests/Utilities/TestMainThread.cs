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

namespace TestUtilities
{
    [ExcludeFromCodeCoverage]
    public class TestMainThread : IDisposable
    {
        private readonly TestMainThreadService _service;
        private readonly Action _onDispose;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly AsyncLocal<BlockingLoop> _blockingLoop = new AsyncLocal<BlockingLoop>();

        internal TestMainThread(TestMainThreadService service, Action onDispose)
        {
            _service = service;
            _onDispose = onDispose;
        }

        public int ThreadId => _service.Thread.ManagedThreadId;

        public void Dispose() => _onDispose();

        public void Post(Action action)
        {
            var bl = _blockingLoop.Value;
            if (bl != null)
            {
                bl.Post(action);
            }
            else
            {
                TestEnvironmentImpl.Instance.TryAddTaskToWait(_service.InvokeAsync(action));
            }
        }

        public bool Wait(Func<Task> method, int ms = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken))
        {
            var delayTask = Task.Delay(ms, cancellationToken);
            var resultTask = BlockUntilCompleted(() => Task.WhenAny(method(), delayTask));
            return resultTask != delayTask;
        }

        public bool Wait<T>(Func<Task<T>> method, out T result, int ms = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken))
        {
            var delayTask = Task.Delay(ms, cancellationToken);
            var resultTask = BlockUntilCompleted(() => Task.WhenAny(method(), delayTask));
            result = resultTask is Task<T> task ? task.GetAwaiter().GetResult() : default(T);
            return resultTask != delayTask;
        }

        private TResult BlockUntilCompleted<TResult>(Func<Task<TResult>> func)
        {
            var task = BlockUntilCompletedImpl(func);
            return ((Task<TResult>)task).Result;
        }

        private Task BlockUntilCompletedImpl(Func<Task> func)
        {
            if (_service.Thread != Thread.CurrentThread)
            {
                try
                {
                    var task = func();
                    task.GetAwaiter().GetResult();
                    return task;
                }
                catch (OperationCanceledException ex)
                {
                    return CreateCanceled(ex);
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
            }

            var sc = SynchronizationContext.Current;
            var blockingLoopSynchronizationContext = new BlockingLoopSynchronizationContext(_service, this, sc);
            SynchronizationContext.SetSynchronizationContext(blockingLoopSynchronizationContext);
            var bl = new BlockingLoop(func, sc);
            try
            {
                _blockingLoop.Value = bl;
                bl.Start();
            }
            finally
            {
                _blockingLoop.Value = null;
                SynchronizationContext.SetSynchronizationContext(sc);
            }

            return bl.Task;
        }

        public static Task CreateCanceled(OperationCanceledException exception)
        {
            var atmb = new AsyncTaskMethodBuilder();
            atmb.SetException(exception);
            return atmb.Task;
        }

        public void CancelPendingTasks() => _cts.Cancel();

        private class BlockingLoop
        {
            private readonly Func<Task> _func;
            private readonly SynchronizationContext _previousSyncContext;
            private readonly AutoResetEvent _are;
            private readonly ConcurrentQueue<Action> _actions;

            public Task Task { get; private set; }

            public BlockingLoop(Func<Task> func, SynchronizationContext previousSyncContext)
            {
                _func = func;
                _previousSyncContext = previousSyncContext;
                _are = new AutoResetEvent(false);
                _actions = new ConcurrentQueue<Action>();
            }

            public void Start()
            {
                Task = _func();
                Task.ContinueWith(Complete);
                while (!Task.IsCompleted)
                {
                    _are.WaitOne();
                    ProcessQueue();
                }
            }

            // TODO: Add support for cancellation token
            public void Post(Action action)
            {
                _actions.Enqueue(action);
                _are.Set();
                if (Task.IsCompleted)
                {
                    _previousSyncContext.Post(c => ProcessQueue(), null);
                }
            }

            private void Complete(Task task) => _are.Set();

            private void ProcessQueue()
            {
                while (_actions.TryDequeue(out var action))
                {
                    action();
                }
            }
        }

        private class BlockingLoopSynchronizationContext : SynchronizationContext
        {
            private readonly TestMainThreadService _service;
            private readonly TestMainThread _mainThread;
            private readonly SynchronizationContext _innerSynchronizationContext;

            public BlockingLoopSynchronizationContext(TestMainThreadService service, TestMainThread mainThread, SynchronizationContext innerSynchronizationContext)
            {
                _service = service;
                _mainThread = mainThread;
                _innerSynchronizationContext = innerSynchronizationContext;
            }

            public override void Send(SendOrPostCallback d, object state)
                => _innerSynchronizationContext.Send(d, state);

            public override void Post(SendOrPostCallback d, object state)
            {
                var bl = _mainThread._blockingLoop.Value;
                if (bl != null)
                {
                    bl.Post(() => d(state));
                }
                else
                {
                    _innerSynchronizationContext.Post(d, state);
                }
            }

            public override SynchronizationContext CreateCopy()
                => new BlockingLoopSynchronizationContext(_service, _mainThread, _innerSynchronizationContext);
        }
    }
}