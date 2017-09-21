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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    //[TestClass]
    public class RemoveImportTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FromImport1() {
            string expectedText = @"from sys import oar

oar";

            RemoveSmartTagTest("FromImport1.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("FromImport1.py", 1, 1, false, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FromImport2() {
            string expectedText = @"from sys import baz

baz";

            RemoveSmartTagTest("FromImport2.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FromImportParens1() {
            string expectedText = @"from sys import (oar)

oar";

            RemoveSmartTagTest("FromImportParens1.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FromImportParens2() {
            string expectedText = @"from sys import (baz)

baz";

            RemoveSmartTagTest("FromImportParens2.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FromImportParensTrailingComma1() {
            string expectedText = @"from sys import (baz, )

baz";

            RemoveSmartTagTest("FromImportParensTrailingComma1.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FromImportParensTrailingComma2() {
            string expectedText = @"from sys import (oar, )

oar";

            RemoveSmartTagTest("FromImportParensTrailingComma2.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void Import1() {
            string expectedText = @"import oar

oar";

            RemoveSmartTagTest("Import1.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void Import2() {
            string expectedText = @"import baz

baz";

            RemoveSmartTagTest("Import2.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void Import3() {
            string expectedText = @"import baz

baz";

            RemoveSmartTagTest("Import3.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void Import4() {
            string expectedText = @"import oar, quox

oar
quox";

            RemoveSmartTagTest("Import4.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void Import5() {
            string expectedText = @"import oar, quox

oar
quox";

            RemoveSmartTagTest("Import5.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void Import6() {
            string expectedText = @"import oar,          quox

oar
quox";

            RemoveSmartTagTest("Import6.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void ImportComment() {
            string expectedText = @"#baz
import oar,          quox
#fob
#oar

oar
quox";

            RemoveSmartTagTest("ImportComment.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FromImportComment() {
            string expectedText = @"#baz
from xyz import oar,          quox
#fob
#oar

oar
quox";

            RemoveSmartTagTest("FromImportComment.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void ImportDup() {
            string expectedText = @"";

            RemoveSmartTagTest("ImportDup.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FromImportDup() {
            string expectedText = @"";

            RemoveSmartTagTest("FromImportDup.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void Import() {
            string expectedText = @"";

            RemoveSmartTagTest("Import.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FromImport() {
            string expectedText = @"";

            RemoveSmartTagTest("FromImport.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FutureImport() {
            string expectedText = @"from __future__ import with_statement";

            RemoveSmartTagTest("FutureImport.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void LocalScopeDontRemoveGlobal() {
            string expectedText = @"import dne

def f():
    import baz

    baz";

            RemoveSmartTagTest("LocalScopeDontRemoveGlobal.py", 4, 10, false, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void LocalScopeOnly() {
            string expectedText = @"import dne

def f():

    oar";

            RemoveSmartTagTest("LocalScopeOnly.py", 4, 10, false, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void ImportTrailingWhitespace() {
            string expectedText = @"fob";

            RemoveSmartTagTest("ImportTrailingWhitespace.py", 1, 1, true, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void ClosureReference() {
            string expectedText = @"def f():
    import something
    def g():
        something";

            RemoveSmartTagTest("ClosureReference.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("ClosureReference.py", 2, 14, false, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void NameMangledUnmangled() {
            string expectedText = @"class C:
    def f(self):
        import __fob
        x = _C__fob";

            RemoveSmartTagTest("NameMangleUnmangled.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("NameMangleUnmangled.py", 3, 14, false, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void NameMangledMangled() {
            string expectedText = @"class C:
    def f(self):
        import __fob
        x = __fob";

            RemoveSmartTagTest("NameMangleMangled.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("NameMangleMangled.py", 3, 14, false, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void EmptyFuncDef1() {
            string expectedText = @"def f():
    pass";

            RemoveSmartTagTest("EmptyFuncDef1.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("EmptyFuncDef1.py", 2, 7, false, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void EmptyFuncDef2() {
            string expectedText = @"def f():
    pass";

            RemoveSmartTagTest("EmptyFuncDef2.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("EmptyFuncDef2.py", 2, 7, false, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void EmptyFuncDefWhitespace() {
            string expectedText = @"def f():
    pass";

            RemoveSmartTagTest("EmptyFuncDefWhitespace.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("EmptyFuncDefWhitespace.py", 2, 7, false, expectedText);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void ImportStar() {
            string expectedText = @"from sys import *";

            RemoveSmartTagTest("ImportStar.py", 1, 1, true, expectedText);
        }

        private static void RemoveSmartTagTest(string filename, int line, int column, bool allScopes, string expectedText) {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\RemoveImport.sln");
                var item = project.ProjectItems.Item(filename);
                var window = item.Open();
                window.Activate();

                var doc = app.GetDocument(item.Document.FullName);

                doc.Invoke(() => {
                    var point = doc.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(line - 1).Start.Add(column - 1);
                    doc.TextView.Caret.MoveTo(point);
                    doc.WaitForAnalyzerAtCaret();
                });

                if (allScopes) {
                    app.ExecuteCommand("EditorContextMenus.CodeWindow.RemoveImports.AllScopes");
                } else {
                    app.ExecuteCommand("EditorContextMenus.CodeWindow.RemoveImports.CurrentScope");
                }

                doc.WaitForText(expectedText);
            }
        }
    }
}
