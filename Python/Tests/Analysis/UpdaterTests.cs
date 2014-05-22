/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalyzerStatusTests {
    [TestClass]
    public class UpdaterTests {
        [TestInitialize]
        public void EnsureCleanedUp() {
            // If a test fails, Dispose() may not have been called for all
            // analyzers.
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [TestMethod, Priority(0)]
        public void InitializeWithoutCrashing() {
            using (var updater = new AnalyzerStatusUpdater("hi")) { }

            using (var updater = new AnalyzerStatusListener(x => { })) { }
        }

        [TestMethod, Priority(0)]
        public void SendUpdates() {
            Dictionary<string, AnalysisProgress> results = null;

            using (var ready = new AutoResetEvent(false))
            using (var listener = new AnalyzerStatusListener(r => { results = r; ready.Set(); })) {
                ready.Reset();
                listener.RequestUpdate();
                ready.WaitOne();
                Assert.IsNotNull(results);
                Assert.AreEqual(0, results.Count);

                using (var sender1 = new AnalyzerStatusUpdater("s1"))
                using (var sender2 = new AnalyzerStatusUpdater("s2")) {
                    // Block until workers have started
                    sender1.WaitForWorkerStarted();
                    sender1.ThrowPendingExceptions();
                    sender2.WaitForWorkerStarted();
                    sender2.ThrowPendingExceptions();

                    ready.Reset();
                    listener.RequestUpdate();
                    ready.WaitOne();
                    Assert.IsNotNull(results);
                    Assert.AreEqual(2, results.Count);
                    Assert.IsTrue(results.ContainsKey("s1"), "s1 not found in {0}" + string.Join(", ", results.Keys));
                    Assert.IsTrue(results.ContainsKey("s2"), "s2 not found in {0}" + string.Join(", ", results.Keys));
                    Assert.AreEqual(int.MaxValue, results["s1"].Progress, "s1.Progress not initialized to MaxValue");
                    Assert.AreEqual(0, results["s1"].Maximum, "s1.Maximum not initialized to 0");
                    Assert.AreEqual(string.Empty, results["s1"].Message, "s1.Message not initialized to empty");
                    Assert.AreEqual(int.MaxValue, results["s2"].Progress, "s2.Progress not initialized to MaxValue");
                    Assert.AreEqual(0, results["s2"].Maximum, "s2.Maximum not initialized to 0");
                    Assert.AreEqual(string.Empty, results["s2"].Message, "s2.Message not initialized to empty");

                    sender1.UpdateStatus(100, 200, "Message1");
                    sender1.FlushQueue(TimeSpan.FromSeconds(1.0));

                    ready.Reset();
                    listener.RequestUpdate();
                    ready.WaitOne();
                    Assert.AreEqual(100, results["s1"].Progress, "s1.Progress not set to 100");
                    Assert.AreEqual(200, results["s1"].Maximum, "s1.Maximum not set to 200");
                    Assert.AreEqual("Message1", results["s1"].Message, "s1.Message not set");
                    Assert.AreEqual(int.MaxValue, results["s2"].Progress, "s2.Progress changed from MaxValue");
                    Assert.AreEqual(0, results["s2"].Maximum, "s2.Maximum changed from 0");
                    Assert.AreEqual(string.Empty, results["s2"].Message, "s2.Message changed from empty");

                    sender2.UpdateStatus(1000, 2000, "Message2");
                    sender2.FlushQueue(TimeSpan.FromSeconds(1.0));

                    ready.Reset();
                    listener.RequestUpdate();
                    ready.WaitOne();
                    Assert.AreEqual(100, results["s1"].Progress, "s1.Progress changed from 100");
                    Assert.AreEqual(200, results["s1"].Maximum, "s1.Maximum changed from 200");
                    Assert.AreEqual("Message1", results["s1"].Message, "s1.Message changed");
                    Assert.AreEqual(1000, results["s2"].Progress, "s2.Progress not set to 1000");
                    Assert.AreEqual(2000, results["s2"].Maximum, "s2.Maximum not set to 2000");
                    Assert.AreEqual("Message2", results["s2"].Message, "s2.Message not set");
                }

                ready.Reset();
                listener.RequestUpdate();
                ready.WaitOne();
                Assert.IsNotNull(results);
                Assert.AreEqual(0, results.Count, "results were not cleaned up");
            }
        }

        [TestMethod, Priority(0)]
        public void LotsOfUpdaters() {
            var updaters = new List<AnalyzerStatusUpdater>();

            // We should stop creating new entries well before 10000
            for (int j = 0; j < 10000; ++j) {
                Console.WriteLine("Creating S{0}", j);
                var newUpdater = new AnalyzerStatusUpdater("S" + j.ToString());
                updaters.Add(newUpdater);
            }
            // Give the updaters a chance to start
            foreach (var updater in updaters) {
                updater.WaitForWorkerStarted();
            }

            // Make sure that we got failures.
            try {
                foreach (var u in updaters) {
                    u.ThrowPendingExceptions();
                }
                Assert.Fail("Should not have been able to create 10000 updaters");
            } catch (InvalidOperationException) {
            } finally {
                foreach (var u in updaters) {
                    u.Dispose();
                }
            }
            updaters.Clear();
        }

        [TestMethod, Priority(0)]
        public void IdentifierInUse() {
            using (var updater = new AnalyzerStatusUpdater("Identifier")) {
                updater.UpdateStatus(1, 100);
                updater.WaitForWorkerStarted();
                // Should not throw
                updater.ThrowPendingExceptions();

                using (var updater2 = new AnalyzerStatusUpdater("Identifier")) {
                    updater2.WaitForWorkerStarted();
                    updater2.UpdateStatus(99, 100);

                    try {
                        updater2.ThrowPendingExceptions();
                        Assert.Fail("Expected IdentifierInUseException");
                    } catch (IdentifierInUseException) {
                    }
                }
            }
        }

        [TestMethod, Priority(0)]
        public void MessageMaximumLength() {
            Dictionary<string, AnalysisProgress> results = null;

            using (var ready = new AutoResetEvent(false))
            using (var listener = new AnalyzerStatusListener(r => { results = r; ready.Set(); })) {
                listener.WaitForWorkerStarted();
                listener.ThrowPendingExceptions();

                // Ensure that updates are being received
                listener.RequestUpdate();
                ready.WaitOne();
                Assert.IsNotNull(results);
                Assert.AreEqual(0, results.Count);

                using (var sender = new AnalyzerStatusUpdater("s")) {
                    sender.WaitForWorkerStarted();
                    sender.ThrowPendingExceptions();

                    // Create a message that is deliberately too long
                    string message = new string('x', AnalyzerStatusUpdater.MAX_MESSAGE_LENGTH * 2);
                    sender.UpdateStatus(0, 0, message);
                    sender.FlushQueue(TimeSpan.FromSeconds(1.0));

                    listener.RequestUpdate();
                    ready.WaitOne();
                    Assert.IsNotNull(results);
                    AssertUtil.ContainsExactly(results.Keys, "s");
                    var receivedMessage = results["s"].Message;
                    Console.WriteLine("Message: <{0}>", receivedMessage);
                    Assert.AreEqual(
                        AnalyzerStatusUpdater.MAX_MESSAGE_LENGTH,
                        receivedMessage.Length,
                        "Message length was not limited"
                    );
                }
            }
        }
    }
}
