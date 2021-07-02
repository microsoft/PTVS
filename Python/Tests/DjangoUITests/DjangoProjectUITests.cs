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
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using TestUtilities.UI.Python.Django;
using PythonConstants = Microsoft.PythonTools.PythonConstants;

namespace DjangoUITests {
    public class DjangoProjectUITests {
        public void NewDjangoProject(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            var project = app.CreateProject(
                PythonVisualStudioApp.TemplateLanguageName,
                PythonVisualStudioApp.DjangoWebProjectTemplate,
                TestData.GetTempPath(),
                "NewDjangoProject"
            );
            var folder = project.ProjectItems.Item(project.Name);
            Assert.IsNotNull(project.ProjectItems.Item("manage.py"));
            Assert.IsNotNull(folder.ProjectItems.Item("settings.py"));
            Assert.IsNotNull(folder.ProjectItems.Item("urls.py"));
            Assert.IsNotNull(folder.ProjectItems.Item("__init__.py"));
            Assert.IsNotNull(folder.ProjectItems.Item("wsgi.py"));
        }

        public void NewDjangoProjectSafeProjectName(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            var project = app.CreateProject(
                PythonVisualStudioApp.TemplateLanguageName,
                PythonVisualStudioApp.DjangoWebProjectTemplate,
                TestData.GetTempPath(),
                "Django Project $100"
            );

            var folder = project.ProjectItems.Item("Django_Project__100");
            Assert.IsNotNull(project.ProjectItems.Item("manage.py"));
            Assert.IsNotNull(folder.ProjectItems.Item("settings.py"));
            Assert.IsNotNull(folder.ProjectItems.Item("urls.py"));
            Assert.IsNotNull(folder.ProjectItems.Item("__init__.py"));
            Assert.IsNotNull(folder.ProjectItems.Item("wsgi.py"));
            var settings = app.ServiceProvider.GetUIThread().Invoke(() => project.GetPythonProject().GetProperty("DjangoSettingsModule"));
            Assert.AreEqual("Django_Project__100.settings", settings);
        }

        public void DjangoCollectStaticFilesCommand(PythonVisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            var sln = app.CopyProjectForTest(@"TestData\DjangoApplication.sln");
            var project = app.OpenProject(sln);
            app.SolutionExplorerTreeView.SelectProject(project);

            app.Dte.ExecuteCommand("Project.CollectStaticFiles");

            using (var console = app.GetInteractiveWindow("Django Management Console - " + project.Name)) {
                Assert.IsNotNull(console);

                console.WaitForTextEnd("The interactive Python process has exited.", ">");

                Assert.IsTrue(console.TextView.TextSnapshot.GetText().Contains("0 static files copied"));
            }
        }

        public void DjangoShellCommand(PythonVisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            // Open a project that has some models defined, and make sure they can be imported without errors
            var sln = app.CopyProjectForTest(@"TestData\DjangoAnalysisTestApp.sln");
            var project = app.OpenProject(sln);
            app.SolutionExplorerTreeView.SelectProject(project);

            app.Dte.ExecuteCommand("Project.OpenDjangoShell");

            using (var console = app.GetInteractiveWindow("Django Management Console - " + project.Name)) {
                Assert.IsNotNull(console);

                bool started = false;
                for (int i = 0; i < 20; i++) {
                    if (console.TextView.TextSnapshot.GetText().Contains("Starting Django")) {
                        started = true;
                        break;
                    }
                    Thread.Sleep(250);
                }

                Assert.IsTrue(started, "Did not see 'Starting Django <ver> shell' message");

                console.WaitForTextEnd(">");
                console.SubmitCode("import myapp.models");
                console.WaitForTextEnd(">import myapp.models", ">");
            }
        }

        public void DjangoCommandsNonDjangoApp(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            var project = app.CreateProject(
                PythonVisualStudioApp.TemplateLanguageName,
                PythonVisualStudioApp.PythonApplicationTemplate,
                TestData.GetTempPath(),
                "DjangoCommandsNoDjangoApp"
            );
            app.SolutionExplorerTreeView.SelectProject(project);

            try {
                app.Dte.ExecuteCommand("Project.ValidateDjangoApp");
                Assert.Fail("Expected COMException");
            } catch (COMException e) {
                // requires a Django project
                Assert.IsTrue(e.Message.Contains("is not valid"), e.ToString());
            }

            try {
                app.Dte.ExecuteCommand("Project.DjangoSyncDB");
                Assert.Fail("Expected COMException");
            } catch (COMException e) {
                // requires a Django project
                Assert.IsTrue(e.Message.Contains("is not valid"), e.ToString());
            }
        }

        public void StartNewApp(PythonVisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            var project = app.CreateProject(
                PythonVisualStudioApp.TemplateLanguageName,
                PythonVisualStudioApp.DjangoWebProjectTemplate,
                TestData.GetTempPath(),
                "StartNewApp"
            );
            app.SolutionExplorerTreeView.SelectProject(project);

            using (var newAppDialog = NewAppDialog.FromDte(app)) {
                newAppDialog.AppName = "Fob";
                newAppDialog.OK();
            }

            app.SolutionExplorerTreeView.WaitForItem(
                app.Dte.Solution.FullName,
                app.Dte.Solution.Projects.Item(1).Name,
                "Fob",
                "models.py"
            );

            var appFolder = project.ProjectItems.Item("Fob");
            Assert.IsNotNull(appFolder.ProjectItems.Item("models.py"));
            Assert.IsNotNull(appFolder.ProjectItems.Item("tests.py"));
            Assert.IsNotNull(appFolder.ProjectItems.Item("views.py"));
            Assert.IsNotNull(appFolder.ProjectItems.Item("__init__.py"));

            var templatesFolder = appFolder.ProjectItems.Item("templates");
            var templatesAppFolder = templatesFolder.ProjectItems.Item("Fob");
            Assert.IsNotNull(templatesAppFolder.ProjectItems.Item("index.html"));

            app.SolutionExplorerTreeView.SelectProject(project);
            app.Dte.ExecuteCommand("Project.DjangoCheckDjango17");

            using (var console = app.GetInteractiveWindow("Django Management Console - " + project.Name)) {
                Assert.IsNotNull(console);
                console.WaitForTextEnd("The interactive Python process has exited.", ">");

                var consoleText = console.TextView.TextSnapshot.GetText();
                AssertUtil.Contains(consoleText, "Executing manage.py check");
                AssertUtil.Contains(consoleText, "System check identified no issues (0 silenced).");
            }

            app.SolutionExplorerTreeView.SelectProject(project);

            using (var newItem = NewItemDialog.FromDte(app)) {
                var htmlPage = newItem.ProjectTypes.FindItem("HTML Page");
                htmlPage.Select();
                newItem.FileName = "NewPage.html";
                newItem.OK();
            }

            System.Threading.Thread.Sleep(1000);

            Assert.IsNotNull(project.ProjectItems.Item("NewPage.html"));
        }

        public void StartNewAppDuplicateName(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            var project = app.CreateProject(
                PythonVisualStudioApp.TemplateLanguageName,
                PythonVisualStudioApp.DjangoWebProjectTemplate,
                TestData.GetTempPath(),
                "StartNewAppDuplicateName"
            );
            app.SolutionExplorerTreeView.SelectProject(project);

            using (var newAppDialog = NewAppDialog.FromDte(app)) {
                newAppDialog.AppName = "Fob";
                newAppDialog.OK();
            }

            app.SolutionExplorerTreeView.WaitForItem(
                app.Dte.Solution.FullName,
                app.Dte.Solution.Projects.Item(1).Name,
                "Fob",
                "models.py"
            );

            app.Dte.Documents.CloseAll(EnvDTE.vsSaveChanges.vsSaveChangesNo);

            app.SolutionExplorerTreeView.SelectProject(project);
            using (var newAppDialog = NewAppDialog.FromDte(app)) {
                newAppDialog.AppName = "Fob";
                newAppDialog.OK();
            }

            using (var dlg = AutomationDialog.WaitForDialog(app)) { }
        }

        public void StartNewAppSameAsProjectName(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            var project = app.CreateProject(
                PythonVisualStudioApp.TemplateLanguageName,
                PythonVisualStudioApp.DjangoWebProjectTemplate,
                TestData.GetTempPath(),
                "StartNewAppSameAsProjectName"
            );
            app.SolutionExplorerTreeView.SelectProject(project);

            using (var newAppDialog = NewAppDialog.FromDte(app)) {
                newAppDialog.AppName = app.Dte.Solution.Projects.Item(1).Name;
                newAppDialog.OK();
            }

            using (var dlg = AutomationDialog.WaitForDialog(app)) { }
        }

        public void DebugProjectProperties(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            var project = app.CreateProject(
                PythonVisualStudioApp.TemplateLanguageName,
                PythonVisualStudioApp.DjangoWebProjectTemplate,
                TestData.GetTempPath(),
                "DebugProjectProperties"
            );
            app.SolutionExplorerTreeView.SelectProject(project);

            app.Dte.ExecuteCommand("Project.Properties");
            var window = app.Dte.Windows.OfType<EnvDTE.Window>().FirstOrDefault(w => w.Caption == project.Name);
            Assert.IsNotNull(window);

            window.Activate();
            var hwnd = window.HWnd;
            var projProps = new ProjectPropertiesWindow(new IntPtr(hwnd));

            // FYI This is broken on Dev15 (15.0 up to latest build as of now 15.3 build 26507)
            // Active page can't be changed via UI automation.
            // Bug 433488 has been filed.
            // - InvokePattern is not available
            // - SelectionItemPattern is available (according to Inspect) but does not work
            // - Default action does nothing
            var debugPage = projProps[new Guid(PythonConstants.DebugPropertyPageGuid)];
            Assert.IsNotNull(debugPage);

            var dbgProps = new PythonProjectDebugProperties(debugPage);
            Assert.AreEqual("Django Web launcher", dbgProps.LaunchMode);
            dbgProps.AssertMatchesProject(project.GetPythonProject());
        }

        public void DjangoProjectWithSubdirectory(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            var sln = app.CopyProjectForTest(@"TestData\DjangoProjectWithSubDirectory.sln");
            var slnDir = PathUtils.GetParent(sln);
            var project = app.OpenProject(sln);

            var pyProj = project.GetPythonProject();
            var dsm = pyProj.Site.GetUIThread().Invoke(() => pyProj.GetProperty("DjangoSettingsModule"));
            Assert.AreEqual("config.settings", dsm);
            var workDir = pyProj.Site.GetUIThread().Invoke(() => pyProj.GetWorkingDirectory()).TrimEnd('\\');
            Assert.AreEqual(Path.Combine(slnDir, "DjangoProjectWithSubDirectory", "project"), workDir, true);

            var cmd = pyProj.FindCommand("DjangoCollectStaticCommand");

            pyProj.Site.GetUIThread().Invoke(() => {
                Assert.IsTrue(cmd.CanExecute(pyProj), "Cannot execute DjangoCollectStaticCommand");
                cmd.Execute(pyProj);
            });

            // The static dir is 'test_static', check that the admin files
            // are copied into there.
            Assert.IsTrue(Directory.Exists(Path.Combine(workDir, "test_static", "admin")), "admin static directory was not created");
            Assert.IsTrue(File.Exists(Path.Combine(workDir, "test_static", "admin", "css", "base.css")), "admin static files were not copied");
        }
    }
}
