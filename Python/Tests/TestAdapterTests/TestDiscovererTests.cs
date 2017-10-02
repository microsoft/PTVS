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

extern alias pt;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using PythonConstants = pt::Microsoft.PythonTools.PythonConstants;

namespace TestAdapterTests {
    [TestClass]
    public class TestDiscovererTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        private const string _runSettings = @"<?xml version=""1.0""?><RunSettings><DataCollectionRunSettings><DataCollectors /></DataCollectionRunSettings><RunConfiguration><ResultsDirectory>C:\Visual Studio 2015\Projects\PythonApplication107\TestResults</ResultsDirectory><TargetPlatform>X86</TargetPlatform><TargetFrameworkVersion>Framework45</TargetFrameworkVersion></RunConfiguration><Python><TestCases><Project path=""C:\Visual Studio 2015\Projects\PythonApplication107\PythonApplication107\PythonApplication107.pyproj"" home=""C:\Visual Studio 2015\Projects\PythonApplication107\PythonApplication107\"" nativeDebugging="""" djangoSettingsModule="""" workingDir=""C:\Visual Studio 2015\Projects\PythonApplication107\PythonApplication107\"" interpreter=""C:\Python35-32\python.exe"" pathEnv=""PYTHONPATH""><Environment /><SearchPaths><Search value=""C:\Visual Studio 2015\Projects\PythonApplication107\PythonApplication107\"" /></SearchPaths>
<Test className=""Test_test1"" file=""C:\Visual Studio 2015\Projects\PythonApplication107\PythonApplication107\test1.py"" line=""17"" column=""9"" method=""test_A"" />
<Test className=""Test_test1"" file=""C:\Visual Studio 2015\Projects\PythonApplication107\PythonApplication107\test1.py"" line=""21"" column=""9"" method=""test_B"" />
<Test className=""Test_test2"" file=""C:\Visual Studio 2015\Projects\PythonApplication107\PythonApplication107\test1.py"" line=""48"" column=""9"" method=""test_C"" /></Project></TestCases></Python></RunSettings>";

        [TestMethod, Priority(1)]
        [TestCategory("10s")]
        public void TestDiscover() {
            var ctx = new MockDiscoveryContext(new MockRunSettings(_runSettings));
            var sink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();

            const string projectPath = @"C:\Visual Studio 2015\Projects\PythonApplication107\PythonApplication107\PythonApplication107.pyproj";
            const string testFilePath = @"C:\Visual Studio 2015\Projects\PythonApplication107\PythonApplication107\test1.py";
            new TestDiscoverer().DiscoverTests(
                new[] { testFilePath },
                ctx,
                logger,
                sink
            );

            PrintTestCases(sink.Tests);

            var expectedTests = new[] {
                TestInfo.FromRelativePaths("Test_test1", "test_A", projectPath, testFilePath, 17, TestOutcome.Passed),
                TestInfo.FromRelativePaths("Test_test1", "test_B", projectPath, testFilePath, 21, TestOutcome.Passed),
                TestInfo.FromRelativePaths("Test_test2", "test_C", projectPath, testFilePath, 48, TestOutcome.Passed)
            };

            Assert.AreEqual(expectedTests.Length, sink.Tests.Count);

            foreach (var expectedTest in expectedTests) {
                var expectedFullyQualifiedName = TestReader.MakeFullyQualifiedTestName(expectedTest.RelativeClassFilePath, expectedTest.ClassName, expectedTest.MethodName);
                var actualTestCase = sink.Tests.SingleOrDefault(tc => tc.FullyQualifiedName == expectedFullyQualifiedName);
                Assert.IsNotNull(actualTestCase, expectedFullyQualifiedName);
                Assert.AreEqual(expectedTest.MethodName, actualTestCase.DisplayName, expectedFullyQualifiedName);
                Assert.AreEqual(new Uri(PythonConstants.TestExecutorUriString), actualTestCase.ExecutorUri);
                Assert.AreEqual(expectedTest.SourceCodeLineNumber, actualTestCase.LineNumber, expectedFullyQualifiedName);
                Assert.IsTrue(IsSameFile(expectedTest.SourceCodeFilePath, actualTestCase.CodeFilePath), expectedFullyQualifiedName);

                sink.Tests.Remove(actualTestCase);
            }

            Debug.WriteLine("");
            Debug.WriteLine("");
            Debug.WriteLine("");
            Debug.WriteLine("");

            PrintTestCases(sink.Tests);
        }

        private static IEnumerable<TestCaseInfo> GetTestCasesFromAst(string code, PythonAnalyzer analyzer) {
            var codeStream = new MemoryStream(Encoding.UTF8.GetBytes(code));
            var m = AstPythonModule.FromStream(analyzer.Interpreter, codeStream, "<string>", analyzer.LanguageVersion, "__main__");
            return TestAnalyzer.GetTestCasesFromAst(m, null);
        }

        [TestMethod, Priority(1)]
        public void DecoratedTests() {
            using (var analyzer = MakeTestAnalyzer()) {
                var code = @"import unittest

def decorator(fn):
    def wrapped(*args, **kwargs):
        return fn(*args, **kwargs)
    return wrapped

class MyTest(unittest.TestCase):
    @decorator
    def testAbc(self):
        pass

    @fake_decorator
    def testDef(self):
        pass
";
                var entry = AddModule(analyzer, "Fob", code);

                entry.Analyze(CancellationToken.None, true);
                analyzer.AnalyzeQueuedEntries(CancellationToken.None);

                var tests = TestAnalyzer.GetTestCasesFromAnalysis(entry)
                    .Select(t => $"{t.MethodName}:{t.StartLine}");
                AssertUtil.ArrayEquals(
                    new[] { "testAbc:10", "testDef:14" },
                    tests.ToArray()
                );

                tests = GetTestCasesFromAst(code, analyzer)
                    .Select(t => $"{t.MethodName}:{t.StartLine}");
                AssertUtil.ArrayEquals(
                    new[] { "testAbc:9", "testDef:13" },
                    tests.ToArray()
                );
            }
        }

        [TestMethod, Priority(1)]
        public void TestCaseSubclasses() {
            using (var analyzer = MakeTestAnalyzer()) {
                var entry1 = AddModule(analyzer, "Pkg.SubPkg", @"import unittest

class TestBase(unittest.TestCase):
    pass
");

                var entry2 = AddModule(
                    analyzer,
                    "Pkg",
                    moduleFile: "Pkg\\__init__.py",
                    code: @"from .SubPkg import TestBase"
                );

                var code = @"from Pkg.SubPkg import TestBase as TB1
from Pkg import TestBase as TB2
from Pkg import *

class MyTest1(TB1):
    def test1(self):
        pass

class MyTest2(TB2):
    def test2(self):
        pass

class MyTest3(TestBase):
    def test3(self):
        pass
";
                var entry3 = AddModule(analyzer, "__main__", code);

                entry1.Analyze(CancellationToken.None, true);
                entry2.Analyze(CancellationToken.None, true);
                entry3.Analyze(CancellationToken.None, true);
                analyzer.AnalyzeQueuedEntries(CancellationToken.None);

                var test = TestAnalyzer.GetTestCasesFromAnalysis(entry3).ToList();
                AssertUtil.ContainsExactly(test.Select(t => t.MethodName), "test1", "test2", "test3");

                // Cannot discover tests from subclasses with just the AST
                test = GetTestCasesFromAst(code, analyzer).ToList();
                AssertUtil.ContainsExactly(test.Select(t => t.MethodName));
            }
        }

        [TestMethod, Priority(1)]
        public void TestCaseRunTests() {
            using (var analyzer = MakeTestAnalyzer()) {
                var code = @"import unittest

class TestBase(unittest.TestCase):
    def runTests(self):
        pass # should not discover this as it isn't runTest or test*
    def runTest(self):
        pass
";
                var entry = AddModule(analyzer, "__main__", code);

                entry.Analyze(CancellationToken.None, true);
                analyzer.AnalyzeQueuedEntries(CancellationToken.None);

                var test = TestAnalyzer.GetTestCasesFromAnalysis(entry).ToList();
                AssertUtil.ContainsExactly(test.Select(t => t.ClassName), "TestBase");

                test = GetTestCasesFromAst(code, analyzer).ToList();
                AssertUtil.ContainsExactly(test.Select(t => t.ClassName), "TestBase");
            }
        }

        /// <summary>
        /// If we have test* and runTest we shouldn't discover runTest
        /// </summary>
        [TestMethod, Priority(1)]
        public void TestCaseRunTestsWithTest() {
            using (var analyzer = MakeTestAnalyzer()) {
                var code = @"import unittest

class TestBase(unittest.TestCase):
    def test_1(self):
        pass
    def runTest(self):
        pass
";
                var entry = AddModule(analyzer, "__main__", code);

                entry.Analyze(CancellationToken.None, true);
                analyzer.AnalyzeQueuedEntries(CancellationToken.None);

                var test = TestAnalyzer.GetTestCasesFromAnalysis(entry).ToList();
                AssertUtil.ContainsExactly(test.Select(t => t.MethodName), "test_1");

                test = GetTestCasesFromAst(code, analyzer).ToList();
                AssertUtil.ContainsExactly(test.Select(t => t.MethodName), "test_1");
            }
        }

        private PythonAnalyzer MakeTestAnalyzer() {
            return PythonAnalyzer.CreateAsync(InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7))).Result;
        }

        private IPythonProjectEntry AddModule(PythonAnalyzer analyzer, string moduleName, string code, string moduleFile = null) {
            using (var source = new StringReader(code)) {
                var entry = analyzer.AddModule(
                    moduleName,
                    TestData.GetPath("Fob\\" + (moduleFile ?? moduleName.Replace('.', '\\') + ".py"))
                );

                using (var parser = Parser.CreateParser(source, PythonLanguageVersion.V27, new ParserOptions() { BindReferences = true })) {
                    entry.UpdateTree(parser.ParseFile(), null);
                }

                return entry;
            }
        }

        private static bool IsSameFile(string a, string b) {
            return String.Compare(new FileInfo(a).FullName, new FileInfo(b).FullName, StringComparison.CurrentCultureIgnoreCase) == 0;
        }

        private static void PrintTestCases(IEnumerable<TestCase> testCases) {
            foreach (var tst in testCases) {
                Console.WriteLine("Test: " + tst.FullyQualifiedName);
                Console.WriteLine("Source: " + tst.Source);
                Console.WriteLine("Display: " + tst.DisplayName);
                Console.WriteLine("Location: " + tst.CodeFilePath);
                Console.WriteLine("Location: " + tst.LineNumber.ToString());
                Console.WriteLine("");
            }
        }
    }
}
