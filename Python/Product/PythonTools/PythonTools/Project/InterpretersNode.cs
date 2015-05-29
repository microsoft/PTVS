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
using System.Text.RegularExpressions;
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
#if DEV14_OR_LATER
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
#endif

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Represents an interpreter as a node in the Solution Explorer.
    /// </summary>
    [ComVisible(true)]
    internal class InterpretersNode : HierarchyNode {
        private readonly MSBuildProjectInterpreterFactoryProvider _interpreters;
        private readonly IInterpreterOptionsService _interpreterService;
        internal readonly IPythonInterpreterFactory _factory;
        private readonly bool _isReference;
        private readonly bool _canDelete, _canRemove;
        private readonly string _captionSuffix;
        private readonly FileSystemWatcher _fileWatcher;
        private readonly Timer _timer;
        private bool _checkedItems, _checkingItems, _disposed;
        internal readonly bool _isGlobalDefault;

        public static readonly object InstallPackageLockMoniker = new object();

        public InterpretersNode(
            PythonProjectNode project,
            ProjectItem item,
            IPythonInterpreterFactory factory,
            bool isInterpreterReference,
            bool canDelete,
            bool isGlobalDefault = false
        )
            : base(project, ChooseElement(project, item)) {
            ExcludeNodeFromScc = true;

            _interpreters = project.Interpreters;
            _interpreterService = project.Site.GetComponentModel().GetService<IInterpreterOptionsService>();
            _factory = factory;
            _isReference = isInterpreterReference;
            _canDelete = canDelete;
            _isGlobalDefault = isGlobalDefault;
            _canRemove = !isGlobalDefault;
            _captionSuffix = isGlobalDefault ? SR.GetString(SR.GlobalDefaultSuffix) : "";

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
            if (!_disposed && _fileWatcher != null) {
                _fileWatcher.Dispose();
                _timer.Dispose();
            }
            _disposed = true;

            base.Close();
        }

        /// <summary>
        /// Disables the file watcher. This function may be called as many times
        /// as you like, but it only requires one call to
        /// <see cref="ResumeWatching"/> to re-enable the watcher.
        /// </summary>
        public void StopWatching() {
            if (!_disposed && _fileWatcher != null) {
                try {
                    _fileWatcher.EnableRaisingEvents = false;
                } catch (IOException) {
                } catch (ObjectDisposedException) {
                }
            }
        }

        /// <summary>
        /// Enables the file watcher, regardless of how many times
        /// <see cref="StopWatching"/> was called. If the file watcher was
        /// enabled successfully, the list of packages is updated.
        /// </summary>
        public void ResumeWatching() {
            if (!_disposed && _fileWatcher != null) {
                try {
                    _fileWatcher.EnableRaisingEvents = true;
                    CheckPackages(null);
                } catch (IOException) {
                } catch (ObjectDisposedException) {
                }
            }
        }

        private void PackagesChanged(object sender, FileSystemEventArgs e) {
            // have a delay before refreshing because there's probably more than one write,
            // so we wait until things have been quiet for a second.
            _timer.Change(1000, Timeout.Infinite);
        }

        private void CheckPackages(object arg) {
            ProjectMgr.Site.GetUIThread().InvokeTask(() => CheckPackagesAsync())
                .HandleAllExceptions(SR.ProductName, GetType())
                .DoNotWait();
        }

        private async Task CheckPackagesAsync() {
            var uiThread = ProjectMgr.Site.GetUIThread();
            uiThread.MustBeCalledFromUIThreadOrThrow();

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
                lines = await Pip.List(_factory).ConfigureAwait(true);
            } catch (MissingInterpreterException) {
                return;
            } catch (NoInterpretersException) {
                return;
            }

            // Ensure we are back on the UI thread
            uiThread.MustBeCalledFromUIThread();

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

                var packageInfo = PythonProjectNode.FindRequirementRegex.Match(line.ToLower());
                if (packageInfo.Groups["name"].Success) {
                    //Log the details of the Installation
                    var packageDetails = new Logging.PackageInstallDetails(
                        packageInfo.Groups["name"].Value,
                        packageInfo.Groups["ver"].Success ? packageInfo.Groups["ver"].Value : String.Empty,
                        _factory.GetType().Name,
                        _factory.Configuration.Version.ToString(),
                        _factory.Configuration.Architecture.ToString(),
                        "Existing", //Installer if we tracked it
                        false, //Installer was not run elevated
                        0); //The installation already existed
                    ProjectMgr.Site.GetPythonToolsService().Logger.LogEvent(Logging.PythonLogEvent.PackageInstalled, packageDetails);
                }
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
                switch ((VsCommands2K)cmd) {
                    case CommonConstants.OpenFolderInExplorerCmdId:
                        Process.Start(new ProcessStartInfo {
                            FileName = _factory.Configuration.PrefixPath,
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

        public new PythonProjectNode ProjectMgr {
            get {
                return (PythonProjectNode)base.ProjectMgr;
            }
        }

        internal override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation) {
            var service = _interpreterService as IInterpreterOptionsService2;
            if (service != null && service.IsInterpreterLocked(_factory, InstallPackageLockMoniker)) {
                // Prevent the environment from being deleted while installing.
                return false;
            }

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

        public override void Remove(bool removeFromStorage) {
            // If _canDelete, a prompt has already been shown by VS.
            Remove(removeFromStorage, !_canDelete);
        }

        private void Remove(bool removeFromStorage, bool showPrompt) {
            if (!_canRemove || (removeFromStorage && !_canDelete)) {
                // Prevent the environment from being deleted or removed if not
                // supported.
                throw new NotSupportedException();
            }

            var service = _interpreterService as IInterpreterOptionsService2;
            if (service != null && service.IsInterpreterLocked(_factory, InstallPackageLockMoniker)) {
                // Prevent the environment from being deleted while installing.
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
                return _factory.Description + _captionSuffix;
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

#if DEV14_OR_LATER
        protected override bool SupportsIconMonikers {
            get { return true; }
        }

        protected override ImageMoniker GetIconMoniker(bool open) {
            if (!_interpreters.IsAvailable(_factory)) {
                // TODO: Find a better icon
                return KnownMonikers.DocumentWarning;
            } else if (_interpreters.ActiveInterpreter == _factory) {
                return KnownMonikers.ActiveEnvironment;
            }

            // TODO: Change to PYEnvironment
            return KnownMonikers.DockPanel;
        }
#else
        public override int ImageIndex {
            get {
                if (ProjectMgr == null) {
                    return NoImage;
                }

                int index;
                if (!_interpreters.IsAvailable(_factory)) {
                    index = ProjectMgr.GetIconIndex(PythonProjectImageName.MissingInterpreter);
                } else if (_interpreters.ActiveInterpreter == _factory) {
                    index = ProjectMgr.GetIconIndex(PythonProjectImageName.ActiveInterpreter);
                } else {
                    index = ProjectMgr.GetIconIndex(PythonProjectImageName.Interpreter);
                }
                return index;
            }
        }

#endif

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
                            Directory.Exists(_factory.Configuration.PrefixPath)
                        ) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                    case PythonConstants.InstallPythonPackage:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_interpreters.IsAvailable(_factory) &&
                            Directory.Exists(_factory.Configuration.PrefixPath)
                        ) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                    case PythonConstants.InstallRequirementsTxt:
                        result |= QueryStatusResult.SUPPORTED;
                        if (File.Exists(CommonUtils.GetAbsoluteFilePath(ProjectMgr.ProjectHome, "requirements.txt"))) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                    case PythonConstants.GenerateRequirementsTxt:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_interpreters.IsAvailable(_factory)) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
                    case PythonConstants.OpenInteractiveForEnvironment:
                        result |= QueryStatusResult.SUPPORTED;
                        if (_interpreters.IsAvailable(_factory) &&
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
                        if (_interpreters.IsAvailable(_factory) &&
                            Directory.Exists(_factory.Configuration.PrefixPath) &&
                            File.Exists(_factory.Configuration.InterpreterPath)) {
                            result |= QueryStatusResult.ENABLED;
                        }
                        return VSConstants.S_OK;
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
