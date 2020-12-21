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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    public class AddImportTests {
        /// <summary>
        /// Imports get added after a doc string
        /// </summary>
        public void DocString(VisualStudioApp app) {
            string expectedText = @"'''fob'''
import itertools

itertools";

            AddLightBulbTest(app, "DocString.py", 3, 10, new[] { "import itertools" }, 0, expectedText);
        }

        /// <summary>
        /// Imports get added after a unicode doc string
        /// </summary>
        public void UnicodeDocString(VisualStudioApp app) {
            string expectedText = @"u'''fob'''
import itertools

itertools";

            AddLightBulbTest(app, "UnicodeDocString.py", 3, 10, new[] { "import itertools" }, 0, expectedText);
        }

        /// <summary>
        /// Future import gets added after doc string, but before other imports.
        /// </summary>
        public void DocStringFuture(VisualStudioApp app) {
            string expectedText = @"'''fob'''
from __future__ import with_statement
import itertools

with_statement";

            AddLightBulbTest(app, "DocStringFuture.py", 4, 10, new[] { "from __future__ import with_statement" }, 0, expectedText);
        }


        /// <summary>
        /// Add a from .. import for a function in another module
        /// </summary>
        public void ImportFunctionFrom(VisualStudioApp app) {
            string expectedText = @"from test_module import module_func
module_func()";

            AddLightBulbTest(app, "ImportFunctionFrom.py", 1, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from .. import for a function in a subpackage
        /// </summary>
        public void ImportFunctionFromSubpackage(VisualStudioApp app) {
            string expectedText = @"from test_package.sub_package import subpackage_method
subpackage_method()";

            AddLightBulbTest(app, "ImportFunctionFromSubpackage.py", 1, 1, new[] { "from test_package.sub_package import subpackage_method" }, 0, expectedText);
        }

        /// <summary>
        /// We should understand assignment from import statements even in the face of errors
        /// </summary>
        public void ImportWithErrors(VisualStudioApp app) {
            // http://pytools.codeplex.com/workitem/547
            AddLightBulbTest(app, "ImportWithError.py", 1, 9, _NoLightBulbs);
            AddLightBulbTest(app, "ImportWithError.py", 2, 3, _NoLightBulbs);
        }

        /// <summary>
        /// Add a from .. import for a function in a built-in module
        /// </summary>
        public void ImportBuiltinFunction(VisualStudioApp app) {
            string expectedText = @"from sys import getrecursionlimit
getrecursionlimit()";

            AddLightBulbTest(app, "ImportBuiltinFunction.py", 1, 1, new[] { "from sys import getrecursionlimit" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module.
        /// </summary>
        public void ImportFunctionFromExistingFromImport(VisualStudioApp app) {
            string expectedText = @"from test_module import module_func_2, module_func
module_func()";

            AddLightBulbTest(app, "ImportFunctionFromExistingFromImport.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import is an "from ... import oar as baz" import.
        /// </summary>
        public void ImportFunctionFromExistingFromImportAsName(VisualStudioApp app) {
            string expectedText = @"from test_module import module_func_2 as oar, module_func
module_func()";

            AddLightBulbTest(app, "ImportFunctionFromExistingFromImportAsName.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import contains parens around the imported items list.
        /// </summary>
        public void ImportFunctionFromExistingFromImportParens(VisualStudioApp app) {
            string expectedText = @"from test_module import (module_func_2, module_func)
module_func()";

            AddLightBulbTest(app, "ImportFunctionFromExistingFromImportParens.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import contains parens around the imported items list and the existing import contains an "as" import.
        /// </summary>
        public void ImportFunctionFromExistingFromImportParensAsName(VisualStudioApp app) {
            string expectedText = @"from test_module import (module_func_2 as oar, module_func)
module_func()";

            AddLightBulbTest(app, "ImportFunctionFromExistingFromImportParensAsName.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import contains parens around the imported items list and the existing import contains an "as" import
        /// and there's a trailing comma at the end.
        /// </summary>
        public void ImportFunctionFromExistingFromImportParensAsNameTrailingComma(VisualStudioApp app) {
            string expectedText = @"from test_module import (module_func_2 as oar, module_func)
module_func()";

            AddLightBulbTest(app, "ImportFunctionFromExistingFromImportParensAsNameTrailingComma.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Add a from ... import for a function in another module when a from import already exists for the same module and
        /// the existing import contains parens around the imported items list and there's a trailing comma at the end.
        /// </summary>
        public void ImportFunctionFromExistingFromImportParensTrailingComma(VisualStudioApp app) {
            string expectedText = @"from test_module import (module_func_2, module_func)
module_func()";

            AddLightBulbTest(app, "ImportFunctionFromExistingFromImportParensTrailingComma.py", 2, 1, new[] { "from test_module import module_func" }, 0, expectedText);
        }

        /// <summary>
        /// Adds an import statement for a package.
        /// </summary>
        public void ImportPackage(VisualStudioApp app) {
            string expectedText = @"import test_package
test_package";

            AddLightBulbTest(app, "ImportPackage.py", 1, 1, new[] { "*", "import test_package" }, 1, expectedText);
        }

        /// <summary>
        /// Adds an import statement for a package.
        /// </summary>
        public void ImportSubPackage(VisualStudioApp app) {
            string expectedText = @"from test_package import sub_package
sub_package";

            AddLightBulbTest(app, "ImportSubPackage.py", 1, 1, new[] { "from test_package import sub_package" }, 0, expectedText);
        }

        private static string[] _NoLightBulbs = new string[0];

        /// <summary>
        /// Adds an import statement for a package.
        /// </summary>
        public void Parameters(VisualStudioApp app) {
            var getreclimit = new[] { "from sys import getrecursionlimit" };

            var project = app.OpenProject(@"TestData\AddImport.sln");
            var item = project.ProjectItems.Item("Parameters.py");
            var window = item.Open();
            window.Activate();

            var doc = app.GetDocument(item.Document.FullName);

            AddLightBulbTest(doc, 1, 19, _NoLightBulbs);
            AddLightBulbTest(doc, 1, 30, getreclimit);

            AddLightBulbTest(doc, 4, 18, _NoLightBulbs);
            AddLightBulbTest(doc, 7, 18, _NoLightBulbs);
            AddLightBulbTest(doc, 10, 20, _NoLightBulbs);
            AddLightBulbTest(doc, 13, 22, _NoLightBulbs);
            AddLightBulbTest(doc, 16, 22, _NoLightBulbs);
            AddLightBulbTest(doc, 19, 22, _NoLightBulbs);

            AddLightBulbTest(doc, 19, 35, getreclimit);

            AddLightBulbTest(doc, 22, 25, _NoLightBulbs);
            AddLightBulbTest(doc, 22, 56, getreclimit);

            AddLightBulbTest(doc, 25, 38, _NoLightBulbs);
            AddLightBulbTest(doc, 25, 38, _NoLightBulbs);
            AddLightBulbTest(doc, 25, 48, getreclimit);

            AddLightBulbTest(doc, 29, 12, _NoLightBulbs);
            AddLightBulbTest(doc, 29, 42, getreclimit);

            AddLightBulbTest(doc, 34, 26, _NoLightBulbs);
            AddLightBulbTest(doc, 34, 31, getreclimit);

            AddLightBulbTest(doc, 42, 16, _NoLightBulbs);
            AddLightBulbTest(doc, 51, 16, _NoLightBulbs);
        }

        /// <summary>
        /// Adds an import statement for a package.
        /// </summary>
        public void AssignedWithoutTypeInfo(VisualStudioApp app) {
            AddLightBulbTest(app, "Assignments.py", 1, 2, _NoLightBulbs);
            AddLightBulbTest(app, "Assignments.py", 1, 8, _NoLightBulbs);
        }

        private static void AddLightBulbTest(EditorWindow doc, int line, int column, string[] expectedActions, int invokeAction = -1, string expectedText = null) {
            doc.InvokeTask(async () => {
                var point = doc.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(line - 1).Start.Add(column - 1);
                doc.TextView.Caret.MoveTo(point);
                await doc.WaitForAnalyzerAtCaretAsync();
            });

            if (expectedActions.Length > 0) {
                using (var sh = doc.StartLightBulbSession()) {
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
                        doc.WaitForText(expectedText);
                    }
                }
            } else {
                doc.StartLightBulbSessionNoSession();
            }
        }

        private static void AddLightBulbTest(VisualStudioApp app, string filename, int line, int column, string[] expectedActions, int invokeAction = -1, string expectedText = null) {
            var project = app.OpenProject(@"TestData\AddImport.sln");
            var item = project.ProjectItems.Item(filename);
            var window = item.Open();
            window.Activate();

            var doc = app.GetDocument(item.Document.FullName);

            AddLightBulbTest(doc, line, column, expectedActions, invokeAction, expectedText);
        }
    }
}
