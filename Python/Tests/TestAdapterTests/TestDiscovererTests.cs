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
                MockRunSettingsXmlBuilder.CreateDiscoveryContext("Pytest", testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
            );
            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new PythonTestDiscoverer();

            discoverer.DiscoverTests(new[] { testFilePath }, discoveryContext, logger, discoverySink);

            ValidateDiscoveredTests(discoverySink.Tests, expectedTests);
        }






        [TestMethod, Priority(0)]
        public void TEMP() {
            var testEnv = TestEnvironment.Create(Version, "Pytest");

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_duration.py");
            File.Copy(TestData.GetPath("TestData", "TestExecutor", "test_duration.py"), testFilePath);

            var expectedTests = new[] {
                new DiscoveryTestInfo("test_sleep_0_1", "test_duration.py::DurationTests::test_sleep_0_1", testFilePath, 5),
                new DiscoveryTestInfo("test_sleep_0_3", "test_duration.py::DurationTests::test_sleep_0_3", testFilePath, 8),
                new DiscoveryTestInfo("test_sleep_0_5", "test_duration.py::DurationTests::test_sleep_0_5", testFilePath, 11),
            };

            var runSettings = new MockRunSettings(
                MockRunSettingsXmlBuilder.CreateDiscoveryContext("Pytest", testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
            );
            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new PythonTestDiscoverer();

            discoverer.DiscoverTests(new[] { testFilePath }, discoveryContext, logger, discoverySink);

            ValidateDiscoveredTests(discoverySink.Tests, expectedTests);
        }





        [Ignore]
        [TestMethod, Priority(0)]
        public void DiscoverWithPytestConfigurationFile() {
            // TODO:
            // similar to pytest above, but have a pytest config file
            // folder should have 2 subfolders, both with test file(s)
            // config file should specify that tests are only located in one of the folders
            // we should validate that only tests from that folder are discovered
        }

        [Ignore]
        [TestMethod, Priority(0)]
        public void DiscoverWithPytestNotInstalled() {
            // TODO:
            // create a virtual env without pytest
            // discover tests
            // check the logger contents, and look for pytest import error
        }

        [TestMethod, Priority(0)]
        public void DiscoverWithUnittest() {
            var testEnv = TestEnvironment.Create(Version, "Unittest");

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_ut.py");
            File.Copy(TestData.GetPath("TestData", "TestExplorerPytest", "test_ut.py"), testFilePath);

            var runSettings = new MockRunSettings(
                MockRunSettingsXmlBuilder.CreateDiscoveryContext("Unittest", testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
            );
            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new PythonTestDiscoverer();

            discoverer.DiscoverTests(new[] { testFilePath }, discoveryContext, logger, discoverySink);

            var expectedTests = new[] {
                new DiscoveryTestInfo("test_ut_fail", "test_ut.py::TestClassUT::test_ut_fail", testFilePath, 4),
                new DiscoveryTestInfo("test_ut_pass", "test_ut.py::TestClassUT::test_ut_pass", testFilePath, 7),
            };

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
