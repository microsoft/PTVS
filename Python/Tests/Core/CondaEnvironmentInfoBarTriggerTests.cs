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
// See the Apache License, Version 2.0 for the specific language governing
// permissions and limitations under the License.

extern alias pythontools;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pythontools::Microsoft.PythonTools.Project;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class CondaEnvironmentInfoBarTriggerTests {
        [TestMethod, Priority(UnitTestPriority.P0)]
        public void DiscoveryCompletedBeforeProjectOpen() {
            var registry = new MockInterpreterOptionsService();
            registry.SimulateCondaInterpreterDiscoveryCompleted();
            var triggerCount = 0;

            using (var trigger = CreateTrigger(registry, () => triggerCount++)) {
                trigger.OnProjectOpened();
            }

            Assert.AreEqual(1, triggerCount);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ProjectOpenedBeforeDiscoveryCompleted() {
            var registry = new MockInterpreterOptionsService();
            var triggerCount = 0;

            using (var trigger = CreateTrigger(registry, () => triggerCount++)) {
                trigger.OnProjectOpened();
                Assert.AreEqual(0, triggerCount);

                registry.SimulateCondaInterpreterDiscoveryCompleted();
            }

            Assert.AreEqual(1, triggerCount);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void RediscoveryMustCompleteBeforeProjectCheck() {
            var registry = new MockInterpreterOptionsService();
            registry.SimulateCondaInterpreterDiscoveryCompleted();
            registry.SimulateCondaInterpreterDiscoveryStarted();
            var triggerCount = 0;

            using (var trigger = CreateTrigger(registry, () => triggerCount++)) {
                trigger.OnProjectOpened();
                Assert.AreEqual(0, triggerCount);

                registry.SimulateCondaInterpreterDiscoveryCompleted();
            }

            Assert.AreEqual(1, triggerCount);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void RepeatedAndConcurrentEventsTriggerOnce() {
            var registry = new MockInterpreterOptionsService();
            var triggerCount = 0;

            using (var trigger = CreateTrigger(
                registry,
                () => Interlocked.Increment(ref triggerCount)
            )) {
                trigger.OnProjectOpened();

                var threads = new Thread[8];
                for (var i = 0; i < threads.Length; i++) {
                    threads[i] = new Thread(registry.SimulateCondaInterpreterDiscoveryCompleted);
                    threads[i].Start();
                }
                foreach (var thread in threads) {
                    thread.Join();
                }

                registry.SimulateCondaInterpreterDiscoveryCompleted();
                trigger.OnProjectOpened();
            }

            Assert.AreEqual(1, triggerCount);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void SuppressedProjectDoesNotPrompt() {
            var registry = new MockInterpreterOptionsService();
            registry.SimulateCondaInterpreterDiscoveryCompleted();
            var suppressed = true;
            var promptCount = 0;

            using (var trigger = CreateTrigger(
                registry,
                () => {
                    if (!suppressed) {
                        promptCount++;
                    }
                }
            )) {
                trigger.OnProjectOpened();
                suppressed = false;
                registry.SimulateCondaInterpreterDiscoveryCompleted();
            }

            Assert.AreEqual(0, promptCount);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void DisposedProjectDoesNotTrigger() {
            var registry = new MockInterpreterOptionsService();
            var triggerCount = 0;
            var trigger = CreateTrigger(registry, () => triggerCount++);

            trigger.OnProjectOpened();
            trigger.Dispose();
            registry.SimulateCondaInterpreterDiscoveryCompleted();

            Assert.AreEqual(0, triggerCount);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void MultipleProjectsEachTriggerOnceAcrossEventOrderings() {
            var registry = new MockInterpreterOptionsService();
            var firstProjectPromptCount = 0;
            var secondProjectPromptCount = 0;

            using (var firstProjectTrigger = CreateTrigger(
                registry,
                () => firstProjectPromptCount++
            )) {
                firstProjectTrigger.OnProjectOpened();
                registry.SimulateCondaInterpreterDiscoveryCompleted();

                using (var secondProjectTrigger = CreateTrigger(
                    registry,
                    () => secondProjectPromptCount++
                )) {
                    secondProjectTrigger.OnProjectOpened();
                    registry.SimulateCondaInterpreterDiscoveryCompleted();
                }
            }

            Assert.AreEqual(1, firstProjectPromptCount);
            Assert.AreEqual(1, secondProjectPromptCount);
        }

        private static CondaEnvironmentInfoBarTrigger CreateTrigger(
            MockInterpreterOptionsService registry,
            System.Action triggerInfoBarCheck
        ) {
            return new CondaEnvironmentInfoBarTrigger(
                registry,
                triggerInfoBarCheck
            );
        }
    }
}
