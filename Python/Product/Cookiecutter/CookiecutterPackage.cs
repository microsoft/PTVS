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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.CookiecutterTools.Commands;
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.CookiecutterTools.Telemetry;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.CookiecutterTools {
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
    [ProvideToolWindow(typeof(CookiecutterToolWindow), Style = VsDockStyle.Linked, Window = ToolWindowGuids80.ServerExplorer)]
    [ProvideOptionPage(typeof(CookiecutterOptionPage), "Cookiecutter", "General", 113, 114, true)]
    [ProvideProfileAttribute(typeof(CookiecutterOptionPage), "Cookiecutter", "General", 113, 114, isToolsOptionPage: true, DescriptionResourceID = 115)]
    [Guid(PackageGuids.guidCookiecutterPkgString)]
    public sealed class CookiecutterPackage : Package {
        internal static CookiecutterPackage Instance;

        private static readonly object _commandsLock = new object();
        private static readonly Dictionary<Command, MenuCommand> _commands = new Dictionary<Command, MenuCommand>();

        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public CookiecutterPackage() {
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

            UIThread.EnsureService(this);

            CookiecutterTelemetry.Initialize();

            RegisterCommands(new Command[] {
                new CookiecutterExplorerCommand(),
                new CreateFromCookiecutterCommand(),
                new AddFromCookiecutterCommand(),
            }, PackageGuids.guidCookiecutterCmdSet);
        }

        protected override void Dispose(bool disposing) {
            CookiecutterTelemetry.Current.Dispose();

            base.Dispose(disposing);
        }

        #endregion

        public static T GetGlobalService<S, T>() where T : class {
            object service = Package.GetGlobalService(typeof(S));
            return service as T;
        }

        internal new object GetService(Type serviceType) {
            return base.GetService(serviceType);
        }

        public EnvDTE80.DTE2 DTE {
            get {
                return GetGlobalService<EnvDTE.DTE, EnvDTE80.DTE2>();
            }
        }

        internal static void ShowContextMenu(CommandID commandId, int x, int y, IOleCommandTarget commandTarget) {
            var shell = CookiecutterPackage.GetGlobalService(typeof(SVsUIShell)) as IVsUIShell;
            var pts = new POINTS[1];
            pts[0].x = (short)x;
            pts[0].y = (short)y;
            shell.ShowContextMenu(0, commandId.Guid, commandId.ID, pts, commandTarget);
        }

        internal WindowPane ShowWindowPane(Type windowType, bool focus) {
            var window = FindWindowPane(windowType, 0, true) as ToolWindowPane;
            if (window != null) {
                var frame = window.Frame as IVsWindowFrame;
                if (frame != null) {
                    ErrorHandler.ThrowOnFailure(frame.Show());
                }

                if (focus) {
                    var content = window.Content as System.Windows.UIElement;
                    if (content != null) {
                        content.Focus();
                    }
                }
            }

            return window;
        }

        internal void NewCookiecutterSession(string targetFolder = null, string targetProjectUniqueName = null) {
            var pane = ShowWindowPane(typeof(CookiecutterToolWindow), true) as CookiecutterToolWindow;
            pane.NewSession(targetFolder, targetProjectUniqueName);
        }

        internal void RegisterCommands(IEnumerable<Command> commands, Guid cmdSet) {
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs) {
                lock (_commandsLock) {
                    foreach (var command in commands) {
                        var beforeQueryStatus = command.BeforeQueryStatus;
                        CommandID toolwndCommandID = new CommandID(cmdSet, command.CommandId);
                        OleMenuCommand menuToolWin = new OleMenuCommand(command.DoCommand, toolwndCommandID);
                        if (beforeQueryStatus != null) {
                            menuToolWin.BeforeQueryStatus += beforeQueryStatus;
                        }
                        mcs.AddCommand(menuToolWin);
                        _commands[command] = menuToolWin;
                    }
                }
            }
        }

        internal string RecommendedFeed {
            get {
                var page = (CookiecutterOptionPage)GetDialogPage(typeof(CookiecutterOptionPage));
                return page.FeedUrl;
            }
        }

        internal bool ShowHelp {
            get {
                var page = (CookiecutterOptionPage)GetDialogPage(typeof(CookiecutterOptionPage));
                return page.ShowHelp;
            }

            set {
                var page = (CookiecutterOptionPage)GetDialogPage(typeof(CookiecutterOptionPage));
                page.ShowHelp = value;
                page.SaveSettingsToStorage();
            }
        }

        internal bool CheckForTemplateUpdate {
            get {
                var page = (CookiecutterOptionPage)GetDialogPage(typeof(CookiecutterOptionPage));
                return page.CheckForTemplateUpdate;
            }

            set {
                var page = (CookiecutterOptionPage)GetDialogPage(typeof(CookiecutterOptionPage));
                page.CheckForTemplateUpdate = value;
                page.SaveSettingsToStorage();
            }
        }
    }
}
