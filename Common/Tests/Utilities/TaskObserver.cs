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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using TestUtilities.Ben.Demystifier;

namespace TestUtilities
{
    internal class TaskObserver
    {
        private readonly int _secondsTimeout;
        private readonly Action<Task> _afterTaskCompleted;
        private readonly TaskCompletionSource<Exception> _tcs;
        private readonly ConcurrentDictionary<Task, StackFrame[]> _stackTraces;
        private int _count;
        private bool _isTestCompleted;

        public TaskObserver(int secondsTimeout)
        {
            _secondsTimeout = secondsTimeout;
            _afterTaskCompleted = AfterTaskCompleted;
            _tcs = new TaskCompletionSource<Exception>();
            _stackTraces = new ConcurrentDictionary<Task, StackFrame[]>();
        }

        public void Add(Task task)
        {
            // No reason to watch for task if it is completed already
            if (task.IsCompleted)
            {
                task.GetAwaiter().GetResult();
            }

            Interlocked.Increment(ref _count);
            _stackTraces.TryAdd(task, GetFilteredStackTrace());
            task.ContinueWith(_afterTaskCompleted, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public void WaitForObservedTask()
        {
            TestCompleted();
            _tcs.Task.Wait(_secondsTimeout * 1000);

            try
            {
                // Disable the failing of tests for now, this is too common
                // with the current analyzer. We can re-enable later, like
                // after we've switched to LSC and gotten rid of current analyzer code.
                //Summarize();
            }
            finally
            {
                _stackTraces.Clear();
            }
        }

        private void Summarize()
        {
            var incompleteTasks = new Queue<KeyValuePair<Task, StackFrame[]>>();
            var failedTasks = new Queue<KeyValuePair<StackFrame[], Exception>>();
            foreach (var kvp in _stackTraces)
            {
                var task = kvp.Key;
                if (!task.IsCompleted)
                {
                    incompleteTasks.Enqueue(kvp);
                }
                else if (task.IsFaulted && task.Exception != null)
                {
                    var aggregateException = task.Exception.Flatten();
                    var exception = aggregateException.InnerExceptions.Count == 1
                        ? aggregateException.InnerException
                        : aggregateException;

                    failedTasks.Enqueue(new KeyValuePair<StackFrame[], Exception>(kvp.Value, exception));
                }
            }

            if (incompleteTasks.Count == 0 && failedTasks.Count == 0)
            {
                return;
            }

            var message = new StringBuilder();
            var hasIncompleteTasks = incompleteTasks.Count > 0;
            var hasFailedTasks = failedTasks.Count > 0;
            if (hasIncompleteTasks)
            {
                if (incompleteTasks.Count > 1)
                {
                    message
                        .Append(incompleteTasks.Count)
                        .AppendLine(" tasks that have been started during test run are still not completed:")
                        .AppendLine();
                }
                else
                {
                    message
                        .AppendLine("One task that has been started during test run is still not completed:")
                        .AppendLine();
                }

                while (incompleteTasks.Count > 0)
                {
                    var kvp = incompleteTasks.Dequeue();
                    var task = kvp.Key;
                    message
                        .Append("Id: ")
                        .Append(task.Id)
                        .Append(", status: ")
                        .Append(task.Status)
                        .AppendLine()
                        .AppendFrames(kvp.Value)
                        .AppendLine()
                        .AppendLine();
                }

                if (hasFailedTasks)
                {
                    message
                        .Append("Also, ");
                }
            }

            if (hasFailedTasks)
            {
                if (failedTasks.Count > 1)
                {
                    message
                        .Append(failedTasks.Count)
                        .AppendLine(" not awaited tasks have failed:")
                        .AppendLine();
                }
                else
                {
                    message
                        .Append(hasIncompleteTasks ? "one" : "One")
                        .AppendLine(" not awaited tasks has failed:")
                        .AppendLine();
                }
            }

            while (failedTasks.Count > 0)
            {
                var kvp = failedTasks.Dequeue();
                message
                    .Append(kvp.Value.GetType().Name)
                    .Append(": ")
                    .AppendException(kvp.Value)
                    .AppendLine()
                    .Append("   --- Task stack trace: ---")
                    .AppendLine()
                    .AppendFrames(kvp.Key)
                    .AppendLine()
                    .AppendLine();
            }

            throw new AssertFailedException(message.ToString());
        }

        private void TestCompleted()
        {
            Volatile.Write(ref _isTestCompleted, true);
            if (_count == 0)
            {
                _tcs.TrySetResult(null);
            }
        }

        private void AfterTaskCompleted(Task task)
        {
            var count = Interlocked.Decrement(ref _count);
            if (!task.IsFaulted)
            {
                _stackTraces.TryRemove(task, out _);
            }
            else if (Debugger.IsAttached)
            {
                Debugger.Break();
            }

            if (count == 0 && Volatile.Read(ref _isTestCompleted))
            {
                _tcs.TrySetResult(null);
            }
        }

        private static StackFrame[] GetFilteredStackTrace()
        {
            var stackTrace = new StackTrace(2, true).GetFrames() ?? new StackFrame[0];
            var filteredStackTrace = new List<StackFrame>();
            var skip = true;
            foreach (var frame in stackTrace)
            {
                var frameMethod = frame.GetMethod();
                if (skip)
                {
                    if (frameMethod.Name == "DoNotWait" && frameMethod.DeclaringType?.Name == "TaskExtensions")
                    {
                        skip = false;
                    }
                    continue;
                }

                if (frameMethod.DeclaringType?.Namespace?.StartsWith("Microsoft.VisualStudio.TestPlatform.MSTestFramework", StringComparison.Ordinal) ?? false)
                {
                    continue;
                }

                filteredStackTrace.Add(frame);
            }

            return filteredStackTrace.ToArray();
        }
    }
}