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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Represents virtual env as a node in the Solution Explorer.
    /// </summary>
    [ComVisible(true)]
    internal class VirtualEnvNode : HierarchyNode {
        private readonly string _caption;
        private readonly FileSystemWatcher _fileWatcher;
        private readonly Timer _timer;
        private readonly TaskScheduler _scheduler;
        private bool _checkedItems, _disposed;

        public VirtualEnvNode(PythonProjectNode project, ProjectItem item)
            : base(project, new MsBuildProjectElement(project, item)) {
            _caption = Path.GetFileName(item.EvaluatedInclude);
            _scheduler = TaskScheduler.FromCurrentSynchronizationContext();

            _fileWatcher = new FileSystemWatcher(CommonUtils.GetAbsoluteDirectoryPath(project.ProjectHome, item.EvaluatedInclude), "*");
            _fileWatcher.IncludeSubdirectories = true;
            _fileWatcher.Deleted += PackagesChanged;
            _fileWatcher.Created += PackagesChanged;
            _fileWatcher.EnableRaisingEvents = true;
            _timer = new Timer(CheckPackages);
            IsExpanded = false;
        }

        public override void Close() {
            lock (this) {
                _fileWatcher.Dispose();
                _timer.Dispose();
                _disposed = true;
            }

            base.Close();
        }

        private void PackagesChanged(object sender, FileSystemEventArgs e) {
            // have a delay before refreshing because there's probably more than one write,
            // so we wait until things have been quite for a second.
            _timer.Change(1000, Timeout.Infinite);
        }

        private void CheckPackages(object arg) {
            bool prevChecked = _checkedItems;
            _checkedItems = true;
            if (!File.Exists(InterpreterPath)) {
                return;
            }

            Process process;
            try {
                process = Process.Start(MakePipCommand("freeze"));
            } catch (Exception e) {
                // race with interpreter being deleted?  other failure?  
                Debug.WriteLine(e);
                return;
            }

            StringBuilder packages = new StringBuilder();
            process.OutputDataReceived += (sender, args) => {
                packages.Append(args.Data + Environment.NewLine);
            };
            process.BeginOutputReadLine();
            process.WaitForExit();

            var lines = new HashSet<string>(packages.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));

            _scheduler.StartNew(() => {
                if (ProjectMgr == null || ProjectMgr.IsClosed) {
                    return;
                }

                Dictionary<string, HierarchyNode> existing = new Dictionary<string, HierarchyNode>();
                for (var child = FirstChild; child != null; child = child.NextSibling) {
                    existing[child.Caption] = child;
                }

                // remove the nodes which were uninstalled.
                foreach (var keyValue in existing) {
                    if (!lines.Contains(keyValue.Key)) {
                        RemoveChild(keyValue.Value);
                    }
                }

                // remove already existing nodes so we don't add them a 2nd time
                lines.ExceptWith(existing.Keys);

                // add the new nodes
                foreach (var line in lines) {
                    AddChild(new VirtualEnvPackageNode(ProjectMgr, line));
                }

                ProjectMgr.OnInvalidateItems(this);
                if (!prevChecked && IsExpanded) {
                    ProjectMgr.OnPropertyChanged(this, (int)__VSHPROPID.VSHPROPID_Expandable, 0);
                }
            }).Wait();
        }

        internal ProcessStartInfo MakePipCommand(string cmd) {
            string pipPath = Path.Combine(Path.Combine(Url, "Scripts", "pip.exe"));
            ProcessStartInfo startInfo;
            if (File.Exists(pipPath)) {
                startInfo = new ProcessStartInfo(pipPath, cmd);
            } else {
                startInfo = new ProcessStartInfo(InterpreterPath, "-m pip " + cmd);
            }
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            return startInfo;
        }

        public override Guid ItemTypeGuid {
            get { return VSConstants.GUID_ItemType_VirtualFolder; }
        }

        public override int MenuCommandId {
            get { return VsMenus.IDM_VS_CTXT_ITEMNODE; }
        }

        internal override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == VsMenus.guidStandardCommandSet97) {
                switch ((VsCommands)cmd) {
                    case VsCommands.Delete:
                    case VsCommands.Remove:
                        string message = string.Format(
                            DynamicProjectSR.GetString(DynamicProjectSR.SearchPathRemoveConfirmation),
                            this.Caption);
                        string title = string.Empty;
                        OLEMSGICON icon = OLEMSGICON.OLEMSGICON_WARNING;
                        OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK | OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL;
                        OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
                        int res = Microsoft.VisualStudio.Shell.VsShellUtilities.ShowMessageBox(this.ProjectMgr.Site, title, message, icon, buttons, defaultButton);
                        bool shouldRemove = res == 1;
                        if (shouldRemove) {
                            Remove(false);
                        }
                        return VSConstants.S_OK;
                }
            } else if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmd) {
                    case PythonConstants.ActivateVirtualEnv:
                        if (!IsVirtualEnvEnabled) {
                            //Make sure we can edit the project file
                            if (!ProjectMgr.QueryEditProjectFile(false)) {
                                return VSConstants.OLE_E_PROMPTSAVECANCELLED;
                            }

                            var parent = ProjectMgr.GetVirtualEnvContainerNode();
                            for (var child = parent.FirstChild; child != null; child = child.NextSibling) {
                                if (((VirtualEnvNode)child).IsVirtualEnvEnabled) {
                                    ProjectMgr.ReDrawNode(child, UIHierarchyElement.OverlayIcon);
                                    ProjectMgr.BoldItem(child, false);
                                    break;
                                }
                            }

                            ProjectMgr.SetProjectProperty(PythonConstants.VirtualEnvCurrentEnvironment, ItemNode.Item.UnevaluatedInclude);
                            ProjectMgr.ReDrawNode(this, UIHierarchyElement.OverlayIcon);
                            ProjectMgr.SetProjectProperty(CommonConstants.InterpreterPath, InterpreterPath);
                            ProjectMgr.SetProjectProperty(PythonConstants.InterpreterId, ItemNode.GetMetadata(PythonConstants.VirtualEnvInterpreterId));
                            ProjectMgr.SetProjectProperty(PythonConstants.InterpreterVersion, ItemNode.GetMetadata(PythonConstants.VirtualEnvInterpreterVersion));
                            ProjectMgr.BoldItem(this, true);
                        }
                        return VSConstants.S_OK;
                    case PythonConstants.DeactivateVirtualEnv:
                        if (IsVirtualEnvEnabled) {
                            if (!ProjectMgr.QueryEditProjectFile(false)) {
                                return VSConstants.OLE_E_PROMPTSAVECANCELLED;
                            }

                            ProjectMgr.SetProjectProperty(PythonConstants.VirtualEnvCurrentEnvironment, "");
                            ProjectMgr.SetProjectProperty(CommonConstants.InterpreterPath, "");
                            ProjectMgr.ReDrawNode(this, UIHierarchyElement.OverlayIcon);
                            ProjectMgr.BoldItem(this, false);
                        }
                        return VSConstants.S_OK;
                    case PythonConstants.InstallPythonPackage:
                        return InstallNewPackage();
                }
            }
            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }

        private int InstallNewPackage() {
            var view = new InstallPythonPackageView();
            var window = new InstallPythonPackage(view);
            var res = window.ShowDialog();
            if (res != null && res.Value) {
                var psi = MakePipCommand("install " + view.Name);

                // don't process events while we're installing, we'll
                // rescan once we're done
                BeginPackageChange();

                ProjectMgr.EnqueueVirtualEnvRequest(
                    psi,
                    "Installing " + view.Name,
                    "Successfully installed " + view.Name,
                    "Failed to install " + view.Name,
                    PackageChangeDone,
                    PackageChangeDone
                );

            }
            return VSConstants.S_OK;
        }

        internal void BeginPackageChange() {
            lock (this) {
                if (!_disposed) {
                    _fileWatcher.EnableRaisingEvents = false;
                }
            }
        }

        internal void PackageChangeDone() {
            lock (this) {
                if (!_disposed) {
                    _fileWatcher.EnableRaisingEvents = true;
                    ThreadPool.QueueUserWorkItem(CheckPackages);
                }
            }
        }

        public new PythonProjectNode ProjectMgr {
            get {
                return (PythonProjectNode)base.ProjectMgr;
            }
        }

        /// <summary>
        /// Virtual env node can only be removed from project.
        /// </summary>        
        internal override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation) {
            return deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject;
        }

        public override void Remove(bool removeFromStorage) {
            //Make sure we can edit the project file
            if (!ProjectMgr.QueryEditProjectFile(false)) {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            ProjectMgr.RemoveVirtualEnvPath(Url);
        }

        public string InterpreterPath {
            get {
                string virtualEnvPath = Path.Combine(Url, "Scripts", "python.exe");
                if (!File.Exists(virtualEnvPath)) {
                    string altPath = Path.Combine(Url, "python.exe");
                    if (File.Exists(altPath)) {
                        return altPath;
                    }
                }
                return virtualEnvPath;
            }
        }

        /// <summary>
        /// Show friendly node caption - relative path or normalized absolute path.
        /// </summary>
        public override string Caption {
            get {
                return _caption;
            }
        }

        /// <summary>
        /// Disable inline editing of Caption of a virtual env Node
        /// </summary>        
        public override string GetEditLabel() {
            return null;
        }

        public override object GetIconHandle(bool open) {
            return this.ProjectMgr.ImageHandler.GetIconHandle(
                CommonProjectNode.ImageOffset + (int)CommonImageName.VirtualEnv
            );
        }

        protected override VSOVERLAYICON OverlayIconIndex {
            get {
                if (IsVirtualEnvEnabled) {
                    return VSOVERLAYICON.OVERLAYICON_POLICY;
                } else if (!Directory.Exists(Url)) {
                    return (VSOVERLAYICON)__VSOVERLAYICON2.OVERLAYICON_NOTONDISK;
                }
                return base.OverlayIconIndex;
            }
        }

        internal bool IsVirtualEnvEnabled {
            get {
                var projectEnv = ProjectMgr.GetProjectProperty(PythonConstants.VirtualEnvCurrentEnvironment);

                if (projectEnv != null) {
                    return projectEnv.Equals(ItemNode.Item.UnevaluatedInclude, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            }
        }

        public new MsBuildProjectElement ItemNode {
            get {
                return (MsBuildProjectElement)base.ItemNode;
            }
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
        /// Disable Copy/Cut/Paste commands on virtual env node.
        /// </summary>
        internal override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == VsMenus.guidStandardCommandSet97) {
                switch ((VsCommands)cmd) {
                    case VsCommands.Delete:
                    case VsCommands.Remove:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
                }
            } else if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmd) {
                    case PythonConstants.ActivateVirtualEnv:
                    case PythonConstants.DeactivateVirtualEnv:
                    case PythonConstants.InstallPythonPackage:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
                }
            }
            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        public override string Url {
            get {
                return ItemNode.GetFullPathForElement();
            }
        }

        /// <summary>
        /// Defines whether this node is valid node for painting the virtual env icon.
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
            return new VirtualEnvNodeProperties(this);
        }

        public override object GetProperty(int propId) {
            if (propId == (int)__VSHPROPID.VSHPROPID_IconIndex || propId == (int)__VSHPROPID.VSHPROPID_OpenFolderIconIndex) {
                ProjectMgr.GetVirtualEnvContainerNode().CheckStartupBold();
            } else if (propId == (int)__VSHPROPID.VSHPROPID_Expandable) {
                if (!_checkedItems) {
                    // we haven't checked if we have files on disk yet, report that we can expand until we do.
                    // We do this lazily so we don't need to spawn a process per virtual env on project load
                    ThreadPool.QueueUserWorkItem(CheckPackages);
                    return true;
                }
            }

            return base.GetProperty(propId);
        }
    }
}
