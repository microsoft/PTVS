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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Project;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;
using VsMenus = Microsoft.VisualStudioTools.Project.VsMenus;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Represents a package installed in a virtual env as a node in the Solution Explorer.
    /// </summary>
    [ComVisible(true)]
    internal class InterpretersPackageNode : HierarchyNode {
        private static readonly Regex PipFreezeRegex = new Regex(
            "^(?<name>[^=]+)==(?<version>.+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly IEnumerable<string> CannotUninstall = new[] { "pip", "distribute", "virtualenv" };
        protected PythonProjectNode _project;
        private readonly bool _canUninstall;
        private readonly string _caption;
        private readonly string _packageName;

        public InterpretersPackageNode(PythonProjectNode project, string name)
            : base(project, new VirtualProjectElement(project)) {
            ExcludeNodeFromScc = true;
            _packageName = name;
            var match = PipFreezeRegex.Match(name);
            if (match.Success) {
                var namePart = match.Groups["name"].Value;
                _caption = string.Format("{0} ({1})", namePart, match.Groups["version"]);
                _canUninstall = !CannotUninstall.Contains(namePart);
            } else {
                _caption = name;
                _canUninstall = false;
            }
        }

        public override string Url {
            get { return _packageName; }
        }

        public static System.Threading.Tasks.Task InstallNewPackage(InterpretersNode parent) {
            var view = InstallPythonPackage.ShowDialog();
            if (view == null) {
                var tcs = new TaskCompletionSource<object>();
                tcs.SetCanceled();
                return tcs.Task;
            }

            var name = view.Name;

            // don't process events while we're installing, we'll
            // rescan once we're done
            parent.BeginPackageChange();

            var redirector = OutputWindowRedirector.GetGeneral(parent.ProjectMgr.Site);
            var statusBar = (IVsStatusbar)parent.ProjectMgr.Site.GetService(typeof(SVsStatusbar));
            statusBar.SetText(SR.GetString(SR.PackageInstallingSeeOutputWindow, name));

            var task = view.InstallUsingPip ?
                Pip.Install(parent._factory, name, parent.ProjectMgr.Site, view.InstallElevated, redirector) :
                EasyInstall.Install(parent._factory, name, parent.ProjectMgr.Site, view.InstallElevated, redirector);

            return task.ContinueWith(t => {
                if (t.IsCompleted) {
                    bool success = t.Result;

                    statusBar.SetText(SR.GetString(
                        success ? SR.PackageInstallSucceeded : SR.PackageInstallFailed,
                        name));
                }
                parent.PackageChangeDone();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public static System.Threading.Tasks.Task UninstallPackage(InterpretersNode parent, string name) {
            // don't process events while we're installing, we'll
            // rescan once we're done
            parent.BeginPackageChange();

            var redirector = OutputWindowRedirector.GetGeneral(parent.ProjectMgr.Site);
            var statusBar = (IVsStatusbar)parent.ProjectMgr.Site.GetService(typeof(SVsStatusbar));
            statusBar.SetText(SR.GetString(SR.PackageUninstallingSeeOutputWindow, name));

            bool elevate = PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ElevatePip;

            return Pip.Uninstall(parent._factory, name, elevate, redirector).ContinueWith(t => {
                if (t.IsCompleted) {
                    bool success = t.Result;

                    statusBar.SetText(SR.GetString(
                        success ? SR.PackageUninstallSucceeded : SR.PackageUninstallFailed,
                        name));
                }
                parent.PackageChangeDone();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public override Guid ItemTypeGuid {
            get { return PythonConstants.InterpretersPackageItemTypeGuid; }
        }

        public override int MenuCommandId {
            get { return VsMenus.IDM_VS_CTXT_ITEMNODE; }
        }

        public new PythonProjectNode ProjectMgr {
            get {
                return (PythonProjectNode)base.ProjectMgr;
            }
        }

        internal override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation) {
            return _canUninstall && deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject;
        }

        public override void Remove(bool removeFromStorage) {
            if (_canUninstall && !Utilities.IsInAutomationFunction(ProjectMgr.Site)) {
                string message = SR.GetString(SR.UninstallPackage,
                    Caption,
                    Parent._factory.Description,
                    Parent._factory.Configuration.PrefixPath);
                int res = VsShellUtilities.ShowMessageBox(
                    ProjectMgr.Site,
                    string.Empty,
                    message,
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                if (res != 1) {
                    return;
                }
            }
            var task = UninstallPackage(Parent, Url);
        }

        private void RemoveSelf() {
            Parent.RemoveChild(this);
            ProjectMgr.OnInvalidateItems(Parent);

            Parent.PackageChangeDone();
        }

        public new InterpretersNode Parent {
            get {
                return (InterpretersNode)base.Parent;
            }
        }

        /// <summary>
        /// Show the name of the package.
        /// </summary>
        public override string Caption {
            get {
                return _caption;
            }
        }

        /// <summary>
        /// Disable inline editing of Caption of a package node
        /// </summary>
        public override string GetEditLabel() {
            return null;
        }

        public override object GetIconHandle(bool open) {
            return this.ProjectMgr.ImageHandler.GetIconHandle(
                CommonProjectNode.ImageOffset + (int)CommonImageName.InterpretersPackage
            );
        }
        /// <summary>
        /// Package node cannot be dragged.
        /// </summary>
        protected internal override string PrepareSelectedNodesForClipBoard() {
            return null;
        }

        /// <summary>
        /// Package node cannot be excluded.
        /// </summary>
        internal override int ExcludeFromProject() {
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        internal override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == VsMenus.guidStandardCommandSet97) {
                switch ((VsCommands)cmd) {
                    case VsCommands.Copy:
                    case VsCommands.Cut:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.INVISIBLE;
                        return VSConstants.S_OK;
                    case VsCommands.Delete:
                        if (!_canUninstall) {
                            // If we can't uninstall the package, still show the
                            // item but disable it. Otherwise, let the default
                            // query handle it, which will display "Remove".
                            result |= QueryStatusResult.SUPPORTED;
                            return VSConstants.S_OK;
                        }
                        break;
                }
            }
            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        /// <summary>
        /// Defines whether this node is valid node for painting the package icon.
        /// </summary>
        /// <returns></returns>
        protected override bool CanShowDefaultIcon() {
            return true;
        }

        public override bool CanAddFiles {
            get {
                return false;
            }
        }

        protected override NodeProperties CreatePropertiesObject() {
            return new InterpretersPackageNodeProperties(this);
        }
    }
}
