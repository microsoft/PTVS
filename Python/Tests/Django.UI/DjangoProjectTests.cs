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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python.Django;

namespace DjangoUITests {
    [TestClass]
    public class DjangoProjectTests {
        private const string AddDjangoAppCmd = "ProjectandSolutionContextMenus.Project.Add.AddNewDjangoapp";

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void NewDjangoProject() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();

            newProjDialog.FocusLanguageNode();
            var djangoApp = newProjDialog.ProjectTypes.FindItem("Django Application");
            djangoApp.Select();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(1, app.Dte.Solution.Projects.Count);
            var curProj = app.Dte.Solution.Projects.Item(1);
            var folder = curProj.ProjectItems.Item(curProj.Name);
            Assert.AreNotEqual(null, curProj.ProjectItems.Item("manage.py"));
            Assert.AreNotEqual(null, folder.ProjectItems.Item("settings.py"));
            Assert.AreNotEqual(null, folder.ProjectItems.Item("urls.py"));
            Assert.AreNotEqual(null, folder.ProjectItems.Item("__init__.py"));
            Assert.AreNotEqual(null, folder.ProjectItems.Item("wsgi.py"));
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/778
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DjangoCommandsNonDjangoApp() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();

            newProjDialog.FocusLanguageNode();

            var djangoApp = newProjDialog.ProjectTypes.FindItem("Python Application");
            djangoApp.Select();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            try {
                app.Dte.ExecuteCommand("ClassViewContextMenus.ClassViewProject.ValidateDjangoApp");
            } catch(COMException e) {
                // requires a Django project
                Assert.IsTrue(e.ToString().Contains("is not available"));
            }

            try {
                app.Dte.ExecuteCommand("ClassViewContextMenus.ClassViewProject.ValidateDjangoApp");
            } catch (COMException e) {
                // requires a Django project
                Assert.IsTrue(e.ToString().Contains("is not available"));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void StartNewApp() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();

            newProjDialog.FocusLanguageNode();

            var djangoApp = newProjDialog.ProjectTypes.FindItem("Django Application");
            djangoApp.Select();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            var projItem = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name
            );
            AutomationWrapper.Select(projItem);
            System.Threading.Thread.Sleep(1000);
            ThreadPool.QueueUserWorkItem(x => {
                try {
                    app.Dte.ExecuteCommand(AddDjangoAppCmd);
                } catch(Exception e) {
                    Debug.WriteLine("Failed to add new app: {0}", e);
                }
            });

            var newAppDialog = new NewAppDialog(app.WaitForDialog());

            newAppDialog.AppName = "Foo";
            newAppDialog.Ok();

            app.SolutionExplorerTreeView.WaitForItem(
                app.Dte.Solution.FullName,
                app.Dte.Solution.Projects.Item(1).Name,
                "Foo",
                "models.py"
            );

            var appFolder = app.Dte.Solution.Projects.Item(1).ProjectItems.Item("Foo");
            Assert.AreNotEqual(null, appFolder.Collection.Item("models.py"));
            Assert.AreNotEqual(null, appFolder.Collection.Item("tests.py"));
            Assert.AreNotEqual(null, appFolder.Collection.Item("views.py"));
            Assert.AreNotEqual(null, appFolder.Collection.Item("__init__.py"));


            projItem = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name
            );
            AutomationWrapper.Select(projItem);
            System.Threading.Thread.Sleep(1000);
            ThreadPool.QueueUserWorkItem(x => {
                try {
                    app.Dte.ExecuteCommand("ClassViewContextMenus.ClassViewProject.ValidateDjangoApp");
                } catch(Exception e) {
                    Debug.WriteLine("Failed to execute command: {0}", e);
                }
            });

            var dlg = new NewAppDialog(app.WaitForDialog());
            for (int i = 0; i < 100; i++) {
                try {
                    dlg.Ok();
                    break;
                } catch (ElementNotEnabledException) {
                    // wait for app to be enabled.
                    Thread.Sleep(1000);
                }
            }

            projItem = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name
            );
            AutomationWrapper.Select(projItem);
            System.Threading.Thread.Sleep(1000);

            ThreadPool.QueueUserWorkItem(x => {
                try {
                    app.Dte.ExecuteCommand("Project.AddNewItem");
                } catch(Exception e) {
                    Debug.WriteLine("Couldn't execute command: {0}", e);
                }
            });

            var newItem = new NewItemDialog(AutomationElement.FromHandle(app.WaitForDialog()));
            newItem.FileName = "NewPage.html";
            newItem.ClickOK();
            
            System.Threading.Thread.Sleep(1000);

            var solutionFolder = app.Dte.Solution.Projects.Item(1).ProjectItems;
            Assert.AreNotEqual(null, solutionFolder.Item("NewPage.html"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void StartNewAppDuplicateName() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();

            newProjDialog.FocusLanguageNode();

            var djangoApp = newProjDialog.ProjectTypes.FindItem("Django Application");
            djangoApp.Select();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            var projItem = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name
            );
            AutomationWrapper.Select(projItem);
            System.Threading.Thread.Sleep(1000);

            ThreadPool.QueueUserWorkItem(x => {
                try {
                    app.Dte.ExecuteCommand(AddDjangoAppCmd);
                } catch(Exception e) {
                    Debug.WriteLine("Failed to add new app: {0}", e);
                }
            });

            var newAppDialog = new NewAppDialog(app.WaitForDialog());

            newAppDialog.AppName = "Foo";
            newAppDialog.Ok();

            app.SolutionExplorerTreeView.WaitForItem(
                app.Dte.Solution.FullName,
                app.Dte.Solution.Projects.Item(1).Name,
                "Foo",
                "models.py"
            );

            AutomationWrapper.Select(projItem);
            System.Threading.Thread.Sleep(1000);
            ThreadPool.QueueUserWorkItem(x => {
                try {
                    app.Dte.ExecuteCommand(AddDjangoAppCmd);
                } catch {
                }
            });

            newAppDialog = new NewAppDialog(app.WaitForDialog());

            newAppDialog.AppName = "Foo";
            newAppDialog.Ok();            

            System.Threading.Thread.Sleep(1000);

            VisualStudioApp.CheckMessageBox(
                TestUtilities.UI.MessageBoxButton.Ok,
                "Folder already exists with the name 'Foo'"
            );
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/748
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void StartNewAppSameAsProjectName() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();

            newProjDialog.FocusLanguageNode();

            var djangoApp = newProjDialog.ProjectTypes.FindItem("Django Application");
            djangoApp.Select();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            var projItem = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name
            );
            AutomationWrapper.Select(projItem);
            System.Threading.Thread.Sleep(1000);

            ThreadPool.QueueUserWorkItem(x => {
                try {
                    app.Dte.ExecuteCommand(AddDjangoAppCmd);
                } catch(Exception e) {
                    Debug.WriteLine("Failed to add new app: {0}", e);
                }
            });

            var newAppDialog = new NewAppDialog(app.WaitForDialog());

            newAppDialog.AppName = app.Dte.Solution.Projects.Item(1).Name;
            newAppDialog.Ok();

            app.SolutionExplorerTreeView.WaitForItem(
                app.Dte.Solution.FullName,
                app.Dte.Solution.Projects.Item(1).Name,
                app.Dte.Solution.Projects.Item(1).Name,
                "models.py"
            );
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DebugProjectProperties() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();

            newProjDialog.FocusLanguageNode();

            var djangoApp = newProjDialog.ProjectTypes.FindItem("Django Application");
            djangoApp.Select();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            var projItem = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name
            );
            AutomationWrapper.Select(projItem);

            app.Dte.ExecuteCommand("ClassViewContextMenus.ClassViewMultiselectProjectreferencesItems.Properties");
            EnvDTE.Window window = null;
            for (int i = 1; i <= app.Dte.Windows.Count; i++) {
                var curWindow = app.Dte.Windows.Item(i);
                if (curWindow.Caption == app.Dte.Solution.Projects.Item(1).Name) {
                    Console.WriteLine("Found window!");
                    window = curWindow;
                    break;
                }
            }
            Assert.IsNotNull(window);
            window.Activate();
            var hwnd = window.HWnd;
            var projProps = new ProjectPropertiesWindow(AutomationElement.FromHandle(new IntPtr(hwnd)));
            var debugPage = projProps["Debug"];
            Assert.IsNotNull(debugPage);

            var dbgProps = new PythonProjectDebugProperties(debugPage);
            Assert.AreEqual("Django launcher", dbgProps.LaunchMode);
        }
    }
}
