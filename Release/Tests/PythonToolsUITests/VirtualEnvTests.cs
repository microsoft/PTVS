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
using System.Threading;
using System.Windows.Automation;
using System.Windows.Input;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using Keyboard = TestUtilities.UI.Keyboard;
using Path = System.IO.Path;

namespace PythonToolsUITests {
    [TestClass]
    public class VirtualEnvTests {
        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CreateVirtualEnv() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();
            newProjDialog.Location = Path.GetTempPath();

            newProjDialog.FocusLanguageNode();

            var consoleApp = newProjDialog.ProjectTypes.FindItem("Python Application");
            consoleApp.Select();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(1, app.Dte.Solution.Projects.Count);

            Assert.AreNotEqual(null, app.Dte.Solution.Projects.Item(1).ProjectItems.Item(Path.GetFileNameWithoutExtension(app.Dte.Solution.FullName) + ".py"));

            app.OpenSolutionExplorer();
            var virtualEnv = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Virtual Environments");

            ((SelectionItemPattern)virtualEnv.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            System.Threading.Thread.Sleep(1000);
            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.CreateVirtualEnvironment"));

            app.WaitForDialog();

            Keyboard.Type(Key.Enter);
            app.WaitForDialogDismissed();

            AutomationElement env = null;
            for (int i = 0; i < 6 && env == null; i++) {
                env = app.SolutionExplorerTreeView.WaitForItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    "Virtual Environments",
                    "env");
            }

            Assert.IsNotNull(env);

            ((SelectionItemPattern)env.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            Thread.Sleep(1000);

            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.InstallPythonPackage"));

            app.WaitForDialog();
            Keyboard.Type("azure");
            Keyboard.Type(Key.Enter);

            app.WaitForDialogDismissed();

            var azure = app.SolutionExplorerTreeView.WaitForItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Virtual Environments",
                "env",
                "azure==0.6.1");

            Assert.IsNotNull(azure);
            ((SelectionItemPattern)azure.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            System.Threading.Thread.Sleep(1000);

            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.UninstallPythonPackage"));

            app.WaitForDialog();
            Keyboard.Type(Key.Enter);

            app.SolutionExplorerTreeView.WaitForItemRemoved(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Virtual Environments",
                "env",
                "azure==0.6.1");
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LoadVirtualEnv() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();
            newProjDialog.Location = Path.GetTempPath();

            newProjDialog.FocusLanguageNode();

            var consoleApp = newProjDialog.ProjectTypes.FindItem("Python Application");
            consoleApp.Select();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(1, app.Dte.Solution.Projects.Count);

            Assert.AreNotEqual(null, app.Dte.Solution.Projects.Item(1).ProjectItems.Item(Path.GetFileNameWithoutExtension(app.Dte.Solution.FullName) + ".py"));

            app.OpenSolutionExplorer();
            var virtualEnv = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Virtual Environments");

            ((SelectionItemPattern)virtualEnv.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            System.Threading.Thread.Sleep(1000);
            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.CreateVirtualEnvironment"));

            app.WaitForDialog();

            Keyboard.Type(Key.Enter);
            app.WaitForDialogDismissed();

            AutomationElement env = null;
            for (int i = 0; i < 6 && env == null; i++) {
                env = app.SolutionExplorerTreeView.WaitForItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    "Virtual Environments",
                    "env");
            }

            var solution = VsIdeTestHostContext.Dte.Solution.FullName;
            VsIdeTestHostContext.Dte.Solution.Close(true);

            VsIdeTestHostContext.Dte.Solution.Open(solution);

            for (int i = 0; i < 6 && env == null; i++) {
                env = app.SolutionExplorerTreeView.WaitForItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    "Virtual Environments",
                    "env");
            }            
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ActivateVirtualEnv() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();
            newProjDialog.Location = Path.GetTempPath();

            newProjDialog.FocusLanguageNode();

            var consoleApp = newProjDialog.ProjectTypes.FindItem("Python Application");
            consoleApp.Select();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(1, app.Dte.Solution.Projects.Count);

            Assert.AreNotEqual(null, app.Dte.Solution.Projects.Item(1).ProjectItems.Item(Path.GetFileNameWithoutExtension(app.Dte.Solution.FullName) + ".py"));

            app.OpenSolutionExplorer();
            var virtualEnv = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Virtual Environments");

            ((SelectionItemPattern)virtualEnv.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            System.Threading.Thread.Sleep(1000);
            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.CreateVirtualEnvironment"));

            app.WaitForDialog();

            Keyboard.Type(Key.Enter);
            app.WaitForDialogDismissed();

            AutomationElement env = null;
            for (int i = 0; i < 6 && env == null; i++) {
                env = app.SolutionExplorerTreeView.WaitForItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    "Virtual Environments",
                    "env");
            }

            Assert.IsNotNull(env);

            ((SelectionItemPattern)env.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            System.Threading.Thread.Sleep(1000);

            app.Dte.ExecuteCommand("Project.ActivateVirtualEnvironment");

            System.Threading.Thread.Sleep(1000);
            Assert.AreEqual(
                app.Dte.Solution.Projects.Item(1).Properties.Item("InterpreterPath").Value,
                Path.Combine(
                    Path.GetDirectoryName(app.Dte.Solution.Projects.Item(1).FullName),
                    "env",
                    "Scripts",
                    "python.exe"
                )
            );

            app.Dte.ExecuteCommand("Project.DeactivateVirtualEnvironment");

            System.Threading.Thread.Sleep(1000);

            Assert.AreEqual(
                "",
                app.Dte.Solution.Projects.Item(1).Properties.Item("InterpreterPath").Value
            );
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RemoveVirtualEnv() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();
            newProjDialog.Location = Path.GetTempPath();

            newProjDialog.FocusLanguageNode();

            var consoleApp = newProjDialog.ProjectTypes.FindItem("Python Application");
            consoleApp.Select();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(1, app.Dte.Solution.Projects.Count);

            Assert.AreNotEqual(null, app.Dte.Solution.Projects.Item(1).ProjectItems.Item(Path.GetFileNameWithoutExtension(app.Dte.Solution.FullName) + ".py"));

            app.OpenSolutionExplorer();
            var virtualEnv = app.SolutionExplorerTreeView.WaitForItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Virtual Environments");

            ((SelectionItemPattern)virtualEnv.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            System.Threading.Thread.Sleep(1000);
            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.CreateVirtualEnvironment"));

            app.WaitForDialog();

            Keyboard.Type(Key.Enter);
            app.WaitForDialogDismissed();

            AutomationElement env = null;
            for (int i = 0; i < 6 && env == null; i++) {
                env = app.SolutionExplorerTreeView.WaitForItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    "Virtual Environments",
                    "env");
            }

            Assert.IsNotNull(env);

            ((SelectionItemPattern)env.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            System.Threading.Thread.Sleep(1000);

            Keyboard.Type(Key.Delete);
            app.WaitForDialog();

            Keyboard.Type(Key.Enter);

            app.SolutionExplorerTreeView.WaitForItemRemoved(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Virtual Environments",
                "env");
        }
    }
}
