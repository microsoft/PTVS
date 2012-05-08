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
// Ambiguous with EnvDTE.Thread.
using AnalysisTest.UI;
using AnalysisTest.UI.Python.Django;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using System.Diagnostics;

namespace AnalysisTest.Django {
    [TestClass]
    [DeploymentItem(@"Python.VS.TestData\", "Python.VS.TestData")]
    public class DjangoProjectTests {
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void NewDjangoProject() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();

            newProjDialog.FocusLanguageNode();

            var djangoApp = newProjDialog.ProjectTypes.FindItem("Django Application");
            djangoApp.SetFocus();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(1, app.Dte.Solution.Projects.Count);
            Assert.AreNotEqual(null, app.Dte.Solution.Projects.Item(1).ProjectItems.Item("manage.py"));
            Assert.AreNotEqual(null, app.Dte.Solution.Projects.Item(1).ProjectItems.Item("settings.py"));
            Assert.AreNotEqual(null, app.Dte.Solution.Projects.Item(1).ProjectItems.Item("urls.py"));
            Assert.AreNotEqual(null, app.Dte.Solution.Projects.Item(1).ProjectItems.Item("__init__.py"));
            Assert.AreNotEqual(null, app.Dte.Solution.Projects.Item(1).ProjectItems.Item("Templates"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void StartNewApp() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();

            newProjDialog.FocusLanguageNode();

            var djangoApp = newProjDialog.ProjectTypes.FindItem("Django Application");
            djangoApp.SetFocus();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            var projItem = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name
            );
            projItem.SetFocus();

            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("ProjectandSolutionContextMenus.Project.Add.AddnewDjangoapp"));

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
        }
    }
}
