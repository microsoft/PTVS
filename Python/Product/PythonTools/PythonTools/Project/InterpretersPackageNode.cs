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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Project;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using VsMenus = Microsoft.VisualStudioTools.Project.VsMenus;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Represents a package installed in a virtual env as a node in the Solution Explorer.
    /// </summary>
    [ComVisible(true)]
    internal class InterpretersPackageNode : HierarchyNode {
        protected PythonProjectNode _project;
        private string _caption;

        public InterpretersPackageNode(PythonProjectNode project, string name)
            : base(project, new VirtualProjectElement(project)) {
            _caption = name;
        }

        public override string Url {
            get { return _caption; }
        }

        public override Guid ItemTypeGuid {
            get { return VSConstants.GUID_ItemType_VirtualFolder; }
        }

        public override int MenuCommandId {
            get { return VsMenus.IDM_VS_CTXT_ITEMNODE; }
        }

        internal override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmd) {
                    case PythonConstants.UninstallPythonPackage:
                        string message = string.Format("'{0}' will be uninstalled.", Caption);
                        int res = VsShellUtilities.ShowMessageBox(
                            ProjectMgr.Site, 
                            string.Empty,
                            message,
                            OLEMSGICON.OLEMSGICON_WARNING,
                            OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                        if (res == 1) {
                            Remove(false);
                        }
                        return VSConstants.S_OK;
                }
            }
            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }

        public new PythonProjectNode ProjectMgr {
            get {
                return (PythonProjectNode)base.ProjectMgr;
            }
        }

        /// <summary>
        /// Package nodes are removed by "uninstalling" not deleting.  This is
        /// mainly because we don't get all of the delete notifications from
        /// solution navigator, so we can't show our improved prompt.
        /// </summary>
        internal override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation) {
            return false;
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
            statusBar.SetText("Installing " + name + ". See Output Window for more details.");

            return Pip.Install(parent._factory, name, parent.ProjectMgr.Site, redirector).ContinueWith(t => {
                if (t.IsCompleted) {
                    bool success = t.Result;

                    var msg = string.Format(success ?
                        "Successfully installed {0}" :
                        "Failed to install {0}",
                        name);
                    statusBar.SetText(msg);
                    redirector.WriteLine(msg);
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
            statusBar.SetText("Uninstalling " + name + ". See Output Window for more details.");

            return Pip.Uninstall(parent._factory, name, redirector).ContinueWith(t => {
                if (t.IsCompleted) {
                    bool success = t.Result;

                    var msg = string.Format(success ?
                        "Successfully uninstalled {0}" :
                        "Failed to uninstall {0}",
                        name);
                    statusBar.SetText(msg);
                    redirector.WriteLine(msg);
                }
                parent.PackageChangeDone();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public override void Remove(bool removeFromStorage) {
            var task = UninstallPackage(Parent, Caption);
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
        /// Disable inline editing of Caption of a virtual env package node
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
        /// Virtual env node cannot be dragged.
        /// </summary>        
        protected internal override string PrepareSelectedNodesForClipBoard() {
            return null;
        }

        /// <summary>
        /// Virtual env Node cannot be excluded.
        /// </summary>
        internal override int ExcludeFromProject() {
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        /// <summary>
        /// Disable Copy/Cut/Paste commands on Search Path node.
        /// </summary>
        internal override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmd) {
                    case PythonConstants.UninstallPythonPackage:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
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
