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

extern alias analysis;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using analysis::Microsoft.VisualStudioTools;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using Path = System.IO.Path;

namespace PythonToolsUITests {
    [TestClass]
    public class VirtualEnvTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            TestData.Deploy();
        }

        private DefaultInterpreterSetter Init() {
            return Init(PythonPaths.Python27 ?? PythonPaths.Python27_x64, true);
        }

        public DefaultInterpreterSetter Init(PythonVersion interp, bool install) {
            interp.AssertInstalled();

            var sp = new ServiceProvider(VsIdeTestHostContext.Dte as Microsoft.VisualStudio.OLE.Interop.IServiceProvider);
            var model = (IComponentModel)sp.GetService(typeof(SComponentModel));
            var interpreterService = model.GetService<IInterpreterOptionsService>();
            var factory = interpreterService.FindInterpreter(interp.Interpreter, interp.Configuration.Version);
            var defaultInterpreterSetter = new DefaultInterpreterSetter(factory);

            if (install) {
                Pip.InstallPip(factory, false).Wait();
                VirtualEnv.Install(factory).Wait();
            }

            return defaultInterpreterSetter;
        }

        private static void CreateTemporaryProject(VisualStudioApp app) {
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
                SR.GetString(SR.Environments));
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
            app.OpenSolutionExplorer();
            for (int i = 0; i < 6 && env == null; i++) {
                env = app.SolutionExplorerTreeView.WaitForItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    SR.GetString(SR.Environments),
                    envName);
            }
            Assert.IsNotNull(env);
            return new AutomationWrapper(env);
        }

        internal static AutomationWrapper AddExistingVirtualEnvironment(VisualStudioApp app, string envPath, out string envName) {
            app.OpenSolutionExplorer();
            var virtualEnv = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name,
                SR.GetString(SR.Environments));
            AutomationWrapper.Select(virtualEnv);

            var createVenv = new AutomationWrapper(AutomationElement.FromHandle(
                app.OpenDialogWithDteExecuteCommand("Project.AddVirtualEnvironment")));

            new TextBox(createVenv.FindByAutomationId("VirtualEnvPath")).SetValue(envPath);
            var baseInterp = new ComboBox(createVenv.FindByAutomationId("BaseInterpreter")).GetSelectedItemText();

            envName = string.Format("{0} ({1})", Path.GetFileName(envPath), baseInterp);

            Console.WriteLine("Expecting environment named: {0}", envName);

            createVenv.ClickButtonByAutomationId("Add");
            app.WaitForDialogDismissed();

            AutomationElement env = null;
            for (int i = 0; i < 6 && env == null; i++) {
                env = app.SolutionExplorerTreeView.WaitForItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    SR.GetString(SR.Environments),
                    envName);
            }
            Assert.IsNotNull(env);
            return new AutomationWrapper(env);
        }


        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void InstallUninstallPackage() {
            using (var dis = Init())
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                CreateTemporaryProject(app);

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
                    SR.GetString(SR.Environments),
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
                    SR.GetString(SR.Environments),
                    envName,
                    "azure (0.6.2)");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LoadVirtualEnv() {
            using (var dis = Init())
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                CreateTemporaryProject(app);

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
                        SR.GetString(SR.Environments),
                        envName);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ActivateVirtualEnv() {
            using (var dis = Init())
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                CreateTemporaryProject(app);

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
                    SR.GetString(SR.Environments));
                AutomationWrapper.Select(virtualEnv);

                env1 = new AutomationWrapper(app.SolutionExplorerTreeView.FindItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    SR.GetString(SR.Environments),
                    envName1));
                env1.Select();
                System.Threading.Thread.Sleep(1000);
                app.Dte.ExecuteCommand("Project.ActivateEnvironment");

                var id1b = Guid.Parse((string)project.Properties.Item("InterpreterId").Value);
                Assert.AreEqual(id1, id1b);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RemoveVirtualEnv() {
            using (var dis = Init())
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                CreateTemporaryProject(app);

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
                    SR.GetString(SR.Environments),
                    envName);

                var projectHome = (string)app.Dte.Solution.Projects.Item(1).Properties.Item("ProjectHome").Value;
                envPath = Path.Combine(projectHome, envPath);
                Assert.IsTrue(Directory.Exists(envPath), envPath);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DeleteVirtualEnv() {
            using (var dis = Init())
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                CreateTemporaryProject(app);

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
                    SR.GetString(SR.Environments),
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
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DefaultBaseInterpreterSelection() {
            using (var dis = Init())
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenProject(TestData.GetPath("TestData\\Environments\\With27And33.pyproj"));

                for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                    System.Threading.Thread.Sleep(1000);
                }

                Assert.AreEqual(1, app.Dte.Solution.Projects.Count);

                var env = new AutomationWrapper(app.SolutionExplorerTreeView.FindItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    SR.GetString(SR.Environments),
                    "Python 2.7"));
                env.Select();
                app.Dte.ExecuteCommand("Project.ActivateEnvironment");

                app.OpenSolutionExplorer();
                var virtualEnv = app.SolutionExplorerTreeView.FindItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    SR.GetString(SR.Environments));
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
                    SR.GetString(SR.Environments),
                    "Python 3.3"));
                env.Select();
                app.Dte.ExecuteCommand("Project.ActivateEnvironment");

                app.OpenSolutionExplorer();
                virtualEnv = app.SolutionExplorerTreeView.FindItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    SR.GetString(SR.Environments));
                AutomationWrapper.Select(virtualEnv);

                createVenv = new AutomationWrapper(AutomationElement.FromHandle(
                    app.OpenDialogWithDteExecuteCommand("Project.AddVirtualEnvironment")));

                baseInterp = new ComboBox(createVenv.FindByAutomationId("BaseInterpreter")).GetSelectedItemText();

                Assert.AreEqual("Python 3.3", baseInterp);
                createVenv.ClickButtonByAutomationId("Cancel");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void NoGlobalSitePackages() {
            using (var dis = Init())
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                CreateTemporaryProject(app);

                string envName, envPath;
                var env = CreateVirtualEnvironment(app, out envName, out envPath);

                env.Select();

                // Need to wait for analysis to complete before checking database
                for (int retries = 120;
                    Process.GetProcessesByName("Microsoft.PythonTools.Analyzer").Any() && retries > 0;
                    --retries) {
                    Thread.Sleep(1000);
                }
                // Need to wait some more for the database to be loaded.
                Thread.Sleep(5000);

                // Ensure virtualenv_support is NOT available in the virtual environment.
                var pyProj = app.Dte.Solution.Projects.Item(1).GetPythonProject();
                var interp = pyProj.GetInterpreter();

                Assert.IsNull(interp.ImportModule("virtualenv_support"));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CreateVEnv() {
            using (var dis = Init(PythonPaths.Python33 ?? PythonPaths.Python33_x64, false)) {
                if (analysis::Microsoft.PythonTools.Interpreter.PythonInterpreterFactoryExtensions
                        .FindModules(dis.CurrentDefault, "virtualenv")
                        .Contains("virtualenv")) {
                    Assert.Inconclusive("Requires Python 3.3 without virtualenv");
                }

                using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                    CreateTemporaryProject(app);

                    string envName, envPath;

                    var env = CreateVirtualEnvironment(app, out envName, out envPath);
                    Assert.IsNotNull(env);
                    Assert.IsNotNull(env.Element);
                    Assert.AreEqual(string.Format("env (Python {0}3.3)",
                        dis.CurrentDefault.Configuration.Architecture == System.Reflection.ProcessorArchitecture.Amd64 ? "64-bit " : ""
                    ), envName);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingVEnv() {
            PythonPaths.Python33.AssertInstalled();
            if (!CommonUtils.IsSameDirectory("C:\\Python33", PythonPaths.Python33.PrefixPath)) {
                Assert.Inconclusive("Python 3.3 not configured correctly");
            }

            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                CreateTemporaryProject(app);

                string envName;
                var envPath = TestData.GetPath(@"TestData\\Environments\\venv");

                var env = AddExistingVirtualEnvironment(app, envPath, out envName);
                Assert.IsNotNull(env);
                Assert.IsNotNull(env.Element);
                Assert.AreEqual("venv (Python 3.3)", envName);
            }
        }
    }
}
