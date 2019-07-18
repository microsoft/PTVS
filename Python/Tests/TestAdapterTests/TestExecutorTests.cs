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

extern alias pt;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using TestUtilities;
using TestUtilities.Python;
using PythonConstants = pt::Microsoft.PythonTools.PythonConstants;

namespace TestAdapterTests {
    [TestClass, Ignore]
    public abstract class TestExecutorTests {
        internal const string OldImportErrorFormat = "No module named {0}";
        internal const string NewImportErrorFormat = "No module named '{0}'";

        [TestInitialize]
        public void CheckVersion() {
            if (Version == null) {
                Assert.Inconclusive("Required version of Python is not installed");
            }
        }

        protected abstract PythonVersion Version { get; }

        protected virtual string ImportErrorFormat => OldImportErrorFormat;

        [TestMethod]
        public void TestBestFile() {
            var file1 = "C:\\Some\\Path\\file1.py";
            var file2 = "C:\\Some\\Path\\file2.py";
            var best = TestExecutor.UpdateBestFile(null, file1);
            Assert.AreEqual(best, file1);

            best = TestExecutor.UpdateBestFile(null, file1);
            Assert.AreEqual(best, file1);

            best = TestExecutor.UpdateBestFile(best, file2);
            Assert.AreEqual("C:\\Some\\Path", best);
        }

        //        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        //        [TestCategory("10s")]
        //        public void TestRunAll() {
        //            var executor = new TestExecutor();
        //            var recorder = new MockTestExecutionRecorder();
        //            var tests = TestInfo.TestAdapterATests.Concat(TestInfo.TestAdapterBTests).ToArray();
        //            var runContext = CreateRunContext(tests, Version.InterpreterPath);
        //            var expectedTests = runContext.TestCases;

        //            executor.RunTests(expectedTests, runContext, recorder);
        //            PrintTestResults(recorder);

        //            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
        //            foreach (var expectedResult in tests) {
        //                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
        //                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
        //                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
        //                Assert.IsTrue(actualResult.Duration >= expectedResult.MinDuration);
        //            }
        //        }

        //        [TestMethod, Priority(0)]
        //        public void TestCancel() {
        //            var executor = new TestExecutor();
        //            var recorder = new MockTestExecutionRecorder();
        //            var expectedTests = TestInfo.TestAdapterATests.Union(TestInfo.TestAdapterBTests).ToArray();
        //            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
        //            var testCases = runContext.TestCases;

        //            var thread = new System.Threading.Thread(o => {
        //                executor.RunTests(testCases, runContext, recorder);
        //            });
        //            thread.Start();

        //            // One of the tests being run is hard coded to take 10 secs
        //            Assert.IsTrue(thread.IsAlive);

        //            System.Threading.Thread.Sleep(100);

        //            executor.Cancel();
        //            System.Threading.Thread.Sleep(100);

        //            // It should take less than 10 secs to cancel
        //            // Depending on which assemblies are loaded, it may take some time
        //            // to obtain the interpreters service.
        //            Assert.IsTrue(thread.Join(10000));

        //            System.Threading.Thread.Sleep(100);

        //            Assert.IsFalse(thread.IsAlive);

        //            // Canceled test cases do not get recorded
        //            Assert.IsTrue(recorder.Results.Count < expectedTests.Length);
        //        }

        //        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        //        [TestCategory("10s")]
        //        public void TestMultiprocessing() {
        //            if (Version.Version <= PythonLanguageVersion.V26 ||
        //                Version.Version == PythonLanguageVersion.V30) {
        //                return;
        //            }

        //            var executor = new TestExecutor();
        //            var recorder = new MockTestExecutionRecorder();
        //            var expectedTests = TestInfo.TestAdapterMultiprocessingTests;
        //            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
        //            var testCases = runContext.TestCases;

        //            executor.RunTests(testCases, runContext, recorder);
        //            PrintTestResults(recorder);

        //            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
        //            foreach (var expectedResult in expectedTests) {
        //                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
        //                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
        //                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
        //            }
        //        }

        //        [TestMethod, Priority(0)]
        //        [TestCategory("10s")]
        //        public void TestRelativeImport() {
        //            var executor = new TestExecutor();
        //            var recorder = new MockTestExecutionRecorder();
        //            var expectedTests = new[] { TestInfo.RelativeImportSuccess };
        //            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
        //            var testCases = runContext.TestCases;

        //            executor.RunTests(testCases, runContext, recorder);
        //            PrintTestResults(recorder);

        //            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
        //            foreach (var expectedResult in expectedTests) {
        //                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
        //                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
        //                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
        //            }
        //        }

        //        [TestMethod, Priority(0)]
        //        [TestCategory("10s")]
        //        public void TestInheritance() {
        //            // TODO: Figure out the proper fix to make this test pass.
        //            // There's a confusion between source file path and class file path.
        //            // Note that the equivalent manual test in IDE works fine.
        //            var executor = new TestExecutor();
        //            var recorder = new MockTestExecutionRecorder();
        //            var expectedTests = TestInfo.TestAdapterBInheritanceTests;
        //            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
        //            var testCases = runContext.TestCases;

        //            executor.RunTests(testCases, runContext, recorder);
        //            PrintTestResults(recorder);

        //            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
        //            foreach (var expectedResult in expectedTests) {
        //                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
        //                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
        //                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
        //            }
        //        }

        //        [TestMethod, Priority(0)]
        //        [TestCategory("10s")]
        //        public void TestLoadError() {
        //            // A load error is when unittest module fails to load the test (prior to running it)
        //            // For example, if the file where the test is defined has an unhandled ImportError.
        //            // We check that this only causes the tests that can't be loaded to fail,
        //            // all other tests in the test run which can be loaded successfully will be run.
        //            var executor = new TestExecutor();
        //            var recorder = new MockTestExecutionRecorder();
        //            var expectedTests = TestInfo.GetTestAdapterLoadErrorTests(ImportErrorFormat);
        //            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
        //            var testCases = runContext.TestCases;

        //            executor.RunTests(testCases, runContext, recorder);
        //            PrintTestResults(recorder);

        //            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
        //            foreach (var expectedResult in expectedTests) {
        //                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
        //                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
        //                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");

        //                if (expectedResult.ContainedErrorMessage != null) {
        //                    Assert.IsNotNull(actualResult.ErrorMessage);
        //                    Assert.IsTrue(
        //                        actualResult.ErrorMessage.Contains(expectedResult.ContainedErrorMessage),
        //                        string.Format("Error message did not contain expected text: {0}", expectedResult.ContainedErrorMessage)
        //                    );
        //                } else {
        //                    Assert.IsNull(actualResult.ErrorMessage);
        //                }
        //            }
        //        }

        //        [TestMethod, Priority(0)]
        //        [TestCategory("10s")]
        //        public void TestStackTrace() {
        //            var executor = new TestExecutor();
        //            var recorder = new MockTestExecutionRecorder();
        //            var expectedTests = new[] {
        //                TestInfo.StackTraceBadLocalImportFailure,
        //                TestInfo.StackTraceNotEqualFailure
        //            };
        //            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
        //            var testCases = runContext.TestCases;

        //            executor.RunTests(testCases, runContext, recorder);

        //            var badLocalImportFile = TestInfo.StackTraceBadLocalImportFailure.SourceCodeFilePath;
        //            var badLocalImportFrames = new StackFrame[] {
        //                new StackFrame("local_func in global_func", badLocalImportFile, 13),
        //                new StackFrame("global_func", badLocalImportFile, 14),
        //                new StackFrame("Utility.class_static", badLocalImportFile, 19),
        //                new StackFrame("Utility.instance_method_b", badLocalImportFile, 22),
        //                new StackFrame("Utility.instance_method_a", badLocalImportFile, 25),
        //                new StackFrame("StackTraceTests.test_bad_import", badLocalImportFile, 6),
        //            };

        //            ValidateStackFrame(recorder.Results[0], badLocalImportFrames);

        //            var notEqualFile = TestInfo.StackTraceNotEqualFailure.SourceCodeFilePath;
        //            var notEqualFrames = new StackFrame[] {
        //                new StackFrame("StackTraceTests.test_not_equal", notEqualFile, 9),
        //            };

        //            ValidateStackFrame(recorder.Results[1], notEqualFrames);
        //        }

        //        private static void ValidateStackFrame(TestResult result, StackFrame[] expectedFrames) {
        //            var stackTrace = result.ErrorStackTrace;
        //            var parser = new PythonStackTraceParser();
        //            var frames = parser.GetStackFrames(stackTrace).ToArray();

        //            Console.WriteLine("Actual frames:");
        //            foreach (var f in frames) {
        //                Console.WriteLine("\"{0}\",\"{1}\",\"{2}\"", f.MethodDisplayName, f.FileName, f.LineNumber);
        //            }

        //            CollectionAssert.AreEqual(expectedFrames, frames, new StackFrameComparer());
        //        }

        //        class StackFrameComparer : IComparer {
        //            public int Compare(object x, object y) {
        //                if (x == y) {
        //                    return 0;
        //                }

        //                var a = x as StackFrame;
        //                var b = y as StackFrame;

        //                if (a == null) {
        //                    return -1;
        //                }

        //                if (b == null) {
        //                    return 1;
        //                }

        //                int res = a.FileName.CompareTo(b.FileName);
        //                if (res != 0) {
        //                    return res;
        //                }

        //                res = a.LineNumber.CompareTo(b.LineNumber);
        //                if (res != 0) {
        //                    return res;
        //                }

        //                res = a.MethodDisplayName.CompareTo(b.MethodDisplayName);
        //                if (res != 0) {
        //                    return res;
        //                }

        //                return 0;
        //            }
        //        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void RunWithPytestEnvironmentVariable() {
            var testEnv = TestEnvironment.Create(Version, "Pytest");

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_env_var.py");
            File.Copy(TestData.GetPath("TestData", "TestExecutor", "test_env_var.py"), testFilePath);

            var expectedTests = new[] {
                new PytestExecutionTestInfo(
                    "test_variable",
                    "test_env_var.py::EnvironmentVariableTests::test_variable",
                    testFilePath,
                    5,
                    "test_env_var.EnvironmentVariableTests",
                    TestOutcome.Passed
                )
            };

            var runSettings = new MockRunSettings(
                MockRunSettingsXmlBuilder.CreateDiscoveryContext(
                    "Pytest", testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath,
                    new[] { Tuple.Create("USER_ENV_VAR", "123") }
                )
            );
            var testCases = CreateTestCasesFromTestInfo(testEnv, expectedTests);
            var runContext = new MockRunContext(runSettings, testCases, testEnv.ResultsFolderPath);
            var recorder = new MockTestExecutionRecorder();
            var executor = new TestExecutor();

            executor.RunTests(testCases, runContext, recorder);

            ValidateExecutedTests(expectedTests, recorder);
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void RunWithPytestDuration() {
            var testEnv = TestEnvironment.Create(Version, "Pytest");

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_duration.py");
            File.Copy(TestData.GetPath("TestData", "TestExecutor", "test_duration.py"), testFilePath);

            var expectedTests = new[] {
                new PytestExecutionTestInfo(
                    "test_sleep_0_1",
                    "test_duration.py::DurationTests::test_sleep_0_1",
                    testFilePath,
                    5,
                    "test_duration.DurationTests",
                    TestOutcome.Passed,
                    minDuration: TimeSpan.FromSeconds(0.1)
                ),
                new PytestExecutionTestInfo(
                    "test_sleep_0_3",
                    "test_duration.py::DurationTests::test_sleep_0_3",
                    testFilePath,
                    8,
                    "test_duration.DurationTests",
                    TestOutcome.Passed,
                    minDuration: TimeSpan.FromSeconds(0.3)
                ),
                new PytestExecutionTestInfo(
                    "test_sleep_0_5",
                    "test_duration.py::DurationTests::test_sleep_0_5",
                    testFilePath,
                    11,
                    "test_duration.DurationTests",
                    TestOutcome.Failed,
                    minDuration: TimeSpan.FromSeconds(0.5)
                ),
            };

            var runSettings = new MockRunSettings(
                MockRunSettingsXmlBuilder.CreateDiscoveryContext("Pytest", testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
            );
            var testCases = CreateTestCasesFromTestInfo(testEnv, expectedTests);
            var runContext = new MockRunContext(runSettings, testCases, testEnv.ResultsFolderPath);
            var recorder = new MockTestExecutionRecorder();
            var executor = new TestExecutor();

            executor.RunTests(testCases, runContext, recorder);

            ValidateExecutedTests(expectedTests, recorder);
        }


        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void RunWithPytestSetupAndTeardown() {
            var testEnv = TestEnvironment.Create(Version, "Pytest");

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_teardown.py");
            File.Copy(TestData.GetPath("TestData", "TestExecutor", "test_teardown.py"), testFilePath);

            var expectedTests = new[] {
                new PytestExecutionTestInfo(
                    "test_success",
                    "test_teardown.py::TeardownTests::test_success",
                    testFilePath,
                    10,
                    "test_teardown.TeardownTests",
                    TestOutcome.Passed,
                    containedStdOut: new[] { "doing setUp", "doing tearDown" }
                ),
                new PytestExecutionTestInfo(
                    "test_failure",
                    "test_teardown.py::TeardownTests::test_failure",
                    testFilePath,
                    13,
                    "test_teardown.TeardownTests",
                    TestOutcome.Failed,
                    containedStdOut: new[] { "doing setUp", "doing tearDown" }
                ),
            };

            var runSettings = new MockRunSettings(
                MockRunSettingsXmlBuilder.CreateDiscoveryContext("Pytest", testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
            );
            var testCases = CreateTestCasesFromTestInfo(testEnv, expectedTests);
            var runContext = new MockRunContext(runSettings, testCases, testEnv.ResultsFolderPath);
            var recorder = new MockTestExecutionRecorder();
            var executor = new TestExecutor();

            executor.RunTests(testCases, runContext, recorder);

            ValidateExecutedTests(expectedTests, recorder);
        }

        private static List<TestCase> CreateTestCasesFromTestInfo(TestEnvironment testEnv, IEnumerable<PytestExecutionTestInfo> expectedTests) {
            return Enumerable.Select(expectedTests, ti => {
                var testCase = new TestCase(ti.FullyQualifiedName, new Uri(PythonConstants.TestExecutorUriString), ti.FilePath) {
                    DisplayName = ti.DisplayName,
                    CodeFilePath = ti.FilePath,
                    LineNumber = ti.LineNumber,
                };

                string id = ".\\" + ti.FullyQualifiedName;
                string xmlClassName = ti.PyTestXmlClassName;
                string execPath = PathUtils.EnsureEndSeparator(testEnv.SourceFolderPath).ToLower() + ti.FullyQualifiedName;

                testCase.SetPropertyValue<string>((TestProperty)Microsoft.PythonTools.TestAdapter.Pytest.Constants.PytestIdProperty, id);
                testCase.SetPropertyValue<string>((TestProperty)Microsoft.PythonTools.TestAdapter.Pytest.Constants.PyTestXmlClassNameProperty, xmlClassName);
                testCase.SetPropertyValue<string>((TestProperty)Microsoft.PythonTools.TestAdapter.Pytest.Constants.PytestTestExecutionPathPropertery, execPath);
                return (TestCase)testCase;
            }).ToList();
        }

        private static void ValidateExecutedTests(PytestExecutionTestInfo[] expectedTests, MockTestExecutionRecorder recorder) {
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.FullyQualifiedName);

                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.FullyQualifiedName + " had incorrect result");
                Assert.IsTrue(actualResult.Duration >= expectedResult.MinDuration, expectedResult.FullyQualifiedName + " had incorrect duration");

                if (expectedResult.ContainedErrorMessage != null) {
                    AssertUtil.Contains(actualResult.ErrorMessage, expectedResult.ContainedErrorMessage);
                }

                if (expectedResult.ContainedStdOut != null) {
                    var stdOut = actualResult.Messages.Single(m => m.Category == "StdOutMsgs");
                    AssertUtil.Contains(stdOut.Text, expectedResult.ContainedStdOut);
                }
            }
        }

        private static void PrintTestResults(MockTestExecutionRecorder recorder) {
            foreach (var message in recorder.Messages) {
                Console.WriteLine(message);
            }

            foreach (var result in recorder.Results) {
                Console.WriteLine($"FullyQualifiedName: {result.TestCase.FullyQualifiedName}");
                Console.WriteLine($"Outcome: {result.Outcome}");
                Console.WriteLine($"Duration: {result.Duration.TotalMilliseconds}ms");
                foreach(var msg in result.Messages) {
                    Console.WriteLine($"Message {msg.Category}:");
                    Console.WriteLine(msg.Text);
                }
                Console.WriteLine("");
            }
        }
    }

    [TestClass]
    public class TestExecutorTests27 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python27 ?? PythonPaths.Python27_x64;
    }

    [TestClass]
    public class TestExecutorTests35 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python35 ?? PythonPaths.Python35_x64;

        protected override string ImportErrorFormat => NewImportErrorFormat;
    }

    [TestClass]
    public class TestExecutorTests36 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python36 ?? PythonPaths.Python36_x64;

        protected override string ImportErrorFormat => NewImportErrorFormat;
    }

    [TestClass]
    public class TestExecutorTests37 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python37 ?? PythonPaths.Python37_x64;

        protected override string ImportErrorFormat => NewImportErrorFormat;
    }
}
