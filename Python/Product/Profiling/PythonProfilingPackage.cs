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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.PythonTools.Common;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudioTools.Project;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    // This attribute is used to register the informations needed to show the this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", AssemblyVersionInfo.Version, IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidPythonProfilingPkgString)]
    // set the window to dock where Toolbox/Performance Explorer dock by default
    [ProvideToolWindow(typeof(PerfToolWindow), Orientation = ToolWindowOrientation.Left, Style = VsDockStyle.Tabbed, Window = EnvDTE.Constants.vsWindowKindToolbox)]
    [ProvideFileFilter("{81da0100-e6db-4783-91ea-c38c3fa1b81e}", "/1", "#113", 100)]
    [ProvideEditorExtension(typeof(ProfilingSessionEditorFactory), ".pyperf", 50,
          ProjectGuid = "{81da0100-e6db-4783-91ea-c38c3fa1b81e}",
          NameResourceID = 105,
          DefaultName = "PythonPerfSession")]
    [ProvideAutomationObject("PythonProfiling")]
    internal sealed class PythonProfilingPackage : AsyncPackage {
        internal static PythonProfilingPackage Instance;
        private static ProfiledProcess _profilingProcess;   // process currently being profiled
        internal static readonly string PythonProjectGuid = "{888888a0-9f3d-457c-b088-3a5042f75d52}";
        internal static readonly string PerformanceFileFilter = Strings.PerformanceReportFilesFilter;
        private AutomationProfiling _profilingAutomation;
        private static OleMenuCommand _stopCommand, _startCommand;

        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public PythonProfilingPackage() {
            Instance = this;
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                var process = _profilingProcess;
                _profilingProcess = null;
                if (process != null) {
                    process.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        protected override int CreateToolWindow(ref Guid toolWindowType, int id)
            => toolWindowType == PerfToolWindow.WindowGuid ? CreatePerfToolWindow(id) : base.CreateToolWindow(ref toolWindowType, id);

        private int CreatePerfToolWindow(int id) {
            try {
                var type = typeof(PerfToolWindow);
                var toolWindow = FindWindowPane(type, id, false) ?? CreateToolWindow(type, id, this);
                return toolWindow != null ? VSConstants.S_OK : VSConstants.E_FAIL;
            } catch (Exception ex) {
                return Marshal.GetHRForException(ex);
            }
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress) {
            Trace.WriteLine("Entering InitializeAsync() of: {0}".FormatUI(this));

            await base.InitializeAsync(cancellationToken, progress);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Ensure Python Tools package is loaded
            var shell = (IVsShell7)GetService(typeof(SVsShell));
            var ptvsPackage = CommonGuidList.guidPythonToolsPackage;
            await shell.LoadPackageAsync(ref ptvsPackage);

            // Add our command handlers for menu (commands must exist in the .vsct file)
            if (GetService(typeof(IMenuCommandService)) is OleMenuCommandService mcs) {
                // Create the command for the menu item.
                CommandID menuCommandID = new CommandID(GuidList.guidPythonProfilingCmdSet, (int)PkgCmdIDList.cmdidStartPythonProfiling);
                MenuCommand menuItem = new MenuCommand(StartProfilingWizard, menuCommandID);
                mcs.AddCommand(menuItem);

                // Create the command for the menu item.
                menuCommandID = new CommandID(GuidList.guidPythonProfilingCmdSet, (int)PkgCmdIDList.cmdidPerfExplorer);
                var oleMenuItem = new OleMenuCommand((o, e) => ShowPeformanceExplorerAsync().DoNotWait(), menuCommandID);
                mcs.AddCommand(oleMenuItem);

                menuCommandID = new CommandID(GuidList.guidPythonProfilingCmdSet, (int)PkgCmdIDList.cmdidAddPerfSession);
                menuItem = new MenuCommand((o, e) => AddPerformanceSessionAsync().DoNotWait(), menuCommandID);
                mcs.AddCommand(menuItem);

                menuCommandID = new CommandID(GuidList.guidPythonProfilingCmdSet, (int)PkgCmdIDList.cmdidStartProfiling);
                oleMenuItem = _startCommand = new OleMenuCommand((o, e) => StartProfilingAsync().DoNotWait(), menuCommandID);
                oleMenuItem.BeforeQueryStatus += IsProfilingActive;
                mcs.AddCommand(oleMenuItem);

                menuCommandID = new CommandID(GuidList.guidPythonProfilingCmdSet, (int)PkgCmdIDList.cmdidStopProfiling);
                _stopCommand = oleMenuItem = new OleMenuCommand(StopProfiling, menuCommandID);
                oleMenuItem.BeforeQueryStatus += IsProfilingInactive;

                mcs.AddCommand(oleMenuItem);
            }

            //Create Editor Factory. Note that the base Package class will call Dispose on it.
            RegisterEditorFactory(new ProfilingSessionEditorFactory(this));
        }

        protected override object GetAutomationObject(string name) {
            if (name == "PythonProfiling") {
                if (_profilingAutomation == null) {
                    var pane = (PerfToolWindow)JoinableTaskFactory.Run(GetPerfToolWindowAsync);
                    _profilingAutomation = new AutomationProfiling(pane.Sessions);
                }
                return _profilingAutomation;
            }

            return base.GetAutomationObject(name);
        }

        internal static Guid GetStartupProjectGuid(IServiceProvider serviceProvider) {
            var buildMgr = (IVsSolutionBuildManager)serviceProvider.GetService(typeof(IVsSolutionBuildManager));
            if (buildMgr != null && ErrorHandler.Succeeded(buildMgr.get_StartupProject(out var hierarchy)) && hierarchy != null) {
                if (ErrorHandler.Succeeded(hierarchy.GetGuidProperty(
                    (uint)VSConstants.VSITEMID.Root,
                    (int)__VSHPROPID.VSHPROPID_ProjectIDGuid,
                    out var guid
                ))) {
                    return guid;
                }
            }
            return Guid.Empty;
        }

        internal IVsSolution Solution {
            get {
                return GetService(typeof(SVsSolution)) as IVsSolution;
            }
        }

        /// <summary>
        /// This function is the callback used to execute a command when the a menu item is clicked.
        /// See the Initialize method to see how the menu item is associated to this function using
        /// the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void StartProfilingWizard(object sender, EventArgs e) {
            if (!IsProfilingInstalled()) {
                MessageBox.Show(Strings.ProfilingSupportMissingError, Strings.ProductTitle);
                return;
            }

            // Used for manually testing the user input service.
            //MessageBox.Show("Test starts");
            //var tempUserInputService = new UserInputService();
            //var tempResult = tempUserInputService.GetCommandFromUserInput();
            //MessageBox.Show("Test ends");

            var targetView = new ProfilingTargetView(this);
            var dialog = new LaunchProfiling(this, targetView);
            var res = dialog.ShowModal() ?? false;
            if (res && targetView.IsValid) {
                var target = targetView.GetTarget();
                if (target != null) {
                    ProfileTarget(target);
                }
            }
        }

        internal SessionNode ProfileTarget(ProfilingTarget target, bool openReport = true) {
            return JoinableTaskFactory.Run(async () => {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                var name = target.GetProfilingName(this, out var save);
                var explorer = await ShowPerformanceExplorerAsync();
                var session = explorer.Sessions.AddTarget(target, name, save);

                StartProfiling(target, session, openReport);
                return session;
            });
        }

        internal void StartProfiling(ProfilingTarget target, SessionNode session, bool openReport = true) {
            JoinableTaskFactory.Run(async () => {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                if (!Utilities.SaveDirtyFiles()) {
                    // Abort
                    return;
                }

                if (target.ProjectTarget != null) {
                    ProfileProjectTarget(session, target.ProjectTarget, openReport);
                } else if (target.StandaloneTarget != null) {
                    ProfileStandaloneTarget(session, target.StandaloneTarget, openReport);
                } else {
                    if (MessageBox.Show(Strings.ProfilingSessionNotConfigured, Strings.NoProfilingTargetTitle, MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                        var newTarget = session.OpenTargetProperties();
                        if (newTarget != null && (newTarget.ProjectTarget != null || newTarget.StandaloneTarget != null)) {
                            StartProfiling(newTarget, session, openReport);
                        }
                    }
                }
            });
        }

        private void ProfileProjectTarget(SessionNode session, ProjectTarget projectTarget, bool openReport) {
            var project = Solution.EnumerateLoadedPythonProjects()
                .SingleOrDefault(p => p.GetProjectIDGuidProperty() == projectTarget.TargetProject);

            if (project != null) {
                ProfileProject(session, project, openReport);
            } else {
                MessageBox.Show(Strings.ProjectNotFoundInSolution, Strings.ProductTitle);
            }
        }

        private static void ProfileProject(SessionNode session, PythonProjectNode project, bool openReport) {
            LaunchConfiguration config = null;
            try {
                config = project?.GetLaunchConfigurationOrThrow();
            } catch (NoInterpretersException ex) {
                PythonToolsPackage.OpenNoInterpretersHelpPage(session._serviceProvider, ex.HelpPage);
                return;
            } catch (MissingInterpreterException ex) {
                MessageBox.Show(ex.Message, Strings.ProductTitle);
                return;
            } catch (IOException ex) {
                MessageBox.Show(ex.Message, Strings.ProductTitle);
                return;
            }
            if (config == null) {
                MessageBox.Show(Strings.ProjectInterpreterNotFound.FormatUI(project.GetNameProperty()), Strings.ProductTitle);
                return;
            }

            if (string.IsNullOrEmpty(config.ScriptName)) {
                MessageBox.Show(Strings.NoProjectStartupFile, Strings.ProductTitle);
                return;
            }

            if (string.IsNullOrEmpty(config.WorkingDirectory) || config.WorkingDirectory == ".") {
                config.WorkingDirectory = project.ProjectHome;
                if (string.IsNullOrEmpty(config.WorkingDirectory)) {
                    config.WorkingDirectory = Path.GetDirectoryName(config.ScriptName);
                }
            }

            RunProfiler(session, config, openReport);
        }

        private static void ProfileStandaloneTarget(SessionNode session, StandaloneTarget runTarget, bool openReport) {
            LaunchConfiguration config;
            if (runTarget.PythonInterpreter != null) {
                var registry = session._serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
                var interpreter = registry.FindConfiguration(runTarget.PythonInterpreter.Id);
                if (interpreter == null) {
                    return;
                }
                config = new LaunchConfiguration(interpreter);
            } else {
                config = new LaunchConfiguration(null);
            }

            config.InterpreterPath = runTarget.InterpreterPath;
            config.ScriptName = runTarget.Script;
            config.ScriptArguments = runTarget.Arguments;
            config.WorkingDirectory = runTarget.WorkingDirectory;

            RunProfiler(session, config, openReport);
        }

        private static void RunProfiler(SessionNode session, LaunchConfiguration config, bool openReport) {
            var process = new ProfiledProcess(
                (PythonToolsService)session._serviceProvider.GetService(typeof(PythonToolsService)),
                config.GetInterpreterPath(),
                string.Join(" ", ProcessOutput.QuoteSingleArgument(config.ScriptName), config.ScriptArguments),
                config.WorkingDirectory,
                session._serviceProvider.GetPythonToolsService().GetFullEnvironment(config)
            );

            string baseName = Path.GetFileNameWithoutExtension(session.Filename);
            string date = DateTime.Now.ToString("yyyyMMdd");
            string outPath = Path.Combine(Path.GetTempPath(), baseName + "_" + date + ".vsp");

            int count = 1;
            while (File.Exists(outPath)) {
                outPath = Path.Combine(Path.GetTempPath(), baseName + "_" + date + "(" + count + ").vsp");
                count++;
            }

            process.ProcessExited += (sender, args) => {
                _profilingProcess = null;
                _stopCommand.Enabled = false;
                _startCommand.Enabled = true;
                if (openReport && File.Exists(outPath)) {
                    for (int retries = 10; retries > 0; --retries) {
                        try {
                            using (new FileStream(outPath, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                            break;
                        } catch (IOException) {
                            Thread.Sleep(100);
                        }
                    }

                    ((PythonProfilingPackage)session._serviceProvider).OpenFileAsync(outPath).DoNotWait();
                }
            };

            session.AddProfile(outPath);

            process.StartProfiling(outPath);
            _profilingProcess = process;
            _stopCommand.Enabled = true;
            _startCommand.Enabled = false;
        }

        private async Task OpenFileAsync(string outPath) {
            await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
            var dte = (EnvDTE.DTE)GetService(typeof(EnvDTE.DTE));
            dte.ItemOperations.OpenFile(outPath);
        }

        /// <summary>
        /// This function is the callback used to execute a command when the a menu item is clicked.
        /// See the Initialize method to see how the menu item is associated to this function using
        /// the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private async Task ShowPeformanceExplorerAsync() {
            try {
                await ShowPerformanceExplorerAsync();
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                MessageBox.Show(Strings.ProfilingSupportMissingError, Strings.ProductTitle);
            }
        }

        internal async Task<PerfToolWindow> ShowPerformanceExplorerAsync() {
            if (!IsProfilingInstalled()) {
                throw new InvalidOperationException();
            }

            var windowPane = await GetPerfToolWindowAsync();

            if (!(windowPane is PerfToolWindow pane)) {
                throw new InvalidOperationException();
            }

            if (!(pane.Frame is IVsWindowFrame frame)) {
                throw new InvalidOperationException();
            }

            ErrorHandler.ThrowOnFailure(frame.Show());
            return pane;
        }

        private async Task<WindowPane> GetPerfToolWindowAsync() {
            await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
            var type = typeof(PerfToolWindow);
            return FindWindowPane(type, 0, false) ?? CreateToolWindow(type, 0, this);
        }

        private async Task AddPerformanceSessionAsync() {
            await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
            var dte = (EnvDTE.DTE)GetService(typeof(EnvDTE.DTE));
            var filename = Strings.PerformanceBaseFileName + ".pyperf";
            var save = false;
            if (dte.Solution.IsOpen && !string.IsNullOrEmpty(dte.Solution.FullName)) {
                filename = Path.Combine(Path.GetDirectoryName(dte.Solution.FullName), filename);
                save = true;
            }

            var explorer = await ShowPerformanceExplorerAsync();
            explorer.Sessions.AddTarget(new ProfilingTarget(), filename, save);
        }

        private async Task StartProfilingAsync() {
            var explorer = await ShowPerformanceExplorerAsync();
            explorer.Sessions.StartProfiling();
        }

        private void StopProfiling(object sender, EventArgs e) {
            var process = _profilingProcess;
            if (process != null) {
                process.StopProfiling();
            }
        }

        private void IsProfilingActive(object sender, EventArgs args) {
            var oleMenu = sender as OleMenuCommand;

            if (_profilingProcess != null) {
                oleMenu.Enabled = false;
            } else {
                oleMenu.Enabled = true;
            }
        }

        private void IsProfilingInactive(object sender, EventArgs args) {
            var oleMenu = sender as OleMenuCommand;

            if (_profilingProcess != null) {
                oleMenu.Enabled = true;
            } else {
                oleMenu.Enabled = false;
            }
        }

        internal bool IsProfilingInstalled() {
            IVsShell shell = (IVsShell)GetService(typeof(IVsShell));
            Guid perfGuid = GuidList.GuidPerfPkg;
            int installed;
            ErrorHandler.ThrowOnFailure(
                shell.IsPackageInstalled(ref perfGuid, out installed)
            );
            return installed != 0;
        }

        public bool IsProfiling {
            get {
                return _profilingProcess != null;
            }
        }
    }
}
