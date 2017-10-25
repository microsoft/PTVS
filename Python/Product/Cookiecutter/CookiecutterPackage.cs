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
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.CookiecutterTools.Model;
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
    public sealed class CookiecutterPackage : Package, IOleCommandTarget {
        internal static CookiecutterPackage Instance;

        private static readonly object _commandsLock = new object();
        private static readonly Dictionary<Command, MenuCommand> _commands = new Dictionary<Command, MenuCommand>();

        private ProjectSystemClient _projectSystem;

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

            _projectSystem = new ProjectSystemClient(DTE);

            CookiecutterTelemetry.Initialize();
        }

        protected override void Dispose(bool disposing) {
            CookiecutterTelemetry.Current.Dispose();

            base.Dispose(disposing);
        }

        #endregion

        #region IOleCommandTarget

        int IOleCommandTarget.Exec(ref Guid commandGroup, uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut) {
            if (commandGroup != PackageGuids.guidCookiecutterCmdSet) {
                return VSConstants.E_FAIL;
            }

            // Commands that support parameters cannot be implemented via IMenuCommandService
            var hr = VSConstants.S_OK;
            switch (commandId) {
                case PackageIds.cmdidViewExternalWebBrowser:
                    hr = ViewExternalBrowser(variantIn, variantOut, commandExecOpt);
                    break;
                case PackageIds.cmdidCookiecutterExplorer:
                    ShowWindowPane(typeof(CookiecutterToolWindow), true);
                    break;
                case PackageIds.cmdidCreateFromCookiecutter:
                    NewCookiecutterSession();
                    break;
                case PackageIds.cmdidAddFromCookiecutter:
                    NewCookiecutterSession(new CookiecutterSessionStartInfo(_projectSystem.GetSelectedFolderProjectLocation()));
                    break;
                case PackageIds.cmdidNewProjectFromTemplate:
                    hr = NewProjectFromTemplate(variantIn, variantOut, commandExecOpt);
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }

            return hr;
        }

        int IOleCommandTarget.QueryStatus(ref Guid commandGroup, uint commandsCount, OLECMD[] commands, IntPtr pCmdText) {
            if (commandGroup == PackageGuids.guidCookiecutterCmdSet && commandsCount == 1) {
                switch (commands[0].cmdID) {
                    case PackageIds.cmdidAddFromCookiecutter:
                        commands[0].cmdf = DTE.Debugger.CurrentMode == EnvDTE.dbgDebugMode.dbgDesignMode &&
                                           _projectSystem.GetSelectedFolderProjectLocation() != null
                            ? (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED)
                            : (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED);
                        break;
                    default:
                        commands[0].cmdf = 0;
                        break;
                }
                return VSConstants.S_OK;
            }
            return VSConstants.E_FAIL;
        }

        #endregion 

        private string GetStringArgument(IntPtr variantIn) {
            if (variantIn == IntPtr.Zero) {
                return null;
            }

            var obj = Marshal.GetObjectForNativeVariant(variantIn);
            return obj as string;
        }

        /// <summary>
        /// Used to determine if the shell is querying for the parameter list of our command.
        /// </summary>
        private static bool IsQueryParameterList(IntPtr variantIn, IntPtr variantOut, uint nCmdexecopt) {
            ushort lo = (ushort)(nCmdexecopt & (uint)0xffff);
            ushort hi = (ushort)(nCmdexecopt >> 16);
            if (lo == (ushort)OLECMDEXECOPT.OLECMDEXECOPT_SHOWHELP) {
                if (hi == VsMenus.VSCmdOptQueryParameterList) {
                    if (variantOut != IntPtr.Zero) {
                        return true;
                    }
                }
            }

            return false;
        }

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

        internal void NewCookiecutterSession(CookiecutterSessionStartInfo ssi = null) {
            var pane = ShowWindowPane(typeof(CookiecutterToolWindow), true) as CookiecutterToolWindow;
            pane.NewSession(ssi);
        }

        private int ViewExternalBrowser(IntPtr variantIn, IntPtr variantOut, uint commandExecOpt) {
            if (IsQueryParameterList(variantIn, variantOut, commandExecOpt)) {
                Marshal.GetNativeVariantForObject("url", variantOut);
                return VSConstants.S_OK;
            }

            var name = GetStringArgument(variantIn) ?? "";
            var uri = name.Trim('"');

            if (File.Exists(uri)) {
                var ext = Path.GetExtension(uri);
                if (string.Compare(ext, ".htm", StringComparison.CurrentCultureIgnoreCase) == 0 ||
                    string.Compare(ext, ".html", StringComparison.CurrentCultureIgnoreCase) == 0) {
                    Process.Start(uri)?.Dispose();
                }
            } else {
                Uri u;
                if (Uri.TryCreate(uri, UriKind.Absolute, out u)) {
                    if (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps) {
                        Process.Start(uri)?.Dispose();
                    }
                }
            }

            return VSConstants.S_OK;
        }

        private int NewProjectFromTemplate(IntPtr variantIn, IntPtr variantOut, uint commandExecOpt) {
            if (IsQueryParameterList(variantIn, variantOut, commandExecOpt)) {
                Marshal.GetNativeVariantForObject("url", variantOut);
                return VSConstants.S_OK;
            }

            var name = GetStringArgument(variantIn) ?? "";
            var args = name.Split('|');
            if (args.Length != 3) {
                return VSConstants.E_FAIL;
            }

            var projectName = args[0];
            var targetFolder = args[1];
            var templateUri = args[2].Trim('"');
            var projectFolder = Path.Combine(targetFolder, projectName);

            NewCookiecutterSession(
                new CookiecutterSessionStartInfo(projectName, projectFolder, templateUri)
            );

            return VSConstants.S_OK;
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
