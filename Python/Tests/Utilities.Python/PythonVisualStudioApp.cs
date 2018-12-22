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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Interop;
using EnvDTE;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Environments;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities.Python;
using Thread = System.Threading.Thread;

namespace TestUtilities.UI.Python {
    public class PythonVisualStudioApp : VisualStudioApp {
        private bool _deletePerformanceSessions;
        private PythonPerfExplorer _perfTreeView;
        private PythonPerfToolBar _perfToolBar;
        private PythonTestExplorer _testExplorer;
        public readonly PythonToolsService PythonToolsService;

        public PythonVisualStudioApp(IServiceProvider site)
            : base(site) {

            var shell = (IVsShell)ServiceProvider.GetService(typeof(SVsShell));
            var pkg = new Guid("6dbd7c1e-1f1b-496d-ac7c-c55dae66c783");
            IVsPackage pPkg;
            ErrorHandler.ThrowOnFailure(shell.LoadPackage(ref pkg, out pPkg));
            System.Threading.Thread.Sleep(1000);

            PythonToolsService = ServiceProvider.GetPythonToolsService_NotThreadSafe();
            Assert.IsNotNull(PythonToolsService, "Failed to get PythonToolsService");

            // Disable AutoListIdentifiers for tests
            var ao = PythonToolsService.AdvancedOptions;
            Assert.IsNotNull(ao, "Failed to get AdvancedOptions");
            var oldALI = ao.AutoListIdentifiers;
            ao.AutoListIdentifiers = false;

            var orwoodProp = Dte.Properties["Environment", "ProjectsAndSolution"].Item("OnRunWhenOutOfDate");
            Assert.IsNotNull(orwoodProp, "Failed to get OnRunWhenOutOfDate property");
            var oldOrwood = orwoodProp.Value;
            orwoodProp.Value = 1;

            OnDispose(() => {
                ao.AutoListIdentifiers = oldALI;
                orwoodProp.Value = oldOrwood;
            });
        }

        public void InvokeOnMainThread(Action action) => ServiceProvider.GetUIThread().Invoke(action);

        protected override void Dispose(bool disposing) {
            if (!IsDisposed) {
                try {
                    ServiceProvider.GetUIThread().Invoke(() => {
                        var iwp = ComponentModel.GetService<InteractiveWindowProvider>();
                        if (iwp != null) {
                            foreach (var w in iwp.AllOpenWindows) {
                                w.InteractiveWindow.Close();
                            }
                        }
                    });
                } catch (Exception ex) {
                    Console.WriteLine("Error while closing all interactive windows");
                    Console.WriteLine(ex);
                }

                if (_deletePerformanceSessions) {
                    try {
                        dynamic profiling = Dte.GetObject("PythonProfiling");

                        for (dynamic session = profiling.GetSession(1);
                            session != null;
                            session = profiling.GetSession(1)) {
                            profiling.RemoveSession(session, true);
                        }
                    } catch (Exception ex) {
                        Console.WriteLine("Error while cleaning up profiling sessions");
                        Console.WriteLine(ex);
                    }
                }
            }
            base.Dispose(disposing);
        }

        // Constants for passing to CreateProject
        private const string _templateLanguageName = "Python";
        public static string TemplateLanguageName {
            get {
                return _templateLanguageName;
            }
        }

        public const string PythonApplicationTemplate = "ConsoleAppProject";
        public const string EmptyWebProjectTemplate = "WebProjectEmpty";
        public const string BottleWebProjectTemplate = "WebProjectBottle";
        public const string FlaskWebProjectTemplate = "WebProjectFlask";
        public const string DjangoWebProjectTemplate = "DjangoProject";
        public const string WorkerRoleProjectTemplate = "WorkerRoleProject";

        public const string EmptyFileTemplate = "EmptyPyFile";
        public const string WebRoleSupportTemplate = "AzureCSWebRole";
        public const string WorkerRoleSupportTemplate = "AzureCSWorkerRole";

        /// <summary>
        /// Opens and activates the solution explorer window.
        /// </summary>
        public void OpenPythonPerformance() {
            try {
                _deletePerformanceSessions = true;
                Dte.ExecuteCommand("Python.PerformanceExplorer");
            } catch {
                // If the package is not loaded yet then the command may not
                // work. Force load the package by opening the Launch dialog.
                using (var dialog = new PythonPerfTarget(OpenDialogWithDteExecuteCommand("Analyze.LaunchProfiling"))) {
                }
                Dte.ExecuteCommand("Python.PerformanceExplorer");
            }
        }

        /// <summary>
        /// Opens and activates the solution explorer window.
        /// </summary>
        public PythonPerfTarget LaunchPythonProfiling() {
            _deletePerformanceSessions = true;
            return new PythonPerfTarget(OpenDialogWithDteExecuteCommand("Analyze.LaunchProfiling"));
        }

        /// <summary>
        /// Provides access to the Python profiling tree view.
        /// </summary>
        public PythonPerfExplorer PythonPerformanceExplorerTreeView {
            get {
                if (_perfTreeView == null) {
                    var element = Element.FindFirst(TreeScope.Descendants,
                        new AndCondition(
                            new PropertyCondition(
                                AutomationElement.ClassNameProperty,
                                "SysTreeView32"
                            ),
                            new PropertyCondition(
                                AutomationElement.NameProperty,
                                "Python Performance"
                            )
                        )
                    );
                    _perfTreeView = new PythonPerfExplorer(element);
                }
                return _perfTreeView;
            }
        }

        /// <summary>
        /// Provides access to the Python profiling tool bar
        /// </summary>
        public PythonPerfToolBar PythonPerformanceExplorerToolBar {
            get {
                if (_perfToolBar == null) {
                    var element = Element.FindFirst(TreeScope.Descendants,
                        new AndCondition(
                            new PropertyCondition(
                                AutomationElement.ClassNameProperty,
                                "ToolBar"
                            ),
                            new PropertyCondition(
                                AutomationElement.NameProperty,
                                "Python Performance"
                            )
                        )
                    );
                    _perfToolBar = new PythonPerfToolBar(element);
                }
                return _perfToolBar;
            }
        }

        /// <summary>
        /// Opens and activates the test explorer window.
        /// </summary>
        public PythonTestExplorer OpenTestExplorer() {
            Dte.ExecuteCommand("TestExplorer.ShowTestExplorer");
            return TestExplorer;
        }

        public PythonTestExplorer TestExplorer {
            get {
                if (_testExplorer == null) {
                    AutomationElement element = null;
                    for (int i = 0; i < 10 && element == null; i++) {
                        element = Element.FindFirst(TreeScope.Descendants,
                            new AndCondition(
                                new PropertyCondition(
                                    AutomationElement.ClassNameProperty,
                                    "ViewPresenter"
                                ),
                                new PropertyCondition(
                                    AutomationElement.NameProperty,
                                    "Test Explorer"
                                )
                            )
                        );
                        if (element == null) {
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                    _testExplorer = new PythonTestExplorer(this, element);
                }
                return _testExplorer;
            }
        }

        public ReplWindowProxy ExecuteInInteractive(Project project, ReplWindowProxySettings settings = null) {
            // Prepare makes sure that IPython mode is disabled, and that the REPL is reset and cleared
            var window = ReplWindowProxy.Prepare(this, settings, project.Name);
            OpenSolutionExplorer().SelectProject(project);
            ExecuteCommand("Python.ExecuteInInteractive");
            return window;
        }

        public void SendToInteractive() {
            ExecuteCommand("Python.SendSelectionToInteractive");
        }

        public void OpenFolder(string folderPath) {
            ExecuteCommand("File.OpenFolder", "\"{0}\"".FormatInvariant(folderPath));
        }

        public ReplWindowProxy WaitForInteractiveWindow(string title, ReplWindowProxySettings settings = null) {
            var iwp = GetService<IComponentModel>(typeof(SComponentModel))?.GetService<InteractiveWindowProvider>();
            IVsInteractiveWindow window = null;
            for (int retries = 20; retries > 0 && window == null; --retries) {
                System.Threading.Thread.Sleep(100);
                window = iwp?.AllOpenWindows.FirstOrDefault(w => ((ToolWindowPane)w).Caption == title);
            }
            if (window == null) {
                Trace.TraceWarning(
                    "Failed to find {0} in {1}",
                    title,
                    string.Join(", ", iwp?.AllOpenWindows.Select(w => ((ToolWindowPane)w).Caption) ?? Enumerable.Empty<string>())
                );
                return null;
            }
            return new ReplWindowProxy(this, window.InteractiveWindow, (ToolWindowPane)window, settings ?? new ReplWindowProxySettings());
        }

        public ReplWindowProxy GetInteractiveWindow(Project project, ReplWindowProxySettings settings = null) {
            return GetInteractiveWindow(project.Name + " Interactive", settings);
        }

        public ReplWindowProxy GetInteractiveWindow(string title, ReplWindowProxySettings settings = null) {
            var iwp = GetService<IComponentModel>(typeof(SComponentModel))?.GetService<InteractiveWindowProvider>();
            var window = iwp?.AllOpenWindows.FirstOrDefault(w => ((ToolWindowPane)w).Caption == title);
            if (window == null) {
                Trace.TraceWarning(
                    "Failed to find {0} in {1}",
                    title,
                    string.Join(", ", iwp?.AllOpenWindows.Select(w => ((ToolWindowPane)w).Caption) ?? Enumerable.Empty<string>())
                );
                return null;
            }
            return new ReplWindowProxy(this, window.InteractiveWindow, (ToolWindowPane)window, settings ?? new ReplWindowProxySettings());
        }

        internal Document WaitForDocument(string docName) {
            for (int i = 0; i < 100; i++) {
                try {
                    return Dte.Documents.Item(docName);
                } catch {
                    System.Threading.Thread.Sleep(100);
                }
            }
            throw new InvalidOperationException("Document not opened: " + docName);
        }

        /// <summary>
        /// Selects the given interpreter as the default.
        /// </summary>
        /// <remarks>
        /// This method should always be called as a using block.
        /// </remarks>
        public DefaultInterpreterSetter SelectDefaultInterpreter(PythonVersion python) {
            return new DefaultInterpreterSetter(
                InterpreterService.FindInterpreter(python.Id),
                ServiceProvider
            );
        }

        /// <summary>
        /// Selects the given interpreter as the default.
        /// </summary>
        /// <remarks>
        /// This method should always be called as a using block.
        /// </remarks>
        public DefaultInterpreterSetter SelectDefaultInterpreter(InterpreterConfiguration python) {
            return new DefaultInterpreterSetter(
                InterpreterService.FindInterpreter(python.Id),
                ServiceProvider
            );
        }

        public DefaultInterpreterSetter SelectDefaultInterpreter(PythonVersion interp, string installPackages) {
            interp.AssertInstalled();
            if (interp.IsIronPython && !string.IsNullOrEmpty(installPackages)) {
                Assert.Inconclusive("Package installation not supported on IronPython");
            }

            var interpreterService = InterpreterService;
            var factory = interpreterService.FindInterpreter(interp.Id);
            var defaultInterpreterSetter = new DefaultInterpreterSetter(factory, ServiceProvider);

            try {
                if (!string.IsNullOrEmpty(installPackages)) {
                    var pm = OptionsService.GetPackageManagers(factory).FirstOrDefault();
                    var ui = new TestPackageManagerUI();
                    pm.PrepareAsync(ui, CancellationTokens.After60s).WaitAndUnwrapExceptions();
                    foreach (var package in installPackages.Split(' ', ',', ';').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s))) {
                        pm.InstallAsync(new PackageSpec(package), ui, CancellationTokens.After60s).WaitAndUnwrapExceptions();
                    }
                }

                Assert.AreEqual(factory.Configuration.Id, OptionsService.DefaultInterpreterId);

                var result = defaultInterpreterSetter;
                defaultInterpreterSetter = null;
                return result;
            } finally {
                if (defaultInterpreterSetter != null) {
                    defaultInterpreterSetter.Dispose();
                }
            }
        }


        public IInterpreterRegistryService InterpreterService {
            get {
                var model = GetService<IComponentModel>(typeof(SComponentModel));
                var service = model.GetService<IInterpreterRegistryService>();
                Assert.IsNotNull(service, "Unable to get IInterpreterRegistryService");
                return service;
            }
        }

        public IInterpreterOptionsService OptionsService {
            get {
                var model = GetService<IComponentModel>(typeof(SComponentModel));
                var service = model.GetService<IInterpreterOptionsService>();
                Assert.IsNotNull(service, "Unable to get InterpreterOptionsService");
                return service;
            }
        }

        public TreeNode CreateCondaEnvironment(EnvDTE.Project project, string packageNames, string envFile, string expectedEnvFile, out string envName, out string envPath) {
            if (packageNames == null && envFile == null) {
                throw new ArgumentException("Must set either package names or environment file");
            }

            var environmentsNode = OpenSolutionExplorer().FindChildOfProject(project, Strings.Environments);
            environmentsNode.Select();

            var dlg = AddCondaEnvironmentDialogWrapper.FromDte(this);
            try {
                Assert.AreNotEqual(string.Empty, dlg.EnvName);

                envName = "test" + Guid.NewGuid().ToString().Replace("-", "");
                dlg.EnvName = envName;

                if (packageNames != null) {
                    dlg.SetPackagesMode();
                    dlg.Packages = packageNames;
                    dlg.WaitForPreviewPackage("python", TimeSpan.FromMinutes(2));
                } else if (envFile != null) {
                    if (expectedEnvFile == string.Empty) {
                        Assert.AreEqual(string.Empty, dlg.EnvFile);
                    } else if (expectedEnvFile != null) {
                        Assert.IsTrue(PathUtils.IsSamePath(dlg.EnvFile, expectedEnvFile));
                    }
                    dlg.SetEnvFileMode();
                    dlg.EnvFile = envFile;
                }

                dlg.ClickAdd();
            } catch (Exception) {
                dlg.CloseWindow();
                throw;
            }

            var id = CondaEnvironmentFactoryConstants.GetInterpreterId(CondaEnvironmentFactoryProvider.EnvironmentCompanyName, envName);
            var config = WaitForEnvironment(id, TimeSpan.FromMinutes(3));
            Assert.IsNotNull(config, "Could not find intepreter configuration");

            envName = string.Format("{0} ({1}, {2})", envName, config.Version, config.Architecture);
            envPath = config.GetPrefixPath();

            Console.WriteLine("Expecting environment named: {0}", envName);

            try {
                return OpenSolutionExplorer().WaitForChildOfProject(project, Strings.Environments, envName);
            } finally {
                var text = GetOutputWindowText("General");
                if (!string.IsNullOrEmpty(text)) {
                    Console.WriteLine("** Output Window text");
                    Console.WriteLine(text);
                    Console.WriteLine("***");
                    Console.WriteLine();
                }
            }
        }

        private InterpreterConfiguration WaitForEnvironment(string id, TimeSpan timeout) {
            for (int i = 0; i < timeout.TotalMilliseconds; i += 1000) {
                var config = InterpreterService.FindConfiguration(id);
                if (config != null) {
                    return config;
                }

                Thread.Sleep(1000);
            }

            return null;
        }

        public TreeNode CreateVirtualEnvironment(EnvDTE.Project project, out string envName) {
            return CreateVirtualEnvironment(project, out envName, out _);
        }

        public TreeNode CreateVirtualEnvironment(EnvDTE.Project project, out string envName, out string envPath) {
            var environmentsNode = OpenSolutionExplorer().FindChildOfProject(project, Strings.Environments);
            environmentsNode.Select();

            var dlg = AddVirtualEnvironmentDialogWrapper.FromDte(this);
            try {
                var baseInterp = dlg.BaseInterpreter;
                var location = dlg.Location;
                var name = dlg.EnvName;

                envName = "{0} ({1})".FormatUI(name, baseInterp);
                envPath = Path.Combine(location, name);

                Console.WriteLine("Expecting environment named: {0}", envName);

                dlg.WaitForReady();
                dlg.ClickAdd();
            } catch (Exception) {
                dlg.CloseWindow();
                throw;
            }

            try {
                return OpenSolutionExplorer().WaitForChildOfProject(project, TimeSpan.FromMinutes(5), Strings.Environments, envName);
            } finally {
                var text = GetOutputWindowText("General");
                if (!string.IsNullOrEmpty(text)) {
                    Console.WriteLine("** Output Window text");
                    Console.WriteLine(text);
                    Console.WriteLine("***");
                    Console.WriteLine();
                }
            }
        }

        public TreeNode AddExistingEnvironment(EnvDTE.Project project, string envPath, out string envName) {
            var environmentsNode = OpenSolutionExplorer().FindChildOfProject(project, Strings.Environments);
            environmentsNode.Select();

            var factory = InterpreterService.Interpreters.FirstOrDefault(interp => PathUtils.IsSameDirectory(interp.Configuration.GetPrefixPath(), envPath));
            envName = string.Format("Python {1} ({2})", PathUtils.GetFileOrDirectoryName(envPath), factory.Configuration.Version, factory.Configuration.Architecture);

            var dlg = AddExistingEnvironmentDialogWrapper.FromDte(this);
            try {
                dlg.Interpreter = envName;

                Console.WriteLine("Expecting environment named: {0}", envName);

                dlg.ClickAdd();
            } catch (Exception) {
                dlg.CloseWindow();
                throw;
            }

            return OpenSolutionExplorer().WaitForChildOfProject(project, Strings.Environments, envName);
        }

        public TreeNode AddLocalCustomEnvironment(EnvDTE.Project project, string envPath, string descriptionOverride, string expectedLangVer, string expectedArch, out string envName) {
            var environmentsNode = OpenSolutionExplorer().FindChildOfProject(project, Strings.Environments);
            environmentsNode.Select();

            var dlg = AddExistingEnvironmentDialogWrapper.FromDte(this);
            try {
                dlg.SelectCustomInterpreter();
                dlg.PrefixPath = envPath;

                // Need to wait for async auto detect to be finished
                dlg.WaitForReady();

                if (expectedLangVer != null) {
                    Assert.AreEqual(expectedLangVer, dlg.LanguageVersion);
                }

                if (expectedArch != null) {
                    Assert.AreEqual(expectedArch, dlg.Architecture);
                }

                dlg.RegisterGlobally = false;

                if (descriptionOverride != null) {
                    dlg.Description = descriptionOverride;
                }

                envName = dlg.Description;
                Console.WriteLine("Expecting environment named: {0}", envName);

                dlg.ClickAdd();
            } catch (Exception) {
                dlg.CloseWindow();
                throw;
            }

            return OpenSolutionExplorer().WaitForChildOfProject(project, Strings.Environments, envName);
        }

        public IPythonOptions Options {
            get {
                return (IPythonOptions)Dte.GetObject("VsPython");
            }
        }

    }
}
