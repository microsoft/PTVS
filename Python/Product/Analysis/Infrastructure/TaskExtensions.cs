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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    internal static class TaskExtensions {
        public static void SetCompletionResultTo<T>(this Task<T> task, TaskCompletionSourceEx<T> tcs) 
            => task.ContinueWith(SetCompletionResultToContinuation, tcs, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        
        private static void SetCompletionResultToContinuation<T>(Task<T> task, object state) {
            var tcs = (TaskCompletionSourceEx<T>) state;
            switch (task.Status) {
                case TaskStatus.RanToCompletion:
                    tcs.TrySetResult(task.Result);
                    break;
                case TaskStatus.Canceled:
                    try {
                        task.GetAwaiter().GetResult();
                    } catch (OperationCanceledException ex) {
                        tcs.TrySetCanceled(ex);
                    }
                    break;
                case TaskStatus.Faulted:
                    tcs.TrySetException(task.Exception);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Suppresses warnings about unawaited tasks and rethrows task exceptions back to the callers synchronization context if it is possible
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
        public static void WaitAndUnwrapExceptions(this Task task) {
            task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Waits for a task to complete. If an exception occurs, the exception
        /// will be raised without being wrapped in a
        /// <see cref="AggregateException"/>.
        /// </summary>
        public static T WaitAndUnwrapExceptions<T>(this Task<T> task) {
            return task.GetAwaiter().GetResult();
        }
    }
}
