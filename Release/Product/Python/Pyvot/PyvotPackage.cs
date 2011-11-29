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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;

namespace Microsoft.PythonTools.Pyvot {
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
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(CommonConstants.UIContextNoSolution)]  // we need to auto-load so we can respond to QueryStatus for our commands
    [ProvideAutoLoad(CommonConstants.UIContextSolutionExists)]
    [Guid(GuidList.guidPyvotPkgString)]
    internal sealed class PyvotPackage : Package {

        private static PyvotPackage Instance = null;

        public PyvotPackage() {
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
                var factories = ComponentModel.GetAllPythonInterpreterFactories();
                var defaultFactory = factories.GetDefaultInterpreter();
                // sort so default always comes first, and otherwise in sorted order
                Array.Sort(factories, (x, y) => {
                    if (x == y) {
                        return 0;
                    } else if (x == defaultFactory) {
                        return -1;
                    } else if (y == defaultFactory) {
                        return 1;
                    } else {
                        return String.Compare(x.GetInterpreterDisplay(), y.GetInterpreterDisplay());
                    }
                });

                PythonToolsPackage.InterpretersChanged += RefreshPerInterpreterCommands;

                for (var i = 0; i < factories.Length && i < (PkgCmdIDList.cmdidInstallPyvotF - PkgCmdIDList.cmdidInstallPyvot0); i++) {
                    // Create the command for the menu item.
                    var cmd = new PyvotCommand(factories[i]);

                    CommandID menuCommandID = new CommandID(GuidList.guidPyvotCmdSet, (int)PkgCmdIDList.cmdidInstallPyvot0 + i);
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

                var supportedPy2 = Factory.Configuration.Version.Major == 2 && (Factory.Configuration.Version.Minor == 7 || Factory.Configuration.Version.Minor == 6);
                var supportedPy3 = Factory.Configuration.Version.Major == 3 && Factory.Configuration.Version.Minor >= 2;

                if (Factory == null || (!supportedPy2 && !supportedPy3)) {
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
                startInfo.WorkingDirectory = Path.Combine(GetPythonToolsInstallPath(), sampleName);
                startInfo.FileName = Factory.Configuration.InterpreterPath;

                startInfo.Arguments = String.Format("\"{0}\" --no-downloads install", Path.Combine(GetPythonToolsInstallPath(), sampleName, "setup.py"));
                startInfo.Verb = "runas";

                try {
                    Process p = Process.Start(startInfo);
                } catch (System.ComponentModel.Win32Exception) {
                }
            }

        }

        class PyvotCommand : PerInterpreterCommand {
            public PyvotCommand(IPythonInterpreterFactory factory)
                : base(factory) {
            }

            public override string Description {
                get {
                    return "Install Pyvot into " + Factory.GetInterpreterDisplay();
                }
            }

            public void Invoke(object sender, EventArgs args) {
                var prompt = new InstallPrompt("Pyvot", Factory.GetInterpreterDisplay());
                if (prompt.ShowDialog() == DialogResult.OK) {
                    InstallSample("Pyvot");
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
