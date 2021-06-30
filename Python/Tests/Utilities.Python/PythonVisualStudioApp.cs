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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Automation;
using EnvDTE;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.VisualStudio.Shell;
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
                using (var dialog = new PythonPerfTarget(OpenDialogWithDteExecuteCommand("Debug.LaunchProfiling"))) {
                }
                Dte.ExecuteCommand("Python.PerformanceExplorer");
            }
        }

        /// <summary>
        /// Opens and activates the solution explorer window.
        /// </summary>
        public PythonPerfTarget LaunchPythonProfiling() {
            _deletePerformanceSessions = true;
            return new PythonPerfTarget(OpenDialogWithDteExecuteCommand("Debug.LaunchProfiling"));
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
                    for (int i = 0; i < 40 && element == null; i++) {
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
                    Assert.IsNotNull(element, "Missing Text Explorer window");
                    var testExplorer = new AutomationWrapper(element);

                    var searchBox = testExplorer.FindByName("Search Test Explorer");
                    Assert.IsNotNull(searchBox, "Missing Search Bar Textbox");

                    _testExplorer = new PythonTestExplorer(this, element, new AutomationWrapper(searchBox));
                }
                return _testExplorer;
            }
        }

        public AutomationElementCollection GetInfoBars() {
            return Element.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "infobarcontrol")
            );
        }

        public AutomationElement FindFirstInfoBar(Condition condition, TimeSpan timeout) {
            for (int i = 0; i < timeout.TotalMilliseconds; i += 500) {
                var infoBars = GetInfoBars();
                foreach (AutomationElement infoBar in infoBars) {
                    var createLink = infoBar.FindFirst(TreeScope.Descendants, condition);
                    if (createLink != null) {
                        return createLink;
                    }
                }
                Thread.Sleep(500);
            }

            return null;
        }

        public PythonCreateVirtualEnvInfoBar FindCreateVirtualEnvInfoBar(TimeSpan timeout) {
            var element = FindFirstInfoBar(PythonCreateVirtualEnvInfoBar.FindCondition, timeout);
            return element != null ? new PythonCreateVirtualEnvInfoBar(element) : null;
        }

        public PythonCreateCondaEnvInfoBar FindCreateCondaEnvInfoBar(TimeSpan timeout) {
            var element = FindFirstInfoBar(PythonCreateCondaEnvInfoBar.FindCondition, timeout);
            return element != null ? new PythonCreateCondaEnvInfoBar(element) : null;
        }

        public PythonInstallPackagesInfoBar FindInstallPackagesInfoBar(TimeSpan timeout) {
            var element = FindFirstInfoBar(PythonInstallPackagesInfoBar.FindCondition, timeout);
            return element != null ? new PythonInstallPackagesInfoBar(element) : null;
        }

        public ReplWindowProxy ExecuteInInteractive(Project project, ReplWindowProxySettings settings = null) {
            // Prepare makes sure that IPython mode is disabled, and that the REPL is reset and cleared
            var window = ReplWindowProxy.Prepare(this, settings, project.Name, workspaceName: null);
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
                window = iwp?.AllOpenWindows.FirstOrDefault(w => ((ToolWindowPane)w).Caption.StartsWith(title));
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
            var window = iwp?.AllOpenWindows.FirstOrDefault(w => ((ToolWindowPane)w).Caption.StartsWith(title));
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

        public TreeNode CreateProjectCondaEnvironment(EnvDTE.Project project, string packageNames, string envFile, string expectedEnvFile, out string envName, out string envPath) {
            var environmentsNode = OpenSolutionExplorer().FindChildOfProject(project, Strings.Environments);
            environmentsNode.Select();

            envName = ApplyCreateCondaEnvironmentDialog(packageNames, envFile, expectedEnvFile);

            var id = CondaEnvironmentFactoryConstants.GetInterpreterId(CondaEnvironmentFactoryProvider.EnvironmentCompanyName, envName);
            var config = WaitForEnvironment(id, TimeSpan.FromMinutes(3));
            Assert.IsNotNull(config, "Could not find intepreter configuration");

            string envLabel = string.Format("{0} ({1}, {2})", envName, config.Version, config.Architecture);
            envPath = config.GetPrefixPath();

            Console.WriteLine("Expecting environment: {0}", envLabel);

            try {
                return OpenSolutionExplorer().WaitForChildOfProject(project, Strings.Environments, envLabel);
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

        public void CreateWorkspaceCondaEnvironment(string packageNames, string envFile, string expectedEnvFile, out string envName, out string envPath, out string envDescription) {
            envName = ApplyCreateCondaEnvironmentDialog(packageNames, envFile, expectedEnvFile);

            var id = CondaEnvironmentFactoryConstants.GetInterpreterId(CondaEnvironmentFactoryProvider.EnvironmentCompanyName, envName);
            var config = WaitForEnvironment(id, TimeSpan.FromMinutes(5));
            Assert.IsNotNull(config, "Could not find intepreter configuration");

            envDescription = string.Format("{0} ({1}, {2})", envName, config.Version, config.Architecture);
            envPath = config.GetPrefixPath();

            Console.WriteLine("Expecting environment: {0}", envDescription);
        }

        private string ApplyCreateCondaEnvironmentDialog(string packageNames, string envFile, string expectedEnvFile) {
            if (packageNames == null && envFile == null) {
                throw new ArgumentException("Must set either package names or environment file");
            }

            string envName;
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
                        Assert.IsTrue(
                            PathUtils.IsSamePath(dlg.EnvFile, expectedEnvFile),
                            string.Format("Env file doesn't match.\nExpected: {0}\nActual: {1}", dlg.EnvFile, expectedEnvFile)
                        );
                    }
                    dlg.SetEnvFileMode();
                    dlg.EnvFile = envFile;
                }

                Console.WriteLine("Creating conda env");
                Console.WriteLine("  Name: {0}", envName);

                dlg.ClickAdd();
            } catch (Exception) {
                dlg.CloseWindow();
                throw;
            }

            return envName;
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

        public TreeNode CreateProjectVirtualEnvironment(EnvDTE.Project project, out string envName) {
            return CreateProjectVirtualEnvironment(project, out envName, out _);
        }

        public TreeNode CreateProjectVirtualEnvironment(EnvDTE.Project project, out string envLabel, out string envPath) {
            var environmentsNode = OpenSolutionExplorer().FindChildOfProject(project, Strings.Environments);
            environmentsNode.Select();

            ApplyVirtualEnvironmentDialog(out string baseInterp, out string location, out string envName);
            envLabel = "{0} ({1})".FormatUI(envName, baseInterp);
            envPath = Path.Combine(location, envName);

            try {
                return OpenSolutionExplorer().WaitForChildOfProject(project, TimeSpan.FromMinutes(5), Strings.Environments, envLabel);
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

        public void CreateWorkspaceVirtualEnvironment(out string baseEnvDescription, out string envPath) {
            ApplyVirtualEnvironmentDialog(out baseEnvDescription, out string location, out string envName);
            envPath = Path.Combine(location, envName);

            try {
                var id = WorkspaceInterpreterFactoryConstants.GetInterpreterId(WorkspaceInterpreterFactoryConstants.EnvironmentCompanyName, envName);
                var config = WaitForEnvironment(id, TimeSpan.FromMinutes(3));
                Assert.IsNotNull(config, "Config was not found.");
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

        private void ApplyVirtualEnvironmentDialog(out string baseInterp, out string location, out string envName) {
            var dlg = AddVirtualEnvironmentDialogWrapper.FromDte(this);
            try {
                baseInterp = dlg.BaseInterpreter;
                location = dlg.Location;
                envName = dlg.EnvName;

                Console.WriteLine("Creating virtual env");
                Console.WriteLine("  Name: {0}", envName);
                Console.WriteLine("  Location: {0}", location);
                Console.WriteLine("  Base Interpreter: {0}", baseInterp);

                dlg.WaitForReady();
                dlg.ClickAdd();
            } catch (Exception) {
                dlg.CloseWindow();
                throw;
            }
        }

        public TreeNode AddExistingEnvironment(EnvDTE.Project project, string envPath, out string envName) {
            var environmentsNode = OpenSolutionExplorer().FindChildOfProject(project, Strings.Environments);
            environmentsNode.Select();

            var factory = InterpreterService.Interpreters.FirstOrDefault(interp => PathUtils.IsSameDirectory(interp.Configuration.GetPrefixPath(), envPath));
            envName = string.Format("Python {1} ({2})", PathUtils.GetFileOrDirectoryName(envPath), factory.Configuration.Version, factory.Configuration.Architecture);

            ApplyAddExistingEnvironmentDialog(envPath, out envName);

            return OpenSolutionExplorer().WaitForChildOfProject(project, Strings.Environments, envName);
        }

        public void AddWorkspaceExistingEnvironment(string envPath, out string envName) {
            ApplyAddExistingEnvironmentDialog(envPath, out envName);
        }

        private void ApplyAddExistingEnvironmentDialog(string envPath, out string envName) {
            var factory = InterpreterService.Interpreters.FirstOrDefault(interp => PathUtils.IsSameDirectory(interp.Configuration.GetPrefixPath(), envPath));
            envName = string.Format("Python {1} ({2})", PathUtils.GetFileOrDirectoryName(envPath), factory.Configuration.Version, factory.Configuration.Architecture);

            var dlg = AddExistingEnvironmentDialogWrapper.FromDte(this);
            try {
                dlg.Interpreter = envName;

                Console.WriteLine("Adding existing env");
                Console.WriteLine("  Name: {0}", envName);
                Console.WriteLine("  Prefix: {0}", envPath);

                dlg.ClickAdd();
            } catch (Exception) {
                dlg.CloseWindow();
                throw;
            }
        }

        public TreeNode AddProjectLocalCustomEnvironment(EnvDTE.Project project, string envPath, string descriptionOverride, string expectedLangVer, string expectedArch, out string envDescription) {
            var environmentsNode = OpenSolutionExplorer().FindChildOfProject(project, Strings.Environments);
            environmentsNode.Select();

            ApplyAddLocalCustomEnvironmentDialog(envPath, descriptionOverride, expectedLangVer, expectedArch, out envDescription, out _, out _, out _);

            return OpenSolutionExplorer().WaitForChildOfProject(project, Strings.Environments, envDescription);
        }

        public void AddWorkspaceLocalCustomEnvironment(string envPath, string descriptionOverride, string expectedLangVer, string expectedArch) {
            ApplyAddLocalCustomEnvironmentDialog(envPath, descriptionOverride, expectedLangVer, expectedArch, out _, out _, out _, out _);
        }

        private void ApplyAddLocalCustomEnvironmentDialog(string envPath, string descriptionOverride, string expectedLangVer, string expectedArch, out string envDescription, out string envPrefixPath, out string languageVer, out string architecture) {
            envDescription = string.Empty;
            envPrefixPath = string.Empty;
            languageVer = string.Empty;

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

                envDescription = dlg.Description;
                envPrefixPath = dlg.PrefixPath;
                languageVer = dlg.LanguageVersion;
                architecture = dlg.Architecture;

                Console.WriteLine("Adding custom env");
                Console.WriteLine("  Description: {0}", envDescription);
                Console.WriteLine("  Prefix: {0}", envPrefixPath);
                Console.WriteLine("  Version: {0}", languageVer);
                Console.WriteLine("  Architecture: {0}", architecture);

                dlg.ClickAdd();
            } catch (Exception) {
                dlg.CloseWindow();
                throw;
            }
        }

        public IPythonOptions Options {
            get {
                return (IPythonOptions)Dte.GetObject("VsPython");
            }
        }

    }
}
