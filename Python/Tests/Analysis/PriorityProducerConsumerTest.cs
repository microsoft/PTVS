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

using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTests {
    [TestClass]
    public class PriorityProducerConsumerTest {
        [TestMethod, Priority(0)]
        public void PriorityProducerConsumer_NoPending() {
            var ppc = new PriorityProducerConsumer<int>();
            ppc.Produce(5);
            var consumerTask = ppc.ConsumeAsync();
            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask.Status);
            Assert.AreEqual(5, consumerTask.Result);
        }

        [TestMethod, Priority(0)]
        public void PriorityProducerConsumer_NoPending_Priority1() {
            var ppc = new PriorityProducerConsumer<int>(2);
            ppc.Produce(5);
            ppc.Produce(6, 1);
            var consumerTask = ppc.ConsumeAsync();
            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask.Status);
            Assert.AreEqual(5, consumerTask.Result);
        }

        [TestMethod, Priority(0)]
        public void PriorityProducerConsumer_NoPending_Priority2() {
            var ppc = new PriorityProducerConsumer<int>(2);
            ppc.Produce(6, 1);
            ppc.Produce(5);
            var consumerTask = ppc.ConsumeAsync();
            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask.Status);
            Assert.AreEqual(5, consumerTask.Result);
        }

        [TestMethod, Priority(0)]
        public void PriorityProducerConsumer_NoPending_Duplicates1() {
            var ppc = new PriorityProducerConsumer<int>(3, true);
            ppc.Produce(5, 2);
            ppc.Produce(6, 1);
            ppc.Produce(5);
            var consumerTask1 = ppc.ConsumeAsync();
            var consumerTask2 = ppc.ConsumeAsync();
            var consumerTask3 = ppc.ConsumeAsync();

            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask1.Status);
            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask2.Status);
            Assert.AreEqual(TaskStatus.WaitingForActivation, consumerTask3.Status);
            Assert.AreEqual(5, consumerTask1.Result);
            Assert.AreEqual(6, consumerTask2.Result);
        }

        [TestMethod, Priority(0)]
        public void PriorityProducerConsumer_NoPending_Duplicates2() {
            var ppc = new PriorityProducerConsumer<int>(3, true);
            ppc.Produce(5);
            ppc.Produce(6, 1);
            ppc.Produce(5, 2);
            var consumerTask1 = ppc.ConsumeAsync();
            var consumerTask2 = ppc.ConsumeAsync();
            var consumerTask3 = ppc.ConsumeAsync();

            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask1.Status);
            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask2.Status);
            Assert.AreEqual(TaskStatus.WaitingForActivation, consumerTask3.Status);
            Assert.AreEqual(5, consumerTask1.Result);
            Assert.AreEqual(6, consumerTask2.Result);
        }

        [TestMethod, Priority(0)]
        public void PriorityProducerConsumer_NoPending_Duplicates3() {
            var ppc = new PriorityProducerConsumer<int>(3, true);
            ppc.Produce(5, 1);
            ppc.Produce(6, 1);
            ppc.Produce(5, 1);
            var consumerTask1 = ppc.ConsumeAsync();
            var consumerTask2 = ppc.ConsumeAsync();
            var consumerTask3 = ppc.ConsumeAsync();

            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask1.Status);
            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask2.Status);
            Assert.AreEqual(TaskStatus.WaitingForActivation, consumerTask3.Status);
            Assert.AreEqual(6, consumerTask1.Result);
            Assert.AreEqual(5, consumerTask2.Result);
        }

        [TestMethod, Priority(0)]
        public async Task PriorityProducerConsumer_Pending() {
            var ppc = new PriorityProducerConsumer<int>();
            var consumerTask = ppc.ConsumeAsync();
            Assert.AreEqual(TaskStatus.WaitingForActivation, consumerTask.Status);

            ppc.Produce(5);
            await consumerTask;

            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask.Status);
            Assert.AreEqual(5, consumerTask.Result);
        }

        [TestMethod, Priority(0)]
        public async Task PriorityProducerConsumer_Pending_Dispose() {
            var ppc = new PriorityProducerConsumer<int>();
            var consumerTask = ppc.ConsumeAsync();
            Assert.AreEqual(TaskStatus.WaitingForActivation, consumerTask.Status);

            ppc.Dispose();
            await consumerTask.ContinueWith(t => { });

            Assert.AreEqual(TaskStatus.Canceled, consumerTask.Status);
        }

        [TestMethod, Priority(0)]
        public async Task PriorityProducerConsumer_Pending_Priority1() {
            var ppc = new PriorityProducerConsumer<int>(2);
            var consumerTask = ppc.ConsumeAsync();
            Assert.AreEqual(TaskStatus.WaitingForActivation, consumerTask.Status);

            ppc.Produce(5);
            ppc.Produce(6, 1);
            await consumerTask;

            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask.Status);
            Assert.AreEqual(5, consumerTask.Result);
        }

        [TestMethod, Priority(0)]
        public async Task PriorityProducerConsumer_Pending_Priority2() {
            var ppc = new PriorityProducerConsumer<int>(2);
            var consumerTask1 = ppc.ConsumeAsync();
            var consumerTask2 = ppc.ConsumeAsync();
            Assert.AreEqual(TaskStatus.WaitingForActivation, consumerTask1.Status);
            Assert.AreEqual(TaskStatus.WaitingForActivation, consumerTask2.Status);

            ppc.Produce(6, 1);
            await consumerTask1;

            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask1.Status);
            Assert.AreEqual(TaskStatus.WaitingForActivation, consumerTask2.Status);
            Assert.AreEqual(6, consumerTask1.Result);

            ppc.Produce(5);
            await consumerTask2;

            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask2.Status);
            Assert.AreEqual(5, consumerTask2.Result);
        }

        [TestMethod, Priority(0)]
        public async Task PriorityProducerConsumer_Pending_Priority3() {
            var ppc = new PriorityProducerConsumer<int>(2);
            var values = new int[3];
            var tcsConsumer = new TaskCompletionSource<bool>();
            var tcsProducer = new TaskCompletionSource<bool>();
            var consumerTask = Task.Run(async () => {
                for (var i = 0; i < 3; i++) {
                    var task = ppc.ConsumeAsync();
                    tcsConsumer.TrySetResult(true);
                    values[i] = await task;
                    await tcsProducer.Task;
                }
            });

            Assert.AreEqual(TaskStatus.WaitingForActivation, consumerTask.Status);

            await tcsConsumer.Task;
            ppc.Produce(5, 1);
            ppc.Produce(6, 1);
            ppc.Produce(7);
            tcsProducer.SetResult(false);

            await consumerTask;

            Assert.AreEqual(TaskStatus.RanToCompletion, consumerTask.Status);
            Assert.AreEqual(5, values[0]);
            Assert.AreEqual(7, values[1]);
            Assert.AreEqual(6, values[2]);
        }
    }
}
