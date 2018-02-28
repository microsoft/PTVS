// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DsTools.Core {
    public static class TaskExtensions {
        public static Task FailOnTimeout(this Task task, int millisecondsTimeout) => task.TimeoutAfterImpl<object>(millisecondsTimeout);

        public static Task<T> FailOnTimeout<T>(this Task task, int millisecondsTimeout) => (Task<T>)task.TimeoutAfterImpl<T>(millisecondsTimeout);

        public static Task TimeoutAfterImpl<T>(this Task task, int millisecondsTimeout) {
            if (task.IsCompleted || (millisecondsTimeout == Timeout.Infinite)) {
                return task;
            }

            if (millisecondsTimeout == 0) {
                return Task.FromException<T>(new TimeoutException());
            }

            var tcs = new TaskCompletionSource<T>();
            var cancelByTimeout = new TimerCallback(state => ((TaskCompletionSource<T>)state).TrySetException(new TimeoutException()));
            var timer = new Timer(cancelByTimeout, tcs, millisecondsTimeout, Timeout.Infinite);
            var taskState = new TimeoutAfterState<T>(timer, tcs);

            var continuation = new Action<Task, object>((source, state) => {
                var timeoutAfterState = (TimeoutAfterState<T>)state;
                timeoutAfterState.Timer.Dispose();

                switch (source.Status) {
                    case TaskStatus.Faulted:
                        timeoutAfterState.Tcs.TrySetException(source.Exception);
                        break;
                    case TaskStatus.Canceled:
                        timeoutAfterState.Tcs.TrySetCanceled();
                        break;
                    case TaskStatus.RanToCompletion:
                        var typedTask = source as Task<T>;
                        timeoutAfterState.Tcs.TrySetResult(typedTask != null ? typedTask.Result : default(T));
                        break;
                }
            });

            task.ContinueWith(continuation, taskState, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return tcs.Task;
        }

        public static Task ContinueOnRanToCompletion<TResult>(this Task<TResult> task, Action<TResult> action) => task.ContinueWith(t => action(t.Result), TaskContinuationOptions.OnlyOnRanToCompletion);

        /// <summary>
        /// Rethrows task exceptions back to the callers synchronization context if it is possible
        /// </summary>
        /// <remarks>
        /// <see cref="OperationCanceledException"/> is always ignored.
        /// </remarks>
        public static void DoNotWait(this Task task) {
            if (task.IsCompleted) {
                ReThrowTaskException(task);
                return;
            }

            var synchronizationContext = SynchronizationContext.Current;
            if (synchronizationContext != null && synchronizationContext.GetType() != typeof (SynchronizationContext)) {
                task.ContinueWith(DoNotWaitSynchronizationContextContinuation, synchronizationContext, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            } else {
                task.ContinueWith(DoNotWaitThreadContinuation, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        private static void ReThrowTaskException(object state) {
            var task = (Task)state;
            if (task.IsFaulted && task.Exception != null) {
                var exception = task.Exception.InnerException;
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        private static void DoNotWaitThreadContinuation(Task task) {
            if (task.IsFaulted && task.Exception != null) {
                var exception = task.Exception.InnerException;
                ThreadPool.QueueUserWorkItem(s => ((ExceptionDispatchInfo)s).Throw(), ExceptionDispatchInfo.Capture(exception));
            }
        }

        private static void DoNotWaitSynchronizationContextContinuation(Task task, object state) {
            var context = (SynchronizationContext) state;
            context.Post(ReThrowTaskException, task);
        }

        /// <summary>
        /// Waits for a task to complete. If an exception occurs, the exception
        /// will be raised without being wrapped in a
        /// <see cref="AggregateException"/>.
        /// </summary>
        public static void WaitAndUnwrapExceptions(this Task task) => task.GetAwaiter().GetResult();

        /// <summary>
        /// Waits for a task to complete. If an exception occurs, the exception
        /// will be raised without being wrapped in a
        /// <see cref="AggregateException"/>.
        /// </summary>
        public static T WaitAndUnwrapExceptions<T>(this Task<T> task) => task.GetAwaiter().GetResult();

        /// <summary>
        /// Waits for a task to complete within a given timeout. 
        /// Returns task result or default(T) if task did not complete
        /// within the timeout specified.
        /// </summary>
        public static T WaitTimeout<T>(this Task<T> task, int msTimeout) {
            try {
                task.Wait(msTimeout);
            } catch(AggregateException) { }

            if(!task.IsCompleted) {
                // Caller most probably will abandon the task on timeout,
                // so make sure all exceptions are observed
                task.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException();
            }
            return task.WaitAndUnwrapExceptions();
        }

        /// <summary>
        /// Silently handles the specified exception.
        /// </summary>
        public static Task SilenceException<T>(this Task task) where T : Exception {
            var tcs = new TaskCompletionSource<object>();
            task.ContinueWith(SilenceExceptionContinuation<T>, tcs, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return tcs.Task;
        }

        private static void SilenceExceptionContinuation<T>(Task task, object state) {
            var tcs = (TaskCompletionSource<object>)state;

            switch (task.Status) {
                case TaskStatus.Faulted:
                    var unhandledExceptions = task.Exception?.InnerExceptions
                        .Where(e => !(e is T))
                        .ToList();

                    if (unhandledExceptions?.Count == 1) {
                        tcs.TrySetException(unhandledExceptions[0]);
                    } else if (unhandledExceptions?.Count > 1) {
                        tcs.TrySetException(unhandledExceptions);
                    } else {
                        tcs.TrySetResult(null);
                    }
                    break;
                case TaskStatus.Canceled:
                    tcs.TrySetCanceled();
                    break;
                case TaskStatus.RanToCompletion:
                    tcs.TrySetResult(null);
                    break;
            }
        }

        public class TimeoutAfterState<T> {
            public Timer Timer { get; }
            public TaskCompletionSource<T> Tcs { get; }

            public TimeoutAfterState(Timer timer, TaskCompletionSource<T> tcs) {
                Timer = timer;
                Tcs = tcs;
            }
        }
    }
}
