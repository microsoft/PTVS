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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

extern alias analysis;
extern alias util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using analysis::Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using Path = System.IO.Path;

namespace PythonToolsUITests {
    //[TestClass]
    public class VirtualEnvTests {
        const string TestPackageSpec = "ptvsd==2.2.0";
        const string TestPackageDisplay = "ptvsd (2.2.0)";

        public TestContext TestContext { get; set; }

        static DefaultInterpreterSetter Init(PythonVisualStudioApp app) {
            var dis = app.SelectDefaultInterpreter(PythonPaths.Python27 ?? PythonPaths.Python27_x64);
            try {
                dis.CurrentDefault.PipInstall("-U virtualenv");
                var r = dis;
                dis = null;
                return r;
            } finally {
                dis?.Dispose();
            }
        }

        static DefaultInterpreterSetter Init(PythonVisualStudioApp app, PythonVersion interp, bool install) {
            var dis = app.SelectDefaultInterpreter(interp);
            try {
                if (install) {
                    dis.CurrentDefault.PipInstall("-U virtualenv");
                }
                var r = dis;
                dis = null;
                return r;
            } finally {
                dis?.Dispose();
            }
        }

        static DefaultInterpreterSetter Init3(PythonVisualStudioApp app, bool installVirtualEnv = false) {
            var dis = app.SelectDefaultInterpreter(
                PythonPaths.Python35 ?? PythonPaths.Python35_x64 ??
                PythonPaths.Python34 ?? PythonPaths.Python34_x64 ??
                PythonPaths.Python33 ?? PythonPaths.Python33_x64
            );
            try {
                if (installVirtualEnv) {
                    dis.CurrentDefault.PipInstall("-U virtualenv");
                }
                var r = dis;
                dis = null;
                return r;
            } finally {
                dis?.Dispose();
            }
        }

        private EnvDTE.Project CreateTemporaryProject(VisualStudioApp app) {
            var project = app.CreateProject(
                PythonVisualStudioApp.TemplateLanguageName,
                PythonVisualStudioApp.PythonApplicationTemplate,
                TestData.GetTempPath(),
                TestContext.TestName
            );

            Assert.IsNotNull(project, "Project was not created");
            return project;
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void InstallUninstallPackage(PythonVisualStudioApp app) {
            using (var dis = Init(app)) {
                var project = CreateTemporaryProject(app);

                string envName;
                var env = app.CreateVirtualEnvironment(project, out envName);
                env.Select();

                app.ExecuteCommand("Python.InstallPackage", "/p:" + TestPackageSpec);

                var azure = app.SolutionExplorerTreeView.WaitForChildOfProject(
                    project,
                    Strings.Environments,
                    envName,
                    TestPackageDisplay
                );

                azure.Select();

                using (var confirmation = AutomationDialog.FromDte(app, "Edit.Delete")) {
                    confirmation.OK();
                }

                app.SolutionExplorerTreeView.WaitForChildOfProjectRemoved(
                    project,
                    Strings.Environments,
                    envName,
                    TestPackageDisplay
                );
            }
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void CreateInstallRequirementsTxt(PythonVisualStudioApp app) {
            using (var dis = Init(app)) {
                var project = CreateTemporaryProject(app);

                var projectHome = project.GetPythonProject().ProjectHome;
                File.WriteAllText(Path.Combine(projectHome, "requirements.txt"), TestPackageSpec);

                string envName;
                var env = app.CreateVirtualEnvironment(project, out envName);
                env.Select();

                app.SolutionExplorerTreeView.WaitForChildOfProject(
                    project,
                    Strings.Environments,
                    envName,
                    TestPackageDisplay
                );
            }
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void InstallGenerateRequirementsTxt(PythonVisualStudioApp app) {
            using (var dis = Init(app)) {
                var project = CreateTemporaryProject(app);

                string envName;
                var env = app.CreateVirtualEnvironment(project, out envName);
                env.Select();

                try {
                    app.ExecuteCommand("Python.InstallRequirementsTxt", "/y", timeout: 5000);
                    Assert.Fail("Command should not have executed");
                } catch (AggregateException ae) {
                    ae.Handle(ex => ex is COMException);
                } catch (COMException) {
                }

                var requirementsTxt = Path.Combine(Path.GetDirectoryName(project.FullName), "requirements.txt");
                File.WriteAllText(requirementsTxt, TestPackageSpec);

                app.ExecuteCommand("Python.InstallRequirementsTxt", "/y");

                app.SolutionExplorerTreeView.WaitForChildOfProject(
                    project,
                    Strings.Environments,
                    envName,
                    TestPackageDisplay
                );

                File.Delete(requirementsTxt);

                app.ExecuteCommand("Python.GenerateRequirementsTxt", "/e:\"" + envName + "\"");

                app.SolutionExplorerTreeView.WaitForChildOfProject(
                    project,
                    "requirements.txt"
                );

                AssertUtil.ContainsAtLeast(
                    File.ReadAllLines(requirementsTxt).Select(s => s.Trim()),
                    TestPackageSpec
                );
            }
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void LoadVirtualEnv(PythonVisualStudioApp app) {
            using (var dis = Init(app)) {
                var project = CreateTemporaryProject(app);
                var projectName = project.UniqueName;

                string envName;
                var env = app.CreateVirtualEnvironment(project, out envName);

                var solution = app.Dte.Solution.FullName;
                app.Dte.Solution.Close(true);

                app.Dte.Solution.Open(solution);
                project = app.Dte.Solution.Item(projectName);

                app.OpenSolutionExplorer().WaitForChildOfProject(
                    project,
                    Strings.Environments,
                    envName
                );
            }
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void ActivateVirtualEnv(PythonVisualStudioApp app) {
            using (var dis = Init(app)) {
                var project = CreateTemporaryProject(app);

                Assert.AreNotEqual(null, project.ProjectItems.Item(Path.GetFileNameWithoutExtension(app.Dte.Solution.FullName) + ".py"));

                var id0 = (string)project.Properties.Item("InterpreterId").Value;

                string envName1, envName2;
                var env1 = app.CreateVirtualEnvironment(project, out envName1);
                var env2 = app.CreateVirtualEnvironment(project, out envName2);

                var id1 = (string)project.Properties.Item("InterpreterId").Value;
                Assert.AreNotEqual(id0, id1);

                env2.Select();
                app.Dte.ExecuteCommand("Python.ActivateEnvironment");

                var id2 = (string)project.Properties.Item("InterpreterId").Value;
                Assert.AreNotEqual(id0, id2);
                Assert.AreNotEqual(id1, id2);

                // Change the selected node
                app.SolutionExplorerTreeView.SelectProject(project);
                app.Dte.ExecuteCommand("Python.ActivateEnvironment", "/env:\"" + envName1 + "\"");

                var id1b = (string)project.Properties.Item("InterpreterId").Value;
                Assert.AreEqual(id1, id1b);
            }
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void RemoveVirtualEnv(PythonVisualStudioApp app) {
            using (var dis = Init(app)) {
                var project = CreateTemporaryProject(app);

                string envName, envPath;
                var env = app.CreateVirtualEnvironment(project, out envName, out envPath);

                env.Select();

                using (var removeDeleteDlg = RemoveItemDialog.FromDte(app)) {
                    removeDeleteDlg.Remove();
                }

                app.OpenSolutionExplorer().WaitForChildOfProjectRemoved(
                    project,
                    Strings.Environments,
                    envName
                );

                var projectHome = (string)project.Properties.Item("ProjectHome").Value;
                envPath = Path.Combine(projectHome, envPath);
                Assert.IsTrue(Directory.Exists(envPath), envPath);
            }
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DeleteVirtualEnv(PythonVisualStudioApp app) {
            using (var procs = new ProcessScope("Microsoft.PythonTools.Analyzer"))
            using (var dis = Init(app)) {
                var options = app.GetService<PythonToolsService>().GeneralOptions;
                var oldAutoAnalyze = options.AutoAnalyzeStandardLibrary;
                app.OnDispose(() => { options.AutoAnalyzeStandardLibrary = oldAutoAnalyze; options.Save(); });
                options.AutoAnalyzeStandardLibrary = false;
                options.Save();

                var project = CreateTemporaryProject(app);

                string envName, envPath;
                TreeNode env = app.CreateVirtualEnvironment(project, out envName, out envPath);

                // Need to wait some more for the database to be loaded.
                app.WaitForNoDialog(TimeSpan.FromSeconds(10.0));

                for (int retries = 3; !procs.ExitNewProcesses() && retries >= 0; --retries) {
                    Thread.Sleep(1000);
                    Console.WriteLine("Failed to close all analyzer processes (remaining retries {0})", retries);
                }

                env.Select();
                using (var removeDeleteDlg = RemoveItemDialog.FromDte(app)) {
                    removeDeleteDlg.Delete();
                }

                app.WaitForNoDialog(TimeSpan.FromSeconds(5.0));

                app.OpenSolutionExplorer().WaitForChildOfProjectRemoved(
                    project,
                    Strings.Environments,
                    envName
                );

                var projectHome = (string)project.Properties.Item("ProjectHome").Value;
                envPath = Path.Combine(projectHome, envPath);
                for (int retries = 10;
                    Directory.Exists(envPath) && retries > 0;
                    --retries) {
                    Thread.Sleep(1000);
                }
                Assert.IsFalse(Directory.Exists(envPath), envPath);
            }
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DefaultBaseInterpreterSelection(PythonVisualStudioApp app) {
            // The project that will be loaded references these environments.
            PythonPaths.Python27.AssertInstalled();
            PythonPaths.Python33.AssertInstalled();

            using (var dis = Init(app)) {
                var project = app.OpenProject(@"TestData\Environments.sln");

                app.OpenSolutionExplorer().SelectProject(project);
                app.Dte.ExecuteCommand("Python.ActivateEnvironment", "/env:\"Python 2.7 (32-bit)\"");

                using (var createVenv = AutomationDialog.FromDte(app, "Python.AddVirtualEnvironment")) {
                    var baseInterp = new ComboBox(createVenv.FindByAutomationId("BaseInterpreter")).GetSelectedItemName();

                    Assert.AreEqual("Python 2.7 (32-bit)", baseInterp);
                    createVenv.Cancel();
                }

                app.Dte.ExecuteCommand("Python.ActivateEnvironment", "/env:\"Python 3.3 (32-bit)\"");

                using (var createVenv = AutomationDialog.FromDte(app, "Python.AddVirtualEnvironment")) {
                    var baseInterp = new ComboBox(createVenv.FindByAutomationId("BaseInterpreter")).GetSelectedItemName();

                    Assert.AreEqual("Python 3.3 (32-bit)", baseInterp);
                    createVenv.Cancel();
                }
            }
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void NoGlobalSitePackages(PythonVisualStudioApp app) {
            using (var dis = Init(app)) {
                var project = CreateTemporaryProject(app);

                string envName, envPath;
                var env = app.CreateVirtualEnvironment(project, out envName, out envPath);

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
                var interp = project.GetPythonProject().GetAnalyzer();
                var module = interp
                    .GetModulesResult(true)
                    .Result
                    .Select(x => x.Name)
                    .Where(x => x == "virtualenv_support")
                    .FirstOrDefault();

                Assert.IsNull(module);
            }
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void CreateVEnv(PythonVisualStudioApp app) {
            using (var dis = Init3(app)) {
                if (dis.CurrentDefault.FindModules("virtualenv").Contains("virtualenv")) {
                    dis.CurrentDefault.PipUninstall("virtualenv");
                }

                Assert.AreEqual(0, Microsoft.PythonTools.Analysis.ModulePath.GetModulesInLib(dis.CurrentDefault.Configuration)
                    .Count(mp => mp.FullName == "virtualenv"),
                    string.Format("Failed to uninstall 'virtualenv' from {0}", dis.CurrentDefault.Configuration.PrefixPath)
                );

                var project = CreateTemporaryProject(app);

                string envName, envPath;

                var env = app.CreateVirtualEnvironment(project, out envName, out envPath);
                Assert.IsNotNull(env);
                Assert.IsNotNull(env.Element);
                Assert.AreEqual(string.Format("env (Python {0} ({1}))",
                    dis.CurrentDefault.Configuration.Version,
                    dis.CurrentDefault.Configuration.Architecture
                ), envName);
            }
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void AddExistingVEnv(PythonVisualStudioApp app) {
            var python = PythonPaths.Python35 ?? PythonPaths.Python34 ?? PythonPaths.Python33;
            python.AssertInstalled();

            var project = CreateTemporaryProject(app);

            string envName;
            var envPath = TestData.GetPath(@"TestData\\Environments\\venv");
            File.WriteAllText(Path.Combine(envPath, "pyvenv.cfg"),
                string.Format(@"home = {0}
include-system-site-packages = false
version = 3.{1}.0", python.PrefixPath, python.Version.ToVersion().Minor));

            var env = app.AddExistingVirtualEnvironment(project, envPath, out envName);
            Assert.IsNotNull(env);
            Assert.IsNotNull(env.Element);
            Assert.AreEqual(
                string.Format("venv (Python 3.{0} (32-bit))", python.Version.ToVersion().Minor),
                envName
            );
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void LaunchUnknownEnvironment(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\Environments\Unknown.sln");

            app.ExecuteCommand("Debug.Start");
            app.CheckMessageBox(MessageBoxButton.Ok, "Global|PythonCore|2.8|x86", "incorrectly configured");
        }

        class MockProjectContextProvider : IProjectContextProvider {
            private readonly object[] _contexts;

            public MockProjectContextProvider(params object[] contexts) {
                _contexts = contexts;

            }

            public IEnumerable<object> Projects {
                get {
                    return _contexts;
                }
            }

            public event EventHandler ProjectsChanaged {
                add {
                }
                remove {
                }
            }

            public void InterpreterLoaded(object context, InterpreterConfiguration factory) {
            }

            public void InterpreterUnloaded(object context, InterpreterConfiguration factory) {
            }

            public event EventHandler<ProjectChangedEventArgs> ProjectChanged {
                add {

                }
                remove {
                }
            }
        }

        class MockLogger : IInterpreterLog {
            public readonly StringBuilder Errors = new StringBuilder();

            public void Log(string msg) {
                Errors.AppendLine(msg);
            }
        }

        //[TestMethod, Priority(0)]
        public void UnavailableEnvironments() {
            var collection = new Microsoft.Build.Evaluation.ProjectCollection();
            try {
                var service = new MockInterpreterOptionsService();
                var proj = collection.LoadProject(TestData.GetPath(@"TestData\Environments\Unavailable.pyproj"));
                var contextProvider = new MockProjectContextProvider(proj);

                var logger = new MockLogger();

                using (var provider = new MSBuildProjectInterpreterFactoryProvider(
                    new[] { new Lazy<IProjectContextProvider>(() => contextProvider) },
                    null,
                    new[] { new Lazy<IInterpreterLog>(() => logger) })) {
                    var configs = provider.GetInterpreterConfigurations().ToArray();
                    // force the load...
                    AssertUtil.AreEqual(
                        logger.Errors.ToString()
                        .Replace(TestData.GetPath("TestData\\Environments\\"), "$")
                        .Split('\r', '\n')
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Select(s => s.Trim()),
                        @"Interpreter $env\ has invalid value for 'Id':",
                        @"Interpreter $env\ has invalid value for 'Version': INVALID VERSION",
                        @"Interpreter $env\ has invalid value for 'InterpreterPath': INVALID<>PATH",
                        @"Interpreter $env\ has invalid value for 'WindowsInterpreterPath': INVALID<>PATH"
                    );

                    var factories = provider.GetInterpreterFactories().ToList();
                    foreach (var fact in factories) {
                        Console.WriteLine("{0}: {1}", fact.GetType().FullName, fact.Configuration.Description);
                    }

                    foreach (var fact in factories) {
                        Assert.IsInstanceOfType(
                            fact,
                            typeof(NotFoundInterpreterFactory),
                            string.Format("{0} was not correct type", fact.Configuration.Description)
                        );
                        Assert.IsFalse(fact.Configuration.IsAvailable(), string.Format("{0} was not unavailable", fact.Configuration.Description));
                    }

                    AssertUtil.AreEqual(factories.Select(f => f.Configuration.Description),
                        "Invalid InterpreterPath (unavailable)",
                        "Invalid WindowsInterpreterPath (unavailable)"
                    );
                }
            } finally {
                collection.UnloadAllProjects();
                collection.Dispose();
            }
        }

        private void EnvironmentReplWorkingDirectoryTest(
            PythonVisualStudioApp app,
            EnvDTE.Project project,
            TreeNode env
        ) {
            var path1 = Path.Combine(Path.GetDirectoryName(project.FullName), Guid.NewGuid().ToString("N"));
            var path2 = Path.Combine(Path.GetDirectoryName(project.FullName), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path1);
            Directory.CreateDirectory(path2);

            app.OpenSolutionExplorer().SelectProject(project);
            app.Dte.ExecuteCommand("Python.Interactive");

            using (var window = app.GetInteractiveWindow(string.Format("{0} Interactive", project.Name))) {
                Assert.IsNotNull(window, string.Format("Failed to find '{0} Interactive'", project.Name));
                app.ServiceProvider.GetUIThread().Invoke(() => project.GetPythonProject().SetProjectProperty("WorkingDirectory", path1));

                window.Reset();
                window.ExecuteText("import os; os.getcwd()").Wait();
                window.WaitForTextEnd(
                    string.Format("'{0}'", path1.Replace("\\", "\\\\")),
                    ">"
                );

                app.ServiceProvider.GetUIThread().Invoke(() => project.GetPythonProject().SetProjectProperty("WorkingDirectory", path2));

                window.Reset();
                window.ExecuteText("import os; os.getcwd()").Wait();
                window.WaitForTextEnd(
                    string.Format("'{0}'", path2.Replace("\\", "\\\\")),
                    ">"
                );
            }
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void EnvironmentReplWorkingDirectory(PythonVisualStudioApp app) {
            using (var dis = Init(app)) {
                var project = CreateTemporaryProject(app);

                app.ServiceProvider.GetUIThread().Invoke(() => {
                    var pp = project.GetPythonProject();
                    pp.AddInterpreter(dis.CurrentDefault.Configuration.Id);
                });

                var envName = dis.CurrentDefault.Configuration.Description;
                var sln = app.OpenSolutionExplorer();
                var env = sln.FindChildOfProject(project, Strings.Environments, envName);

                EnvironmentReplWorkingDirectoryTest(app, project, env);
            }
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void VirtualEnvironmentReplWorkingDirectory(PythonVisualStudioApp app) {
            using (var dis = Init(app)) {
                var project = CreateTemporaryProject(app);

                string envName;
                var env = app.CreateVirtualEnvironment(project, out envName);

                EnvironmentReplWorkingDirectoryTest(app, project, env);
            }
        }
    }
}
