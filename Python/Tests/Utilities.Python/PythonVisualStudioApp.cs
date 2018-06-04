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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using EnvDTE;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using Microsoft.Win32;
using TestUtilities.Python;
using Process = System.Diagnostics.Process;

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

        public TreeNode CreateVirtualEnvironment(EnvDTE.Project project, out string envName) {
            string dummy;
            return CreateVirtualEnvironment(project, out envName, out dummy);
        }

        public TreeNode CreateVirtualEnvironment(EnvDTE.Project project, out string envName, out string envPath) {
            var environmentsNode = OpenSolutionExplorer().FindChildOfProject(project, Strings.Environments);
            environmentsNode.Select();

            using (var pss = new ProcessScope("python")) {
                using (var createVenv = AutomationDialog.FromDte(this, "Python.AddVirtualEnvironment")) {
                    envPath = new TextBox(createVenv.FindByAutomationId("VirtualEnvPath")).GetValue();
                    var baseInterp = new ComboBox(createVenv.FindByAutomationId("BaseInterpreter")).GetSelectedItemName();
                    envName = "{0} ({1})".FormatUI(envPath, baseInterp);

                    Console.WriteLine("Expecting environment named: {0}", envName);

                    // Force a wait for the view to be updated.
                    var wnd = (DialogWindowVersioningWorkaround)HwndSource.FromHwnd(
                        new IntPtr(createVenv.Element.Current.NativeWindowHandle)
                    ).RootVisual;
                    wnd.Dispatcher.Invoke(() => {
                        var view = (AddVirtualEnvironmentView)wnd.DataContext;
                        return view.UpdateInterpreter(view.BaseInterpreter);
                    }).Wait();

                    createVenv.ClickButtonByAutomationId("Create");
                    createVenv.ClickButtonAndClose("Close", nameIsAutomationId: true);
                }

                var nowRunning = pss.WaitForNewProcess(TimeSpan.FromMinutes(1));
                if (nowRunning == null || !nowRunning.Any()) {
                    Assert.Fail("Failed to see python process start to create virtualenv");
                }
                foreach (var p in nowRunning) {
                    if (p.HasExited) {
                        continue;
                    }
                    try {
                        p.WaitForExit(30000);
                    } catch (Win32Exception ex) {
                        Console.WriteLine("Error waiting for process ID {0}\n{1}", p.Id, ex);
                    }
                }
            }

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

        public TreeNode AddExistingVirtualEnvironment(EnvDTE.Project project, string envPath, out string envName) {
            var environmentsNode = OpenSolutionExplorer().FindChildOfProject(project, Strings.Environments);
            environmentsNode.Select();

            using (var createVenv = AutomationDialog.FromDte(this, "Python.AddVirtualEnvironment")) {
                new TextBox(createVenv.FindByAutomationId("VirtualEnvPath")).SetValue(envPath);
                var baseInterp = new ComboBox(createVenv.FindByAutomationId("BaseInterpreter")).GetSelectedItemName();

                envName = string.Format("{0} ({1})", PathUtils.GetFileOrDirectoryName(envPath), baseInterp);

                Console.WriteLine("Expecting environment named: {0}", envName);

                createVenv.ClickButtonAndClose("Add", nameIsAutomationId: true);
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
