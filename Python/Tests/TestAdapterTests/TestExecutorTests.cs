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
using System.Threading.Tasks;
using Microsoft.PythonTools.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Python;

namespace TestAdapterTests {
    [TestClass]
    public class TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void FromCommandLineArgsRaceCondition() {
            // https://pytools.codeplex.com/workitem/1429

            var mre = new ManualResetEvent(false);
            var tasks = new Task<bool>[100];
            try {
                for (int i = 0; i < tasks.Length; i += 1) {
                    tasks[i] = Task.Run(() => {
                        mre.WaitOne();
                        using (var arg = VisualStudioApp.FromProcessId(123)) {
                            return arg is VisualStudioApp;
                        }
                    });
                }
                mre.Set();
                Assert.IsTrue(Task.WaitAll(tasks, TimeSpan.FromSeconds(30.0)));
                Assert.IsTrue(tasks.All(t => t.Result));
            } finally {
                mre.Dispose();
                Task.WaitAll(tasks, TimeSpan.FromSeconds(30.0));
            }
        }

        [TestMethod, Priority(0)]
        public void TestRun() {
            PythonPaths.Python27_x64.AssertInstalled();
            PythonPaths.Python33_x64.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var runContext = new MockRunContext();
            var expectedTests = TestInfo.TestAdapterATests.Concat(TestInfo.TestAdapterBTests).ToArray();
            var testCases = expectedTests.Select(tr => tr.TestCase);

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder.Results);

            foreach (var expectedResult in expectedTests) {
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);

                Assert.IsNotNull(actualResult);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
            }
        }

        [TestMethod, Priority(0)]
        public void TestRunAll() {
            PythonPaths.Python27_x64.AssertInstalled();
            PythonPaths.Python33_x64.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var runContext = new MockRunContext();
            var expectedTests = TestInfo.TestAdapterATests.Concat(TestInfo.TestAdapterBTests).ToArray();

            executor.RunTests(new[] { TestInfo.TestAdapterLibProjectFilePath, TestInfo.TestAdapterAProjectFilePath, TestInfo.TestAdapterBProjectFilePath }, runContext, recorder);
            PrintTestResults(recorder.Results);

            foreach (var expectedResult in expectedTests) {
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);

                Assert.IsNotNull(actualResult, expectedResult.TestCase.FullyQualifiedName + " not found in results");
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
            }
        }

        [TestMethod, Priority(0)]
        public void TestCancel() {
            PythonPaths.Python27_x64.AssertInstalled();
            PythonPaths.Python33_x64.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var runContext = new MockRunContext();
            var expectedTests = TestInfo.TestAdapterATests.Union(TestInfo.TestAdapterBTests).ToArray();
            var testCases = expectedTests.Select(tr => tr.TestCase);

            var thread = new System.Threading.Thread(o => {
                executor.RunTests(testCases, runContext, recorder);
            });
            thread.Start();

            // One of the tests being run is hard coded to take 10 secs
            Assert.IsTrue(thread.IsAlive);

            System.Threading.Thread.Sleep(100);

            executor.Cancel();
            System.Threading.Thread.Sleep(100);

            // It should take less than 10 secs to cancel
            // Depending on which assemblies are loaded, it may take some time
            // to obtain the interpreters service.
            Assert.IsTrue(thread.Join(10000));

            System.Threading.Thread.Sleep(100);

            Assert.IsFalse(thread.IsAlive);

            // Canceled test cases do not get recorded
            Assert.IsTrue(recorder.Results.Count < expectedTests.Length);
        }

        [TestMethod, Priority(0)]
        public void TestMultiprocessing() {
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var runContext = new MockRunContext();
            var expectedTests = TestInfo.TestAdapterMultiprocessingTests;
            var testCases = expectedTests.Select(tr => tr.TestCase);

            executor.RunTests(new[] { TestInfo.TestAdapterMultiprocessingProjectFilePath }, runContext, recorder);
            PrintTestResults(recorder.Results);

            foreach (var expectedResult in expectedTests) {
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);

                Assert.IsNotNull(actualResult, expectedResult.TestCase.FullyQualifiedName + " not found in results");
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
            }
        }

        [TestMethod, Priority(0)]
        public void TestEnvironment() {
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var runContext = new MockRunContext();
            var expectedTests = new[] { TestInfo.EnvironmentTestSuccess };
            var testCases = expectedTests.Select(tr => tr.TestCase);

            executor.RunTests(new[] { TestInfo.TestAdapterEnvironmentProject }, runContext, recorder);
            PrintTestResults(recorder.Results);

            foreach (var expectedResult in expectedTests) {
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);

                Assert.IsNotNull(actualResult, expectedResult.TestCase.FullyQualifiedName + " not found in results");
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
            }
        }

        [TestMethod, Priority(0)]
        public void TestExtensionReference() {
            PythonPaths.Python27.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var runContext = new MockRunContext();
            var expectedTests = new[] { TestInfo.ExtensionReferenceTestSuccess };
            var testCases = expectedTests.Select(tr => tr.TestCase);

            executor.RunTests(new[] { TestInfo.TestAdapterExtensionReferenceProject }, runContext, recorder);
            PrintTestResults(recorder.Results);

            foreach (var expectedResult in expectedTests) {
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);

                Assert.IsNotNull(actualResult, expectedResult.TestCase.FullyQualifiedName + " not found in results");
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
            }
        }

        private static void PrintTestResults(IEnumerable<TestResult> results) {
            foreach (var result in results) {
                Console.WriteLine("Test: " + result.TestCase.FullyQualifiedName);
                Console.WriteLine("Result: " + result.Outcome);
                foreach(var msg in result.Messages) {
                    Console.WriteLine("Message " + msg.Category + ":");
                    Console.WriteLine(msg.Text);
                }
                Console.WriteLine("");
            }
        }
    }
}
