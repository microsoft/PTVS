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

using TestAdapterTests.Mocks;

namespace TestAdapterTests
{
    [TestClass, Ignore]
    public abstract class TestExecutorTests
    {
        private const string FrameworkPytest = "Pytest";
        private const string FrameworkUnittest = "Unittest";

        protected abstract PythonVersion Version { get; }

        [ClassCleanup]
        public static void Cleanup()
        {
            TestEnvironment.Clear();
        }

        [TestInitialize]
        public void CheckVersion()
        {
            if (Version == null)
            {
                Assert.Inconclusive("Required version of Python is not installed");
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        [TestCategory("10s")]
        public void RunUnittest()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            var testFile1Path = Path.Combine(testEnv.SourceFolderPath, "test_ut.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "BasicUnittest", "test_ut.py"), testFile1Path);

            var testFile2Path = Path.Combine(testEnv.SourceFolderPath, "test_runtest.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "BasicUnittest", "test_runtest.py"), testFile2Path);

            var expectedTests = new[] {
                new TestInfo(
                    "test_ut_fail",
                    "test_ut.py::TestClassUT::test_ut_fail",
                    testFile1Path,
                    4,
                    outcome: TestOutcome.Failed
                ),
                new TestInfo(
                    "test_ut_pass",
                    "test_ut.py::TestClassUT::test_ut_pass",
                    testFile1Path,
                    7,
                    outcome: TestOutcome.Passed
                ),
                new TestInfo(
                    "runTest",
                    "test_runtest.py::TestClassRunTest::runTest",
                    testFile2Path,
                    4,
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

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunUnittestLargeTestCount()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            // Test that we don't try passing 1000 tests via command line arguments
            // since that would exceed the 32k limit and fail.
            var testContentsFormat = @"import unittest

class ManyTest(unittest.TestCase):
{0}

if __name__ == '__main__':
    unittest.main()
";
            var testFunctions = new StringBuilder();
            var expectedTests = new List<TestInfo>();
            var moduleName = "test_many";
            var className = "ManyTest";
            var testFilePath = Path.Combine(testEnv.SourceFolderPath, $"{moduleName}.py");

            for (int i = 0; i < 1000; i++)
            {
                var funcName = $"test_func_{i}";
                testFunctions.AppendLine($"    def {funcName}(self): pass");

                expectedTests.Add(new TestInfo(
                    funcName,
                    $"{moduleName}.py::{className}::{funcName}",
                    testFilePath,
                    4 + i,
                    outcome: TestOutcome.Passed
                ));
            }

            var testContents = string.Format(testContentsFormat, testFunctions.ToString());
            File.WriteAllText(testFilePath, testContents);

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests.ToArray());
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        [TestCategory("10s")]
        public void RunPytest()
        {
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


        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunPytestParameterizedAndDiscovery()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            var testFilePath1 = Path.Combine(testEnv.SourceFolderPath, "test_indirect_list.py");
            var testFilePath2 = Path.Combine(testEnv.SourceFolderPath, "test_Parameters.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "Parameterized", "test_indirect_list.py"), testFilePath1);
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "Parameterized", "test_Parameters.py"), testFilePath2);

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );
            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new PytestTestDiscoverer();
            discoverer.DiscoverTests(new[] { testFilePath1, testFilePath2 }, discoveryContext, logger, discoverySink);

            Console.WriteLine($"Discovered Tests");
            foreach (var test in discoverySink.Tests)
            {
                Console.WriteLine($"{test.DisplayName}");
            }

            Assert.IsTrue(discoverySink.Tests.Any());
            Assert.AreEqual(23, discoverySink.Tests.Count());

            var testCases = discoverySink.Tests;
            var runContext = new MockRunContext(runSettings, testCases, testEnv.ResultsFolderPath);
            var recorder = new MockTestExecutionRecorder();
            var executor = new PytestTestExecutor();
            executor.RunTests(testCases, runContext, recorder);

            PrintTestResults(recorder);

            // Check FQN parameter set doesn't contain "."
            Assert.IsFalse(recorder.Results
                .Select(tr => tr.TestCase.FullyQualifiedName)
                .Where(fqn => fqn.IndexOf('[') != -1)
                .Any(fqn => fqn.Substring(fqn.IndexOf('[')).Contains(".")));
            Assert.IsFalse(recorder.Results.Any(tr => tr.TestCase.DisplayName.Contains(".")));
            Assert.IsFalse(recorder.Results.Any(tr => tr.Outcome != TestOutcome.Passed));
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunPytestSubmoduleWithIniAndDiscovery()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestExplorerPytestSubmodule"), testEnv.SourceFolderPath);
            var testFilePath1 = Path.Combine(testEnv.SourceFolderPath, "Tests\\test_pt.py");
            var pytestIniPath = Path.Combine(testEnv.SourceFolderPath, "Tests\\pytest.ini");

            Assert.IsTrue(File.Exists(pytestIniPath), $"File path '{pytestIniPath}' does not exist");

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath1)
                    .ToXml()
            );
            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new PytestTestDiscoverer();
            discoverer.DiscoverTests(new[] { testFilePath1 }, discoveryContext, logger, discoverySink);

            Console.WriteLine($"Discovered Tests");
            foreach (var test in discoverySink.Tests)
            {
                Console.WriteLine($"{test.DisplayName}");
            }

            Assert.IsTrue(discoverySink.Tests.Any());
            Assert.AreEqual(discoverySink.Tests.Count(), 1);
            Assert.IsTrue(discoverySink.Tests.First().FullyQualifiedName.Contains("Tests\\"));

            var testCases = discoverySink.Tests;
            var runContext = new MockRunContext(runSettings, testCases, testEnv.ResultsFolderPath);
            var recorder = new MockTestExecutionRecorder();
            var executor = new PytestTestExecutor();
            executor.RunTests(testCases, runContext, recorder);

            PrintTestResults(recorder);

            Assert.AreEqual(recorder.Results.Count(), 1);
            //Tests will be skipped if pytest.ini location is used for execution instead of rootdir
            Assert.AreEqual(TestOutcome.Passed, recorder.Results.First().Outcome);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunPytestMissingPytestIdShowsErrorAndReturnsPartialResults()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            var testFilePath1 = Path.Combine(testEnv.SourceFolderPath, "test_indirect_list.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "Parameterized", "test_indirect_list.py"), testFilePath1);

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );
            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new PytestTestDiscoverer();
            discoverer.DiscoverTests(new[] { testFilePath1 }, discoveryContext, logger, discoverySink);

            Console.WriteLine($"Discovered Tests");
            foreach (var test in discoverySink.Tests)
            {
                Console.WriteLine($"{test.DisplayName}");
            }

            Assert.IsTrue(discoverySink.Tests.Any());
            Assert.AreEqual(discoverySink.Tests.Count(), 1);

            // create a Missing Test with valid file but missing pytestId
            var tc = discoverySink.Tests.First();
            var validId = tc.GetPropertyValue<string>(Microsoft.PythonTools.TestAdapter.Pytest.Constants.PytestIdProperty, "");
            var missingTest = new TestCase(tc.DisplayName + "_copy", tc.ExecutorUri, tc.Source);
            missingTest.CodeFilePath = tc.CodeFilePath;
            var missingPytestId = validId + "_copy";
            missingTest.SetPropertyValue<string>(Microsoft.PythonTools.TestAdapter.Pytest.Constants.PytestIdProperty, missingPytestId);
            discoverySink.Tests.Add(missingTest);

            var testCases = discoverySink.Tests;
            var runContext = new MockRunContext(runSettings, testCases, testEnv.ResultsFolderPath);
            var recorder = new MockTestExecutionRecorder();
            var executor = new PytestTestExecutor();
            executor.RunTests(testCases, runContext, recorder);

            //Check for Error Message
            var errors = string.Join(Environment.NewLine, recorder.Messages);
            AssertUtil.Contains(
                errors,
                "ERROR: not found:"
            );

            PrintTestResults(recorder);

            //Check for Partial Results
            Assert.IsTrue(recorder.Results.Any());
            Assert.AreEqual(TestOutcome.Passed, recorder.Results.Single(r => r.TestCase.DisplayName == discoverySink.Tests[0].DisplayName).Outcome);
            Assert.AreEqual(TestOutcome.Skipped, recorder.Results.Single(r => r.TestCase.DisplayName == discoverySink.Tests[1].DisplayName).Outcome);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunPytestUppercaseFileName()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_Uppercase.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "Uppercase", "test_Uppercase.py"), testFilePath);

            var expectedTests = new[] {
                new TestInfo(
                   "test_A",
                   "test_Uppercase.py::Test_UppercaseClass::test_A",
                    testFilePath,
                    4,
                    outcome: TestOutcome.Passed,
                    pytestXmlClassName: "test_Uppercase.Test_UppercaseClass"
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunPytestSubpackages()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestExecutor", "SubPackages"), testEnv.SourceFolderPath);

            var testFilePath1 = Path.Combine(testEnv.SourceFolderPath, "package1\\packageA\\test1.py");
            var testFilePath2 = Path.Combine(testEnv.SourceFolderPath, "package1\\packageA\\test2.py");

            var expectedTests = new[] {
                new TestInfo(
                   "test_A",
                   "package1\\packageA\\test1.py::Test_test1::test_A",
                    testFilePath1,
                    4,
                    outcome: TestOutcome.Passed,
                    pytestXmlClassName: "package1.packageA.test1.Test_test1"
                ),
                 new TestInfo(
                   "test_A",
                   "package1\\packageA\\test2.py::Test_test2::test_A",
                    testFilePath2,
                    4,
                    outcome: TestOutcome.Passed,
                    pytestXmlClassName: "package1.packageA.test2.Test_test2"
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath1)
                    .WithTestFile(testFilePath2)
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunPytestLargeTestCount()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            // Test that we don't try passing 1000 tests via command line arguments
            // since that would exceed the 32k limit and fail.
            var testContents = new StringBuilder();
            var expectedTests = new List<TestInfo>();
            var moduleName = "test_many";
            var testFilePath = Path.Combine(testEnv.SourceFolderPath, $"{moduleName}.py");

            for (int i = 0; i < 1000; i++)
            {
                var funcName = $"test_func_{i}";
                testContents.AppendLine($"def {funcName}(): pass");

                expectedTests.Add(new TestInfo(
                    funcName,
                    $"{moduleName}.py::{moduleName}::{funcName}",
                    testFilePath,
                    i + 1,
                    outcome: TestOutcome.Passed,
                    pytestXmlClassName: moduleName
                ));
            }

            File.WriteAllText(testFilePath, testContents.ToString());

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests.ToArray());
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunUnittestCancel()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);
            var executor = new UnittestTestExecutor();
            TestCancel(testEnv, executor);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunPytestCancel()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);
            var executor = new PytestTestExecutor();
            TestCancel(testEnv, executor);
        }


        private void TestCancel(TestEnvironment testEnv, ITestExecutor executor)
        {
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

            var thread = new System.Threading.Thread(o =>
            {
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


        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunUnittestRelativeImport()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "RelativeImport"), testEnv.SourceFolderPath);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "relativeimportpackage\\test_relative_import.py");

            var expectedTests = new[] {
                new TestInfo(
                    "test_relative_import",
                    "relativeimportpackage\\test_relative_import.py::RelativeImportTests::test_relative_import",
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

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunUnittestInheritance()
        {
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

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunUnittestImportError()
        {
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

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunUnittestCoverage()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest, installCoverage: true);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestExecutor", "Coverage"), testEnv.SourceFolderPath);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_coverage.py");

            var expectedTests = new[] {
                new TestInfo(
                    "test_one",
                    "test_coverage.py::TestCoverage::test_one",
                    testFilePath,
                    6,
                    outcome: TestOutcome.Passed
                ),
                new TestInfo(
                    "test_one",
                    "test_coverage.py::TestCoverage::test_two",
                    testFilePath,
                    10,
                    outcome: TestOutcome.Passed
                ),
            };

            var expectedCoverages = new[] {
                new CoverageInfo(
                    "test_coverage.py",
                    new[] { 1, 3, 5, 6, 7, 8, 10, 11, 13, 16 }
                ),
                new CoverageInfo(
                    "package1\\__init__.py",
                    new[] { 1, 2, 3, 4, 5, 9, 10, 12 }
                )
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath)
                    .WithCoverage()
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests, expectedCoverages);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunPytestCoverage()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest, installCoverage: true);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestExecutor", "Coverage"), testEnv.SourceFolderPath);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_coverage.py");

            var expectedTests = new[] {
                new TestInfo(
                    "test_one",
                    "test_coverage.py::TestCoverage::test_one",
                    testFilePath,
                    6,
                    outcome: TestOutcome.Passed,
                    pytestXmlClassName: "test_coverage.TestCoverage"
                ),
                new TestInfo(
                    "test_one",
                    "test_coverage.py::TestCoverage::test_two",
                    testFilePath,
                    10,
                    outcome: TestOutcome.Passed,
                    pytestXmlClassName: "test_coverage.TestCoverage"
                ),
                new TestInfo(
                    "test_global",
                    "test_coverage.py::test_coverage::test_global",
                    testFilePath,
                    13,
                    outcome: TestOutcome.Passed,
                    pytestXmlClassName: "test_coverage",
                    pytestExecPathSuffix: "test_global"
                ),
            };

            var expectedCoverages = new[] {
                new CoverageInfo(
                    "test_coverage.py",
                    new[] { 1, 3, 5, 6, 7, 8, 10, 11, 13, 14, 16 }
                ),
                new CoverageInfo(
                    "package1\\__init__.py",
                    new[] { 1, 2, 3, 4, 5, 9, 10, 12 }
                )
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath)
                    .WithCoverage()
                    .ToXml()
            );

            ExecuteTests(testEnv, runSettings, expectedTests, expectedCoverages);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunUnitTestStackTrace()
        {
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

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunPytestEnvironmentVariable()
        {
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

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunPytestDuration()
        {
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

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunUnittestDuration()
        {
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

        [TestMethod, Priority(UnitTestPriority.P0)]
        [TestCategory("10s")]
        public void RunPytestSetupAndTeardown()
        {
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

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunPytestCodeFilePathNotFound()
        {
            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder("pytest", "", "", "")
                    .WithTestFile("DummyFilePath")
                    .ToXml()
            );

            var differentDummyFilePath = "DifferentDummyFilePath";
            var testCases = new List<TestCase>() { new TestCase("fakeTest", pt.Microsoft.PythonTools.PythonConstants.PytestExecutorUri, differentDummyFilePath) { CodeFilePath = differentDummyFilePath } };
            var runContext = new MockRunContext(runSettings, testCases, "");
            var recorder = new MockTestExecutionRecorder();
            var executor = new PytestTestExecutor();

            //should not throw
            executor.RunTests(testCases, runContext, recorder);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void RunPytestNullCodeFilePath()
        {
            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder("pytest", "", "", "")
                    .WithTestFile("DummyFilePath")
                    .ToXml()
            );

            var differentDummyFilePath = "DifferentDummyFilePath";
            var testCases = new List<TestCase>() { new TestCase("fakeTest", pt.Microsoft.PythonTools.PythonConstants.PytestExecutorUri, differentDummyFilePath) { CodeFilePath = null } };
            var runContext = new MockRunContext(runSettings, testCases, "");
            var recorder = new MockTestExecutionRecorder();
            var executor = new PytestTestExecutor();

            //should not throw
            executor.RunTests(testCases, runContext, recorder);
        }

        private static void ExecuteTests(TestEnvironment testEnv, MockRunSettings runSettings, TestInfo[] expectedTests, CoverageInfo[] expectedCoverages = null)
        {
            var testCases = CreateTestCasesFromTestInfo(testEnv, expectedTests);
            var runContext = new MockRunContext(runSettings, testCases, testEnv.ResultsFolderPath);
            var recorder = new MockTestExecutionRecorder();

            ITestExecutor executor = null;
            switch (testEnv.TestFramework)
            {
                case FrameworkPytest:
                    executor = new PytestTestExecutor();
                    break;

                case FrameworkUnittest:
                    executor = new UnittestTestExecutor();
                    break;
                default:
                    Assert.Fail();
                    break;
            }

            executor.RunTests(testCases, runContext, recorder);

            ValidateExecutedTests(expectedTests, recorder);
            ValidateCoverage(testEnv.SourceFolderPath, expectedCoverages, recorder);
        }

        private static List<TestCase> CreateTestCasesFromTestInfo(TestEnvironment testEnv, IEnumerable<TestInfo> expectedTests)
        {
            return Enumerable.Select(expectedTests, ti =>
            {
                var testCase = new TestCase(ti.FullyQualifiedName, testEnv.ExecutionUri, ti.FilePath)
                {
                    DisplayName = ti.DisplayName,
                    CodeFilePath = ti.FilePath,
                    LineNumber = ti.LineNumber,
                };

                string id = ".\\" + ti.FullyQualifiedName;
                if (testEnv.TestFramework == FrameworkPytest)
                {
                    var classParts = ti.PytestXmlClassName.Split('.');
                    id = (classParts.Length > 1) ? id : ".\\" + Path.GetFileName(ti.FilePath) + "::" + ti.DisplayName;
                }

                // FullyQualifiedName as exec path suffix only works for test class case,
                // for standalone methods, specify the exec path suffix when creating TestInfo.
                string execPath;
                if (ti.PytestExecPathSuffix != null)
                {
                    execPath = PathUtils.EnsureEndSeparator(ti.FilePath).ToLower() + "::" + ti.PytestExecPathSuffix;
                }
                else
                {
                    execPath = PathUtils.EnsureEndSeparator(testEnv.SourceFolderPath).ToLower() + ti.FullyQualifiedName;
                }

                testCase.SetPropertyValue<string>((TestProperty)Microsoft.PythonTools.TestAdapter.Pytest.Constants.PytestIdProperty, id);
                return (TestCase)testCase;
            }).ToList();
        }

        private static void ValidateExecutedTests(TestInfo[] expectedTests, MockTestExecutionRecorder recorder)
        {
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests)
            {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.FullyQualifiedName);

                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.FullyQualifiedName + " had incorrect result");
                Assert.IsTrue(actualResult.Duration >= expectedResult.MinDuration, expectedResult.FullyQualifiedName + " had incorrect duration");

                if (expectedResult.ContainedErrorMessage != null)
                {
                    AssertUtil.Contains(actualResult.ErrorMessage, expectedResult.ContainedErrorMessage);
                }

                if (expectedResult.ContainedStdOut != null)
                {
                    var stdOut = actualResult.Messages.Single(m => m.Category == "StdOutMsgs");
                    AssertUtil.Contains(stdOut.Text, expectedResult.ContainedStdOut);
                }

                if (expectedResult.StackFrames != null)
                {
                    ValidateStackFrame(actualResult, expectedResult.StackFrames);
                }
            }
        }

        private static void ValidateStackFrame(TestResult result, StackFrame[] expectedFrames)
        {
            var stackTrace = result.ErrorStackTrace;
            var parser = new PythonStackTraceParser();
            var frames = parser.GetStackFrames(stackTrace).ToArray();

            Console.WriteLine("Actual frames:");
            foreach (var f in frames)
            {
                Console.WriteLine("\"{0}\",\"{1}\",\"{2}\"", f.MethodDisplayName, f.FileName, f.LineNumber);
            }

            CollectionAssert.AreEqual(expectedFrames, frames, new StackFrameComparer());
        }

        private static void PrintTestResults(MockTestExecutionRecorder recorder)
        {
            Console.WriteLine("Messages:");
            foreach (var message in recorder.Messages)
            {
                Console.WriteLine(message);
            }
            Console.WriteLine("");

            Console.WriteLine("Attachments:");
            foreach (var attachment in recorder.Attachments)
            {
                Console.WriteLine($"DisplayName: {attachment.DisplayName}");
                Console.WriteLine($"Uri: {attachment.Uri}");
                Console.WriteLine($"Count: {attachment.Attachments.Count}");
                Console.WriteLine("");
            }
            Console.WriteLine("");

            Console.WriteLine("Results:");
            foreach (var result in recorder.Results)
            {
                Console.WriteLine($"FullyQualifiedName: {result.TestCase.FullyQualifiedName}");
                Console.WriteLine($"Outcome: {result.Outcome}");
                Console.WriteLine($"Duration: {result.Duration.TotalMilliseconds}ms");
                foreach (var msg in result.Messages)
                {
                    Console.WriteLine($"Message {msg.Category}:");
                    Console.WriteLine(msg.Text);
                }
                Console.WriteLine("");
            }
        }

        private static void ValidateCoverage(string sourceDir, CoverageInfo[] expectedCoverages, MockTestExecutionRecorder recorder)
        {
            var coverageAttachment = recorder.Attachments.SingleOrDefault(x => x.Uri == pt.Microsoft.PythonTools.PythonConstants.PythonCodeCoverageUri);
            if (expectedCoverages != null)
            {
                Assert.IsNotNull(coverageAttachment, "Coverage attachment not found");
                Assert.AreEqual(1, coverageAttachment.Attachments.Count, "Expected 1 coverage data item");

                var coverageItem = coverageAttachment.Attachments[0];
                var coverageFilePath = coverageItem.Uri.LocalPath;
                Assert.IsTrue(File.Exists(coverageFilePath), $"File path '{coverageFilePath}' does not exist");

                ValidateCoverage(sourceDir, expectedCoverages, coverageFilePath);
            }
            else
            {
                Assert.IsNull(coverageAttachment, "Coverage attachment should not have been found");
            }
        }

        private static void ValidateCoverage(string sourceDir, CoverageInfo[] expectedCoverages, string coverageFilePath)
        {
            using (var stream = new FileStream(coverageFilePath, FileMode.Open, FileAccess.Read))
            {
                var converter = new CoveragePyConverter(sourceDir, stream);
                var result = converter.Parse()
                    .Where(fi => PathUtils.IsSubpathOf(sourceDir, fi.Filename))
                    .ToArray();

                Assert.AreEqual(expectedCoverages.Length, result.Length, "Unexpected number of files in coverage results");

                foreach (var expectedInfo in expectedCoverages)
                {
                    var filePath = Path.Combine(sourceDir, expectedInfo.FileName);
                    var actual = result.SingleOrDefault(x => PathUtils.IsSamePath(x.Filename, filePath));
                    Assert.IsNotNull(actual, $"Expected coverage result for '{filePath}'");

                    AssertUtil.ContainsExactly(actual.Hits, expectedInfo.CoveredLines);
                }
            }
        }

        class CoverageInfo
        {
            public CoverageInfo(string fileName, int[] coveredLines)
            {
                FileName = fileName;
                CoveredLines = coveredLines;
            }

            public string FileName { get; }

            public int[] CoveredLines { get; }
        }

        class StackFrameComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                if (x == y)
                {
                    return 0;
                }

                var a = x as StackFrame;
                var b = y as StackFrame;

                if (a == null)
                {
                    return -1;
                }

                if (b == null)
                {
                    return 1;
                }

                int res = a.FileName.CompareTo(b.FileName);
                if (res != 0)
                {
                    return res;
                }

                res = a.LineNumber.CompareTo(b.LineNumber);
                if (res != 0)
                {
                    return res;
                }

                res = a.MethodDisplayName.CompareTo(b.MethodDisplayName);
                if (res != 0)
                {
                    return res;
                }

                return 0;
            }
        }
    }

    [TestClass]
    public class TestExecutorTests27 : TestExecutorTests
    {
        [ClassInitialize]
        public static void DoDeployment(TestContext context)
        {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python27_x64 ?? PythonPaths.Python27;
    }

    [TestClass]
    public class TestExecutorTests35 : TestExecutorTests
    {
        [ClassInitialize]
        public static void DoDeployment(TestContext context)
        {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python35_x64 ?? PythonPaths.Python35;
    }

    [TestClass]
    public class TestExecutorTests37 : TestExecutorTests
    {
        [ClassInitialize]
        public static void DoDeployment(TestContext context)
        {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python37_x64 ?? PythonPaths.Python37;
    }
}
