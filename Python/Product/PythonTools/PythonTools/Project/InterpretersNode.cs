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
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Build.Evaluation;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using Clipboard = System.Windows.Forms.Clipboard;
using MessageBox = System.Windows.Forms.MessageBox;
using Task = System.Threading.Tasks.Task;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using VsMenus = Microsoft.VisualStudioTools.Project.VsMenus;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Represents an interpreter as a node in the Solution Explorer.
    /// </summary>
    [ComVisible(true)]
    internal class InterpretersNode : HierarchyNode {
        private readonly MSBuildProjectInterpreterFactoryProvider _interpreters;
        internal readonly IPythonInterpreterFactory _factory;
        private readonly bool _isReference;
        private readonly bool _canDelete;
        private readonly FileSystemWatcher _fileWatcher;
        private readonly Timer _timer;
        private bool _checkedItems, _checkingItems, _disposed;
        private bool _installingPackage;

        public InterpretersNode(PythonProjectNode project,
                                ProjectItem item,
                                IPythonInterpreterFactory factory,
                                bool isInterpreterReference,
                                bool canDelete)
            : base(project, ChooseElement(project, item)) {
            ExcludeNodeFromScc = true;

            _interpreters = project.Interpreters;
            _factory = factory;
            _isReference = isInterpreterReference;
            _canDelete = canDelete;

            if (Directory.Exists(_factory.Configuration.LibraryPath)) {
                // TODO: Need to handle watching for creation
                try {
                    _fileWatcher = new FileSystemWatcher(_factory.Configuration.LibraryPath);
                } catch (ArgumentException) {
                    // Path was not actually valid, despite Directory.Exists
                    // returning true.
                }
                if (_fileWatcher != null) {
                    try {
                        _fileWatcher.IncludeSubdirectories = true;
                        _fileWatcher.Deleted += PackagesChanged;
                        _fileWatcher.Created += PackagesChanged;
                        _fileWatcher.EnableRaisingEvents = true;
                        // Only create the timer if the file watcher is running.
                        _timer = new Timer(CheckPackages);
                    } catch (IOException) {
                        // Raced with directory deletion
                        _fileWatcher.Dispose();
                        _fileWatcher = null;
                    }
                }
            }
        }

        public override int MenuCommandId {
            get { return PythonConstants.EnvironmentMenuId; }
        }

        public override Guid MenuGroupId {
            get { return GuidList.guidPythonToolsCmdSet; }
        }

        private static ProjectElement ChooseElement(PythonProjectNode project, ProjectItem item) {
            if (item != null) {
                return new MsBuildProjectElement(project, item);
            } else {
                return new VirtualProjectElement(project);
            }
        }

        public override void Close() {
            lock (this) {
                if (!_disposed && _fileWatcher != null) {
                    _fileWatcher.Dispose();
                    _timer.Dispose();
                }
                _disposed = true;
            }

            base.Close();
        }

        private void PackagesChanged(object sender, FileSystemEventArgs e) {
            // have a delay before refreshing because there's probably more than one write,
            // so we wait until things have been quiet for a second.
            _timer.Change(1000, Timeout.Infinite);
        }

        private void CheckPackages(object arg) {
            UIThread.InvokeTask(() => CheckPackagesAsync())
                .WaitAndHandleAllExceptions(SR.GetString(SR.PythonToolsForVisualStudio), GetType());
        }

        private async Task CheckPackagesAsync() {
            bool prevChecked = _checkedItems;
            // Use _checkingItems to prevent the expanded state from
            // disappearing too quickly.
            _checkingItems = true;
            _checkedItems = true;
            if (!Directory.Exists(_factory.Configuration.LibraryPath)) {
                _checkingItems = false;
                ProjectMgr.OnPropertyChanged(this, (int)__VSHPROPID.VSHPROPID_Expandable, 0);
                return;
            }

            HashSet<string> lines;
            bool anyChanges = false;
            try {
                lines = await Pip.List(_factory);
            } catch (NoInterpretersException) {
                return;
            }
            if (ProjectMgr == null || ProjectMgr.IsClosed) {
                return;
            }

            var existing = AllChildren.ToDictionary(c => c.Url);

            // remove the nodes which were uninstalled.
            foreach (var keyValue in existing) {
                if (!lines.Contains(keyValue.Key)) {
                    RemoveChild(keyValue.Value);
                    anyChanges = true;
                }
            }

            // remove already existing nodes so we don't add them a 2nd time
            lines.ExceptWith(existing.Keys);

            // add the new nodes
            foreach (var line in lines) {
                AddChild(new InterpretersPackageNode(ProjectMgr, line));
                anyChanges = true;
            }
            _checkingItems = false;

            var wasExpanded = prevChecked && GetIsExpanded();
            ProjectMgr.OnInvalidateItems(this);
            if (!prevChecked) {
                ProjectMgr.OnPropertyChanged(this, (int)__VSHPROPID.VSHPROPID_Expandable, 0);
            }
            if (wasExpanded) {
                ExpandItem(EXPANDFLAGS.EXPF_ExpandFolder);
            }

            if (prevChecked && anyChanges) {
                var withDb = _factory as IPythonInterpreterFactoryWithDatabase;
                if (withDb != null) {
                    withDb.GenerateDatabase(GenerateDatabaseOptions.SkipUnchanged);
                }
            }
        }

        public override Guid ItemTypeGuid {
            get { return PythonConstants.InterpreterItemTypeGuid; }
        }

        internal override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == VsMenus.guidStandardCommandSet2K) {
                switch((VsCommands2K)cmd) {
                    case CommonConstants.OpenFolderInExplorerCmdId:
                        Process.Start(new ProcessStartInfo {
                            FileName = _factory.Configuration.PrefixPath,
                            Verb = "open",
                            UseShellExecute = true
                        });
                        return VSConstants.S_OK;
                }
            }
            
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmd) {
                    case PythonConstants.ActivateEnvironment:
                        return ProjectMgr.SetInterpreterFactory(_factory);
                    case PythonConstants.InstallPythonPackage:
                        var task = InterpretersPackageNode.InstallNewPackage(this);
                        return VSConstants.S_OK;
                    case PythonConstants.OpenInteractiveForEnvironment:
                        try {
                            var window = ExecuteInReplCommand.EnsureReplWindow(_factory, ProjectMgr);
                            var pane = window as ToolWindowPane;
                            if (pane != null) {
                                ErrorHandler.ThrowOnFailure(((IVsWindowFrame)pane.Frame).Show());
                                window.Focus();
                            }
                        } catch (InvalidOperationException ex) {
                            MessageBox.Show(
                                string.Format("An error occurred opening this interactive window.{0}{0}{1}", Environment.NewLine, ex),
                                SR.GetString(SR.PythonToolsForVisualStudio)
                            );
                        }
                        return VSConstants.S_OK;
                }
            } 
            
            if (cmdGroup == ProjectMgr.SharedCommandGuid) {
                switch ((SharedCommands)cmd) {
                    case SharedCommands.OpenCommandPromptHere:
                        var pyProj = ProjectMgr as PythonProjectNode;
                        if (pyProj != null && _factory != null && _factory.Configuration != null) {
                            return pyProj.OpenCommandPrompt(
                                _factory.Configuration.PrefixPath,
                                _factory,
                                _factory.Description
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

        internal void BeginPackageChange() {
            lock (this) {
                Debug.Assert(!_installingPackage);
                _installingPackage = true;
                if (!_disposed && _fileWatcher != null) {
                    _fileWatcher.EnableRaisingEvents = false;
                }
            }
        }

        internal void PackageChangeDone() {
            lock (this) {
                Debug.Assert(_installingPackage);
                _installingPackage = false;
                if (!_disposed && _fileWatcher != null) {
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

        internal override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation) {
            if (_installingPackage) {
                return false;
            }

            if (deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject) {
                // Interpreter and InterpreterReference can both be removed from
                // the project.
                return true;
            } else if (deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage) {
                // Only Interpreter can be deleted.
                return _canDelete;
            }
            return false;
        }

        public override void Remove(bool removeFromStorage) {
            // If _canDelete, a prompt has already been shown by VS.
            Remove(removeFromStorage, !_canDelete);
        }

        private void Remove(bool removeFromStorage, bool showPrompt) {
            if (_installingPackage) {
                // Prevent the environment from being deleting while installing.
                // This situation should not occur through the UI, but might be
                // invocable through DTE.
                return;
            }

            if (showPrompt && !Utilities.IsInAutomationFunction(ProjectMgr.Site)) {
                string message = SR.GetString(removeFromStorage ?
                        SR.EnvironmentDeleteConfirmation :
                        SR.EnvironmentRemoveConfirmation,
                    Caption,
                    _factory.Configuration.PrefixPath);
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

            //Make sure we can edit the project file
            if (!ProjectMgr.QueryEditProjectFile(false)) {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            ProjectMgr.RemoveInterpreter(_factory, !_isReference && removeFromStorage && _canDelete);
        }

        /// <summary>
        /// Show interpreter display (description and version).
        /// </summary>
        public override string Caption {
            get {
                return _factory.Description;
            }
        }

        public new MsBuildProjectElement ItemNode {
            get {
                return (MsBuildProjectElement)base.ItemNode;
            }
        }

        /// <summary>
        /// Prevent editing the description
        /// </summary>
        public override string GetEditLabel() {
            return null;
        }

        public override object GetIconHandle(bool open) {
            if (ProjectMgr == null) {
                return null;
            }

            int index;
            if (!_interpreters.IsAvailable(_factory)) {
                index = ProjectMgr.GetIconIndex(PythonProjectImageName.MissingInterpreter);
            } else if (_interpreters.ActiveInterpreter == _factory) {
                index = ProjectMgr.GetIconIndex(PythonProjectImageName.ActiveInterpreter);
            } else {
                index = ProjectMgr.GetIconIndex(PythonProjectImageName.Interpreter);
            }
            return this.ProjectMgr.ImageHandler.GetIconHandle(index);
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
                        if (_factory != null && Directory.Exists(_factory.Configuration.PrefixPath)) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                }
            }
            
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmd) {
                    case PythonConstants.ActivateEnvironment:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_interpreters.IsAvailable(_factory) &&
                            _interpreters.ActiveInterpreter != _factory &&
                            Directory.Exists(_factory.Configuration.PrefixPath)) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                    case PythonConstants.InstallPythonPackage:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_interpreters.IsAvailable(_factory) &&
                            Directory.Exists(_factory.Configuration.PrefixPath) &&
                            !_installingPackage) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                    case PythonConstants.OpenInteractiveForEnvironment:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_interpreters.IsAvailable(_factory) &&
                            File.Exists(_factory.Configuration.InterpreterPath)) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                }
            }
            
            if (cmdGroup == ProjectMgr.SharedCommandGuid) {
                switch ((SharedCommands)cmd) {
                    case SharedCommands.OpenCommandPromptHere:
                    case SharedCommands.CopyFullPath:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_interpreters.IsAvailable(_factory) &&
                            Directory.Exists(_factory.Configuration.PrefixPath) &&
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
                if (!CommonUtils.IsValidPath(_factory.Configuration.PrefixPath)) {
                    return string.Format("UnknownInterpreter\\{0}\\{1}", _factory.Id, _factory.Configuration.Version);
                }

                return _factory.Configuration.PrefixPath;
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
            if (_factory is DerivedInterpreterFactory) {
                return new InterpretersNodeWithBaseInterpreterProperties(this);
            } else {
                return new InterpretersNodeProperties(this);
            }
        }

        public override object GetProperty(int propId) {
            if (propId == (int)__VSHPROPID.VSHPROPID_Expandable) {
                if (!_checkedItems) {
                    // We haven't checked if we have files on disk yet, report
                    // that we can expand until we do.
                    // We do this lazily so we don't need to spawn a process for
                    // each interpreter on project load.
                    ThreadPool.QueueUserWorkItem(CheckPackages);
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
