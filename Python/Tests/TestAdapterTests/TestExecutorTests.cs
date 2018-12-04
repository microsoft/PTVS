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

        [TestMethod, Priority(0)]
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

        internal static MockRunContext CreateRunContext(
            IEnumerable<TestInfo> expected,
            string interpreter = null,
            bool dryRun = false,
            bool showConsole = false
        ) {
            var projects = new List<string>();
            var testCases = new List<TestCase>();

            var baseDir = TestData.GetTempPath();
            var sources = Path.Combine(baseDir, "Source");
            var results = Path.Combine(baseDir, "Results");

            foreach (var proj in expected.GroupBy(e => e.ProjectFilePath)) {
                var projectSource = Path.GetDirectoryName(proj.Key);

                Func<string, string> Rebase = d => PathUtils.GetAbsoluteFilePath(
                    baseDir,
                    PathUtils.GetRelativeFilePath(TestData.GetPath(), d)
                );

                var projName = Rebase(projectSource);
                FileUtils.CopyDirectory(projectSource, projName, true);

                var sb = new StringBuilder();
                foreach (var e in proj) {
                    var tc = e.TestCase;
                    tc.CodeFilePath = Rebase(tc.CodeFilePath);
                    testCases.Add(tc);

                    sb.AppendLine(_runSettingTest.FormatInvariant(
                        Rebase(e.SourceCodeFilePath),
                        e.ClassName,
                        e.MethodName,
                        e.SourceCodeLineNumber,
                        8
                    ));
                }

                projects.Add(string.Format(_runSettingProject,
                    PathUtils.EnsureEndSeparator(projName),
                    Path.GetFileName(proj.Key),
                    interpreter ?? GetInterpreterPath(proj.Key),
                    sb.ToString(),
                    string.Join(Environment.NewLine, GetEnvironmentVariables(proj.Key)),
                    string.Join(Environment.NewLine, GetSearchPaths(proj.Key))
                ));
            }

            foreach (var p in projects) {
                Console.WriteLine(p);
            }

            return new MockRunContext(new MockRunSettings(string.Format(_runSettings,
                results,
                string.Join(Environment.NewLine, projects),
                dryRun ? "true" : "false",
                showConsole ? "true" : "false"
            )), testCases);
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void TestRun() {
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = TestInfo.TestAdapterATests.Concat(TestInfo.TestAdapterBTests).ToArray();
            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
            var testCases = runContext.TestCases;

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
                Assert.IsTrue(actualResult.Duration >= expectedResult.MinDuration);
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void TestRunAll() {
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var tests = TestInfo.TestAdapterATests.Concat(TestInfo.TestAdapterBTests).ToArray();
            var runContext = CreateRunContext(tests, Version.InterpreterPath);
            var expectedTests = runContext.TestCases;

            executor.RunTests(expectedTests, runContext, recorder);
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in tests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
                Assert.IsTrue(actualResult.Duration >= expectedResult.MinDuration);
            }
        }

        [TestMethod, Priority(0)]
        public void TestCancel() {
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = TestInfo.TestAdapterATests.Union(TestInfo.TestAdapterBTests).ToArray();
            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
            var testCases = runContext.TestCases;

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
        [TestCategory("10s")]
        public void TestMultiprocessing() {
            if (Version.Version <= PythonLanguageVersion.V26 ||
                Version.Version == PythonLanguageVersion.V30) {
                return;
            }

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = TestInfo.TestAdapterMultiprocessingTests;
            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
            var testCases = runContext.TestCases;

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
        [TestCategory("10s")]
        public void TestRelativeImport() {
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = new[] { TestInfo.RelativeImportSuccess };
            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
            var testCases = runContext.TestCases;

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
        [TestCategory("10s")]
        public void TestInheritance() {
            // TODO: Figure out the proper fix to make this test pass.
            // There's a confusion between source file path and class file path.
            // Note that the equivalent manual test in IDE works fine.
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = TestInfo.TestAdapterBInheritanceTests;
            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
            var testCases = runContext.TestCases;

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
        [TestCategory("10s")]
        public void TestLoadError() {
            // A load error is when unittest module fails to load the test (prior to running it)
            // For example, if the file where the test is defined has an unhandled ImportError.
            // We check that this only causes the tests that can't be loaded to fail,
            // all other tests in the test run which can be loaded successfully will be run.
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = TestInfo.GetTestAdapterLoadErrorTests(ImportErrorFormat);
            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
            var testCases = runContext.TestCases;

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");

                if (expectedResult.ContainedErrorMessage != null) {
                    Assert.IsNotNull(actualResult.ErrorMessage);
                    Assert.IsTrue(
                        actualResult.ErrorMessage.Contains(expectedResult.ContainedErrorMessage),
                        string.Format("Error message did not contain expected text: {0}", expectedResult.ContainedErrorMessage)
                    );
                } else {
                    Assert.IsNull(actualResult.ErrorMessage);
                }
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void TestStackTrace() {
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = new[] {
                TestInfo.StackTraceBadLocalImportFailure,
                TestInfo.StackTraceNotEqualFailure
            };
            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
            var testCases = runContext.TestCases;

            executor.RunTests(testCases, runContext, recorder);

            var badLocalImportFile = TestInfo.StackTraceBadLocalImportFailure.SourceCodeFilePath;
            var badLocalImportFrames = new StackFrame[] {
                new StackFrame("local_func in global_func", badLocalImportFile, 13),
                new StackFrame("global_func", badLocalImportFile, 14),
                new StackFrame("Utility.class_static", badLocalImportFile, 19),
                new StackFrame("Utility.instance_method_b", badLocalImportFile, 22),
                new StackFrame("Utility.instance_method_a", badLocalImportFile, 25),
                new StackFrame("StackTraceTests.test_bad_import", badLocalImportFile, 6),
            };

            ValidateStackFrame(recorder.Results[0], badLocalImportFrames);

            var notEqualFile = TestInfo.StackTraceNotEqualFailure.SourceCodeFilePath;
            var notEqualFrames = new StackFrame[] {
                new StackFrame("StackTraceTests.test_not_equal", notEqualFile, 9),
            };

            ValidateStackFrame(recorder.Results[1], notEqualFrames);
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

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void TestEnvironment() {
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = new[] { TestInfo.EnvironmentTestSuccess };
            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
            var testCases = runContext.TestCases;

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
        [TestCategory("10s")]
        public void TestDuration() {
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = new[] {
                TestInfo.DurationSleep01TestSuccess,
                TestInfo.DurationSleep03TestSuccess,
                TestInfo.DurationSleep05TestSuccess,
                TestInfo.DurationSleep08TestSuccess,
                TestInfo.DurationSleep15TestFailure
            };
            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
            var testCases = runContext.TestCases;

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
                Assert.IsTrue(actualResult.Duration >= expectedResult.MinDuration);
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void TestTeardown() {
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = new[] {
                TestInfo.TeardownSuccess,
                TestInfo.TeardownFailure
            };
            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath);
            var testCases = runContext.TestCases;

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
                var stdOut = actualResult.Messages.Single(m => m.Category == "StdOutMsgs");
                AssertUtil.Contains(stdOut.Text, "doing setUp", "doing tearDown");
            }
        }

        [TestMethod, Priority(0)]
        public void TestPassOnCommandLine() {
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = TestInfo.TestAdapterATests;
            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath, dryRun: true);
            var testCases = runContext.TestCases;

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            AssertUtil.ArrayEquals(
                expectedTests.Select(t => t.TestCase.FullyQualifiedName).ToList(),
                recorder.Results.Select(t => t.TestCase.FullyQualifiedName).ToList()
            );
        }

        [TestMethod, Priority(0)]
        public void TestPassInTestList() {
            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = Enumerable.Repeat(TestInfo.TestAdapterATests, 10).SelectMany();
            var runContext = CreateRunContext(expectedTests, Version.InterpreterPath, dryRun: true);
            var testCases = runContext.TestCases;

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            AssertUtil.ArrayEquals(
                expectedTests.Select(t => t.TestCase.FullyQualifiedName).ToList(),
                recorder.Results.Select(t => t.TestCase.FullyQualifiedName).ToList()
            );
        }

        internal static void PrintTestResults(MockTestExecutionRecorder recorder) {
            foreach (var message in recorder.Messages) {
                Console.WriteLine(message);
            }
            foreach (var result in recorder.Results) {
                Console.WriteLine("Test: {0}", result.TestCase.FullyQualifiedName);
                Console.WriteLine("Result: {0}", result.Outcome);
                Console.WriteLine("Duration: {0}ms", result.Duration.TotalMilliseconds);
                foreach(var msg in result.Messages) {
                    Console.WriteLine("Message {0}:", msg.Category);
                    Console.WriteLine(msg.Text);
                }
                Console.WriteLine("");
            }
        }
    }

    [TestClass]
    public class TestExecutorTests26 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python26 ?? PythonPaths.Python26_x64;
    }

    [TestClass]
    public class TestExecutorTests27 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python27 ?? PythonPaths.Python27_x64;

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void TestExtensionReference() {
            // This test uses a 32-bit Python 2.7 .pyd
            PythonPaths.Python27.AssertInstalled();

            var executor = new TestExecutor();
            var recorder = new MockTestExecutionRecorder();
            var expectedTests = new[] { TestInfo.ExtensionReferenceTestSuccess };
            var runContext = CreateRunContext(expectedTests);
            var testCases = runContext.TestCases;

            executor.RunTests(testCases, runContext, recorder);
            PrintTestResults(recorder);

            var resultNames = recorder.Results.Select(tr => tr.TestCase.FullyQualifiedName).ToSet();
            foreach (var expectedResult in expectedTests) {
                AssertUtil.ContainsAtLeast(resultNames, expectedResult.TestCase.FullyQualifiedName);
                var actualResult = recorder.Results.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == expectedResult.TestCase.FullyQualifiedName);
                Assert.AreEqual(expectedResult.Outcome, actualResult.Outcome, expectedResult.TestCase.FullyQualifiedName + " had incorrect result");
            }
        }
    }

    [TestClass]
    public class TestExecutorTests31 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python31 ?? PythonPaths.Python31_x64;
    }

    [TestClass]
    public class TestExecutorTests32 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python32 ?? PythonPaths.Python32_x64;
    }

    [TestClass]
    public class TestExecutorTests33 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python33 ?? PythonPaths.Python33_x64;

        protected override string ImportErrorFormat => NewImportErrorFormat;
    }

    [TestClass]
    public class TestExecutorTests34 : TestExecutorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override PythonVersion Version => PythonPaths.Python34 ?? PythonPaths.Python34_x64;

        protected override string ImportErrorFormat => NewImportErrorFormat;
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
}
