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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.PythonTools.Infrastructure;
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

        [TestMethod, Priority(1)]
        public void FromCommandLineArgsRaceCondition() {
            // https://pytools.codeplex.com/workitem/1429

            var mre = new ManualResetEvent(false);
            var tasks = new Task<bool>[100];
            try {
                for (int i = 0; i < tasks.Length; i += 1) {
                    tasks[i] = Task.Run(() => {
                        mre.WaitOne();
                        using (var arg = VisualStudioProxy.FromProcessId(123)) {
                            return arg is VisualStudioProxy;
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

        // {0} is the test results directory
        // {1} is one or more formatted _runSettingProject lines
        // {2} is 'true' or 'false' depending on whether the tests should be run
        // {3} is 'true' or 'false' depending on whether the console should be shown
        private const string _runSettings = @"<?xml version=""1.0""?><RunSettings><DataCollectionRunSettings><DataCollectors /></DataCollectionRunSettings><RunConfiguration><ResultsDirectory>{0}</ResultsDirectory><TargetPlatform>X86</TargetPlatform><TargetFrameworkVersion>Framework45</TargetFrameworkVersion></RunConfiguration><Python><TestCases>
{1}
</TestCases>
<DryRun value=""{2}"" /><ShowConsole value=""{3}"" /></Python></RunSettings>";

        // {0} is the project home directory, ending with a backslash
        // {1} is the project filename, including extension
        // {2} is the interpreter path
        // {3} is one or more formatted _runSettingTest lines
        // {4} is one or more formatten _runSettingEnvironment lines
        private const string _runSettingProject = @"<Project path=""{0}{1}"" home=""{0}"" nativeDebugging="""" djangoSettingsModule="""" workingDir=""{0}"" interpreter=""{2}"" pathEnv=""PYTHONPATH""><Environment>{4}</Environment><SearchPaths>{5}</SearchPaths>
{3}
</Project>";

        // {0} is the variable name
        // {1} is the variable value
        private const string _runSettingEnvironment = @"<Variable name=""{0}"" value=""{1}"" />";

        // {0} is the search path
        private const string _runSettingSearch = @"<Search value=""{0}"" />";

        // {0} is the full path to the file
        // {1} is the class name
        // {2} is the method name
        // {3} is the line number (1-indexed)
        // {4} is the column number (1-indexed)
        private const string _runSettingTest = @"<Test className=""{1}"" file=""{0}"" line=""{3}"" column=""{4}"" method=""{2}"" />";

        private static string GetInterpreterPath(string projectFile) {
            var doc = new XmlDocument();
            doc.Load(projectFile);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("m", "http://schemas.microsoft.com/developer/msbuild/2003");
            var id = doc.SelectSingleNode("/m:Project/m:PropertyGroup/m:InterpreterId", ns).FirstChild.Value;
            return PythonPaths.Versions.First(p => p.Id == id).InterpreterPath;
        }

        private static IEnumerable<string> GetEnvironmentVariables(string projectFile) {
            var doc = new XmlDocument();
            doc.Load(projectFile);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("m", "http://schemas.microsoft.com/developer/msbuild/2003");
            var env = doc.SelectSingleNode("/m:Project/m:PropertyGroup/m:Environment", ns)?.FirstChild?.Value;
            if (env == null) {
                return Enumerable.Empty<string>();
            }
            return PathUtils.ParseEnvironment(env).Select(kv => string.Format(_runSettingEnvironment, kv.Key, kv.Value));
        }

        private static IEnumerable<string> GetSearchPaths(string projectFile) {
            var doc = new XmlDocument();
            doc.Load(projectFile);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("m", "http://schemas.microsoft.com/developer/msbuild/2003");
            var searchPaths = doc.SelectSingleNode("/m:Project/m:PropertyGroup/m:SearchPath", ns)?.FirstChild?.Value;

            var projectDir = PathUtils.GetParent(projectFile);
            var elements = new List<string> { _runSettingSearch.FormatInvariant(projectDir) };
            if (searchPaths == null) {
                return elements;
            }
            elements.AddRange(searchPaths.Split(';').Select(p =>
                _runSettingSearch.FormatInvariant(PathUtils.GetAbsoluteDirectoryPath(projectDir, p))
            ));
            return elements;
        }

        private static MockRunContext CreateRunContext(
            IEnumerable<TestInfo> expected,
            string interpreter = null,
            string testResults = null,
            bool dryRun = false,
            bool showConsole = false
        ) {
            var projects = new List<string>();

            foreach (var proj in expected.GroupBy(e => e.ProjectFilePath)) {
                var projName = Path.GetDirectoryName(proj.Key);
                if (!projName.EndsWith("\\")) {
                    projName += "\\";
                }
                projects.Add(string.Format(_runSettingProject,
                    projName,
                    proj.Key,
                    interpreter ?? GetInterpreterPath(proj.Key),
                    string.Join(Environment.NewLine, proj.Select(e =>string.Format(_runSettingTest,
                        e.SourceCodeFilePath,
                        e.ClassName,
                        e.MethodName,
                        e.SourceCodeLineNumber,
                        8
                    ))),
                    string.Join(Environment.NewLine, GetEnvironmentVariables(proj.Key)),
                    string.Join(Environment.NewLine, GetSearchPaths(proj.Key))
                ));
            }

            foreach (var p in projects) {
                Console.WriteLine(p);
            }

            return new MockRunContext(new MockRunSettings(string.Format(_runSettings,
                testResults ?? TestData.GetTempPath(),
                string.Join(Environment.NewLine, projects),
                dryRun ? "true" : "false",
                showConsole ? "true" : "false"
            )));
        }

        [TestMethod, Priority(1)]
        [TestCategory("10s")]
        public void TestRun() {
            PythonPaths.Python27_x64.AssertInstalled();
            PythonPaths.Python33_x64.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = TestInfo.TestAdapterATests.Concat(TestInfo.TestAdapterBTests).ToArray();
            var runContext = CreateRunContext(expectedTests);
            var testCases = expectedTests.Select(tr => tr.TestCase);

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
                Assert.IsTrue(actualResult.Duration >= TimeSpan.Zero);
            }
        }

        [TestMethod, Priority(1)]
        [TestCategory("10s")]
        public void TestRunAll() {
            PythonPaths.Python27_x64.AssertInstalled();
            PythonPaths.Python33_x64.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = TestInfo.TestAdapterATests.Concat(TestInfo.TestAdapterBTests).ToArray();
            var runContext = CreateRunContext(expectedTests);

            executor.RunTests(expectedTests.Select(ti => ti.SourceCodeFilePath), runContext, recorder);
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
                Assert.IsTrue(actualResult.Duration >= TimeSpan.Zero);
            }
        }

        [TestMethod, Priority(1)]
        public void TestCancel() {
            PythonPaths.Python27_x64.AssertInstalled();
            PythonPaths.Python33_x64.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = TestInfo.TestAdapterATests.Union(TestInfo.TestAdapterBTests).ToArray();
            var runContext = CreateRunContext(expectedTests);
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

        [TestMethod, Priority(1)]
        [TestCategory("10s")]
        public void TestMultiprocessing() {
            PythonPaths.Python27_x64.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = TestInfo.TestAdapterMultiprocessingTests;
            var runContext = CreateRunContext(expectedTests);
            var testCases = expectedTests.Select(tr => tr.TestCase);

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
            }
        }

        [TestMethod, Priority(1)]
        [TestCategory("10s")]
        public void TestInheritance() {
            // TODO: Figure out the proper fix to make this test pass.
            // There's a confusion between source file path and class file path.
            // Note that the equivalent manual test in IDE works fine.
            PythonPaths.Python27_x64.AssertInstalled();
            PythonPaths.Python33_x64.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = TestInfo.TestAdapterBInheritanceTests;
            var runContext = CreateRunContext(expectedTests);
            var testCases = expectedTests.Select(tr => tr.TestCase);

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
            }
        }

        [TestMethod, Priority(1)]
        [TestCategory("10s")]
        public void TestLoadError27() {
            PythonPaths.Python27_x64.AssertInstalled();

            TestLoadError("LoadErrorTest27");
        }

        [TestMethod, Priority(1)]
        [TestCategory("10s")]
        public void TestLoadError34() {
            PythonPaths.Python34_x64.AssertInstalled();

            TestLoadError("LoadErrorTest34");
        }

        [TestMethod, Priority(1)]
        [TestCategory("10s")]
        public void TestLoadError35() {
            // Handling of import error when loading a test changed in Python 3.5
            // so it's important to test 3.4 and 3.5
            PythonPaths.Python35_x64.AssertInstalled();

            TestLoadError("LoadErrorTest35");
        }

        private static void TestLoadError(string projectName) {
            // A load error is when unittest module fails to load the test (prior to running it)
            // For example, if the file where the test is defined has an unhandled ImportError.
            // We check that this only causes the tests that can't be loaded to fail,
            // all other tests in the test run which can be loaded successfully will be run.
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = TestInfo.GetTestAdapterLoadErrorTests(projectName);
            var runContext = CreateRunContext(expectedTests);
            var testCases = expectedTests.Select(tr => tr.TestCase);

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
            }
        }

        [TestMethod, Priority(1)]
        [TestCategory("10s")]
        public void TestEnvironment() {
            PythonPaths.Python27_x64.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = new[] { TestInfo.EnvironmentTestSuccess };
            var runContext = CreateRunContext(expectedTests);
            var testCases = expectedTests.Select(tr => tr.TestCase);

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
            }
        }

        [TestMethod, Priority(1)]
        [TestCategory("10s")]
        public void TestExtensionReference() {
            PythonPaths.Python27.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = new[] { TestInfo.ExtensionReferenceTestSuccess };
            var runContext = CreateRunContext(expectedTests);
            var testCases = expectedTests.Select(tr => tr.TestCase);

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
            }
        }

        [TestMethod, Priority(0)]
        public void TestPassOnCommandLine() {
            PythonPaths.Python27.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = TestInfo.TestAdapterATests;
            var runContext = CreateRunContext(expectedTests, dryRun: true);
            var testCases = expectedTests.Select(tr => tr.TestCase);

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            AssertUtil.ArrayEquals(
                expectedTests.Select(t => t.TestCase.FullyQualifiedName).ToList(),
                recorder.Results.Select(t => t.TestCase.FullyQualifiedName).ToList()
            );
        }

        [TestMethod, Priority(0)]
        public void TestPassInTestList() {
            PythonPaths.Python27.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = Enumerable.Repeat(TestInfo.TestAdapterATests, 10).SelectMany();
            var runContext = CreateRunContext(expectedTests, dryRun: true);
            var testCases = expectedTests.Select(tr => tr.TestCase);

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            AssertUtil.ArrayEquals(
                expectedTests.Select(t => t.TestCase.FullyQualifiedName).ToList(),
                recorder.Results.Select(t => t.TestCase.FullyQualifiedName).ToList()
            );
        }

        private static void PrintTestResults(MockTestExecutionRecorder recorder) {
            foreach (var message in recorder.Messages) {
                Console.WriteLine(message);
            }
            foreach (var result in recorder.Results) {
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
