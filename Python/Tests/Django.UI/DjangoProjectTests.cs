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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Django;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using TestUtilities.UI.Python.Django;
using PythonConstants = Microsoft.PythonTools.PythonConstants;

namespace DjangoUITests {
    [TestClass]
    public class DjangoProjectTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void NewDjangoProject() {
            using (var app = new VisualStudioApp()) {
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
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void NewDjangoProjectSafeProjectName() {
            using (var app = new VisualStudioApp()) {
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
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void DjangoCollectStaticFilesCommand() {
            using (var app = new PythonVisualStudioApp()) {
                var service = app.GetService<IComponentModel>(typeof(SComponentModel)).GetService<IInterpreterOptionsService>();

                var envWithDjango = service.Interpreters.LastOrDefault(env => env.FindModules("django").Contains("django"));
                if (envWithDjango == null) {
                    Assert.Inconclusive("No available environments have Django installed");
                }

                using (var dis = new DefaultInterpreterSetter(envWithDjango)) {
                    var project = app.OpenProject("TestData\\DjangoApplication1\\DjangoApplication1.sln");
                    app.SolutionExplorerTreeView.SelectProject(project);

                    app.Dte.ExecuteCommand("Project.CollectStaticFiles");

                    var console = app.GetInteractiveWindow("Django Management Console - " + project.Name);
                    Assert.IsNotNull(console);

                    console.WaitForTextEnd("The Python REPL process has exited", ">>> ");

                    Assert.IsTrue(console.Text.Contains("0 static files copied"));
                }
            }
        }


        /// <summary>
        /// http://pytools.codeplex.com/workitem/778
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void DjangoCommandsNonDjangoApp() {
            using (var app = new PythonVisualStudioApp()) {
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
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void StartNewApp() {
            using (var app = new PythonVisualStudioApp()) {
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
                Assert.IsNotNull(appFolder.Collection.Item("models.py"));
                Assert.IsNotNull(appFolder.Collection.Item("tests.py"));
                Assert.IsNotNull(appFolder.Collection.Item("views.py"));
                Assert.IsNotNull(appFolder.Collection.Item("__init__.py"));

                app.SolutionExplorerTreeView.SelectProject(project);
                app.Dte.ExecuteCommand("Project.ValidateDjangoApp");

                var console = app.GetInteractiveWindow("Django Management Console - " + project.Name);
                Assert.IsNotNull(console);
                console.WaitForTextEnd("Executing manage.py validate", "0 errors found", "The Python REPL process has exited", ">>> ");

                app.SolutionExplorerTreeView.SelectProject(project);

                using (var newItem = NewItemDialog.FromDte(app)) {
                    AutomationWrapper.Select(newItem.ProjectTypes.FindItem("HTML Page"));
                    newItem.FileName = "NewPage.html";
                    newItem.OK();
                }

                System.Threading.Thread.Sleep(1000);

                Assert.IsNotNull(project.ProjectItems.Item("NewPage.html"));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void StartNewAppDuplicateName() {
            using (var app = new VisualStudioApp()) {
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
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/748
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void StartNewAppSameAsProjectName() {
            using (var app = new VisualStudioApp()) {
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
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void DebugProjectProperties() {
            using (var app = new PythonVisualStudioApp()) {
                var project = app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    PythonVisualStudioApp.DjangoWebProjectTemplate,
                    TestData.GetTempPath(),
                    "DebugProjectProperties"
                );
                app.SolutionExplorerTreeView.SelectProject(project);

                app.Dte.ExecuteCommand("ClassViewContextMenus.ClassViewMultiselectProjectreferencesItems.Properties");
                var window = app.Dte.Windows.OfType<EnvDTE.Window>().FirstOrDefault(w => w.Caption == project.Name);
                Assert.IsNotNull(window);

                window.Activate();
                var hwnd = window.HWnd;
                var projProps = new ProjectPropertiesWindow(new IntPtr(hwnd));

                var debugPage = projProps[new Guid(PythonConstants.DebugPropertyPageGuid)];
                Assert.IsNotNull(debugPage);

                var dbgProps = new PythonProjectDebugProperties(debugPage);
                Assert.AreEqual("Django Web launcher", dbgProps.LaunchMode);
                dbgProps.AssertMatchesProject(project.GetPythonProject());
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void DjangoProjectWithSubdirectory() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject("TestData\\DjangoProjectWithSubDirectory.sln");

                var pyProj = (IPythonProject2)project.GetPythonProject();
                var dsm = pyProj.Site.GetUIThread().Invoke(() => pyProj.GetProperty("DjangoSettingsModule"));
                Assert.AreEqual("config.settings", dsm);
                var workDir = pyProj.Site.GetUIThread().Invoke(() => pyProj.GetWorkingDirectory()).TrimEnd('\\');
                Assert.AreEqual(TestData.GetPath("TestData\\DjangoProjectWithSubDirectory\\project"), workDir, true);

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
}
