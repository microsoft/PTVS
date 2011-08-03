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
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

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
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [Description("Python Tools Profiling Package")]
    // This attribute is used to register the informations needed to show the this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidPythonProfilingPkgString)]
    // set the window to dock where Toolbox/Performance Explorer dock by default
    [ProvideToolWindow(typeof(PerfToolWindow), Orientation = ToolWindowOrientation.Left, Style = VsDockStyle.Tabbed, Window = EnvDTE.Constants.vsWindowKindToolbox)]
    [ProvideFileFilterAttribute("{81da0100-e6db-4783-91ea-c38c3fa1b81e}", "/1", "Python Performance Session (*.pyperf);*.pyperf", 100)]
    [ProvideEditorExtension(typeof(ProfilingSessionEditorFactory), ".pyperf", 50,
          ProjectGuid = "{81da0100-e6db-4783-91ea-c38c3fa1b81e}",
          NameResourceID = 105,         
          DefaultName = "PythonPerfSession")]
    [ProvideAutomationObject("PythonProfiling")]
    sealed class PythonProfilingPackage : Package {
        internal static PythonProfilingPackage Instance;
        private static ProfiledProcess _profilingProcess;   // process currently being profiled
        internal static string PythonProjectGuid = "{888888a0-9f3d-457c-b088-3a5042f75d52}";
        internal const string PerformanceFileFilter = "Performance Report Files|*.vsp;*.vsps";
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

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initilaization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize() {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs) {
                // Create the command for the menu item.
                CommandID menuCommandID = new CommandID(GuidList.guidPythonProfilingCmdSet, (int)PkgCmdIDList.cmdidStartPythonProfiling);
                MenuCommand menuItem = new MenuCommand(StartProfilingWizard, menuCommandID);
                mcs.AddCommand(menuItem);

                // Create the command for the menu item.
                menuCommandID = new CommandID(GuidList.guidPythonProfilingCmdSet, (int)PkgCmdIDList.cmdidPerfExplorer);
                menuItem = new MenuCommand(ShowPeformanceExplorer, menuCommandID);
                mcs.AddCommand(menuItem);

                menuCommandID = new CommandID(GuidList.guidPythonProfilingCmdSet, (int)PkgCmdIDList.cmdidAddPerfSession);
                menuItem = new MenuCommand(AddPerformanceSession, menuCommandID);
                mcs.AddCommand(menuItem);

                menuCommandID = new CommandID(GuidList.guidPythonProfilingCmdSet, (int)PkgCmdIDList.cmdidStartProfiling);
                var oleMenuItem = _startCommand = new OleMenuCommand(StartProfiling, menuCommandID);
                oleMenuItem.BeforeQueryStatus += IsProfilingActive;
                mcs.AddCommand(oleMenuItem);

                menuCommandID = new CommandID(GuidList.guidPythonProfilingCmdSet, (int)PkgCmdIDList.cmdidStopProfiling);
                _stopCommand = oleMenuItem = new OleMenuCommand(StopProfiling, menuCommandID);
                oleMenuItem.BeforeQueryStatus += IsProfilingInactive;                
            
                mcs.AddCommand(oleMenuItem);
            }

            //Create Editor Factory. Note that the base Package class will call Dispose on it.
            base.RegisterEditorFactory(new ProfilingSessionEditorFactory(this));
        }

        protected override object GetAutomationObject(string name) {
            if (name == "PythonProfiling") {
                if (_profilingAutomation == null) {
                    var pane = (PerfToolWindow)this.FindToolWindow(typeof(PerfToolWindow), 0, true);
                    _profilingAutomation  = new AutomationProfiling(pane.Sessions);
                }
                return _profilingAutomation;
            }

            return base.GetAutomationObject(name);
        }

        /// <summary>
        /// This function is the callback used to execute a command when the a menu item is clicked.
        /// See the Initialize method to see how the menu item is associated to this function using
        /// the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void StartProfilingWizard(object sender, EventArgs e) {
            var profiling = new LaunchProfiling();
            var res = profiling.ShowDialog();
            if (res != null && res.Value) {
                var target = profiling.Target;

                ProfileTarget(target);
            }
        }

        internal SessionNode ProfileTarget(ProfilingTarget target, bool openReport = true) {
            bool save;
            string name = target.GetProfilingName(out save);
            var session = ShowPerformanceExplorer().Sessions.AddTarget(target, name, save);

            StartProfiling(target, session, openReport);
            return session;
        }

        internal void StartProfiling(ProfilingTarget target, SessionNode session, bool openReport = true) {
            if (target.ProjectTarget != null) {
                ProfileProjectTarget(session, target.ProjectTarget, openReport);
            } else if (target.StandaloneTarget != null) {
                ProfileStandaloneTarget(session, target.StandaloneTarget, openReport);
            } else {
                if (MessageBox.Show("Profiling session is not configured - would you like to configure now and then launch?", "No Profiling Target", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    var newTarget = session.OpenTargetProperties();
                    if (newTarget != null && (newTarget.ProjectTarget != null || newTarget.StandaloneTarget != null)) {
                        StartProfiling(newTarget, session, openReport);
                    }
                }
            }
        }

        private void ProfileProjectTarget(SessionNode session, ProjectTarget projectTarget, bool openReport) {
            var targetGuid = projectTarget.TargetProject;

            var dte = (EnvDTE.DTE)GetGlobalService(typeof(EnvDTE.DTE));
            EnvDTE.Project projectToProfile = null;
            foreach (EnvDTE.Project project in dte.Solution.Projects) {
                var kind = project.Kind;
                
                if (String.Equals(kind, PythonProfilingPackage.PythonProjectGuid, StringComparison.OrdinalIgnoreCase)) {
                    var guid = project.Properties.Item("Guid").Value as string;

                    Guid guidVal;
                    if (Guid.TryParse(guid, out guidVal) && guidVal == projectTarget.TargetProject) {
                        projectToProfile = project;
                        break;
                    }
                }
            }

            if (projectToProfile != null) {
                ProfileProject(session, projectToProfile, openReport);
            } else {
                MessageBox.Show("Project could not be found in current solution.", "Python Tools for Visual Studio");
            }
        }

        internal static void ProfileProject(SessionNode session, EnvDTE.Project projectToProfile, bool openReport) {
            var interpreterId = (string)projectToProfile.Properties.Item("InterpreterId").Value;
            var interpreterVersion = (string)projectToProfile.Properties.Item("InterpreterVersion").Value;
            var args = (string)projectToProfile.Properties.Item("CommandLineArguments").Value;
            var interpreterPath = (string)projectToProfile.Properties.Item("InterpreterPath").Value;
            var searchPath = (string)projectToProfile.Properties.Item("SearchPath").Value;
            string interpreter;

            Guid intGuid;
            Version intVersion;
            ProcessorArchitecture arch;
            string searchPathEnvVarName;
            if (!Guid.TryParse(interpreterId, out intGuid) ||
                !Version.TryParse(interpreterVersion, out intVersion) ||
                !TryGetInterpreter(intVersion, intGuid, out interpreter, out searchPathEnvVarName, out arch)) {
                MessageBox.Show(String.Format("Could not find interpreter for project {0}", projectToProfile.Name));
                return;
            }

            if (!String.IsNullOrWhiteSpace(interpreterPath)) {
                interpreter = interpreterPath;
            }

            string startupFile = (string)projectToProfile.Properties.Item("StartupFile").Value;
            if (String.IsNullOrEmpty(startupFile)) {
                MessageBox.Show("Project has no configured startup file, cannot start profiling.", "Python Tools for Visual Studio");
                return;
            }

            string workingDir = projectToProfile.Properties.Item("WorkingDirectory").Value as string;
            if (String.IsNullOrEmpty(workingDir) || workingDir == ".") {
                workingDir = Path.GetDirectoryName(projectToProfile.FullName);
            }

            var env = new Dictionary<string, string>();
            if (!String.IsNullOrWhiteSpace(searchPath) && !String.IsNullOrWhiteSpace(searchPathEnvVarName)) {
                env[searchPathEnvVarName] = searchPath;
            }
            RunProfiler(session, interpreter, startupFile, args, workingDir, env, openReport, arch);
        }

        private static void ProfileStandaloneTarget(SessionNode session, StandaloneTarget runTarget, bool openReport) {
            string interpreter;
            ProcessorArchitecture arch;
            if (runTarget.PythonInterpreter != null) {

                Version selectedVersion = new Version(runTarget.PythonInterpreter.Version);
                Guid interpreterId = runTarget.PythonInterpreter.Id;
                string searchPathEnv;
                if (!TryGetInterpreter(selectedVersion, interpreterId, out interpreter, out searchPathEnv, out arch)) {
                    return;
                }
            } else {
                interpreter = runTarget.InterpreterPath;
                arch = ProcessorArchitecture.X86;
            }

            RunProfiler(session, interpreter, runTarget.Script, runTarget.Arguments, runTarget.WorkingDirectory, null, openReport, arch);
        }

        private static bool TryGetInterpreter(Version selectedVersion, Guid interpreterId, out string interpreter, out string searchPathEnv, out ProcessorArchitecture architecture) {
            interpreter = null;
            architecture = ProcessorArchitecture.None;
            searchPathEnv = null;
            var service = (IComponentModel)(GetGlobalService(typeof(SComponentModel)));
            var factoryProviders = service.GetExtensions<IPythonInterpreterFactoryProvider>();
            
            foreach (var factProvider in factoryProviders) {
                foreach (var fact in factProvider.GetInterpreterFactories()) {
                    if (fact.Id == interpreterId &&
                        fact.Configuration.Version == selectedVersion) {
                        interpreter = fact.Configuration.InterpreterPath;
                        architecture = fact.Configuration.Architecture;
                        searchPathEnv = fact.Configuration.PathEnvironmentVariable;
                        break;
                    }
                }
            }
            if (interpreter == null) {
                MessageBox.Show("Could not find selected interpreter", "Python Tools for Visual Studio");
                return false;
            }
            return true;
        }


        private static void RunProfiler(SessionNode session, string interpreter, string script, string arguments, string workingDir, Dictionary<string, string> env, bool openReport, ProcessorArchitecture arch) {
            var process = new ProfiledProcess(interpreter, String.Format("\"{0}\" {1}", script, arguments), workingDir, env, arch);

            string baseName = Path.GetFileNameWithoutExtension(session.Filename);
            string date = DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString() + DateTime.Now.Day.ToString();
            string outPath = Path.Combine(Path.GetTempPath(), baseName + date + ".vsp");

            int count = 1;
            while (File.Exists(outPath)) {
                outPath = Path.Combine(Path.GetTempPath(), baseName + date + "(" + count + ").vsp");
                count++;
            }
            
            process.ProcessExited += (sender, args) => {
                var dte = (EnvDTE.DTE)PythonProfilingPackage.GetGlobalService(typeof(EnvDTE.DTE));
                _profilingProcess = null;
                _stopCommand.Enabled = false;
                _startCommand.Enabled = true;
                if (openReport && File.Exists(outPath)) {                    
                    dte.ItemOperations.OpenFile(outPath);
                }
            };

            session.AddProfile(outPath);
            
            process.StartProfiling(outPath);
            _profilingProcess = process;
            _stopCommand.Enabled = true;
            _startCommand.Enabled = false;
        }

        /// <summary>
        /// This function is the callback used to execute a command when the a menu item is clicked.
        /// See the Initialize method to see how the menu item is associated to this function using
        /// the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void ShowPeformanceExplorer(object sender, EventArgs e) {
            ShowPerformanceExplorer();
        }

        internal PerfToolWindow ShowPerformanceExplorer() {
            var pane = this.FindToolWindow(typeof(PerfToolWindow), 0, true);
            if (pane == null) {
                throw new InvalidOperationException();
            }
            IVsWindowFrame frame = pane.Frame as IVsWindowFrame;
            if (frame == null) {
                throw new InvalidOperationException();
            }

            ErrorHandler.ThrowOnFailure(frame.Show());
            return pane as PerfToolWindow;
        }

        private void AddPerformanceSession(object sender, EventArgs e) {
            var dte = (EnvDTE.DTE)PythonToolsPackage.GetGlobalService(typeof(EnvDTE.DTE));
            string filename = "Performance.pyperf";
            bool save = false;
            if (dte.Solution.IsOpen && !String.IsNullOrEmpty(dte.Solution.FullName)) {
                filename = Path.Combine(Path.GetDirectoryName(dte.Solution.FullName), filename);
                save = true;
            }
            ShowPerformanceExplorer().Sessions.AddTarget(new ProfilingTarget(), filename, save);
        }

        private void StartProfiling(object sender, EventArgs e) {
            ShowPerformanceExplorer().Sessions.StartProfiling();
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

        public bool IsProfiling {
            get {
                return _profilingProcess != null;
            }
        }
    }
}
