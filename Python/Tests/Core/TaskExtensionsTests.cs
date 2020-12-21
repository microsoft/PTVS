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

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PythonToolsTests {
    [TestClass]
    public class TaskExtensionsTests {
        [TestMethod]
        public async Task DoNotWait() {
            var expected1 = new InvalidOperationException();
            var expected2 = new InvalidOperationException();
            var actions = new BlockingCollection<Action>();
            var exceptions = new ConcurrentQueue<Exception>();

            var thread = new Thread(ConsumerThread);
            thread.Start();

            actions.Add(() => ThrowCanceledFromConsumerThread(thread.ManagedThreadId).DoNotWait());
            actions.Add(() => ThrowCanceledFromBackgroundThread(thread.ManagedThreadId).DoNotWait());
            actions.Add(() => ThrowExceptionFromConsumerThread(thread.ManagedThreadId, expected1).DoNotWait());
            actions.Add(() => ThrowExceptionFromBackgroundThread(thread.ManagedThreadId, expected2).DoNotWait());

            await Task.Delay(3_000);

            actions.CompleteAdding();
            thread.Join(3_000);

            Assert.IsTrue(thread.ThreadState == ThreadState.Stopped);
            Assert.IsTrue(exceptions.Count == 2, $"{nameof(exceptions)} should contain exactly two exceptions");
            CollectionAssert.AreEquivalent(exceptions, new[] { expected1, expected2 });

            void ConsumerThread() {
                SynchronizationContext.SetSynchronizationContext(new BlockingCollectionSynchronizationContext(actions));
                foreach (var action in actions.GetConsumingEnumerable()) {
                    try {
                        action();
                    } catch (Exception ex) {
                        exceptions.Enqueue(ex);
                    }
                }
            }
        }

        private static async Task ThrowCanceledFromConsumerThread(int managedThreadId) {
            try {
                Assert.AreEqual(managedThreadId, Thread.CurrentThread.ManagedThreadId);
                throw new CustomOperationCanceledException();
            } finally {
                Assert.AreEqual(managedThreadId, Thread.CurrentThread.ManagedThreadId);
            }
        }

        private static async Task ThrowCanceledFromBackgroundThread(int managedThreadId) {
            try {
                Assert.AreEqual(managedThreadId, Thread.CurrentThread.ManagedThreadId);
                await BackgroundThreadThrowCanceled();
            } finally {
                Assert.AreEqual(managedThreadId, Thread.CurrentThread.ManagedThreadId);
            }
        }

        private static async Task BackgroundThreadThrowCanceled() {
            await new BackgroundThreadAwaitable();
            throw new CustomOperationCanceledException();
        }

        private static async Task ThrowExceptionFromConsumerThread(int managedThreadId, Exception exception) {
            try {
                Assert.AreEqual(managedThreadId, Thread.CurrentThread.ManagedThreadId);
                throw exception;
            } finally {
                Assert.AreEqual(managedThreadId, Thread.CurrentThread.ManagedThreadId);
            }
        }

        private static async Task ThrowExceptionFromBackgroundThread(int managedThreadId, Exception exception) {
            try {
                Assert.AreEqual(managedThreadId, Thread.CurrentThread.ManagedThreadId);
                await BackgroundThreadThrowException(exception);
            } finally {
                Assert.AreEqual(managedThreadId, Thread.CurrentThread.ManagedThreadId);
            }
        }

        private static async Task BackgroundThreadThrowException(Exception exception) {
            await new BackgroundThreadAwaitable();
            throw exception;
        }

        private class CustomOperationCanceledException : OperationCanceledException { }

        private class BlockingCollectionSynchronizationContext : SynchronizationContext {
            private readonly BlockingCollection<Action> _queue;

            public BlockingCollectionSynchronizationContext(BlockingCollection<Action> queue) {
                _queue = queue;
            }

            public override void Send(SendOrPostCallback d, object state) => throw new NotSupportedException();
            public override void Post(SendOrPostCallback d, object state) => _queue.Add(() => d(state));
            public override int Wait(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout) => WaitHelper(waitHandles, waitAll, millisecondsTimeout);
            public override SynchronizationContext CreateCopy() => new BlockingCollectionSynchronizationContext(_queue);
        }

        private struct BackgroundThreadAwaitable {
            public BackgroundThreadAwaiter GetAwaiter() => new BackgroundThreadAwaiter();
        }

        private struct BackgroundThreadAwaiter : ICriticalNotifyCompletion {
            private static readonly WaitCallback WaitCallback = state => ((Action)state)();
            public bool IsCompleted => TaskScheduler.Current == TaskScheduler.Default && Thread.CurrentThread.IsThreadPoolThread;
            public void OnCompleted(Action continuation) => ThreadPool.QueueUserWorkItem(WaitCallback, continuation);
            public void UnsafeOnCompleted(Action continuation) => ThreadPool.UnsafeQueueUserWorkItem(WaitCallback, continuation);
            public void GetResult() { }
        }
    }
}