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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using Clipboard = System.Windows.Forms.Clipboard;
using Task = System.Threading.Tasks.Task;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using VsMenus = Microsoft.VisualStudioTools.Project.VsMenus;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Represents an interpreter as a node in the Solution Explorer.
    /// </summary>
    [ComVisible(true)]
    internal class InterpretersNode : HierarchyNode {
        private readonly IInterpreterRegistryService _interpreterService;
        internal readonly IPythonInterpreterFactory _factory;
        internal readonly IPackageManager _packageManager;
        private readonly bool _isReference;
        private readonly bool _canDelete, _canRemove;
        private readonly string _captionSuffix;
        private bool _suppressPackageRefresh;
        private bool _checkedItems, _checkingItems, _disposed;
        internal readonly string _absentId;
        internal readonly bool _isGlobalDefault;

        public static readonly object InstallPackageLockMoniker = new object();

        public InterpretersNode(
            PythonProjectNode project,
            IPythonInterpreterFactory factory,
            bool isInterpreterReference,
            bool canDelete,
            bool isGlobalDefault = false,
            bool? canRemove = null
        )
            : base(project, MakeElement(project)) {
            ExcludeNodeFromScc = true;

            _interpreterService = project.Site.GetComponentModel().GetService<IInterpreterRegistryService>();
            _factory = factory;
            _isReference = isInterpreterReference;
            _canDelete = canDelete;
            _isGlobalDefault = isGlobalDefault;
            _canRemove = canRemove.HasValue ? canRemove.Value : !isGlobalDefault;
            _captionSuffix = isGlobalDefault ? Strings.GlobalDefaultSuffix : "";

            var interpreterOpts = project.Site.GetComponentModel().GetService<IInterpreterOptionsService>();
            _packageManager = interpreterOpts?.GetPackageManagers(factory).FirstOrDefault();
            if (_packageManager != null) {
                _packageManager.InstalledPackagesChanged += InstalledPackagesChanged;
                _packageManager.EnableNotifications();
            }
        }

        private InterpretersNode(PythonProjectNode project, string id) : base(project, MakeElement(project)) {
            _absentId = id;
            _canRemove = true;
            _captionSuffix = Strings.MissingSuffix;
        }

        public static InterpretersNode CreateAbsentInterpreterNode(PythonProjectNode project, string id) {
            return new InterpretersNode(project, id);
        }

        public override int MenuCommandId {
            get { return PythonConstants.EnvironmentMenuId; }
        }

        public override Guid MenuGroupId {
            get { return GuidList.guidPythonToolsCmdSet; }
        }

        private static ProjectElement MakeElement(PythonProjectNode project) {
            return new VirtualProjectElement(project);
        }

        public override void Close() {
            if (!_disposed) {
                if (_packageManager != null) {
                    _packageManager.InstalledPackagesChanged -= InstalledPackagesChanged;
                }
            }
            _disposed = true;

            base.Close();
        }

        private void InstalledPackagesChanged(object sender, EventArgs e) {
            RefreshPackages();
        }

        private void RefreshPackages() {
            RefreshPackagesAsync(_packageManager)
                .SilenceException<OperationCanceledException>()
                .HandleAllExceptions(ProjectMgr.Site, GetType())
                .DoNotWait();
        }

        private async Task RefreshPackagesAsync(IPackageManager packageManager) {
            if (_suppressPackageRefresh || packageManager == null) {
                return;
            }

            bool prevChecked = _checkedItems;
            // Use _checkingItems to prevent the expanded state from
            // disappearing too quickly.
            _checkingItems = true;
            _checkedItems = true;

            var packages = new Dictionary<string, PackageSpec>();
            foreach (var p in await packageManager.GetInstalledPackagesAsync(CancellationToken.None)) {
                packages[p.FullSpec] = p;
            }

            await ProjectMgr.Site.GetUIThread().InvokeAsync(() => {
                if (ProjectMgr == null || ProjectMgr.IsClosed) {
                    return;
                }

                try {
                    var logger = ProjectMgr.Site.GetPythonToolsService().Logger;
                    if (logger != null) {
                        foreach (var p in packages) {
                            logger.LogEvent(PythonLogEvent.PythonPackage,
                                new PackageInfo { Name = p.Value.Name.ToLowerInvariant() });
                        }
                    }
                } catch (Exception ex) {
                    Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                }

                bool anyChanges = false;
                var existing = AllChildren.OfType<InterpretersPackageNode>().ToDictionary(c => c.Url);

                // remove the nodes which were uninstalled.
                foreach (var keyValue in existing) {
                    if (!packages.Remove(keyValue.Key)) {
                        RemoveChild(keyValue.Value);
                        anyChanges = true;
                    }
                }

                // add the new nodes
                foreach (var p in packages.OrderBy(kv => kv.Key)) {
                    AddChild(new InterpretersPackageNode(ProjectMgr, p.Value));
                    anyChanges = true;
                }
                _checkingItems = false;

                ProjectMgr.OnInvalidateItems(this);
                if (!prevChecked) {
                    if (anyChanges) {
                        ProjectMgr.OnPropertyChanged(this, (int)__VSHPROPID.VSHPROPID_Expandable, 0);
                    }
                    if (ProjectMgr.ParentHierarchy != null) {
                        ExpandItem(EXPANDFLAGS.EXPF_CollapseFolder);
                    }
                }
            });
        }


        /// <summary>
        /// Disables the file watcher. This function may be called as many times
        /// as you like, but it only requires one call to
        /// <see cref="ResumeWatching"/> to re-enable the watcher.
        /// </summary>
        public void StopWatching() {
            _suppressPackageRefresh = true;
        }

        /// <summary>
        /// Enables the file watcher, regardless of how many times
        /// <see cref="StopWatching"/> was called. If the file watcher was
        /// enabled successfully, the list of packages is updated.
        /// </summary>
        public void ResumeWatching() {
            _suppressPackageRefresh = false;
            RefreshPackagesAsync(_packageManager)
                .SilenceException<OperationCanceledException>()
                .HandleAllExceptions(ProjectMgr.Site, GetType())
                .DoNotWait();
        }

        public override Guid ItemTypeGuid {
            get { return PythonConstants.InterpreterItemTypeGuid; }
        }

        internal override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == VsMenus.guidStandardCommandSet2K) {
                switch ((VsCommands2K)cmd) {
                    case CommonConstants.OpenFolderInExplorerCmdId:
                        Process.Start(new ProcessStartInfo {
                            FileName = _factory.Configuration.GetPrefixPath(),
                            Verb = "open",
                            UseShellExecute = true
                        });
                        return VSConstants.S_OK;
                }
            }

            if (cmdGroup == ProjectMgr.SharedCommandGuid) {
                switch ((SharedCommands)cmd) {
                    case SharedCommands.OpenCommandPromptHere:
                        var pyProj = ProjectMgr as PythonProjectNode;
                        if (pyProj != null && _factory != null && _factory.Configuration != null) {
                            return pyProj.OpenCommandPrompt(
                                _factory.Configuration.GetPrefixPath(),
                                _factory.Configuration,
                                _factory.Configuration.Description
                            );
                        }
                        break;
                    case SharedCommands.CopyFullPath:
                        Clipboard.SetText(_factory.Configuration.InterpreterPath);
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

        internal override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation) {
            if (deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject) {
                // Interpreter and InterpreterReference can both be removed from
                // the project, but the default environment cannot
                return _canRemove;
            } else if (deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage) {
                // Only Interpreter can be deleted.
                return _canDelete;
            }
            return false;
        }

        public override bool Remove(bool removeFromStorage) {
            // If _canDelete, a prompt has already been shown by VS.
            return Remove(removeFromStorage, !_canDelete);
        }

        private bool Remove(bool removeFromStorage, bool showPrompt) {
            if (!_canRemove || (removeFromStorage && !_canDelete)) {
                // Prevent the environment from being deleted or removed if not
                // supported.
                throw new NotSupportedException();
            }

            if (showPrompt && !Utilities.IsInAutomationFunction(ProjectMgr.Site)) {
                string message = !removeFromStorage ?
                    Strings.EnvironmentRemoveConfirmation.FormatUI(Caption) :
                    _factory == null ?
                        Strings.EnvironmentDeleteConfirmation_NoPath.FormatUI(Caption) :
                        Strings.EnvironmentDeleteConfirmation.FormatUI(Caption, _factory.Configuration.GetPrefixPath());
                int res = VsShellUtilities.ShowMessageBox(
                    ProjectMgr.Site,
                    string.Empty,
                    message,
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                if (res != 1) {
                    return false;
                }
            }

            //Make sure we can edit the project file
            if (!ProjectMgr.QueryEditProjectFile(false)) {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            if (!string.IsNullOrEmpty(_absentId)) {
                Debug.Assert(!removeFromStorage, "Cannot remove absent environment from storage");
                ProjectMgr.RemoveInterpreterFactory(_absentId);
                return true;
            }

            if (_factory == null) {
                Debug.Fail("Attempted to remove null factory from project");
                return true;
            }

            ProjectMgr.RemoveInterpreter(_factory, !_isReference && removeFromStorage && _canDelete);
            return true;
        }

        /// <summary>
        /// Show interpreter display (description and version).
        /// </summary>
        public override string Caption {
            get {
                if (!string.IsNullOrEmpty(_absentId)) {
                    string company, tag;
                    if (CPythonInterpreterFactoryConstants.TryParseInterpreterId(_absentId, out company, out tag)) {
                        if (company == PythonRegistrySearch.PythonCoreCompany) {
                            company = "Python";
                        }
                        return "{0} {1}{2}".FormatUI(company, tag, _captionSuffix);
                    }
                    return _absentId + _captionSuffix;
                }
                if (_factory == null) {
                    Debug.Fail("null factory in interpreter node");
                    return "(null)";
                }
                return _factory.Configuration.Description + _captionSuffix;
            }
        }

        /// <summary>
        /// Prevent editing the description
        /// </summary>
        public override string GetEditLabel() {
            return null;
        }

        protected override VSOVERLAYICON OverlayIconIndex {
            get {
                if (!Directory.Exists(Url)) {
                    return (VSOVERLAYICON)__VSOVERLAYICON2.OVERLAYICON_NOTONDISK;
                } else if (_isReference) {
                    return VSOVERLAYICON.OVERLAYICON_SHORTCUT;
                }
                return base.OverlayIconIndex;
            }
        }

        protected override bool SupportsIconMonikers {
            get { return true; }
        }

        protected override ImageMoniker GetIconMoniker(bool open) {
            if (_factory == null || !_factory.Configuration.IsAvailable()) {
                // TODO: Find a better icon
                return KnownMonikers.DocumentWarning;
            } else if (ProjectMgr.ActiveInterpreter == _factory) {
                return KnownMonikers.ActiveEnvironment;
            }

            // TODO: Change to PYEnvironment
            return KnownMonikers.DockPanel;
        }

        /// <summary>
        /// Interpreter node cannot be dragged.
        /// </summary>
        protected internal override string PrepareSelectedNodesForClipBoard() {
            return null;
        }



        /// <summary>
        /// Disable Copy/Cut/Paste commands on interpreter node.
        /// </summary>
        internal override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == VsMenus.guidStandardCommandSet2K) {
                switch ((VsCommands2K)cmd) {
                    case CommonConstants.OpenFolderInExplorerCmdId:
                        result = QueryStatusResult.SUPPORTED;
                        if (_factory != null && Directory.Exists(_factory.Configuration.GetPrefixPath())) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                }
            }

            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmd) {
                    case PythonConstants.ActivateEnvironment:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_factory != null && _factory.Configuration.IsAvailable() &&
                            ProjectMgr.ActiveInterpreter != _factory &&
                            Directory.Exists(_factory.Configuration.GetPrefixPath())
                        ) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                    case PythonConstants.InstallPythonPackage:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_factory != null && _factory.Configuration.IsAvailable() &&
                            Directory.Exists(_factory.Configuration.GetPrefixPath())
                        ) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                    case PythonConstants.InstallRequirementsTxt:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_factory != null && _factory.IsRunnable() &&
                            File.Exists(PathUtils.GetAbsoluteFilePath(ProjectMgr.ProjectHome, "requirements.txt"))) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                    case PythonConstants.GenerateRequirementsTxt:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_factory != null && _factory.Configuration.IsAvailable()) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                    case PythonConstants.OpenInteractiveForEnvironment:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_factory != null && _factory.Configuration.IsAvailable() &&
                            File.Exists(_factory.Configuration.InterpreterPath)
                        ) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                }
            }

            if (cmdGroup == ProjectMgr.SharedCommandGuid) {
                switch ((SharedCommands)cmd) {
                    case SharedCommands.OpenCommandPromptHere:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_factory != null && _factory.Configuration.IsAvailable() &&
                            Directory.Exists(_factory.Configuration.GetPrefixPath()) &&
                            File.Exists(_factory.Configuration.InterpreterPath)) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                    case SharedCommands.CopyFullPath:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_factory != null && _factory.Configuration.IsAvailable() &&
                            Directory.Exists(_factory.Configuration.GetPrefixPath()) &&
                            File.Exists(_factory.Configuration.InterpreterPath)) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                }
            }

            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        public override string Url {
            get {
                if (!string.IsNullOrEmpty(_absentId)) {
                    return "UnknownInterpreter\\{0}".FormatInvariant(_absentId);
                }

                if (_factory == null) {
                    Debug.Fail("null factory in interpreter node");
                    return "UnknownInterpreter";
                }

                if (!PathUtils.IsValidPath(_factory.Configuration.GetPrefixPath())) {
                    return "UnknownInterpreter\\{0}".FormatInvariant(_factory.Configuration.Id);
                }

                return _factory.Configuration.GetPrefixPath();
            }
        }

        /// <summary>
        /// Defines whether this node is valid node for painting the interpreter
        /// icon.
        /// </summary>
        protected override bool CanShowDefaultIcon() {
            return true;
        }

        public override bool CanAddFiles {
            get {
                return false;
            }
        }

        protected override NodeProperties CreatePropertiesObject() {
            return new InterpretersNodeProperties(this);
        }

        public override object GetProperty(int propId) {
            if (propId == (int)__VSHPROPID.VSHPROPID_Expandable) {
                if (_packageManager == null) {
                    // No package manager, so we are not expandable
                    return false;
                }

                if (!_checkedItems) {
                    // We haven't checked if we have files on disk yet, report
                    // that we can expand until we do.
                    // We do this lazily so we don't need to spawn a process for
                    // each interpreter on project load.
                    ThreadPool.QueueUserWorkItem(_ => RefreshPackages());
                    return true;
                } else if (_checkingItems) {
                    // Still checking, so keep reporting true.
                    return true;
                }
            }

            return base.GetProperty(propId);
        }
    }
}
