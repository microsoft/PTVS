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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    public class TestExplorerTests {
        private static TestInfo[] AllPytests = new TestInfo[] {
            // test_pt.py
            new TestInfo("test_pt_fail", "test_pt", "test_pt", "test_pt.py", 4, "Failed", "assert False"),
            new TestInfo("test_pt_pass", "test_pt", "test_pt", "test_pt.py", 1, "Passed"),
            new TestInfo("test_method_pass", "test_pt", "TestClassPT", "test_pt.py", 8, "Passed"),

            // test_ut.py
            new TestInfo("test_ut_fail", "test_ut", "TestClassUT", "test_ut.py", 4, "Failed", "AssertionError: Not implemented"),
            new TestInfo("test_ut_pass", "test_ut", "TestClassUT", "test_ut.py", 7, "Passed"),

            // test_mark.py
            new TestInfo("test_webtest", "test_mark", "test_mark", "test_mark.py", 5, "Passed"),
            new TestInfo("test_skip", "test_mark", "test_mark", "test_mark.py", 9, "Skipped", "skip unconditionally"),
            new TestInfo("test_skipif_not_skipped", "test_mark", "test_mark", "test_mark.py", 17, "Passed"),
            new TestInfo("test_skipif_skipped", "test_mark", "test_mark", "test_mark.py", 13, "Skipped", "skip VAL == 1"),

            // test_fixture.py
            new TestInfo("test_data[0]", "test_fixture", "test_fixture", "test_fixture.py", 7, "Passed"),
            new TestInfo("test_data[1]", "test_fixture", "test_fixture", "test_fixture.py", 7, "Passed"),
            new TestInfo("test_data[3]", "test_fixture", "test_fixture", "test_fixture.py", 7, "Failed", "assert 3 != 3"),
        };

        // TODO: when new unittest support is ready, reimplement this (and add test for workspace)
        //public void RunAllUnittestProject(PythonVisualStudioApp app) {
        //    var sln = app.CopyProjectForTest(@"TestData\TestExplorer.sln");
        //    var project = app.OpenProject(sln);

        //    var testExplorer = app.OpenTestExplorer();
        //    Assert.IsNotNull(testExplorer, "Could not open test explorer");

        //    testExplorer.GroupByProjectNamespaceClass();

        //    var successTest = testExplorer.WaitForPythonTest("test1.py", "Test_test1", "test_success", PythonTestExplorer.TestState.NotRun);
        //    var failedTest = testExplorer.WaitForPythonTest("test1.py", "Test_test1", "test_failure", PythonTestExplorer.TestState.NotRun);

        //    Console.WriteLine("Running all tests");
        //    testExplorer.RunAll(TimeSpan.FromSeconds(5));

        //    successTest = testExplorer.WaitForPythonTest("test1.py", "Test_test1", "test_success", PythonTestExplorer.TestState.Passed);
        //    failedTest = testExplorer.WaitForPythonTest("test1.py", "Test_test1", "test_failure", PythonTestExplorer.TestState.Failed);

        //    failedTest.Select();
        //    testExplorer.Summary.WaitForDetails("test_failure");

        //    // Check the stack trace for helper() and test_failure() methods
        //    var stack = testExplorer.Summary.StrackTraceList;
        //    Assert.AreEqual(2, stack.Items.Count);
        //    var frame0 = stack.GetFrame(0);
        //    var frame1 = stack.GetFrame(1);
        //    Assert.AreEqual("Test_test1.helper", frame0.Name);
        //    Assert.AreEqual("Test_test1.test_failure", frame1.Name);

        //    // Click on helper() method in call stack to navigate to code
        //    Assert.IsNotNull(frame0.Hyperlink, "Link not found for helper() frame");
        //    frame0.Hyperlink.Invoke();

        //    // Validate that editor selection is in helper() method
        //    var doc = app.Dte.ActiveDocument;
        //    Assert.IsNotNull(doc, "Active document is null");
        //    var selection = doc.Selection as TextSelection;
        //    Assert.IsNotNull(selection, "Selection is null");
        //    Assert.AreEqual("test1.py", doc.Name);
        //    Assert.AreEqual(11, selection.CurrentLine);
        //}

        public void RunAllPytestProject(PythonVisualStudioApp app) {
            var defaultSetter = new InterpreterWithPackageSetter(app.ServiceProvider, "pytest");
            using (defaultSetter) {
                var sln = app.CopyProjectForTest(@"TestData\TestExplorerPytest.sln");
                var project = app.OpenProject(sln);

                RunAllPytest(app);
            }
        }

        public void RunAllPytestWorkspace(PythonVisualStudioApp app) {
            var defaultSetter = new InterpreterWithPackageSetter(app.ServiceProvider, "pytest");
            using (defaultSetter) {
                // Create a workspace folder with pytest enabled and with the
                // same set of test files used for the project-based test.
                var workspaceFolderPath = Path.Combine(TestData.GetTempPath(), "TestExplorerPytest");
                Directory.CreateDirectory(workspaceFolderPath);

                var pythonSettingsJson = "{\"TestFramework\": \"Pytest\"}";
                File.WriteAllText(Path.Combine(workspaceFolderPath, "PythonSettings.json"), pythonSettingsJson);

                var sourceProjectFolderPath = TestData.GetPath("TestData", "TestExplorerPytest");
                foreach (var filePath in Directory.GetFiles(sourceProjectFolderPath, "*.py")) {
                    var destFilePath = Path.Combine(workspaceFolderPath, Path.GetFileName(filePath));
                    File.Copy(filePath, destFilePath, true);
                }

                app.OpenFolder(workspaceFolderPath);

                RunAllPytest(app);
            }
        }

        private static void RunAllPytest(PythonVisualStudioApp app) {
            var testExplorer = app.OpenTestExplorer();
            Assert.IsNotNull(testExplorer, "Could not open test explorer");

            Console.WriteLine("Waiting for tests discovery");
            app.WaitForOutputWindowText("Tests", "Discovery finished: 12 tests found", 15_000);

            testExplorer.GroupByProjectNamespaceClass();

            foreach (var test in AllPytests) {
                var item = testExplorer.WaitForItem(test.Path);
                Assert.IsNotNull(item, $"Coult not find {string.Join(":", test.Path)}");
            }

            Console.WriteLine("Running all tests");
            testExplorer.RunAll(TimeSpan.FromSeconds(10));
            app.WaitForOutputWindowText("Tests", "Run finished: 12 tests run", 10_000);

            foreach (var test in AllPytests) {
                var item = testExplorer.WaitForItem(test.Path);
                Assert.IsNotNull(item, $"Coult not find {string.Join(":", test.Path)}");

                item.Select();
                item.SetFocus();

                var details = testExplorer.GetDetailsWithRetry();

                AssertUtil.Contains(details, $"Test Name:	{test.Name}");
                AssertUtil.Contains(details, $"Test Outcome:	{test.Outcome}");
                AssertUtil.Contains(details, $"{test.SourceFile} : line {test.SourceLine}");

                if (test.ResultMessage != null) {
                    AssertUtil.Contains(details, $"Result Message:	{test.ResultMessage}");
                }
            }
        }

        class TestInfo {
            public TestInfo(string name, string project, string classOrModule, string sourceFile, int sourceLine, string outcome, string resultMessage = null) {
                Name = name;
                Project = project;
                ClassOrModule = classOrModule;
                SourceFile = sourceFile;
                SourceLine = sourceLine;
                Outcome = outcome;
                ResultMessage = resultMessage;

                Path = new string[] { Project, SourceFile, ClassOrModule, Name };
            }

            public string Name { get; }
            public string Project { get; }
            public string ClassOrModule { get; }
            public string SourceFile { get; }
            public int SourceLine { get; }
            public string Outcome { get; }
            public string ResultMessage { get; }
            public string[] Path { get; }
        }
    }
}
