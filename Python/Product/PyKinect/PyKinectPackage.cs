/* 
 * ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * Available under the Microsoft PyKinect 1.0 Alpha license.  See LICENSE.txt
 * for more information.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;

namespace Microsoft.Samples {
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
    // This attribute is used to register the informations needed to show the this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", AssemblyVersionInfo.Version, IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids.NoSolution)]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids.SolutionExists)]
    [Guid(GuidList.guidPyKinectPkgString)]
    internal sealed class PyKinectPackage : Package {

        private static PyKinectPackage Instance = null;

        public PyKinectPackage() {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
            Instance = this;
        }

        /////////////////////////////////////////////////////////////////////////////
        // Overriden Package Implementation
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initilaization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize() {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            // this is ugly but we need to force PTVS to be loaded so we can call into it...
            // If we don't then in a per-user install we won't be able to load MS.PT.Analysis.dll
            var shell = (IVsShell)GetService(typeof(SVsShell));
            Guid ptvsPackage = new Guid("6dbd7c1e-1f1b-496d-ac7c-c55dae66c783");
            IVsPackage package;
            if (!ErrorHandler.Failed(shell.LoadPackage(ref ptvsPackage, out package))) {
                RegisterPerInterpreterCommands();
            }
        }

        private readonly List<MenuCommand> _interpCommands = new List<MenuCommand>();

        private void RegisterPerInterpreterCommands() {
            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs) {
                var interpService = ComponentModel.GetService<IInterpreterOptionsService>();
                var factories = interpService.Interpreters.ToList();
                var defaultFactory = interpService.DefaultInterpreter;
                factories.Remove(defaultFactory);
                factories.Insert(0, defaultFactory);
                interpService.InterpretersChanged += RefreshPerInterpreterCommands;

                for (var i = 0; i < factories.Count && i < (PkgCmdIDList.cmdidInstallPyKinectF - PkgCmdIDList.cmdidInstallPyKinect0); i++) {
                    // Create the command for the menu item.
                    var cmd = new PyKinectCommand(factories[i]);

                    CommandID menuCommandID = new CommandID(GuidList.guidPyKinectCmdSet, (int)PkgCmdIDList.cmdidInstallPyKinect0 + i);
                    var menuItem = new OleMenuCommand(cmd.Invoke, menuCommandID);
                    menuItem.BeforeQueryStatus += cmd.QueryStatusMethod;
                    mcs.AddCommand(menuItem);
                    _interpCommands.Add(menuItem);
                }
            }
        }

        private void RefreshPerInterpreterCommands(object sender, EventArgs args) {
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            foreach(var command in _interpCommands) {
                mcs.RemoveCommand(command);
            }

            RegisterPerInterpreterCommands();
        }

        abstract class PerInterpreterCommand {
            public readonly IPythonInterpreterFactory Factory;

            public PerInterpreterCommand(IPythonInterpreterFactory factory) {
                Factory = factory;
            }

            public void QueryStatusMethod(object sender, EventArgs args) {
                var oleMenu = sender as OleMenuCommand;

                if (Factory == null || Factory.Configuration.Version.Major != 2 || Factory.Configuration.Version.Minor != 7) {
                    oleMenu.Visible = false;
                    oleMenu.Enabled = false;
                    oleMenu.Supported = false;
                } else {
                    oleMenu.Visible = true;
                    oleMenu.Enabled = true;
                    oleMenu.Supported = true;
                    oleMenu.Text = Description;
                }
            }

            public abstract string Description {
                get;
            }

            protected void InstallSample(string sampleName) {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.UseShellExecute = true;
                startInfo.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                startInfo.Arguments = String.Format("/c \"cd \"{2}\" & \"{0}\" \"{1}\" install\" & pause",
                    Factory.Configuration.InterpreterPath,
                    Path.Combine(GetPythonToolsInstallPath(), sampleName, "setup.py"),
                    Path.Combine(GetPythonToolsInstallPath(), sampleName));
                startInfo.Verb = "runas";

                try {
                    Process p = Process.Start(startInfo);
                } catch (System.ComponentModel.Win32Exception) {
                }
            }

        }

        class PyKinectCommand : PerInterpreterCommand {
            public PyKinectCommand(IPythonInterpreterFactory factory)
                : base(factory) {
            }

            public override string Description {
                get {
                    return "Install PyKinect into " + Factory.Description;
                }
            }

            public void Invoke(object sender, EventArgs args) {
                var prompt = new InstallPrompt("PyKinect", Factory.Description);
                if (prompt.ShowDialog() == DialogResult.OK) {
                    InstallSample("PyKinect");
                }
            }

        }

        public static IComponentModel ComponentModel {
            get {
                return (IComponentModel)GetGlobalService(typeof(SComponentModel));
            }
        }

        #endregion

        internal static string GetPythonToolsInstallPath() {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

    }
}
