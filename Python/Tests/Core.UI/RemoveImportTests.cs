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

using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;

namespace PythonToolsUITests {
    [TestClass]
    public class RemoveImportTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void FromImport1() {
            string expectedText = @"from sys import oar

oar";

            RemoveSmartTagTest("FromImport1.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("FromImport1.py", 1, 1, false, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void FromImport2() {
            string expectedText = @"from sys import baz

baz";

            RemoveSmartTagTest("FromImport2.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void FromImportParens1() {
            string expectedText = @"from sys import (oar)

oar";

            RemoveSmartTagTest("FromImportParens1.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void FromImportParens2() {
            string expectedText = @"from sys import (baz)

baz";

            RemoveSmartTagTest("FromImportParens2.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void FromImportParensTrailingComma1() {
            string expectedText = @"from sys import (baz, )

baz";

            RemoveSmartTagTest("FromImportParensTrailingComma1.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void FromImportParensTrailingComma2() {
            string expectedText = @"from sys import (oar, )

oar";

            RemoveSmartTagTest("FromImportParensTrailingComma2.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void Import1() {
            string expectedText = @"import oar

oar";

            RemoveSmartTagTest("Import1.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void Import2() {
            string expectedText = @"import baz

baz";

            RemoveSmartTagTest("Import2.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void Import3() {
            string expectedText = @"import baz

baz";

            RemoveSmartTagTest("Import3.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void Import4() {
            string expectedText = @"import oar, quox

oar
quox";

            RemoveSmartTagTest("Import4.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void Import5() {
            string expectedText = @"import oar, quox

oar
quox";

            RemoveSmartTagTest("Import5.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void Import6() {
            string expectedText = @"import oar,          quox

oar
quox";

            RemoveSmartTagTest("Import6.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportComment() {
            string expectedText = @"#baz
import oar,          quox
#fob
#oar

oar
quox";

            RemoveSmartTagTest("ImportComment.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void FromImportComment() {
            string expectedText = @"#baz
from xyz import oar,          quox
#fob
#oar

oar
quox";

            RemoveSmartTagTest("FromImportComment.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportDup() {
            string expectedText = @"";

            RemoveSmartTagTest("ImportDup.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void FromImportDup() {
            string expectedText = @"";

            RemoveSmartTagTest("FromImportDup.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void Import() {
            string expectedText = @"";

            RemoveSmartTagTest("Import.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void FromImport() {
            string expectedText = @"";

            RemoveSmartTagTest("FromImport.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void FutureImport() {
            string expectedText = @"from __future__ import with_statement";

            RemoveSmartTagTest("FutureImport.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void LocalScopeDontRemoveGlobal() {
            string expectedText = @"import dne

def f():
    import baz

    baz";

            RemoveSmartTagTest("LocalScopeDontRemoveGlobal.py", 4, 10, false, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void LocalScopeOnly() {
            string expectedText = @"import dne

def f():

    oar";

            RemoveSmartTagTest("LocalScopeOnly.py", 4, 10, false, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ImportTrailingWhitespace() {
            string expectedText = @"fob";

            RemoveSmartTagTest("ImportTrailingWhitespace.py", 1, 1, true, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ClosureReference() {
            string expectedText = @"def f():
    import something
    def g():
        something";

            RemoveSmartTagTest("ClosureReference.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("ClosureReference.py", 2, 14, false, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void NameMangledUnmangled() {
            string expectedText = @"class C:
    def f(self):
        import __fob
        x = _C__fob";

            RemoveSmartTagTest("NameMangleUnmangled.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("NameMangleUnmangled.py", 3, 14, false, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void NameMangledMangled() {
            string expectedText = @"class C:
    def f(self):
        import __fob
        x = __fob";

            RemoveSmartTagTest("NameMangleMangled.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("NameMangleMangled.py", 3, 14, false, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void EmptyFuncDef1() {
            string expectedText = @"def f():
    pass";

            RemoveSmartTagTest("EmptyFuncDef1.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("EmptyFuncDef1.py", 2, 7, false, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void EmptyFuncDef2() {
            string expectedText = @"def f():
    pass";

            RemoveSmartTagTest("EmptyFuncDef2.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("EmptyFuncDef2.py", 2, 7, false, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void EmptyFuncDefWhitespace() {
            string expectedText = @"def f():
    pass";

            RemoveSmartTagTest("EmptyFuncDefWhitespace.py", 1, 1, true, expectedText);
            RemoveSmartTagTest("EmptyFuncDefWhitespace.py", 2, 7, false, expectedText);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
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
