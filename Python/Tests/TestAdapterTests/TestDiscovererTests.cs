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
using PythonConstants = pt::Microsoft.PythonTools.PythonConstants;

namespace TestAdapterTests
{
    [TestClass, Ignore]
    public abstract partial class TestDiscovererTests
    {
        private const string FrameworkPytest = "Pytest";
        private const string FrameworkUnittest = "Unittest";

        protected abstract PythonVersion Version { get; }

        protected virtual string ImportErrorFormat => "ModuleNotFoundError: No module named '{0}'";

        [ClassCleanup]
        public static void ClassCleanup()
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
        public void DiscoverPytest()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_pt.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "BasicPytest", "test_pt.py"), testFilePath);

            var expectedTests = new[] {
                new TestInfo("test_pt_pass", "test_pt.py::test_pt::test_pt_pass", testFilePath, 1),
                new TestInfo("test_pt_fail", "test_pt.py::test_pt::test_pt_fail", testFilePath, 4),
                new TestInfo("test_method_pass", "test_pt.py::TestClassPT::test_method_pass", testFilePath, 8),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverPytestUppercaseFileName()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_Uppercase.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "Uppercase", "test_Uppercase.py"), testFilePath);

            var expectedTests = new[] {
                new TestInfo("test_A", "test_Uppercase.py::Test_UppercaseClass::test_A", testFilePath, 4),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverPytestTimeoutError()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_timeout_pt.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "Timeout", "test_timeout_pt.py"), testFilePath);

            int waitTimeInSeconds = 1;
            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath, waitTimeInSeconds)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new PytestTestDiscoverer();

            discoverer.DiscoverTests(new[] { testFilePath }, discoveryContext, logger, discoverySink);
            Assert.AreEqual(0, discoverySink.Tests.Count);

            var errors = string.Join(Environment.NewLine, logger.GetErrors());
            AssertUtil.Contains(
                errors,
                Strings.PythonTestDiscovererTimeoutErrorMessage
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverPytestSearchPath()
        {
            // test_search_path.py has an import at global scope that requires search path to resolve
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "ImportFromSearchPath"), testEnv.SourceFolderPath);

            // <SourceFolderPath>/TestFolder/
            // <SourceFolderPath>/TestFolder/test_search_path.py
            // <SourceFolderPath>/SearchPath/
            // <SourceFolderPath>/SearchPath/searchpathmodule.py
            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "TestFolder", "test_search_path.py");
            var searchPath = Path.Combine(testEnv.SourceFolderPath, "SearchPath");

            var expectedTests = new[] {
                new TestInfo("test_imported_module", "TestFolder\\test_search_path.py::SearchPathTests::test_imported_module", testFilePath, 5),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath)
                    .WithSearchPath(searchPath)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverPytestSyntaxErrorPartialResults()
        {
            // one file has a valid passing test,
            // the other has a test with a syntax error in it
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "SyntaxErrorPytest"), testEnv.SourceFolderPath);

            var testFilePath1 = Path.Combine(testEnv.SourceFolderPath, "test_basic.py");
            var testFilePath2 = Path.Combine(testEnv.SourceFolderPath, "test_syntax_error.py");

            var expectedTests = new[] {
                new TestInfo("test_success", "test_basic.py::test_basic::test_success", testFilePath1, 1),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath1)
                    .WithTestFile(testFilePath2)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath1, testFilePath2 }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverPytestSyntaxErrorLogErrors()
        {
            // one file has a valid passing test,
            // the other has a test with a syntax error in it
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "SyntaxErrorPytest"), testEnv.SourceFolderPath);

            var testFilePath1 = Path.Combine(testEnv.SourceFolderPath, "test_basic.py");
            var testFilePath2 = Path.Combine(testEnv.SourceFolderPath, "test_syntax_error.py");

            var expectedTests = new[] {
                new TestInfo("test_success", "test_basic.py::test_basic::test_success", testFilePath1, 1),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath1)
                    .WithTestFile(testFilePath2)
                    .ToXml()
            );

            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new PytestTestDiscoverer();

            discoverer.DiscoverTests(new[] { testFilePath1, testFilePath2 }, discoveryContext, logger, discoverySink);

            var errors = string.Join(Environment.NewLine, logger.GetErrors());

            AssertUtil.Contains(errors,
                "SyntaxError: invalid syntax"
            );
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        [TestCategory("10s")]
        public void DiscoverPytestImportError()
        {
            // one file has a valid passing test,
            // the other has an unknown module import at global scope
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "ImportErrorPytest"), testEnv.SourceFolderPath);

            var testFilePath1 = Path.Combine(testEnv.SourceFolderPath, "test_basic.py");
            var testFilePath2 = Path.Combine(testEnv.SourceFolderPath, "test_import_error.py");

            var expectedTests = new[] {
                new TestInfo("test_success", "test_basic.py::test_basic::test_success", testFilePath1, 1),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath1)
                    .WithTestFile(testFilePath2)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath1, testFilePath2 }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        [TestCategory("10s")]
        public void DiscoverUnitTestImportError()
        {
            // one file has a valid passing test,
            // the other has an unknown module import at global scope
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "ImportErrorUnittest"), testEnv.SourceFolderPath);

            var testFilePath1 = Path.Combine(testEnv.SourceFolderPath, "test_no_error.py");
            var testFilePath2 = Path.Combine(testEnv.SourceFolderPath, "test_import_error.py");

            var expectedTests = new[] {
                new TestInfo("test_no_error", "test_no_error.py::NoErrorTests::test_no_error", testFilePath1, 4),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath1)
                    .WithTestFile(testFilePath2)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath1, testFilePath2 }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverUnitTestSyntaxErrorPartialResults()
        {
            // one file has a valid passing test,
            // the other has an unknown module import at global scope
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "SyntaxErrorUnittest"), testEnv.SourceFolderPath);

            var testFilePath1 = Path.Combine(testEnv.SourceFolderPath, "test_basic_ut.py");
            var testFilePath2 = Path.Combine(testEnv.SourceFolderPath, "test_syntax_error_ut.py");

            var expectedTests = new[] {
                new TestInfo("test_ut_fail", "test_basic_ut.py::TestClassUT::test_ut_fail", testFilePath1, 4),
                new TestInfo("test_ut_pass", "test_basic_ut.py::TestClassUT::test_ut_pass", testFilePath1, 7),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath1)
                    .WithTestFile(testFilePath2)
                    .ToXml()
            );

            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new UnittestTestDiscoverer();

            discoverer.DiscoverTests(new[] { testFilePath1, testFilePath2 }, discoveryContext, logger, discoverySink);

            ValidateDiscoveredTests(testEnv.TestFramework, discoverySink.Tests, expectedTests);
        }


        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverUnitTestSyntaxErrorLogErrors()
        {
            // one file has a valid passing test,
            // the other has an unknown module import at global scope
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "SyntaxErrorUnittest"), testEnv.SourceFolderPath);

            var testFilePath1 = Path.Combine(testEnv.SourceFolderPath, "test_basic_ut.py");
            var testFilePath2 = Path.Combine(testEnv.SourceFolderPath, "test_syntax_error_ut.py");

            var expectedTests = new[] {
                new TestInfo("test_ut_fail", "test_basic_ut.py::TestClassUT::test_ut_fail", testFilePath1, 4),
                new TestInfo("test_ut_pass", "test_basic_ut.py::TestClassUT::test_ut_pass", testFilePath1, 7),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath1)
                    .WithTestFile(testFilePath2)
                    .ToXml()
            );

            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new UnittestTestDiscoverer();

            discoverer.DiscoverTests(new[] { testFilePath1, testFilePath2 }, discoveryContext, logger, discoverySink);

            var errors = string.Join(Environment.NewLine, logger.GetErrors());

            if (Version.Version > Microsoft.PythonTools.Parsing.PythonLanguageVersion.V27)
            {
                AssertUtil.Contains(errors,
                    "SyntaxError: invalid syntax"
                );
            }
            else
            {
                Assert.Inconclusive("Python 2.7 unittest errors are not currently being printed to error logs");
            }
        }


        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverPytestConfigPythonFiles()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "ConfigPythonFiles"), testEnv.SourceFolderPath);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_pt.py");
            var checkFilePath = Path.Combine(testEnv.SourceFolderPath, "check_pt.py");
            var exampleFilePath = Path.Combine(testEnv.SourceFolderPath, "example_pt.py");

            // pytest.ini declares that tests are only files named check_*.py and test_*.py
            // so the test defined in example_pt.py should not be discovered
            var expectedTests = new[] {
                new TestInfo("test_1", "test_pt.py::test_pt::test_1", testFilePath, 1),
                new TestInfo("test_2", "check_pt.py::check_pt::test_2", checkFilePath, 1),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(checkFilePath)
                    .WithTestFile(testFilePath)
                    .WithTestFile(exampleFilePath)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { checkFilePath, testFilePath, exampleFilePath }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverPytestConfigPythonFunctions()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "ConfigPythonFunctions"), testEnv.SourceFolderPath);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_misc_prefixes.py");

            // pytest.ini declares that tests are only functions named check_* and verify_*
            // so the test named test_* and example_* should not be discovered
            var expectedTests = new[] {
                new TestInfo("check_func", "test_misc_prefixes.py::test_misc_prefixes::check_func", testFilePath, 4),
                new TestInfo("verify_func", "test_misc_prefixes.py::test_misc_prefixes::verify_func", testFilePath, 10),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverPytestNotInstalled()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest, installFramework: false);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_pt.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "BasicPytest", "test_pt.py"), testFilePath);

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new PytestTestDiscoverer();

            discoverer.DiscoverTests(new[] { testFilePath }, discoveryContext, logger, discoverySink);
            Assert.AreEqual(0, discoverySink.Tests.Count);

            var errors = string.Join(Environment.NewLine, logger.GetErrors());
            AssertUtil.Contains(errors, string.Format(ImportErrorFormat, "pytest"));
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        [TestCategory("10s")]
        public void DiscoverUnittest()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            var testFile1Path = Path.Combine(testEnv.SourceFolderPath, "test_ut.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "BasicUnittest", "test_ut.py"), testFile1Path);

            var testFile2Path = Path.Combine(testEnv.SourceFolderPath, "test_runtest.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "BasicUnittest", "test_runtest.py"), testFile2Path);

            var expectedTests = new[] {
                new TestInfo("test_ut_fail", "test_ut.py::TestClassUT::test_ut_fail", testFile1Path, 4),
                new TestInfo("test_ut_pass", "test_ut.py::TestClassUT::test_ut_pass", testFile1Path, 7),
                new TestInfo("runTest", "test_runtest.py::TestClassRunTest::runTest", testFile2Path, 4),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFile1Path }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverUnittestConfiguration()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "ConfigUnittest"), testEnv.SourceFolderPath);

            // We have 3 files
            // product/prefix_not_included.py (should not be found, outside test folder)
            // test/test_not_included.py (should not be found, incorrect filename pattern)
            // test/prefix_included.py (should be found)
            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test", "prefix_included.py");

            var expectedTests = new[] {
                new TestInfo("test_included", "test\\prefix_included.py::PrefixIncluded::test_included", testFilePath, 4),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(Path.Combine(testEnv.SourceFolderPath, "product"))
                    .WithTestFilesFromFolder(Path.Combine(testEnv.SourceFolderPath, "test"))
                    .WithUnitTestConfiguration("test", "prefix_*.py")
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverUnittestUppercaseFileName()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_Uppercase.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "Uppercase", "test_Uppercase.py"), testFilePath);

            var expectedTests = new[] {
                new TestInfo("test_A", "test_Uppercase.py::Test_UppercaseClass::test_A", testFilePath, 4),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath }, runSettings, expectedTests);
        }


        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverUnittestTimeoutError()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_ut.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "Timeout", "test_timeout_ut.py"), testFilePath);

            int waitTimeInSeconds = 1;
            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath, waitTimeInSeconds)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();
            var discoverer = new UnittestTestDiscoverer();

            discoverer.DiscoverTests(new[] { testFilePath }, discoveryContext, logger, discoverySink);
            Assert.AreEqual(0, discoverySink.Tests.Count);

            var errors = string.Join(Environment.NewLine, logger.GetErrors());
            AssertUtil.Contains(
                errors,
                Strings.PythonTestDiscovererTimeoutErrorMessage
            );
        }


        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverUnittestDecoratorsIgnoreLineNumbers()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_decorators_ut.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "Decorators", "test_decorators_ut.py"), testFilePath);

            // disable line checking until we fix https://github.com/microsoft/PTVS/issues/5497
            var expectedTests = new[] {
                new TestInfo("test_ut_fail", "test_decorators_ut.py::TestClassDecoratorsUT::test_ut_fail", testFilePath, -1),
                new TestInfo("test_ut_pass", "test_decorators_ut.py::TestClassDecoratorsUT::test_ut_pass", testFilePath, -1),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath }, runSettings, expectedTests);
        }

        [Ignore] //until we fix https://github.com/microsoft/PTVS/issues/5497
        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverUnittestDecoratorsCorrectLineNumbers()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_decorators_ut.py");
            File.Copy(TestData.GetPath("TestData", "TestDiscoverer", "Decorators", "test_decorators_ut.py"), testFilePath);

            var expectedTests = new[] {
                new TestInfo("test_ut_fail", "test_decorators_ut.py::TestClassDecoratorsUT::test_ut_fail", testFilePath, 5),
                //bschnurr note: currently unittest/_discovery.py is returning decorators line number
                new TestInfo("test_ut_pass", "test_decorators_ut.py::TestClassDecoratorsUT::test_ut_pass", testFilePath, 9),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverUnittestRelativeImport()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "RelativeImport"), testEnv.SourceFolderPath);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "relativeimportpackage\\test_relative_import.py");

            var expectedTests = new[] {
                new TestInfo(
                    "test_relative_import",
                    "relativeimportpackage\\test_relative_import.py::RelativeImportTests::test_relative_import",
                    testFilePath,
                    5
                ),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverUnittestInheritance()
        {
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "Inheritance"), testEnv.SourceFolderPath);

            var baseTestFilePath = Path.Combine(testEnv.SourceFolderPath, "test_base.py");
            var derivedTestFilePath = Path.Combine(testEnv.SourceFolderPath, "test_derived.py");

            var expectedTests = new[] {
                new TestInfo("test_base_pass", "test_base.py::BaseClassTests::test_base_pass", baseTestFilePath, 4),
                new TestInfo("test_base_fail", "test_base.py::BaseClassTests::test_base_fail", baseTestFilePath, 7),
                // TODO: investigate potential bug in product code,
                // file name incorrect for these two, should be in baseTestFilePath
                new TestInfo("test_base_pass", "test_derived.py::DerivedClassTests::test_base_pass", derivedTestFilePath, 4),
                new TestInfo("test_base_fail", "test_derived.py::DerivedClassTests::test_base_fail", derivedTestFilePath, 7),
                new TestInfo("test_derived_pass", "test_derived.py::DerivedClassTests::test_derived_pass", derivedTestFilePath, 5),
                new TestInfo("test_derived_fail", "test_derived.py::DerivedClassTests::test_derived_fail", derivedTestFilePath, 8),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFilesFromFolder(testEnv.SourceFolderPath)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { baseTestFilePath, derivedTestFilePath }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverUnitTestWarnings()
        {
            // one file has a valid passing test,
            // the other has an unknown module import at global scope
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkUnittest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "Warnings"), testEnv.SourceFolderPath);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_warnings.py");
            var expectedTests = new[] {
                new TestInfo("test_A", "test_warnings.py::Test_WarnClass::test_A", testFilePath, 6),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath }, runSettings, expectedTests);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        [TestCategory("10s")]
        public void DiscoverPytestWarnings()
        {
            // one file has a valid passing test,
            // the other has an unknown module import at global scope
            var testEnv = TestEnvironment.GetOrCreate(Version, FrameworkPytest);

            FileUtils.CopyDirectory(TestData.GetPath("TestData", "TestDiscoverer", "Warnings"), testEnv.SourceFolderPath);

            var testFilePath = Path.Combine(testEnv.SourceFolderPath, "test_warnings.py");
            var expectedTests = new[] {
                new TestInfo("test_A", "test_warnings.py::Test_WarnClass::test_A", testFilePath, 6),
            };

            var runSettings = new MockRunSettings(
                new MockRunSettingsXmlBuilder(testEnv.TestFramework, testEnv.InterpreterPath, testEnv.ResultsFolderPath, testEnv.SourceFolderPath)
                    .WithTestFile(testFilePath)
                    .ToXml()
            );

            DiscoverTests(testEnv, new[] { testFilePath }, runSettings, expectedTests);
        }


        private static void DiscoverTests(TestEnvironment testEnv, string[] sources, MockRunSettings runSettings, TestInfo[] expectedTests)
        {
            var discoveryContext = new MockDiscoveryContext(runSettings);
            var discoverySink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();

            ITestDiscoverer discoverer = null;
            switch (testEnv.TestFramework)
            {
                case FrameworkPytest:
                    discoverer = new PytestTestDiscoverer();
                    break;

                case FrameworkUnittest:
                    discoverer = new UnittestTestDiscoverer();
                    break;
                default:
                    Assert.Fail($"unknown testframework: {testEnv.TestFramework.ToString()}");
                    break;
            }

            discoverer.DiscoverTests(sources, discoveryContext, logger, discoverySink);

            ValidateDiscoveredTests(testEnv.TestFramework, discoverySink.Tests, expectedTests);
        }

        private static void ValidateDiscoveredTests(string testFramework, IList<TestCase> actualTests, TestInfo[] expectedTests)
        {
            PrintTestCases(actualTests);

            Assert.AreEqual(expectedTests.Length, actualTests.Count);

            foreach (var expectedTest in expectedTests)
            {
                var actualTestCase = actualTests.SingleOrDefault(tc => tc.FullyQualifiedName == expectedTest.FullyQualifiedName);
                Assert.IsNotNull(actualTestCase, expectedTest.FullyQualifiedName);
                switch (testFramework)
                {
                    case FrameworkPytest:
                        Assert.AreEqual(new Uri(PythonConstants.PytestExecutorUriString), actualTestCase.ExecutorUri);
                        break;
                    case FrameworkUnittest:
                        Assert.AreEqual(new Uri(PythonConstants.UnitTestExecutorUriString), actualTestCase.ExecutorUri);
                        break;
                    default:
                        Assert.Fail($"Unexpected test framework: {testFramework}");
                        break;
                }
                Assert.AreEqual(expectedTest.DisplayName, actualTestCase.DisplayName, expectedTest.FullyQualifiedName);
                Assert.IsTrue(IsSameFile(expectedTest.FilePath, actualTestCase.CodeFilePath), expectedTest.FullyQualifiedName);
                if (expectedTest.LineNumber > 0)
                {
                    Assert.AreEqual(expectedTest.LineNumber, actualTestCase.LineNumber, expectedTest.FullyQualifiedName);
                }
            }
        }

        private static bool IsSameFile(string a, string b)
        {
            return String.Compare(new FileInfo(a).FullName, new FileInfo(b).FullName, StringComparison.CurrentCultureIgnoreCase) == 0;
        }

        private static void PrintTestCases(IEnumerable<TestCase> testCases)
        {
            Console.WriteLine("Discovered test cases:");
            Console.WriteLine("----------------------");
            foreach (var tst in testCases)
            {
                Console.WriteLine($"FullyQualifiedName: {tst.FullyQualifiedName}");
                Console.WriteLine($"Source: {tst.Source}");
                Console.WriteLine($"Display: {tst.DisplayName}");
                Console.WriteLine($"CodeFilePath: {tst.CodeFilePath}");
                Console.WriteLine($"LineNumber: {tst.LineNumber.ToString()}");
                Console.WriteLine($"PytestId: {tst.GetPropertyValue<string>(Microsoft.PythonTools.TestAdapter.Pytest.Constants.PytestIdProperty, null)}");
                Console.WriteLine("");
            }
        }
    }

    [TestClass]
    public class TestDiscovererTests27 : TestDiscovererTests
    {
        [ClassInitialize]
        public static void DoDeployment(TestContext context)
        {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python27_x64 ?? PythonPaths.Python27;

        protected override string ImportErrorFormat => "ImportError: No module named {0}";
    }

    [TestClass]
    public class TestDiscovererTests35 : TestDiscovererTests
    {
        [ClassInitialize]
        public static void DoDeployment(TestContext context)
        {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python35_x64 ?? PythonPaths.Python35;

        protected override string ImportErrorFormat => "ImportError: No module named '{0}'";
    }

    [TestClass]
    public class TestDiscovererTests37 : TestDiscovererTests
    {
        [ClassInitialize]
        public static void DoDeployment(TestContext context)
        {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python37_x64 ?? PythonPaths.Python37;
    }
}
