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
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    public class TestExplorerTests {
        public void RunAll(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\TestExplorer.sln");
            var project = app.OpenProject(sln);

            var testExplorer = app.OpenTestExplorer();
            Assert.IsNotNull(testExplorer, "Could not open test explorer");

            testExplorer.GroupByNamespace();

            var successTest = testExplorer.WaitForPythonTest("test1.py", "Test_test1", "test_success", PythonTestExplorer.TestState.NotRun);
            var failedTest = testExplorer.WaitForPythonTest("test1.py", "Test_test1", "test_failure", PythonTestExplorer.TestState.NotRun);

            Console.WriteLine("Running all tests");
            testExplorer.RunAll(TimeSpan.FromSeconds(5));

            successTest = testExplorer.WaitForPythonTest("test1.py", "Test_test1", "test_success", PythonTestExplorer.TestState.Passed);
            failedTest = testExplorer.WaitForPythonTest("test1.py", "Test_test1", "test_failure", PythonTestExplorer.TestState.Failed);

            failedTest.Select();
            testExplorer.Summary.WaitForDetails("test_failure");

            // Check the stack trace for helper() and test_failure() methods
            var stack = testExplorer.Summary.StrackTraceList;
            Assert.AreEqual(2, stack.Items.Count);
            var frame0 = stack.GetFrame(0);
            var frame1 = stack.GetFrame(1);
            Assert.AreEqual("Test_test1.helper", frame0.Name);
            Assert.AreEqual("Test_test1.test_failure", frame1.Name);

            // Click on helper() method in call stack to navigate to code
            Assert.IsNotNull(frame0.Hyperlink, "Link not found for helper() frame");
            frame0.Hyperlink.Invoke();

            // Validate that editor selection is in helper() method
            var doc = app.Dte.ActiveDocument;
            Assert.IsNotNull(doc, "Active document is null");
            var selection = doc.Selection as TextSelection;
            Assert.IsNotNull(selection, "Selection is null");
            Assert.AreEqual("test1.py", doc.Name);
            Assert.AreEqual(11, selection.CurrentLine);
        }
    }
}
