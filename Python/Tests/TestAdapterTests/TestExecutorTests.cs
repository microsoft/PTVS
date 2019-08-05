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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using TestAdapterTests.Mocks;
using TestUtilities;
using TestUtilities.Python;
using PythonConstants = pt::Microsoft.PythonTools.PythonConstants;

namespace TestAdapterTests {
    [TestClass, Ignore]
    public abstract class TestExecutorTests {
        private const string FrameworkPytest = "Pytest";
        private const string FrameworkUnittest = "Unittest";

        protected abstract PythonVersion Version { get; }

        [ClassCleanup]
        public static void Cleanup() {
            TestEnvironment.Clear();
        }

        [TestInitialize]
        public void CheckVersion() {
            if (Version == null) {
                Assert.Inconclusive("Required version of Python is not installed");
            }
        }

        [TestMethod, Priority(0)]
        public void TestBestFile() {
            var file1 = "C:\\Some\\Path\\file1.py";
            var file2 = "C:\\Some\\Path\\file2.py";
            var best = TestExecutorUnitTest.UpdateBestFile(null, file1);
            Assert.AreEqual(best, file1);

            best = TestExecutorUnitTest.UpdateBestFile(null, file1);
            Assert.AreEqual(best, file1);

            best = TestExecutorUnitTest.UpdateBestFile(best, file2);
            Assert.AreEqual("C:\\Some\\Path", best);
        }

        //To be fixed in https://github.com/microsoft/PTVS/issues/5538
        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        [TestCategory("10s")]
        public void RunUnittest() {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_ut.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "BasicUnittest", "test_ut.py"), testFilePath);

            var expectedTests = new[] {
                new TestInfo(
                    "test_ut_fail",
                    "test_ut.py::TestClassUT::test_ut_fail",
                    testFilePath,
                    4,
                    outcome: TestOutcome.Failed
                ),
                new TestInfo(
                    "test_ut_pass",
                    "test_ut.py::TestClassUT::test_ut_pass",
                    testFilePath,
                    7,
                    outcome: TestOutcome.Passed
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests);
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void RunPytest() {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_pt.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "BasicPytest", "test_pt.py"), testFilePath);

            var expectedTests = new[] {
                new TestInfo(
                    "test_pt_pass",
                    "test_pt.py::test_pt::test_pt_pass",
                    testFilePath,
                    1,
                    outcome: TestOutcome.Passed,
                    pytestXmlClassName: "test_pt",
                    pytestExecPathSuffix: "test_pt_pass"
                ),
                new TestInfo(
                    "test_pt_fail",
                    "test_pt.py::test_pt::test_pt_fail",
                    testFilePath,
                    4,
                    outcome: TestOutcome.Failed,
                    pytestXmlClassName: "test_pt",
                    pytestExecPathSuffix: "test_pt_fail"
                ),
                new TestInfo(
                    "test_method_pass",
                    "test_pt.py::TestClassPT::test_method_pass",
                    testFilePath,
                    8,
                    outcome: TestOutcome.Passed,
                    pytestXmlClassName: "test_pt.TestClassPT"
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests);
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void RunUnittestCancel() {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_cancel.py");
            File.Copy(TestData.GetPath("TestData", "TestExecutor", "test_cancel.py"), testFilePath);

            var expectedTests = new[] {
                new TestInfo(
                    "test_sleep_1",
                    "test_cancel.py::CancelTests::test_sleep_1",
                    testFilePath,
                    5,
                    outcome: TestOutcome.Passed,
                    minDuration: TimeSpan.FromSeconds(0.1),
                    pytestXmlClassName: "test_cancel.CancelTests"
                ),
                new TestInfo(
                    "test_sleep_2",
                    "test_cancel.py::CancelTests::test_sleep_2",
                    testFilePath,
                    8,
                    outcome: TestOutcome.Passed,
                    minDuration: TimeSpan.FromSeconds(5),
                    pytestXmlClassName: "test_cancel.CancelTests"
                ),
                new TestInfo(
                    "test_sleep_3",
                    "test_cancel.py::CancelTests::test_sleep_3",
                    testFilePath,
                    11,
                    outcome: TestOutcome.Passed,
                    minDuration: TimeSpan.FromSeconds(5),
                    pytestXmlClassName: "test_cancel.CancelTests"
                ),
                new TestInfo(
                    "test_sleep_4",
                    "test_cancel.py::CancelTests::test_sleep_4",
                    testFilePath,
                    14,
                    outcome: TestOutcome.Passed,
                    minDuration: TimeSpan.FromSeconds(0.1),
                    pytestXmlClassName: "test_cancel.CancelTests"
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            var testCases = CreateTestCasesFromTestInfo(testEnv, expectedTests);
            var runContext = new MockRunContext(runSettings, testCases, testEnv.ResultsFolderPath);
            var recorder = new MockTestExecutionRecorder();
            var executor = new TestExecutorUnitTest();

            var thread = new System.Threading.Thread(o => {
                executor.RunTests(testCases, runContext, recorder);
            });
            thread.Start();

            // 2 of the tests being run are hard coded to take 5 secs
            Assert.IsTrue(thread.IsAlive);

            System.Threading.Thread.Sleep(100);

            executor.Cancel();
            System.Threading.Thread.Sleep(100);

            // Running all tests should take a bit more than 10 secs
            // Worse case is we had time to start one of the 5 secs sleep test
            // before we asked to cancel, but it definitely should take less
            // than 10 secs because the other 5 secs sleep test should not run.
            // Depending on which assemblies are loaded, it may take some time
            // to obtain the interpreters service.
            Assert.IsTrue(thread.Join(8000));

            System.Threading.Thread.Sleep(100);

            Assert.IsFalse(thread.IsAlive);

            // Canceled test cases do not get recorded
            Assert.IsTrue(recorder.Results.Count < expectedTests.Length);
        }

        [Ignore] // TODO: is this a bug in product? See equivalent test for test discoverer
        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void RunUnittestRelativeImport() {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "RelativeImport"), testEnv.SourceFolderPath);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "relativeimportpackage\\test_relative_import.py");

            var expectedTests = new[] {
                new TestInfo(
                    "test_relative_import",
                    "test_relative_import.py::RelativeImportTests::test_relative_import",
                    testFilePath,
                    5,
                    outcome: TestOutcome.Passed
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath)
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests);
        }

        //To be fixed in https://github.com/microsoft/PTVS/issues/5538
        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        [TestCategory("10s")]
        public void RunUnittestInheritance() {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "Inheritance"), testEnv.SourceFolderPath);

            var baseTestFilePath = Path.Combine(testEnv.SourceFolderPath, "test_base.py");
            var derivedTestFilePath = Path.Combine(testEnv.SourceFolderPath, "test_derived.py");

            var expectedTests = new[] {
                new TestInfo(
                    "test_base_pass",
                    "test_base.py::BaseClassTests::test_base_pass",
                    baseTestFilePath,
                    4,
                    outcome: TestOutcome.Passed
                ),
                new TestInfo(
                    "test_base_fail",
                    "test_base.py::BaseClassTests::test_base_fail",
                    baseTestFilePath,
                    7,
                    outcome: TestOutcome.Failed,
                    containedErrorMessage: "Force a failure in base class code."
                ),
                new TestInfo(
                    "test_base_pass",
                    "test_derived.py::DerivedClassTests::test_base_pass",
                    baseTestFilePath,
                    4,
                    TestOutcome.Passed
                ),
                new TestInfo(
                    "test_base_fail",
                    "test_derived.py::DerivedClassTests::test_base_fail",
                    baseTestFilePath,
                    7,
                    outcome: TestOutcome.Failed,
                    containedErrorMessage: "Force a failure in base class code."
                ),
                new TestInfo(
                    "test_derived_pass",
                    "test_derived.py::DerivedClassTests::test_derived_pass",
                    derivedTestFilePath,
                    5,
                    outcome: TestOutcome.Passed
                ),
                new TestInfo(
                    "test_derived_fail",
                    "test_derived.py::DerivedClassTests::test_derived_fail",
                    derivedTestFilePath,
                    8,
                    outcome: TestOutcome.Failed,
                    containedErrorMessage: "Force a failure in derived class code."
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests);
        }

        //To be fixed in https://github.com/microsoft/PTVS/issues/5538
        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        [TestCategory("10s")]
        public void RunUnittestImportError() {
            // A load error is when unittest module fails to load the test (prior to running it)
            // For example, if the file where the test is defined has an unhandled ImportError.
            // We check that this only causes the tests that can't be loaded to fail,
            // all other tests in the test run which can be loaded successfully will be run.
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "ImportErrorUnittest"), testEnv.SourceFolderPath);

            var testFilePath1 = Path.Combine(testEnv.SourceFolderPath, "test_no_error.py");
            var testFilePath2 = Path.Combine(testEnv.SourceFolderPath, "test_import_error.py");

            var expectedTests = new[] {
                new TestInfo(
                    "test_no_error",
                    "test_no_error.py::NoErrorTests::test_no_error",
                    testFilePath1,
                    4,
                    outcome: TestOutcome.Passed
                ),
                new TestInfo(
                    "test_import_error",
                    "test_import_error.py::ImportErrorTests::test_import_error",
                    testFilePath2,
                    5,
                    outcome: TestOutcome.Failed
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests);
        }

        //To be fixed in https://github.com/microsoft/PTVS/issues/5538
        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        [TestCategory("10s")]
        public void RunUnitTestStackTrace() {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_stack_trace.py");
            File.Copy(TestData.GetPath("TestData", "TestExecutor", "test_stack_trace.py"), testFilePath);

            var expectedTests = new[] {
                new TestInfo(
                    "test_bad_import",
                    "test_stack_trace.py::StackTraceTests::test_bad_import",
                    testFilePath,
                    5,
                    outcome: TestOutcome.Failed,
                    stackFrames: new StackFrame[] {
                        new StackFrame("local_func in global_func", testFilePath, 13),
                        new StackFrame("global_func", testFilePath, 14),
                        new StackFrame("Utility.class_static", testFilePath, 19),
                        new StackFrame("Utility.instance_method_b", testFilePath, 22),
                        new StackFrame("Utility.instance_method_a", testFilePath, 25),
                        new StackFrame("StackTraceTests.test_bad_import", testFilePath, 6),
                    }
                ),
                new TestInfo(
                    "test_not_equal",
                    "test_stack_trace.py::StackTraceTests::test_not_equal",
                    testFilePath,
                    8,
                    outcome: TestOutcome.Failed,
                    stackFrames: new StackFrame[] {
                        new StackFrame("StackTraceTests.test_not_equal", testFilePath, 9),
                    }
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests);
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void RunPytestEnvironmentVariable() {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_env_var.py");
            File.Copy(TestData.GetPath("TestData", "TestExecutor", "test_env_var.py"), testFilePath);

            var expectedTests = new[] {
                new TestInfo(
                    "test_variable",
                    "test_env_var.py::EnvironmentVariableTests::test_variable",
                    testFilePath,
                    5,
                    outcome: TestOutcome.Passed,
                    pytestXmlClassName: "test_env_var.EnvironmentVariableTests"
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .WithEnvironmentVariable("USER_ENV_VAR", "123")
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests);
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void RunPytestDuration() {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_duration.py");
            File.Copy(TestData.GetPath("TestData", "TestExecutor", "test_duration.py"), testFilePath);

            var expectedTests = new[] {
                new TestInfo(
                    "test_sleep_0_1",
                    "test_duration.py::DurationTests::test_sleep_0_1",
                    testFilePath,
                    5,
                    outcome: TestOutcome.Passed,
                    minDuration: TimeSpan.FromSeconds(0.1),
                    pytestXmlClassName: "test_duration.DurationTests"
                ),
                new TestInfo(
                    "test_sleep_0_3",
                    "test_duration.py::DurationTests::test_sleep_0_3",
                    testFilePath,
                    8,
                    TestOutcome.Passed,
                    minDuration: TimeSpan.FromSeconds(0.3),
                    pytestXmlClassName: "test_duration.DurationTests"
                ),
                new TestInfo(
                    "test_sleep_0_5",
                    "test_duration.py::DurationTests::test_sleep_0_5",
                    testFilePath,
                    11,
                    TestOutcome.Failed,
                    minDuration: TimeSpan.FromSeconds(0.5),
                    pytestXmlClassName: "test_duration.DurationTests"
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests);
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void RunUnittestDuration() {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_duration.py");
            File.Copy(TestData.GetPath("TestData", "TestExecutor", "test_duration.py"), testFilePath);

            var expectedTests = new[] {
                new TestInfo(
                    "test_sleep_0_1",
                    "test_duration.py::DurationTests::test_sleep_0_1",
                    testFilePath,
                    5,
                    outcome: TestOutcome.Passed,
                    minDuration: TimeSpan.FromSeconds(0.1)
                ),
                new TestInfo(
                    "test_sleep_0_3",
                    "test_duration.py::DurationTests::test_sleep_0_3",
                    testFilePath,
                    8,
                    outcome: TestOutcome.Passed,
                    minDuration: TimeSpan.FromSeconds(0.3)
                ),
                new TestInfo(
                    "test_sleep_0_5",
                    "test_duration.py::DurationTests::test_sleep_0_5",
                    testFilePath,
                    11,
                    outcome: TestOutcome.Failed,
                    minDuration: TimeSpan.FromSeconds(0.5)
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests);
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void RunPytestSetupAndTeardown() {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_teardown.py");
            File.Copy(TestData.GetPath("TestData", "TestExecutor", "test_teardown.py"), testFilePath);

            var expectedTests = new[] {
                new TestInfo(
                    "test_success",
                    "test_teardown.py::TeardownTests::test_success",
                    testFilePath,
                    10,
                    outcome: TestOutcome.Passed,
                    containedStdOut: new[] { "doing setUp", "doing tearDown" },
                    pytestXmlClassName: "test_teardown.TeardownTests"
                ),
                new TestInfo(
                    "test_failure",
                    "test_teardown.py::TeardownTests::test_failure",
                    testFilePath,
                    13,
                    outcome: TestOutcome.Failed,
                    containedStdOut: new[] { "doing setUp", "doing tearDown" },
                    pytestXmlClassName: "test_teardown.TeardownTests"
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests);
        }

        private static void ExecuteTests(TestEnvironment testEnv, MockRunSettings runSettings, TestInfo[] expectedTests) {
            var testCases = CreateTestCasesFromTestInfo(testEnv, expectedTests);
            var runContext = new MockRunContext(runSettings, testCases, testEnv.ResultsFolderPath);
            var recorder = new MockTestExecutionRecorder();

            ITestExecutor executor = null;
            switch (testEnv.TestFramework) {
                case FrameworkPytest:
                    executor = new TestExecutorPytest();
                    break;

                case FrameworkUnittest:
                    executor = new TestExecutorUnitTest();
                    break;
                default:
                    Assert.Fail();
                    break;
            }

            executor.RunTests(testCases, runContext, recorder);

            ValidateExecutedTests(expectedTests, recorder);
        }

        private static List<TestCase> CreateTestCasesFromTestInfo(TestEnvironment testEnv, IEnumerable<TestInfo> expectedTests) {
            return Enumerable.Select(expectedTests, ti => {
                var testCase = new TestCase(ti.FullyQualifiedName, testEnv.ExecutionUri, ti.FilePath) {
                    DisplayName = ti.DisplayName,
                    CodeFilePath = ti.FilePath,
                    LineNumber = ti.LineNumber,
                };

                string id = ".\\" + ti.FullyQualifiedName;
                if (testEnv.TestFramework == FrameworkPytest) {
                    var classParts = ti.PytestXmlClassName.Split('.');
                    id = (classParts.Length > 1) ? ".\\" + ti.FullyQualifiedName : ".\\" + Path.GetFileName(ti.FilePath) + "::" + ti.DisplayName;
                }
             
                string xmlClassName = ti.PytestXmlClassName;

                // FullyQualifiedName as exec path suffix only works for test class case,
                // for standalone methods, specify the exec path suffix when creating TestInfo.
                string execPath;
                if (ti.PytestExecPathSuffix != null) {
                    execPath = PathUtils.EnsureEndSeparator(ti.FilePath).ToLower() + "::" + ti.PytestExecPathSuffix;
                } else {
                    execPath = PathUtils.EnsureEndSeparator(testEnv.SourceFolderPath).ToLower() + ti.FullyQualifiedName;
                }

                testCase.SetPropertyValue<string>((TestProperty)Microsoft.PythonTools.TestAdapter.Pytest.Constants.PytestIdProperty, id);
                testCase.SetPropertyValue<string>((TestProperty)Microsoft.PythonTools.TestAdapter.Pytest.Constants.PyTestXmlClassNameProperty, xmlClassName);
                testCase.SetPropertyValue<string>((TestProperty)Microsoft.PythonTools.TestAdapter.Pytest.Constants.PytestTestExecutionPathPropertery, execPath);
                return (TestCase)testCase;
            }).ToList();
        }

        private static void ValidateExecutedTests(TestInfo[] expectedTests, MockTestExecutionRecorder recorder) {
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

                if (expectedResult.StackFrames != null) {
                    ValidateStackFrame(actualResult, expectedResult.StackFrames);
                }
            }
        }

        private static void ValidateStackFrame(TestResult result, StackFrame[] expectedFrames) {
            var stackTrace = result.ErrorStackTrace;
            var parser = new PythonStackTraceParser();
            var frames = parser.GetStackFrames(stackTrace).ToArray();

            Console.WriteLine("Actual frames:");
            foreach (var f in frames) {
                Console.WriteLine("\"{0}\",\"{1}\",\"{2}\"", f.MethodDisplayName, f.FileName, f.LineNumber);
            }

            CollectionAssert.AreEqual(expectedFrames, frames, new StackFrameComparer());
        }

        private static void PrintTestResults(MockTestExecutionRecorder recorder) {
            foreach (var message in recorder.Messages) {
                Console.WriteLine(message);
            }

            foreach (var result in recorder.Results) {
                Console.WriteLine($"FullyQualifiedName: {result.TestCase.FullyQualifiedName}");
                Console.WriteLine($"Outcome: {result.Outcome}");
                Console.WriteLine($"Duration: {result.Duration.TotalMilliseconds}ms");
                foreach (var msg in result.Messages) {
                    Console.WriteLine($"Message {msg.Category}:");
                    Console.WriteLine(msg.Text);
                }
                Console.WriteLine("");
            }
        }

        class StackFrameComparer : IComparer {
            public int Compare(object x, object y) {
                if (x == y) {
                    return 0;
                }

                var a = x as StackFrame;
                var b = y as StackFrame;

                if (a == null) {
                    return -1;
                }

                if (b == null) {
                    return 1;
                }

                int res = a.FileName.CompareTo(b.FileName);
                if (res != 0) {
                    return res;
                }

                res = a.LineNumber.CompareTo(b.LineNumber);
                if (res != 0) {
                    return res;
                }

                res = a.MethodDisplayName.CompareTo(b.MethodDisplayName);
                if (res != 0) {
                    return res;
                }

                return 0;
            }
        }
    }

    [TestClass]
    public class TestExecutorTests27 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python27_x64 ?? PythonPaths.Python27;
    }

    [TestClass]
    public class TestExecutorTests35 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python35_x64 ?? PythonPaths.Python35;
    }

    [TestClass]
    public class TestExecutorTests36 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python36_x64 ?? PythonPaths.Python36;
    }

    [TestClass]
    public class TestExecutorTests37 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python37_x64 ?? PythonPaths.Python37;
    }
}
