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

using System.Linq;
using AnalysisTest.ProjectSystem;
using AnalysisTest.UI;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using EnvDTE;

namespace AnalysisTest {
    [TestClass]
    [DeploymentItem(@"..\\PythonTools\\CompletionDB\\", "CompletionDB")]
    [DeploymentItem(@"Python.VS.TestData\", "Python.VS.TestData")]
    public class AddImportTests {
        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }


        /// <summary>
        /// Imports get added after a doc string
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DocString() {
            string expectedText = @"'''foo'''
import itertools

itertools";

            AddSmartTagTest("DocString.py", 3, 10, new[] { "import itertools" }, 0, expectedText);
        }

        /// <summary>
        /// Imports get added after a unicode doc string
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void UnicodeDocString() {
            string expectedText = @"u'''foo'''
import itertools

itertools";

            AddSmartTagTest("UnicodeDocString.py", 3, 10, new[] { "import itertools" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from .. import for a function in another module
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ImportFunctionFrom() {
            string expectedText = @"from test_module import module_func
module_func()";

            AddSmartTagTest("ImportFunctionFrom.py", 1, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from .. import for a function in a subpackage
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ImportFunctionFromSubpackage() {
            string expectedText = @"from test_package.sub_package import subpackage_method
subpackage_method()";

            AddSmartTagTest("ImportFunctionFromSubpackage.py", 1, 1, new[] { "from test_package.sub_package import subpackage_method" }, 0, expectedText);
        }


        /// <summary>
        /// Add a from .. import for a function in a built-in module
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ImportBuiltinFunction() {
            string expectedText = @"from sys import getrecursionlimit
getrecursionlimit()";

            AddSmartTagTest("ImportBuiltinFunction.py", 1, 1, new[] { "from sys import getrecursionlimit" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ImportFunctionFromExistingFromImport() {
            string expectedText = @"from test_module import module_func_2, module_func
module_func()";

            AddSmartTagTest("ImportFunctionFromExistingFromImport.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import is an "from ... import bar as baz" import.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ImportFunctionFromExistingFromImportAsName() {
            string expectedText = @"from test_module import module_func_2 as bar, module_func
module_func()";

            AddSmartTagTest("ImportFunctionFromExistingFromImportAsName.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import contains parens around the imported items list.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ImportFunctionFromExistingFromImportParens() {
            string expectedText = @"from test_module import (module_func_2, module_func)
module_func()";

            AddSmartTagTest("ImportFunctionFromExistingFromImportParens.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import contains parens around the imported items list and the existing import contains an "as" import.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ImportFunctionFromExistingFromImportParensAsName() {
            string expectedText = @"from test_module import (module_func_2 as bar, module_func)
module_func()";

            AddSmartTagTest("ImportFunctionFromExistingFromImportParensAsName.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import contains parens around the imported items list and the existing import contains an "as" import
        /// and there's a trailing comma at the end.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ImportFunctionFromExistingFromImportParensAsNameTrailingComma() {
            string expectedText = @"from test_module import (module_func_2 as bar, module_func)
module_func()";

            AddSmartTagTest("ImportFunctionFromExistingFromImportParensAsNameTrailingComma.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import contains parens around the imported items list and there's a trailing comma at the end.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ImportFunctionFromExistingFromImportParensTrailingComma() {
            string expectedText = @"from test_module import (module_func_2, module_func)
module_func()";

            AddSmartTagTest("ImportFunctionFromExistingFromImportParensTrailingComma.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Adds an import statement for a package.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ImportPackage() {
            string expectedText = @"import test_package
test_package";

            AddSmartTagTest("ImportPackage.py", 1, 1, new[] { "import test_package" }, 0, expectedText);
        }

        /// <summary>
        /// Adds an import statement for a package.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ImportSubPackage() {
            string expectedText = @"from test_package import sub_package
sub_package";

            AddSmartTagTest("ImportSubPackage.py", 1, 1, new[] { "from test_package import sub_package" }, 0, expectedText);
        }

        private static void AddSmartTagTest(string filename, int line, int column, string[] expectedActions, int invokeAction, string expectedText) {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\AddImport.sln");
            var item = project.ProjectItems.Item(filename);
            var window = item.Open();
            window.Activate();

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var doc = app.GetDocument(item.Document.FullName);

            doc.Invoke(() => {
                var point = doc.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(line - 1).Start.Add(column - 1);
                doc.TextView.Caret.MoveTo(point);
            });

            var session = doc.StartSmartTagSession();
            Assert.AreEqual(1, session.ActionSets.Count);
            var set = session.ActionSets.First();

            Assert.AreEqual(set.Actions.Count, expectedActions.Length);
            for (int i = 0; i < set.Actions.Count; i++) {
                Assert.AreEqual(set.Actions[i].DisplayText, expectedActions[i].Replace("_", "__"));
            }

            if (invokeAction != -1) {
                doc.Invoke(() => set.Actions[invokeAction].Invoke());
                Assert.AreEqual(expectedText, doc.Text);
            }

            VsIdeTestHostContext.Dte.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
        }
    }
}
