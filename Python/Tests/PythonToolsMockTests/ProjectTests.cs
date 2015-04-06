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
using System.Linq;
using System.Windows.Input;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.MockVsTests;
using TestUtilities;
using TestUtilities.SharedProject;

namespace PythonToolsMockTests {
    [TestClass]
    public class ProjectTests : SharedProjectTest {
        public static ProjectType PythonProject = ProjectTypes.First(x => x.ProjectExtension == ".pyproj");

        [TestMethod]
        public void BasicProjectTest() {
            var sln = new ProjectDefinition(
                "HelloWorld",
                PythonProject,
                Compile("server")
            ).Generate();

            using (var vs = sln.ToMockVs()) {
                Assert.IsNotNull(vs.WaitForItem("HelloWorld", "Python Environments"));
                Assert.IsNotNull(vs.WaitForItem("HelloWorld", "References"));
                Assert.IsNotNull(vs.WaitForItem("HelloWorld", "Search Paths"));
                Assert.IsNotNull(vs.WaitForItem("HelloWorld", "server.py"));
                var view = vs.OpenItem("HelloWorld", "server.py");

                view.Type("import ");

                var session = view.TopSession as ICompletionSession;

                AssertUtil.Contains(session.Completions(), "sys");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void CutRenamePaste() {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("CutRenamePaste"),
                        Compile("CutRenamePaste\\CutRenamePaste")
                    )
                );

                using (var solution = testDef.Generate().ToMockVs()) {
                    var project = solution.WaitForItem("DragDropCopyCutPaste");
                    var file = solution.WaitForItem("DragDropCopyCutPaste", "CutRenamePaste", "CutRenamePaste" + projectType.CodeExtension);

                    file.Select();
                    solution.ControlX();

                    file.Select();
                    solution.Type(Key.F2);
                    solution.Type("CutRenamePasteNewName");
                    solution.Type(Key.Enter);

                    solution.Sleep(1000);
                    project.Select();
                    solution.ControlV();

                    solution.CheckMessageBox("The source URL 'CutRenamePaste" + projectType.CodeExtension + "' could not be found.");
                }
            }
        }

    }
}
