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
using System.Reflection;
using System.Threading;
using System.Windows.Input;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;
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

        [TestMethod]
        public void ShouldWarnOnRun() {
            var sln = new ProjectDefinition(
                "HelloWorld",
                PythonProject,
                Compile("app", "print \"hello\"")
            ).Generate();

            using (var vs = sln.ToMockVs())
            using (var analyzerChanged = new AutoResetEvent(false)) {
                var project = vs.GetProject("HelloWorld").GetPythonProject();
                project.ProjectAnalyzerChanged += (s, e) => analyzerChanged.Set();

                var v27 = InterpreterFactoryCreator.CreateInterpreterFactory(new InterpreterFactoryCreationOptions {
                    LanguageVersion = new Version(2, 7),
                    PrefixPath = "C:\\Python27",
                    InterpreterPath = "C:\\Python27\\python.exe"
                });
                var v34 = InterpreterFactoryCreator.CreateInterpreterFactory(new InterpreterFactoryCreationOptions {
                    LanguageVersion = new Version(3, 4),
                    PrefixPath = "C:\\Python34",
                    InterpreterPath = "C:\\Python34\\python.exe"
                });

                var uiThread = (UIThreadBase)project.GetService(typeof(UIThreadBase));

                uiThread.Invoke(() => {
                    project.Interpreters.AddInterpreter(v27);
                    project.Interpreters.AddInterpreter(v34);
                });

                project.SetInterpreterFactory(v27);
                Assert.IsTrue(analyzerChanged.WaitOne(10000), "Timed out waiting for analyzer change #1");
                uiThread.Invoke(() => project.GetAnalyzer()).WaitForCompleteAnalysis(_ => true);
                Assert.IsFalse(project.ShouldWarnOnLaunch, "Should not warn on 2.7");

                project.SetInterpreterFactory(v34);
                Assert.IsTrue(analyzerChanged.WaitOne(10000), "Timed out waiting for analyzer change #2");
                uiThread.Invoke(() => project.GetAnalyzer()).WaitForCompleteAnalysis(_ => true);
                Assert.IsTrue(project.ShouldWarnOnLaunch, "Expected warning on 3.4");

                project.SetInterpreterFactory(v27);
                Assert.IsTrue(analyzerChanged.WaitOne(10000), "Timed out waiting for analyzer change #3");
                uiThread.Invoke(() => project.GetAnalyzer()).WaitForCompleteAnalysis(_ => true);
                Assert.IsFalse(project.ShouldWarnOnLaunch, "Expected warning to go away on 2.7");
            }
        }

    }
}
