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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using PythonConstants = pt::Microsoft.PythonTools.PythonConstants;

namespace TestAdapterTests {
    [TestClass, Ignore]
    public abstract partial class TestDiscovererTests {
        protected abstract PythonVersion Version { get; }

        [TestInitialize]
        public void CheckVersion() {
            if (Version == null) {
                Assert.Inconclusive("Required version of Python is not installed");
            }
        }

        [TestMethod, Priority(0)]
        public void DiscoverWithPytest() {
            var testEnv = TestEnvironment.Create(Version, "Pytest");

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_pt.py");
            File.Copy(TestData.GetPath("TestData", "TestExplorerPytest", "test_pt.py"), testFilePath);

            var expectedTests = new[] {
                new DiscoveryTestInfo("test_pt_pass", "test_pt.py::test_pt::test_pt_pass", testFilePath, 1),
                new DiscoveryTestInfo("test_pt_fail", "test_pt.py::test_pt::test_pt_fail", testFilePath, 4),
                new DiscoveryTestInfo("test_method_pass", "test_pt.py::TestClassPT::test_method_pass", testFilePath, 8),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            DiscoverTests(new[] { testFilePath }, runSettings, expectedTests);
        }

        [TestMethod, Priority(0)]
        public void DiscoverWithPytestSearchPath() {
            // test_search_path.py has an import at global scope that requires search path to resolve
            var testEnv = TestEnvironment.Create(Version, "Pytest");

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "ImportFromSearchPath"), testEnv.SourceFolderPath);

            // <SourceFolderPath>/TestFolder/
            // <SourceFolderPath>/TestFolder/test_search_path.py
            // <SourceFolderPath>/SearchPath/
            // <SourceFolderPath>/SearchPath/searchpathmodule.py
            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "TestFolder", "test_search_path.py");
            var searchPath = Path.Combine(testEnv.SourceFolderPath, "SearchPath");

            var expectedTests = new[] {
                new DiscoveryTestInfo("test_imported_module", "testfolder\\test_search_path.py::SearchPathTests::test_imported_module", testFilePath, 5),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath)
                    .WithSearchPath(searchPath)
                    .ToXml()
            );

            DiscoverTests(new[] { testFilePath }, runSettings, expectedTests);
        }

        [Ignore] // discovers 0 tests, maybe pytest cannot handle this?
        [TestMethod, Priority(0)]
        public void DiscoverWithPytestSyntaxError() {
            // one file has a valid passing test,
            // the other has a test with a syntax error in it
            var testEnv = TestEnvironment.Create(Version, "Pytest");

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "SyntaxError"), testEnv.SourceFolderPath);

            var testFilePath1 = Path.Combine(testEnv.SourceFolderPath, "test_basic.py");
            var testFilePath2 = Path.Combine(testEnv.SourceFolderPath, "test_syntax_error.py");

            var expectedTests = new[] {
                new DiscoveryTestInfo("test_success", "test_basic.py::test_basic::test_success", testFilePath1, 1),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath1)
                    .WithTestFile(testFilePath2)
                    .ToXml()
            );

            DiscoverTests(new[] { testFilePath1, testFilePath2 }, runSettings, expectedTests);
        }

        [Ignore] // discovers 0 tests, maybe pytest cannot handle this?
        [TestMethod, Priority(0)]
        public void DiscoverWithPytestImportError() {
            // one file has a valid passing test,
            // the other has an unknown module import at global scope
            var testEnv = TestEnvironment.Create(Version, "Pytest");

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "ImportError"), testEnv.SourceFolderPath);

            var testFilePath1 = Path.Combine(testEnv.SourceFolderPath, "test_basic.py");
            var testFilePath2 = Path.Combine(testEnv.SourceFolderPath, "test_import_error.py");

            var expectedTests = new[] {
                new DiscoveryTestInfo("test_success", "test_basic.py::test_basic::test_success", testFilePath1, 1),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath1)
                    .WithTestFile(testFilePath2)
                    .ToXml()
            );

            DiscoverTests(new[] { testFilePath1, testFilePath2 }, runSettings, expectedTests);
        }

        [Ignore] // discovers 3 tests instead of 2, it shouldn't be finding the one in example_pt.py
        [TestMethod, Priority(0)]
        public void DiscoverWithPytestConfigPythonFiles() {
            var testEnv = TestEnvironment.Create(Version, "Pytest");

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "ConfigPythonFiles"), testEnv.SourceFolderPath);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_pt.py");
            var checkFilePath = Path.Combine(testEnv.SourceFolderPath, "check_pt.py");
            var exampleFilePath = Path.Combine(testEnv.SourceFolderPath, "example_pt.py");

            // pytest.ini declares that tests are only files named check_*.py and test_*.py
            // so the test defined in example_pt.py should not be discovered
            var expectedTests = new[] {
                new DiscoveryTestInfo("test_1", "test_pt.py::test_pt::test_1", testFilePath, 1),
                new DiscoveryTestInfo("test_2", "check_pt.py::check_pt::test_2", checkFilePath, 1),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(checkFilePath)
                    .WithTestFile(testFilePath)
                    .WithTestFile(exampleFilePath)
                    .ToXml()
            );

            DiscoverTests(new[] { checkFilePath, testFilePath, exampleFilePath }, runSettings, expectedTests);
        }

        [TestMethod, Priority(0)]
        public void DiscoverWithPytestConfigPythonFunctions() {
            var testEnv = TestEnvironment.Create(Version, "Pytest");

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "ConfigPythonFunctions"), testEnv.SourceFolderPath);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_misc_prefixes.py");

            // pytest.ini declares that tests are only functions named check_* and verify_*
            // so the test named test_* and example_* should not be discovered
            var expectedTests = new[] {
                new DiscoveryTestInfo("check_func", "test_misc_prefixes.py::test_misc_prefixes::check_func", testFilePath, 4),
                new DiscoveryTestInfo("verify_func", "test_misc_prefixes.py::test_misc_prefixes::verify_func", testFilePath, 10),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath)
                    .ToXml()
            );

            DiscoverTests(new[] { testFilePath }, runSettings, expectedTests);
        }

        [TestMethod, Priority(0)]
        public void DiscoverWithPytestNotInstalled() {
            var testEnv = TestEnvironment.Create(Version, "Pytest", installFramework: false);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_pt.py");
            File.Copy(TestData.GetPath("TestData", "TestExplorerPytest", "test_pt.py"), testFilePath);

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new PythonTestDiscoverer();

            discoverer.DiscoverTests(new[] { testFilePath }, discoveryContext, logger, discoverySink);
            Assert.AreEqual(0, discoverySink.Tests.Count);

            var errors = string.Join(Environment.NewLine, logger.GetErrors());
            AssertUtil.Contains(errors, "ModuleNotFoundError: No module named 'pytest'");
        }

        [TestMethod, Priority(0)]
        public void DiscoverWithUnittest() {
            var testEnv = TestEnvironment.Create(Version, "Unittest");

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_ut.py");
            File.Copy(TestData.GetPath("TestData", "TestExplorerPytest", "test_ut.py"), testFilePath);

            var expectedTests = new[] {
                new DiscoveryTestInfo("test_ut_fail", "test_ut.py::TestClassUT::test_ut_fail", testFilePath, 4),
                new DiscoveryTestInfo("test_ut_pass", "test_ut.py::TestClassUT::test_ut_pass", testFilePath, 7),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            DiscoverTests(new[] { testFilePath }, runSettings, expectedTests);
        }

        private static void DiscoverTests(string[] sources, MockRunSettings runSettings, DiscoveryTestInfo[] expectedTests) {
            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new PythonTestDiscoverer();

            discoverer.DiscoverTests(sources, discoveryContext, logger, discoverySink);

            ValidateDiscoveredTests(discoverySink.Tests, expectedTests);
        }

        private static void ValidateDiscoveredTests(IList<TestCase> actualTests, DiscoveryTestInfo[] expectedTests) {
            PrintTestCases(actualTests);

            Assert.AreEqual(expectedTests.Length, actualTests.Count);

            foreach (var expectedTest in expectedTests) {
                var actualTestCase = actualTests.SingleOrDefault(tc => tc.FullyQualifiedName == expectedTest.FullyQualifiedName);
                Assert.IsNotNull(actualTestCase, expectedTest.FullyQualifiedName);
                Assert.AreEqual(new Uri(PythonConstants.TestExecutorUriString), actualTestCase.ExecutorUri);
                Assert.AreEqual(expectedTest.DisplayName, actualTestCase.DisplayName, expectedTest.FullyQualifiedName);
                Assert.AreEqual(expectedTest.LineNumber, actualTestCase.LineNumber, expectedTest.FullyQualifiedName);
                Assert.IsTrue(IsSameFile(expectedTest.FilePath, actualTestCase.CodeFilePath), expectedTest.FullyQualifiedName);
            }
        }

        private static bool IsSameFile(string a, string b) {
            return String.Compare(new FileInfo(a).FullName, new FileInfo(b).FullName, StringComparison.CurrentCultureIgnoreCase) == 0;
        }

        private static void PrintTestCases(IEnumerable<TestCase> testCases) {
            Console.WriteLine("Discovered test cases:");
            Console.WriteLine("----------------------");
            foreach (var tst in testCases) {
                Console.WriteLine($"FullyQualifiedName: {tst.FullyQualifiedName}");
                Console.WriteLine($"Source: {tst.Source}");
                Console.WriteLine($"Display: {tst.DisplayName}");
                Console.WriteLine($"CodeFilePath: {tst.CodeFilePath}");
                Console.WriteLine($"LineNumber: {tst.LineNumber.ToString()}");
                Console.WriteLine($"PytestId: {tst.GetPropertyValue<string>(Microsoft.PythonTools.TestAdapter.Pytest.Constants.PytestIdProperty, null)}");
                Console.WriteLine($"PytestXmlClassName: {tst.GetPropertyValue<string>(Microsoft.PythonTools.TestAdapter.Pytest.Constants.PyTestXmlClassNameProperty, null)}");
                Console.WriteLine($"PytestTestExecPath: {tst.GetPropertyValue<string>(Microsoft.PythonTools.TestAdapter.Pytest.Constants.PytestTestExecutionPathPropertery, null)}");
                Console.WriteLine("");
            }
        }
    }

    [TestClass]
    public class TestDiscovererTests27 : TestDiscovererTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python27 ?? PythonPaths.Python27_x64;
    }

    [TestClass]
    public class TestDiscovererTests35 : TestDiscovererTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python35 ?? PythonPaths.Python35_x64;
    }

    [TestClass]
    public class TestDiscovererTests36 : TestDiscovererTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python36 ?? PythonPaths.Python36_x64;
    }

    [TestClass]
    public class TestDiscovererTests37 : TestDiscovererTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python37 ?? PythonPaths.Python37_x64;
    }
}
