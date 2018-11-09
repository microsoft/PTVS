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

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using Path = System.IO.Path;

namespace PythonToolsUITests {
    public class EnvironmentUITests {
        const string TestPackageSpec = "ptvsd==2.2.0";
        const string TestPackageDisplay = "ptvsd (2.2.0)";

        static DefaultInterpreterSetter InitPython2(PythonVisualStudioApp app) {
            var dis = app.SelectDefaultInterpreter(PythonPaths.Python27 ?? PythonPaths.Python27_x64);
            var pm = app.OptionsService.GetPackageManagers(dis.CurrentDefault).FirstOrDefault();
            try {
                if (!pm.GetInstalledPackageAsync(new PackageSpec("virtualenv"), CancellationTokens.After60s).WaitAndUnwrapExceptions().IsValid) {
                    pm.InstallAsync(new PackageSpec("virtualenv"), new TestPackageManagerUI(), CancellationTokens.After60s).WaitAndUnwrapExceptions();
                }
                var r = dis;
                dis = null;
                return r;
            } finally {
                dis?.Dispose();
            }
        }

        static DefaultInterpreterSetter InitPython3(PythonVisualStudioApp app) {
            return app.SelectDefaultInterpreter(
                PythonPaths.Python37 ?? PythonPaths.Python37_x64 ??
                PythonPaths.Python36 ?? PythonPaths.Python36_x64 ??
                PythonPaths.Python35 ?? PythonPaths.Python35_x64 ??
                PythonPaths.Python34 ?? PythonPaths.Python34_x64 ??
                PythonPaths.Python33 ?? PythonPaths.Python33_x64
            );
        }

        static EnvDTE.Project CreateTemporaryProject(VisualStudioApp app, [CallerMemberName] string projectName = null) {
            var project = app.CreateProject(
                PythonVisualStudioApp.TemplateLanguageName,
                PythonVisualStudioApp.PythonApplicationTemplate,
                TestData.GetTempPath(),
                projectName ?? Path.GetFileNameWithoutExtension(Path.GetRandomFileName())
            );
            Assert.IsNotNull(project, "Project was not created");
            return project;
        }

        public void InstallUninstallPackage(PythonVisualStudioApp app) {
            using (var dis = InitPython3(app)) {
                var project = CreateTemporaryProject(app);

                var env = app.CreateVirtualEnvironment(project, out string envName);
                env.Select();

                app.ExecuteCommand("Python.InstallPackage", "/p:" + TestPackageSpec);

                var azure = app.SolutionExplorerTreeView.WaitForChildOfProject(
                    project,
                    TimeSpan.FromSeconds(30),
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

        public void CreateInstallRequirementsTxt(PythonVisualStudioApp app) {
            using (var dis = InitPython3(app)) {
                var project = CreateTemporaryProject(app);

                var projectHome = project.GetPythonProject().ProjectHome;
                File.WriteAllText(Path.Combine(projectHome, "requirements.txt"), TestPackageSpec);

                var env = app.CreateVirtualEnvironment(project, out string envName);
                env.Select();

                app.SolutionExplorerTreeView.WaitForChildOfProject(
                    project,
                    Strings.Environments,
                    envName,
                    TestPackageDisplay
                );
            }
        }

        public void InstallGenerateRequirementsTxt(PythonVisualStudioApp app) {
            using (var dis = InitPython3(app)) {
                var project = CreateTemporaryProject(app);

                var env = app.CreateVirtualEnvironment(project, out string envName);
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
                    TimeSpan.FromSeconds(30),
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

        public void LoadVEnv(PythonVisualStudioApp app) {
            using (var dis = InitPython3(app)) {
                var project = CreateTemporaryProject(app);
                var projectName = project.UniqueName;

                var env = app.CreateVirtualEnvironment(project, out string envName);

                var solution = app.Dte.Solution.FullName;
                app.Dte.Solution.Close(true);

                app.Dte.Solution.Open(solution);
                project = app.Dte.Solution.Item(projectName);

                app.OpenSolutionExplorer().WaitForChildOfProject(
                    project,
                    TimeSpan.FromSeconds(60),
                    Strings.Environments,
                    envName
                );
            }
        }

        public void ActivateVEnv(PythonVisualStudioApp app) {
            using (var dis = InitPython3(app)) {
                var project = CreateTemporaryProject(app);

                Assert.AreNotEqual(null, project.ProjectItems.Item(Path.GetFileNameWithoutExtension(app.Dte.Solution.FullName) + ".py"));

                var id0 = (string)project.Properties.Item("InterpreterId").Value;

                var env1 = app.CreateVirtualEnvironment(project, out string envName1);
                var env2 = app.CreateVirtualEnvironment(project, out string envName2);

                // At this point, env2 is active
                var id2 = (string)project.Properties.Item("InterpreterId").Value;
                Assert.AreNotEqual(id0, id2);

                // Activate env1 (previously stored node is now invalid, we need to query again)
                env1 = app.OpenSolutionExplorer().WaitForChildOfProject(project, Strings.Environments, envName1);
                env1.Select();
                app.Dte.ExecuteCommand("Python.ActivateEnvironment");

                var id1 = (string)project.Properties.Item("InterpreterId").Value;
                Assert.AreNotEqual(id0, id1);
                Assert.AreNotEqual(id2, id1);

                // Activate env2
                app.SolutionExplorerTreeView.SelectProject(project);
                app.Dte.ExecuteCommand("Python.ActivateEnvironment", "/env:\"" + envName2 + "\"");

                var id2b = (string)project.Properties.Item("InterpreterId").Value;
                Assert.AreEqual(id2, id2b);
            }
        }

        public void RemoveVEnv(PythonVisualStudioApp app) {
            using (var dis = InitPython3(app)) {
                var project = CreateTemporaryProject(app);

                var env = app.CreateVirtualEnvironment(project, out string envName, out string envPath);

                env.Select();

                using (var removeDeleteDlg = RemoveItemDialog.FromDte(app)) {
                    removeDeleteDlg.Remove();
                }

                app.OpenSolutionExplorer().WaitForChildOfProjectRemoved(
                    project,
                    Strings.Environments,
                    envName
                );

                Assert.IsTrue(Directory.Exists(envPath), envPath);
            }
        }

        public void DeleteVEnv(PythonVisualStudioApp app) {
            using (var procs = new ProcessScope("Microsoft.PythonTools.Analyzer"))
            using (var dis = InitPython3(app)) {
                var options = app.GetService<PythonToolsService>().GeneralOptions;
                var oldAutoAnalyze = options.AutoAnalyzeStandardLibrary;
                app.OnDispose(() => { options.AutoAnalyzeStandardLibrary = oldAutoAnalyze; options.Save(); });
                options.AutoAnalyzeStandardLibrary = false;
                options.Save();

                var project = CreateTemporaryProject(app);

                TreeNode env = app.CreateVirtualEnvironment(project, out string envName, out string envPath);

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

                for (int retries = 10;
                    Directory.Exists(envPath) && retries > 0;
                    --retries) {
                    Thread.Sleep(1000);
                }
                Assert.IsFalse(Directory.Exists(envPath), envPath);
            }
        }

        public void DefaultBaseInterpreterSelection(PythonVisualStudioApp app) {
            // The project that will be loaded references these environments.
            PythonPaths.Python27.AssertInstalled();
            PythonPaths.Python37.AssertInstalled();

            using (var dis = InitPython3(app)) {
                var sln = app.CopyProjectForTest(@"TestData\Environments.sln");
                var project = app.OpenProject(sln);

                app.OpenSolutionExplorer().SelectProject(project);
                app.Dte.ExecuteCommand("Python.ActivateEnvironment", "/env:\"Python 2.7 (32-bit)\"");

                var environmentsNode = app.OpenSolutionExplorer().FindChildOfProject(project, Strings.Environments);
                environmentsNode.Select();

                using (var createVenv = AddVirtualEnvironmentDialogWrapper.FromDte(app)) {
                    var baseInterp = createVenv.BaseInterpreter;

                    Assert.AreEqual("Python 2.7 (32-bit)", baseInterp);
                    createVenv.Cancel();
                }

                app.Dte.ExecuteCommand("Python.ActivateEnvironment", "/env:\"Python 3.7 (32-bit)\"");

                environmentsNode = app.OpenSolutionExplorer().FindChildOfProject(project, Strings.Environments);
                environmentsNode.Select();

                using (var createVenv = AddVirtualEnvironmentDialogWrapper.FromDte(app)) {
                    var baseInterp = createVenv.BaseInterpreter;

                    Assert.AreEqual("Python 3.7 (32-bit)", baseInterp);
                    createVenv.Cancel();
                }
            }
        }

        public void CreateVEnv(PythonVisualStudioApp app) {
            using (var dis = InitPython3(app)) {
                var pm = app.OptionsService.GetPackageManagers(dis.CurrentDefault).FirstOrDefault();
                if (pm.GetInstalledPackageAsync(new PackageSpec("virtualenv"), CancellationTokens.After60s).WaitAndUnwrapExceptions().IsValid) {
                    pm.UninstallAsync(new PackageSpec("virtualenv"), new TestPackageManagerUI(), CancellationTokens.After60s).WaitAndUnwrapExceptions();
                }

                Assert.AreEqual(0, Microsoft.PythonTools.Analysis.ModulePath.GetModulesInLib(dis.CurrentDefault.Configuration)
                    .Count(mp => mp.FullName == "virtualenv"),
                    string.Format("Failed to uninstall 'virtualenv' from {0}", dis.CurrentDefault.Configuration.PrefixPath)
                );

                var project = CreateTemporaryProject(app);

                var env = app.CreateVirtualEnvironment(project, out string envName);
                Assert.IsNotNull(env);
                Assert.IsNotNull(env.Element);
                Assert.AreEqual(string.Format("env (Python {0} ({1}))",
                    dis.CurrentDefault.Configuration.Version,
                    dis.CurrentDefault.Configuration.Architecture
                ), envName);
            }
        }

        public void CreateCondaEnvFromPackages(PythonVisualStudioApp app) {
            var python = PythonPaths.Anaconda36_x64 ?? PythonPaths.Anaconda36 ?? PythonPaths.Anaconda27_x64?? PythonPaths.Anaconda27;
            python.AssertInstalled();

            var project = CreateTemporaryProject(app);

            var env = app.CreateCondaEnvironment(project, "python=3.7 requests", null, null, out string envName, out string envPath);
            Assert.IsNotNull(env);
            Assert.IsNotNull(env.Element);

            FileUtils.DeleteDirectory(envPath);
        }

        public void CreateCondaEnvFromEnvFile(PythonVisualStudioApp app) {
            var python = PythonPaths.Anaconda36_x64 ?? PythonPaths.Anaconda36 ?? PythonPaths.Anaconda27_x64 ?? PythonPaths.Anaconda27;
            python.AssertInstalled();

            var envFileContents = @"name: test
dependencies:
  - cookies==2.2.1";
            var envFilePath = Path.Combine(TestData.GetTempPath("EnvFiles"), "environment.yml");
            File.WriteAllText(envFilePath, envFileContents);
            var project = CreateTemporaryProject(app);
            var envFileItem = project.ProjectItems.AddFromFileCopy(envFilePath);

            var env = app.CreateCondaEnvironment(project, null, envFileItem.FileNames[0], envFileItem.FileNames[0], out string envName, out string envPath);
            Assert.IsNotNull(env);
            Assert.IsNotNull(env.Element);

            FileUtils.DeleteDirectory(envPath);
        }

        public void AddExistingVEnvLocal(PythonVisualStudioApp app) {
            var python = PythonPaths.Python37 ?? PythonPaths.Python36 ?? PythonPaths.Python35 ?? PythonPaths.Python34 ?? PythonPaths.Python33;
            python.AssertInstalled();

            var project = CreateTemporaryProject(app);

            var envPath = TestData.GetTempPath("venv");
            FileUtils.CopyDirectory(TestData.GetPath(@"TestData\\Environments\\venv"), envPath);
            File.WriteAllText(Path.Combine(envPath, "pyvenv.cfg"),
                string.Format(@"home = {0}
include-system-site-packages = false
version = 3.{1}.0", python.PrefixPath, python.Version.ToVersion().Minor));

            var env = app.AddLocalCustomEnvironment(project, envPath, null, python.Configuration.Version.ToString(), python.Architecture.ToString(), out string envName);
            Assert.IsNotNull(env);
            Assert.IsNotNull(env.Element);
            Assert.AreEqual(
                string.Format("venv (Python 3.{0} (32-bit))", python.Version.ToVersion().Minor),
                envName
            );
        }

        public void AddCustomEnvLocal(PythonVisualStudioApp app) {
            var python = PythonPaths.Python37 ?? PythonPaths.Python36 ?? PythonPaths.Python35 ?? PythonPaths.Python34 ?? PythonPaths.Python33;
            python.AssertInstalled();

            var project = CreateTemporaryProject(app);

            var envPath = python.PrefixPath;

            var env = app.AddLocalCustomEnvironment(project, envPath, "Test", python.Configuration.Version.ToString(), python.Architecture.ToString(), out string envName);
            Assert.IsNotNull(env);
            Assert.IsNotNull(env.Element);
            Assert.AreEqual(
                string.Format("Test", python.Version.ToVersion().Minor),
                envName
            );
        }

        public void AddExistingEnv(PythonVisualStudioApp app) {
            var python = PythonPaths.Python37 ?? PythonPaths.Python36 ?? PythonPaths.Python35 ?? PythonPaths.Python34 ?? PythonPaths.Python33;
            python.AssertInstalled();

            var project = CreateTemporaryProject(app);

            var envPath = python.PrefixPath;

            var env = app.AddExistingEnvironment(project, envPath, out string envName);
            Assert.IsNotNull(env);
            Assert.IsNotNull(env.Element);
            Assert.AreEqual(
                string.Format("Python 3.{0} (32-bit)", python.Version.ToVersion().Minor),
                envName
            );
        }

        public void LaunchUnknownEnvironment(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\Environments\Unknown.sln");
            var project = app.OpenProject(sln);

            app.ExecuteCommand("Debug.Start");
            app.CheckMessageBox(MessageBoxButton.Close, "Global|PythonCore|2.8|x86", "incorrectly configured");
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

        public void EnvironmentReplWorkingDirectory(PythonVisualStudioApp app) {
            using (var dis = InitPython3(app)) {
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

        public void VirtualEnvironmentReplWorkingDirectory(PythonVisualStudioApp app) {
            using (var dis = InitPython3(app)) {
                var project = CreateTemporaryProject(app);

                var env = app.CreateVirtualEnvironment(project, out _);

                EnvironmentReplWorkingDirectoryTest(app, project, env);
            }
        }

        public void SwitcherSingleProject(PythonVisualStudioApp app) {
            var globalDefault37 = PythonPaths.Python37_x64 ?? PythonPaths.Python37;
            globalDefault37.AssertInstalled();

            var added27 = PythonPaths.Python27_x64 ?? PythonPaths.Python27;
            added27.AssertInstalled();

            var added36 = PythonPaths.Python36_x64 ?? PythonPaths.Python36;
            added36.AssertInstalled();

            using (var dis = app.SelectDefaultInterpreter(globalDefault37)) {
                // Project has no references, uses global default
                var project = CreateTemporaryProject(app);
                CheckSwitcherText(app, globalDefault37.Configuration.Description);

                // Project has one referenced interpreter
                app.ServiceProvider.GetUIThread().Invoke(() => {
                    var pp = project.GetPythonProject();
                    pp.AddInterpreter(added27.Configuration.Id);
                });
                CheckSwitcherText(app, added27.Configuration.Description);

                // Project has two referenced interpreters (active remains the same)
                app.ServiceProvider.GetUIThread().Invoke(() => {
                    var pp = project.GetPythonProject();
                    pp.AddInterpreter(added36.Configuration.Id);
                });
                CheckSwitcherText(app, added27.Configuration.Description);

                // No switcher visible when solution closed and no file opened
                app.Dte.Solution.Close(SaveFirst: false);
                CheckSwitcherHidden(app);
            }
        }

        public void SwitcherWorkspace(PythonVisualStudioApp app) {
            var globalDefault37 = PythonPaths.Python37_x64 ?? PythonPaths.Python37;
            globalDefault37.AssertInstalled();

            var python27 = PythonPaths.Python27_x64;
            python27.AssertInstalled();

            var folders = TestData.GetTempPath();
            FileUtils.CopyDirectory(TestData.GetPath("TestData", "EnvironmentsSwitcherFolders"), folders);

            var folder1 = Path.Combine(folders, "Folder1");
            var folder2 = Path.Combine(folders, "Folder2");

            using (var dis = app.SelectDefaultInterpreter(globalDefault37)) {
                app.OpenFolder(folder1);
                app.OpenDocument(Path.Combine(folder1, "app1.py"));
                CheckSwitcherText(app, globalDefault37.Configuration.Description);

                app.OpenFolder(folder2);
                app.OpenDocument(Path.Combine(folder2, "app2.py"));
                CheckSwitcherText(app, python27.Configuration.Description);

                app.Dte.Solution.Close();
                CheckSwitcherHidden(app);
            }
        }

        public void SwitcherNoProject(PythonVisualStudioApp app) {
            var globalDefault37 = PythonPaths.Python37_x64 ?? PythonPaths.Python37;
            globalDefault37.AssertInstalled();

            using (var dis = app.SelectDefaultInterpreter(globalDefault37)) {
                // Loose Python file shows global default
                app.OpenDocument(TestData.GetPath("TestData", "Environments", "Program.py"));
                CheckSwitcherText(app, globalDefault37.Configuration.Description);

                // No switcher visible when solution closed and no file opened
                app.Dte.ActiveWindow.Close();
                CheckSwitcherHidden(app);

                // No switcher visible when loose cpp file open
                app.OpenDocument(TestData.GetPath("TestData", "Environments", "Program.cpp"));
                CheckSwitcherHidden(app);
            }
        }

        private void CheckSwitcherText(PythonVisualStudioApp app, string expected) {
            var status = app.FindByAutomationId("PythonEnvironmentStatusText");
            Assert.AreEqual(expected, status.Current.Name);

        }

        private void CheckSwitcherHidden(PythonVisualStudioApp app) {
            var status = app.FindByAutomationId("PythonEnvironmentStatusText");
            for (int i = 20; i >= 0; --i) {
                if (status.Current.Name == "(no Python environment)") {
                    break;
                }

                if (i == 0) {
                    Assert.AreEqual("(no Python environment)", status.Current.Name);
                }

                Thread.Sleep(500);
            }

            Assert.IsTrue(status.Current.IsOffscreen, "Status switcher should not be visible");
        }
    }
}
