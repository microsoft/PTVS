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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Input;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreters;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using Keyboard = TestUtilities.UI.Keyboard;
using Path = System.IO.Path;

namespace PythonToolsUITests {
    [TestClass]
    public class VirtualEnvTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }

        internal static AutomationWrapper CreateVirtualEnvironment(VisualStudioApp app, out string envName) {
            string dummy;
            return CreateVirtualEnvironment(app, out envName, out dummy);
        }

        internal static AutomationWrapper CreateVirtualEnvironment(VisualStudioApp app, out string envName, out string envPath) {
            app.OpenSolutionExplorer();
            var virtualEnv = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Python Environments");
            AutomationWrapper.Select(virtualEnv);

            var createVenv = new AutomationWrapper(AutomationElement.FromHandle(
                app.OpenDialogWithDteExecuteCommand("Project.AddVirtualEnvironment")));

            envPath = new TextBox(createVenv.FindByAutomationId("VirtualEnvPath")).GetValue();
            var baseInterp = new ComboBox(createVenv.FindByAutomationId("BaseInterpreter")).GetSelectedItemText();

            envName = string.Format("{0} ({1})", envPath, baseInterp);

            Console.WriteLine("Expecting environment named: {0}", envName);

            createVenv.ClickButtonByAutomationId("Create");
            app.WaitForDialogDismissed();

            AutomationElement env = null;
            for (int i = 0; i < 6 && env == null; i++) {
                env = app.SolutionExplorerTreeView.WaitForItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    "Python Environments",
                    envName);
            }
            Assert.IsNotNull(env);
            return new AutomationWrapper(env);
        }


        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void InstallUninstallPackage() {
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

            string envName;
            var env = CreateVirtualEnvironment(app, out envName);
            env.Select();

            var installPackage = new AutomationWrapper(AutomationElement.FromHandle(
                app.OpenDialogWithDteExecuteCommand("Project.InstallPythonPackage")));

            var packageName = new TextBox(installPackage.FindByAutomationId("Name"));
            packageName.SetValue("azure==0.6.2");
            installPackage.ClickButtonByAutomationId("Ok");
            app.WaitForDialogDismissed();

            var azure = app.SolutionExplorerTreeView.WaitForItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Python Environments",
                envName,
                "azure (0.6.2)");

            Assert.IsNotNull(azure);
            AutomationWrapper.Select(azure);

            var confirmation = new AutomationWrapper(AutomationElement.FromHandle(
                app.OpenDialogWithDteExecuteCommand("Edit.Delete")));
            confirmation.ClickButtonByName("OK");

            app.SolutionExplorerTreeView.WaitForItemRemoved(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Python Environments",
                envName,
                "azure (0.6.2)");
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

            string envName;
            var env = CreateVirtualEnvironment(app, out envName);

            var solution = VsIdeTestHostContext.Dte.Solution.FullName;
            VsIdeTestHostContext.Dte.Solution.Close(true);

            VsIdeTestHostContext.Dte.Solution.Open(solution);

            AutomationElement env2 = null;
            for (int i = 0; i < 6 && env2 == null; i++) {
                env2 = app.SolutionExplorerTreeView.WaitForItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    "Python Environments",
                    envName);
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
            var project = app.Dte.Solution.Projects.Item(1);

            Assert.AreNotEqual(null, project.ProjectItems.Item(Path.GetFileNameWithoutExtension(app.Dte.Solution.FullName) + ".py"));

            var id0 = Guid.Parse((string)project.Properties.Item("InterpreterId").Value);

            string envName1, envName2;
            var env1 = CreateVirtualEnvironment(app, out envName1);
            var env2 = CreateVirtualEnvironment(app, out envName2);

            var id1 = Guid.Parse((string)project.Properties.Item("InterpreterId").Value);
            Assert.AreNotEqual(id0, id1);

            env1.Select();
            try {
                app.Dte.ExecuteCommand("Project.ActivateEnvironment");
                Assert.Fail("First env should already be active");
            } catch (COMException) {
            }

            env2.Select();
            app.Dte.ExecuteCommand("Project.ActivateEnvironment");

            var id2 = Guid.Parse((string)project.Properties.Item("InterpreterId").Value);
            Assert.AreNotEqual(id0, id2);
            Assert.AreNotEqual(id1, id2);

            var virtualEnv = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Python Environments");
            AutomationWrapper.Select(virtualEnv);

            env1 = new AutomationWrapper(app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Python Environments",
                envName1));
            env1.Select();
            System.Threading.Thread.Sleep(1000);
            app.Dte.ExecuteCommand("Project.ActivateEnvironment");

            var id1b = Guid.Parse((string)project.Properties.Item("InterpreterId").Value);
            Assert.AreEqual(id1, id1b);
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

            string envName, envPath;
            var env = CreateVirtualEnvironment(app, out envName, out envPath);

            env.Select();

            var removeDeleteDlg = new AutomationWrapper(AutomationElement.FromHandle(
                app.OpenDialogWithDteExecuteCommand("Edit.Delete")));
            removeDeleteDlg.ClickButtonByName("Remove");
            app.WaitForDialogDismissed();

            app.SolutionExplorerTreeView.WaitForItemRemoved(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Python Environments",
                envName);

            var projectHome = (string)app.Dte.Solution.Projects.Item(1).Properties.Item("ProjectHome").Value;
            envPath = Path.Combine(projectHome, envPath);
            Assert.IsTrue(Directory.Exists(envPath), envPath);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DeleteVirtualEnv() {
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

            string envName, envPath;
            var env = CreateVirtualEnvironment(app, out envName, out envPath);

            env.Select();

            // Need to wait for analysis to complete before deleting - otherwise
            // it will always fail.
            for (int retries = 120;
                Process.GetProcessesByName("Microsoft.PythonTools.Analyzer").Any() && retries > 0;
                --retries) {
                Thread.Sleep(1000);
            }
            // Need to wait some more for the database to be loaded.
            Thread.Sleep(5000);

            var removeDeleteDlg = new AutomationWrapper(AutomationElement.FromHandle(
                app.OpenDialogWithDteExecuteCommand("Edit.Delete")));
            removeDeleteDlg.ClickButtonByName("Delete");
            app.WaitForDialogDismissed();

            app.SolutionExplorerTreeView.WaitForItemRemoved(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Python Environments",
                envName);

            var projectHome = (string)app.Dte.Solution.Projects.Item(1).Properties.Item("ProjectHome").Value;
            envPath = Path.Combine(projectHome, envPath);
            for (int retries = 10;
                Directory.Exists(envPath) && retries > 0;
                --retries) {
                Thread.Sleep(1000);
            }
            Assert.IsFalse(Directory.Exists(envPath), envPath);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DefaultBaseInterpreterSelection() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenProject(TestData.GetPath("TestData\\Environments\\With27And33.pyproj"));

            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(1, app.Dte.Solution.Projects.Count);

            var env = new AutomationWrapper(app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Python Environments",
                "Python 2.7"));
            env.Select();
            app.Dte.ExecuteCommand("Project.ActivateEnvironment");

            app.OpenSolutionExplorer();
            var virtualEnv = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Python Environments");
            AutomationWrapper.Select(virtualEnv);

            var createVenv = new AutomationWrapper(AutomationElement.FromHandle(
                app.OpenDialogWithDteExecuteCommand("Project.AddVirtualEnvironment")));

            AutomationWrapper.DumpElement(createVenv.Element);
            var baseInterp = new ComboBox(createVenv.FindByAutomationId("BaseInterpreter")).GetSelectedItemText();

            Assert.AreEqual("Python 2.7", baseInterp);
            createVenv.ClickButtonByAutomationId("Cancel");

            app.OpenSolutionExplorer();
            env = new AutomationWrapper(app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Python Environments",
                "Python 3.3"));
            env.Select();
            app.Dte.ExecuteCommand("Project.ActivateEnvironment");

            app.OpenSolutionExplorer();
            virtualEnv = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                "Python Environments");
            AutomationWrapper.Select(virtualEnv);

            createVenv = new AutomationWrapper(AutomationElement.FromHandle(
                app.OpenDialogWithDteExecuteCommand("Project.AddVirtualEnvironment")));

            baseInterp = new ComboBox(createVenv.FindByAutomationId("BaseInterpreter")).GetSelectedItemText();

            Assert.AreEqual("Python 3.3", baseInterp);
            createVenv.ClickButtonByAutomationId("Cancel");
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void NoGlobalSitePackages() {
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

            string envName, envPath;
            var env = CreateVirtualEnvironment(app, out envName, out envPath);

            env.Select();

            // Need to wait for analysis to complete before deleting - otherwise
            // it will always fail.
            for (int retries = 120;
                Process.GetProcessesByName("Microsoft.PythonTools.Analyzer").Any() && retries > 0;
                --retries) {
                Thread.Sleep(1000);
            }
            // Need to wait some more for the database to be loaded.
            Thread.Sleep(5000);

            // Ensure virtualenv is NOT available in the virtual environment.
            var pyProj = app.Dte.Solution.Projects.Item(1).GetPythonProject();
            var interp = pyProj.GetInterpreter();
            
            Assert.IsNull(interp.ImportModule("virtualenv"));
        }
    }
}
