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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;

namespace PythonToolsUITests {
    [TestClass]
    public class AddImportTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        /// <summary>
        /// Imports get added after a doc string
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void DocString() {
            string expectedText = @"'''fob'''
import itertools

itertools";

            AddSmartTagTest("DocString.py", 3, 10, new[] { "import itertools" }, 0, expectedText);
        }

        /// <summary>
        /// Imports get added after a unicode doc string
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void UnicodeDocString() {
            string expectedText = @"u'''fob'''
import itertools

itertools";

            AddSmartTagTest("UnicodeDocString.py", 3, 10, new[] { "import itertools" }, 0, expectedText);
        }

        /// <summary>
        /// Future import gets added after doc string, but before other imports.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void DocStringFuture() {
            string expectedText = @"'''fob'''
from __future__ import with_statement
import itertools

with_statement";

            AddSmartTagTest("DocStringFuture.py", 4, 10, new[] { "from __future__ import with_statement" }, 0, expectedText);
        }


        /// <summary>
        /// Add a from .. import for a function in another module
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportFunctionFrom() {
            string expectedText = @"from test_module import module_func
module_func()";

            AddSmartTagTest("ImportFunctionFrom.py", 1, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from .. import for a function in a subpackage
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportFunctionFromSubpackage() {
            string expectedText = @"from test_package.sub_package import subpackage_method
subpackage_method()";

            AddSmartTagTest("ImportFunctionFromSubpackage.py", 1, 1, new[] { "from test_package.sub_package import subpackage_method" }, 0, expectedText);
        }

        /// <summary>
        /// We should understand assignment from import statements even in the face of errors
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportWithErrors() {
            // http://pytools.codeplex.com/workitem/547
            AddSmartTagTest("ImportWithError.py", 1, 9, _NoSmartTags);
            AddSmartTagTest("ImportWithError.py", 2, 3, _NoSmartTags);
        }

        /// <summary>
        /// Add a from .. import for a function in a built-in module
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportBuiltinFunction() {
            string expectedText = @"from sys import getrecursionlimit
getrecursionlimit()";

            AddSmartTagTest("ImportBuiltinFunction.py", 1, 1, new[] { "from sys import getrecursionlimit" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportFunctionFromExistingFromImport() {
            string expectedText = @"from test_module import module_func_2, module_func
module_func()";

            AddSmartTagTest("ImportFunctionFromExistingFromImport.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import is an "from ... import oar as baz" import.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportFunctionFromExistingFromImportAsName() {
            string expectedText = @"from test_module import module_func_2 as oar, module_func
module_func()";

            AddSmartTagTest("ImportFunctionFromExistingFromImportAsName.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import contains parens around the imported items list.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportFunctionFromExistingFromImportParens() {
            string expectedText = @"from test_module import (module_func_2, module_func)
module_func()";

            AddSmartTagTest("ImportFunctionFromExistingFromImportParens.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import contains parens around the imported items list and the existing import contains an "as" import.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportFunctionFromExistingFromImportParensAsName() {
            string expectedText = @"from test_module import (module_func_2 as oar, module_func)
module_func()";

            AddSmartTagTest("ImportFunctionFromExistingFromImportParensAsName.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import contains parens around the imported items list and the existing import contains an "as" import
        /// and there's a trailing comma at the end.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportFunctionFromExistingFromImportParensAsNameTrailingComma() {
            string expectedText = @"from test_module import (module_func_2 as oar, module_func)
module_func()";

            AddSmartTagTest("ImportFunctionFromExistingFromImportParensAsNameTrailingComma.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import contains parens around the imported items list and there's a trailing comma at the end.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportFunctionFromExistingFromImportParensTrailingComma() {
            string expectedText = @"from test_module import (module_func_2, module_func)
module_func()";

            AddSmartTagTest("ImportFunctionFromExistingFromImportParensTrailingComma.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Adds an import statement for a package.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportPackage() {
            string expectedText = @"import test_package
test_package";

            AddSmartTagTest("ImportPackage.py", 1, 1, new[] { "*", "import test_package" }, 1, expectedText);
        }

        /// <summary>
        /// Adds an import statement for a package.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportSubPackage() {
            string expectedText = @"from test_package import sub_package
sub_package";

            AddSmartTagTest("ImportSubPackage.py", 1, 1, new[] { "from test_package import sub_package" }, 0, expectedText);
        }

        private static string[] _NoSmartTags = new string[0];

        /// <summary>
        /// Adds an import statement for a package.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void Parameters() {
            var getreclimit = new[] { "from sys import getrecursionlimit" };

            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\AddImport.sln");
                var item = project.ProjectItems.Item("Parameters.py");
                var window = item.Open();
                window.Activate();

                var doc = app.GetDocument(item.Document.FullName);

                AddSmartTagTest(doc, 1, 19, _NoSmartTags);
                AddSmartTagTest(doc, 1, 30, getreclimit);

                AddSmartTagTest(doc, 4, 18, _NoSmartTags);
                AddSmartTagTest(doc, 7, 18, _NoSmartTags);
                AddSmartTagTest(doc, 10, 20, _NoSmartTags);
                AddSmartTagTest(doc, 13, 22, _NoSmartTags);
                AddSmartTagTest(doc, 16, 22, _NoSmartTags);
                AddSmartTagTest(doc, 19, 22, _NoSmartTags);

                AddSmartTagTest(doc, 19, 35, getreclimit);

                AddSmartTagTest(doc, 22, 25, _NoSmartTags);
                AddSmartTagTest(doc, 22, 56, getreclimit);

                AddSmartTagTest(doc, 25, 38, _NoSmartTags);
                AddSmartTagTest(doc, 25, 38, _NoSmartTags);
                AddSmartTagTest(doc, 25, 48, getreclimit);

                AddSmartTagTest(doc, 29, 12, _NoSmartTags);
                AddSmartTagTest(doc, 29, 42, getreclimit);

                AddSmartTagTest(doc, 34, 26, _NoSmartTags);
                AddSmartTagTest(doc, 34, 31, getreclimit);

                AddSmartTagTest(doc, 42, 16, _NoSmartTags);
                AddSmartTagTest(doc, 51, 16, _NoSmartTags);
            }
        }

        /// <summary>
        /// Adds an import statement for a package.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void AssignedWithoutTypeInfo() {
            AddSmartTagTest("Assignments.py", 1, 2, _NoSmartTags);
            AddSmartTagTest("Assignments.py", 1, 8, _NoSmartTags);
        }

        private static void AddSmartTagTest(EditorWindow doc, int line, int column, string[] expectedActions, int invokeAction = -1, string expectedText = null) {
            doc.Invoke(() => {
                var point = doc.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(line - 1).Start.Add(column - 1);
                doc.TextView.Caret.MoveTo(point);
            });

            if (expectedActions.Length > 0) {
                using (var sh = doc.StartSmartTagSession()) {
                    var actions = sh.Session.Actions.ToList();
                    if (expectedActions[0] == "*") {
                        AssertUtil.ContainsAtLeast(
                            actions.Select(a => a.DisplayText.Replace("__", "_")),
                            expectedActions.Skip(1)
                        );
                    } else {
                        AssertUtil.AreEqual(
                            actions.Select(a => a.DisplayText.Replace("__", "_")).ToArray(),
                            expectedActions
                        );
                    }

                    if (invokeAction >= 0) {
                        var action = actions.FirstOrDefault(a => a.DisplayText.Replace("__", "_") == expectedActions[invokeAction]);
                        Assert.IsNotNull(action, "No action named " + expectedActions[invokeAction]);
                        doc.Invoke(() => action.Invoke());
                        Assert.AreEqual(expectedText, doc.Text);
                    }
                }
            } else {
                doc.StartSmartTagSessionNoSession();
            }
        }

        private static void AddSmartTagTest(string filename, int line, int column, string[] expectedActions, int invokeAction = -1, string expectedText = null) {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\AddImport.sln");
                var item = project.ProjectItems.Item(filename);
                var window = item.Open();
                window.Activate();

                var doc = app.GetDocument(item.Document.FullName);

                AddSmartTagTest(doc, line, column, expectedActions, invokeAction, expectedText);
            }
        }
    }
}
