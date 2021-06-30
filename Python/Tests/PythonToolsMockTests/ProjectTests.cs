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

extern alias analysis;
extern alias pythontools;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using analysis::Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.MockVsTests;
using pythontools::Microsoft.PythonTools;
using pythontools::Microsoft.PythonTools.Editor;
using pythontools::Microsoft.PythonTools.Project;
using pythontools::Microsoft.VisualStudioTools.Project.Automation;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.SharedProject;
using TestUtilities.UI.Python;

namespace PythonToolsMockTests {
    [TestClass]
    public class ProjectTests {
        static PythonProjectGenerator Generator = PythonProjectGenerator.CreateStatic();

        [TestMethod, Priority(UnitTestPriority.P2_FAILING)]
        public void BasicProjectTest() {
            var sln = Generator.Project(
                "HelloWorld",
                ProjectGenerator.Compile("server", "")
            ).Generate();

            using (var vs = sln.ToMockVs()) {
                Assert.IsNotNull(vs.WaitForItem("HelloWorld", "Python Environments"));
                Assert.IsNotNull(vs.WaitForItem("HelloWorld", "References"));
                Assert.IsNotNull(vs.WaitForItem("HelloWorld", "Search Paths"));
                Assert.IsNotNull(vs.WaitForItem("HelloWorld", "server.py"));
                var view = vs.OpenItem("HelloWorld", "server.py");

                var bi = PythonTextBufferInfo.TryGetForBuffer(view.TextView.TextBuffer);
                for (int retries = 20; retries > 0 && bi.AnalysisEntry == null; --retries) {
                    Thread.Sleep(500);
                }

                view.Invoke(() => view.Type("import"));
                view.Invoke(() => view.Type(" "));

                using (var sh = view.WaitForSession<ICompletionSession>()) {
                    AssertUtil.Contains(sh.Session.Completions(), "sys");
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void CutRenamePaste() {
            var testDef = Generator.Project("DragDropCopyCutPaste",
                ProjectGenerator.ItemGroup(
                    ProjectGenerator.Folder("CutRenamePaste"),
                    ProjectGenerator.Compile("CutRenamePaste\\CutRenamePaste")
                )
            );

            using (var solution = testDef.Generate().ToMockVs()) {
                var project = solution.WaitForItem("DragDropCopyCutPaste");
                var file = solution.WaitForItem("DragDropCopyCutPaste", "CutRenamePaste", $"CutRenamePaste{testDef.ProjectType.CodeExtension}");

                file.Select();
                solution.ControlX();

                file.Select();
                solution.Type(Key.F2);
                solution.Type("CutRenamePasteNewName");
                solution.Type(Key.Enter);

                solution.Sleep(1000);
                project.Select();
                solution.ControlV();

                solution.CheckMessageBox($"The source URL 'CutRenamePaste{testDef.ProjectType.CodeExtension}' could not be found.");
            }
        }

        [TestMethod, Priority(UnitTestPriority.P2_FAILING)]
        [TestCategory("Installed")]
        public void ShouldWarnOnRun() {
            var sln = Generator.Project(
                "HelloWorld",
                ProjectGenerator.Compile("app", "print \"hello\"")
            ).Generate();

            using (var vs = sln.ToMockVs())
            using (var analyzerChanged = new AutoResetEvent(false)) {
                var project = vs.GetProject("HelloWorld").GetPythonProject();
                project.ProjectAnalyzerChanged += (s, e) => analyzerChanged.SetIfNotDisposed();

                var uiThread = (UIThreadBase)project.GetService(typeof(UIThreadBase));
                var interpreters = ((IComponentModel)project.GetService(typeof(SComponentModel)))
                    .GetService<IInterpreterRegistryService>()
                    .Interpreters;

                var v27 = interpreters.Where(x => x.Configuration.Id == "Global|PythonCore|2.7-32").First();
                var v34 = interpreters.Where(x => x.Configuration.Id == "Global|PythonCore|3.4-32").First();
                var interpOptions = (UIThreadBase)project.GetService(typeof(IComponentModel));

                uiThread.Invoke(() => {
                    project.AddInterpreter(v27.Configuration.Id);
                    project.AddInterpreter(v34.Configuration.Id);
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

        [TestMethod, Priority(UnitTestPriority.P2_FAILING)]
        [TestCategory("Installed")] // Requires .targets file to be installed
        public void OAProjectMustBeRightType() {
            var sln = Generator.Project(
                "HelloWorld",
                ProjectGenerator.Compile("server", "")
            ).Generate();

            using (var vs = sln.ToMockVs()) {
                var proj = vs.GetProject("HelloWorld");
                Assert.IsNotNull(proj);
                Assert.IsInstanceOfType(proj, typeof(OAProject));
                Assert.IsInstanceOfType(proj, typeof(IOleCommandTarget));
                Assert.IsInstanceOfType(proj.Object, typeof(OAVSProject));
                Assert.IsInstanceOfType(((OAProject)proj).Project, typeof(PythonProjectNode));
            }
        }
    }
}
