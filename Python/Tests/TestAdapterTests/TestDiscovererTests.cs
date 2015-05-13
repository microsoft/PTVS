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
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace TestAdapterTests {
    [TestClass]
    public class TestDiscovererTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void TestDiscover() {
            var ctx = new MockDiscoveryContext();
            var sink = new MockTestCaseDiscoverySink();
            var logger = new MockMessageLogger();

            new TestDiscoverer().DiscoverTests(
                new[] { TestInfo.TestAdapterLibProjectFilePath, TestInfo.TestAdapterAProjectFilePath, TestInfo.TestAdapterBProjectFilePath },
                ctx,
                logger,
                sink
            );

            PrintTestCases(sink.Tests);

            var expectedTests = TestInfo.TestAdapterATests.Concat(TestInfo.TestAdapterBTests).ToArray();

            Assert.AreEqual(expectedTests.Length, sink.Tests.Count);

            foreach (var expectedTest in expectedTests) {
                var expectedFullyQualifiedName = TestAnalyzer.MakeFullyQualifiedTestName(expectedTest.RelativeClassFilePath, expectedTest.ClassName, expectedTest.MethodName);
                var actualTestCase = sink.Tests.SingleOrDefault(tc => tc.FullyQualifiedName == expectedFullyQualifiedName);
                Assert.IsNotNull(actualTestCase, expectedFullyQualifiedName);
                Assert.AreEqual(expectedTest.MethodName, actualTestCase.DisplayName, expectedFullyQualifiedName);
                Assert.AreEqual(new Uri(TestExecutor.ExecutorUriString), actualTestCase.ExecutorUri);
                Assert.AreEqual(expectedTest.SourceCodeLineNumber, actualTestCase.LineNumber, expectedFullyQualifiedName);
                Assert.IsTrue(IsSameFile(expectedTest.SourceCodeFilePath, actualTestCase.CodeFilePath), expectedFullyQualifiedName);
                Assert.IsTrue(IsSameFile(expectedTest.ProjectFilePath, actualTestCase.Source), expectedFullyQualifiedName);

                sink.Tests.Remove(actualTestCase);
            }

            Debug.WriteLine("");
            Debug.WriteLine("");
            Debug.WriteLine("");
            Debug.WriteLine("");

            PrintTestCases(sink.Tests);
        }

        [TestMethod, Priority(0)]
        public void DecoratedTests() {
            using (var analyzer = MakeTestAnalyzer()) {
                AddModule(analyzer, "Fob", @"import unittest

def decorator(fn):
    def wrapped(*args, **kwargs):
        return fn(*args, **kwargs)
    return wrapped

class MyTest(unittest.TestCase):
    @decorator
    def testAbc(self):
        pass
");

                var test = analyzer.GetTestCases().Single();
                Assert.AreEqual("testAbc", test.DisplayName);
                Assert.AreEqual(10, test.LineNumber);
            }
        }

        [TestMethod, Priority(0)]
        public void TestCaseSubclasses() {
            using (var analyzer = MakeTestAnalyzer()) {
                AddModule(analyzer, "Pkg.SubPkg", @"import unittest

class TestBase(unittest.TestCase):
    pass
");

                AddModule(
                    analyzer,
                    "Pkg",
                    moduleFile: "Pkg\\__init__.py",
                    code: @"from .SubPkg import TestBase"
                );

                AddModule(analyzer, "__main__", @"from Pkg.SubPkg import TestBase as TB1
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
");

                var test = analyzer.GetTestCases().ToList();
                AssertUtil.ContainsExactly(test.Select(t => t.DisplayName), "test1", "test2", "test3");
            }
        }

        [TestMethod, Priority(0)]
        public void TestCaseRunTests() {
            using (var analyzer = MakeTestAnalyzer()) {
                AddModule(analyzer, "__main__", @"import unittest

class TestBase(unittest.TestCase):
    def runTests(self):
        pass # should not discover this as it isn't runTest or test*
    def runTest(self):
        pass
");

                var test = analyzer.GetTestCases().ToList();
                AssertUtil.ContainsExactly(test.Select(t => t.DisplayName), "TestBase");
            }
        }

        /// <summary>
        /// If we have test* and runTest we shouldn't discover runTest
        /// </summary>
        [TestMethod, Priority(0)]
        public void TestCaseRunTestsWithTest() {
            using (var analyzer = MakeTestAnalyzer()) {
                AddModule(analyzer, "__main__", @"import unittest

class TestBase(unittest.TestCase):
    def test_1(self):
        pass
    def runTest(self):
        pass
");

                var test = analyzer.GetTestCases().ToList();
                AssertUtil.ContainsExactly(test.Select(t => t.DisplayName), "test_1");
            }
        }

        private TestAnalyzer MakeTestAnalyzer() {
            return new TestAnalyzer(
                InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7)),
                // Not real files/directories, but not an entirely fake path
                TestData.GetPath("Fob.pyproj"),
                TestData.GetPath("Fob"),
                new Uri("executor://TestOnly/v1")
            );
        }

        private void AddModule(TestAnalyzer analyzer, string moduleName, string code, string moduleFile = null) {
            using (var source = new StringReader(code)) {
                analyzer.AddModule(
                    moduleName,
                    TestData.GetPath("Fob\\" + (moduleFile ?? moduleName.Replace('.', '\\') + ".py")),
                    source
                );
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
