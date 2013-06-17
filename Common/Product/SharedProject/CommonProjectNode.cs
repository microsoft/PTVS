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
using System.Diagnostics.Contracts;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Navigation;
using Microsoft.VisualStudioTools.Project.Automation;
using Microsoft.Windows.Design.Host;
using MSBuild = Microsoft.Build.Evaluation;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using VSConstants = Microsoft.VisualStudio.VSConstants;

namespace Microsoft.VisualStudioTools.Project {

    public enum CommonImageName {
        File = 0,
        Project = 1,
        SearchPathContainer,
        SearchPath,
        MissingSearchPath,
        StartupFile,
        InterpretersContainer = SearchPathContainer,
        Interpreter = SearchPath,
        InterpretersPackage = SearchPath
    }

    internal abstract class CommonProjectNode : ProjectNode, IVsProjectSpecificEditorMap2, IVsDeferredSaveProject {
        private CommonProjectPackage/*!*/ _package;
        private Guid _mruPageGuid = new Guid(CommonConstants.AddReferenceMRUPageGuid);
        private VSLangProj.VSProject _vsProject = null;
        private static ImageList _imageList;
        private ProjectDocumentsListenerForStartupFileUpdates _projectDocListenerForStartupFileUpdates;
        private static int _imageOffset;
        private FileSystemWatcher _watcher, _attributesWatcher;
        private int _suppressFileWatcherCount;
        private bool _isRefreshing;
        internal bool _boldedStartupItem;
        private bool _showingAllFiles;
        private object _automationObject;
        private CommonPropertyPage _propPage;
        private readonly Dictionary<string, FileSystemEventHandler> _fileChangedHandlers = new Dictionary<string, FileSystemEventHandler>();
        private List<FileSystemChange> _fileSystemChanges;
        private object _fileSystemChangesLock = new object();
        internal UIThreadSynchronizer _uiSync;
        private MSBuild.Project userBuildProject;
        private readonly Dictionary<string, FileSystemWatcher> _symlinkWatchers = new Dictionary<string, FileSystemWatcher>();
        private readonly System.Threading.Timer _processFileChangesTimer;

        public CommonProjectNode(CommonProjectPackage/*!*/ package, ImageList/*!*/ imageList) {
            Contract.Assert(package != null);
            Contract.Assert(imageList != null);

            _processFileChangesTimer = new System.Threading.Timer(ProcessFileChanges);
            _package = package;
            CanFileNodesHaveChilds = true;
            OleServiceProvider.AddService(typeof(VSLangProj.VSProject), new OleServiceProvider.ServiceCreatorCallback(CreateServices), false);
            SupportsProjectDesigner = true;
            _imageList = imageList;

            //Store the number of images in ProjectNode so we know the offset of the language icons.
            _imageOffset = ImageHandler.ImageList.Images.Count;
            foreach (Image img in ImageList.Images) {
                ImageHandler.AddImage(img);
            }

            InitializeCATIDs();

            _uiSync = new UIThreadSynchronizer();
        }

        #region abstract methods

        public abstract Type GetProjectFactoryType();
        public abstract Type GetEditorFactoryType();
        public abstract string GetProjectName();

        public virtual CommonFileNode CreateCodeFileNode(ProjectElement item) {
            return new CommonFileNode(this, item);
        }
        public virtual CommonFileNode CreateNonCodeFileNode(ProjectElement item) {
            return new CommonNonCodeFileNode(this, item);
        }
        public abstract string GetFormatList();
        public abstract Type GetGeneralPropertyPageType();
        public abstract Type GetLibraryManagerType();

        #endregion

        #region Properties

        public new CommonProjectPackage/*!*/ Package {
            get { return _package; }
        }

        public static int ImageOffset {
            get { return _imageOffset; }
        }

        /// <summary>
        /// Get the VSProject corresponding to this project
        /// </summary>
        protected internal VSLangProj.VSProject VSProject {
            get {
                if (_vsProject == null)
                    _vsProject = new OAVSProject(this);
                return _vsProject;
            }
        }

        private IVsHierarchy InteropSafeHierarchy {
            get {
                IntPtr unknownPtr = Utilities.QueryInterfaceIUnknown(this);
                if (IntPtr.Zero == unknownPtr) {
                    return null;
                }
                IVsHierarchy hier = Marshal.GetObjectForIUnknown(unknownPtr) as IVsHierarchy;
                return hier;
            }
        }

        /// <summary>
        /// Indicates whether the project is currently is busy refreshing its hierarchy.
        /// </summary>
        public bool IsRefreshing {
            get { return _isRefreshing; }
            set { _isRefreshing = value; }
        }

        /// <summary>
        /// Language specific project images
        /// </summary>
        public static ImageList ImageList {
            get {
                return _imageList;
            }
            set {
                _imageList = value;
            }
        }

        public CommonPropertyPage PropertyPage {
            get { return _propPage; }
            set { _propPage = value; }
        }

        #endregion

        #region overridden properties

        public override bool CanShowAllFiles {
            get {
                return true;
            }
        }

        public override bool IsShowingAllFiles {
            get {
                return _showingAllFiles;
            }
        }

        /// <summary>
        /// Since we appended the language images to the base image list in the constructor,
        /// this should be the offset in the ImageList of the langauge project icon.
        /// </summary>
        public override int ImageIndex {
            get {
                return _imageOffset + (int)CommonImageName.Project;
            }
        }

        public override Guid ProjectGuid {
            get {
                return GetProjectFactoryType().GUID;
            }
        }
        public override string ProjectType {
            get {
                return GetProjectName();
            }
        }
        internal override object Object {
            get {
                return null;
            }
        }
        #endregion

        #region overridden methods

        public override object GetAutomationObject() {
            if (_automationObject == null) {
                _automationObject = base.GetAutomationObject();
            }
            return _automationObject;
        }

        internal override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == CommonConstants.Std97CmdGroupGuid) {
                switch ((VSConstants.VSStd97CmdID)cmd) {
                    case VSConstants.VSStd97CmdID.BuildCtx:
                    case VSConstants.VSStd97CmdID.RebuildCtx:
                    case VSConstants.VSStd97CmdID.CleanCtx:
                        result = QueryStatusResult.SUPPORTED | QueryStatusResult.INVISIBLE;
                        return VSConstants.S_OK;
                }
            } else if (cmdGroup == Microsoft.VisualStudioTools.Project.VsMenus.guidStandardCommandSet2K) {
                switch ((VsCommands2K)cmd) {
                    case VsCommands2K.ECMD_PUBLISHSELECTION:
                        if (pCmdText != IntPtr.Zero && NativeMethods.OLECMDTEXT.GetFlags(pCmdText) == NativeMethods.OLECMDTEXT.OLECMDTEXTF.OLECMDTEXTF_NAME) {
                            NativeMethods.OLECMDTEXT.SetText(pCmdText, "Publish " + this.Caption);
                        }

                        if (IsPublishingEnabled) {
                            result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        } else {
                            result |= QueryStatusResult.SUPPORTED;
                        }
                        return VSConstants.S_OK;

                    case VsCommands2K.ECMD_PUBLISHSLNCTX:
                        if (IsPublishingEnabled) {
                            result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        } else {
                            result |= QueryStatusResult.SUPPORTED;
                        }
                        return VSConstants.S_OK;
                    case CommonConstants.OpenFolderInExplorerCmdId:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
                }
            } else if (cmdGroup == SharedCommandGuid) {
                switch ((SharedCommands)cmd) {
                    case SharedCommands.AddExistingFolder:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
            }
            }

            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        private bool IsPublishingEnabled {
            get {
                return !String.IsNullOrWhiteSpace(GetProjectProperty(CommonConstants.PublishUrl));
            }
        }

        internal override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == Microsoft.VisualStudioTools.Project.VsMenus.guidStandardCommandSet2K) {
                switch ((VsCommands2K)cmd) {
                    case VsCommands2K.ECMD_PUBLISHSELECTION:
                    case VsCommands2K.ECMD_PUBLISHSLNCTX:
                        Publish(PublishProjectOptions.Default, true);
                        return VSConstants.S_OK;
                    case CommonConstants.OpenFolderInExplorerCmdId:
                        Process.Start(this.ProjectHome);
                        return VSConstants.S_OK;
                }
            } else if (cmdGroup == SharedCommandGuid) {
                switch ((SharedCommands)cmd) {
                    case SharedCommands.AddExistingFolder:
                        return AddExistingFolderToNode(this);
            }
            }
            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }

        internal int AddExistingFolderToNode(HierarchyNode parent) {
            var dir = BrowseForFolder(String.Format("Add Existing Folder - {0}",Caption), parent.FullPathToChildren);
            if (dir != null) {
                DropFilesOrFolders(new[] { dir }, parent);
            }
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Publishes the project as configured by the user in the Publish option page.
        /// 
        /// If async is true this function begins the publishing and returns w/o waiting for it to complete.  No errors are reported.
        /// 
        /// If async is false this function waits for the publish to finish and raises a PublishFailedException with an
        /// inner exception indicating the underlying reason for the publishing failure.
        /// 
        /// Returns true if the publish was succeessfully started, false if the project is not configured for publishing
        /// </summary>
        public bool Publish(PublishProjectOptions publishOptions, bool async) {
            string publishUrl = publishOptions.DestinationUrl ?? GetProjectProperty(CommonConstants.PublishUrl);
            bool found = false;
            if (!String.IsNullOrWhiteSpace(publishUrl)) {
                var url = new Url(publishUrl);

                var publishers = CommonPackage.ComponentModel.GetExtensions<IProjectPublisher>();
                foreach (var publisher in publishers) {
                    if (publisher.Schema == url.Uri.Scheme) {
                        var project = new PublishProject(this, publishOptions);
                        Exception failure = null;
                        var frame = new DispatcherFrame();
                        var thread = new System.Threading.Thread(x => {
                            try {
                                publisher.PublishFiles(project, url.Uri);
                                project.Done();
                                frame.Continue = false;
                            } catch (Exception e) {
                                failure = e;
                                project.Failed(e.Message);
                                frame.Continue = false;
                            }
                        });
                        thread.Start();
                        found = true;
                        if (!async) {
                            Dispatcher.PushFrame(frame);
                            if (failure != null) {
                                throw new PublishFailedException(String.Format("Publishing of the project {0} failed", Caption), failure);
                            }
                        }
                        break;
                    }
                }

                if (!found) {
                    var statusBar = (IVsStatusbar)CommonPackage.GetGlobalService(typeof(SVsStatusbar));
                    statusBar.SetText(String.Format("Publish failed: Unknown publish scheme ({0})", url.Uri.Scheme));
                }
            } else {
                var statusBar = (IVsStatusbar)CommonPackage.GetGlobalService(typeof(SVsStatusbar));
                statusBar.SetText(String.Format("Project is not configured for publishing in project properties."));
            }
            return found;
        }

        public virtual CommonProjectConfig MakeConfiguration(string activeConfigName) {
            return new CommonProjectConfig(this, activeConfigName);
        }

        /// <summary>
        /// As we don't register files/folders in the project file, removing an item is a noop.
        /// </summary>
        public override int RemoveItem(uint reserved, uint itemId, out int result) {
            result = 1;
            return VSConstants.S_OK;
        }

        internal override void BuildAsync(uint vsopts, string config, IVsOutputWindowPane output, string target, Action<MSBuildResult, string> uiThreadCallback) {
            uiThreadCallback(MSBuildResult.Successful, target);
        }

        /// <summary>
        /// Overriding main project loading method to inject our hierarachy of nodes.
        /// </summary>
        protected override void Reload() {
            base.Reload();

            OnProjectPropertyChanged += CommonProjectNode_OnProjectPropertyChanged;

            // track file creation/deletes and update our glyphs when files start/stop existing for files in the project.
            if (_watcher != null) {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }

            string userProjectFilename = FileName + ".user";
            if (File.Exists(userProjectFilename)) {
                userBuildProject = BuildProject.ProjectCollection.LoadProject(userProjectFilename);
            }

            bool? showAllFiles = null;
            if (userBuildProject != null) {
                showAllFiles = GetShowAllFilesSetting(userBuildProject.GetPropertyValue(CommonConstants.ProjectView));
            }

            _showingAllFiles = showAllFiles ??
                GetShowAllFilesSetting(BuildProject.GetPropertyValue(CommonConstants.ProjectView)) ??
                false;

            _watcher = CreateFileSystemWatcher(ProjectHome);
            _attributesWatcher = CreateAttributesWatcher(ProjectHome);

            // add everything that's on disk that we don't have in the project
            MergeDiskNodes(this, ProjectHome);
        }

        private FileSystemWatcher CreateFileSystemWatcher(string dir) {
            var watcher = new FileSystemWatcher(dir);
            watcher.IncludeSubdirectories = true;
            watcher.Created += new FileSystemEventHandler(FileExistanceChanged);
            watcher.Deleted += new FileSystemEventHandler(FileExistanceChanged);
            watcher.Renamed += new RenamedEventHandler(FileNameChanged);
            watcher.Changed += FileContentsChanged;
            watcher.Error += WatcherError;
            watcher.EnableRaisingEvents = true;
            watcher.InternalBufferSize = 1024 * 4;  // 4k is minimum buffer size
            return watcher;
        }

        private FileSystemWatcher CreateAttributesWatcher(string dir) {
            var watcher = new FileSystemWatcher(dir);
            watcher.NotifyFilter = NotifyFilters.Attributes;
            watcher.Changed += FileAttributesChanged;
            watcher.Error += WatcherError;
            watcher.EnableRaisingEvents = true;
            watcher.InternalBufferSize = 1024 * 4;  // 4k is minimum buffer size
            return watcher;
        }

        /// <summary>
        /// When the file system watcher buffer overflows we need to scan the entire 
        /// directory for changes.
        /// </summary>
        private void WatcherError(object sender, ErrorEventArgs e) {
            bool createdChanges;
            lock (this) {
                List<FileSystemChange> changes;
                GetFileChangeList(out changes, out createdChanges);
                
                
                changes.Clear(); // none of the other changes matter now, we'll rescan the world
                changes.Add(new FileSystemChange(this, WatcherChangeTypes.All, null));
            }

            QueueProcessingChanges();
        }

        private void QueueProcessingChanges() {
            _processFileChangesTimer.Change(1000, Timeout.Infinite);
        }

        protected override void SaveMSBuildProjectFileAs(string newFileName) {
            base.SaveMSBuildProjectFileAs(newFileName);

            if (userBuildProject != null) {
                userBuildProject.Save(FileName + ".user");
            }
        }

        protected override void SaveMSBuildProjectFile(string filename) {
            base.SaveMSBuildProjectFile(filename);

            if (userBuildProject != null) {
                userBuildProject.Save(filename + ".user");
            }
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            if (this.userBuildProject != null) {
                userBuildProject.ProjectCollection.UnloadProject(userBuildProject);
            }
        }

        protected internal override int ShowAllFiles() {
            if (!QueryEditProjectFile(false)) {
                return VSConstants.E_FAIL;
            }

            if (_showingAllFiles) {
                UpdateShowAllFiles(this, enabled: false);
            } else {
                UpdateShowAllFiles(this, enabled: true);
                ExpandItem(EXPANDFLAGS.EXPF_ExpandFolder);
            }

            _showingAllFiles = !_showingAllFiles;

            string newPropValue = _showingAllFiles ? CommonConstants.ShowAllFiles : CommonConstants.ProjectFiles;

            var projProperty = BuildProject.GetProperty(CommonConstants.ProjectView);
            if (projProperty != null && 
                !projProperty.IsImported && 
                !String.IsNullOrWhiteSpace(projProperty.EvaluatedValue)) {
                // setting is persisted in main project file, update it there.
                BuildProject.SetProperty(CommonConstants.ProjectView, newPropValue);
            } else {
                // save setting in user project file
                if (userBuildProject == null) {
                    // user project file doesn't exist yet, create it.
                    userBuildProject = new MSBuild.Project(BuildProject.ProjectCollection);
                    userBuildProject.FullPath = FileName + ".user";
                }
                userBuildProject.SetProperty(CommonConstants.ProjectView, newPropValue);
                
            }
            SetProjectFileDirty(true);

            // update project state
            return VSConstants.S_OK;
        }

        private void UpdateShowAllFiles(HierarchyNode node, bool enabled) {
            for (var curNode = node.FirstChild; curNode != null; curNode = curNode.NextSibling) {
                UpdateShowAllFiles(curNode, enabled);

                var allFiles = curNode.ItemNode as AllFilesProjectElement;
                if (allFiles != null) {
                    curNode.IsVisible = enabled;
                    OnInvalidateItems(node);
                }
            }
        }

        private static bool? GetShowAllFilesSetting(string showAllFilesValue) {
            bool? showAllFiles = null;
            string showAllFilesSetting = showAllFilesValue.Trim();
            if (String.Equals(showAllFilesSetting, CommonConstants.ProjectFiles)) {
                showAllFiles = false;
            } else if (String.Equals(showAllFilesSetting, CommonConstants.ShowAllFiles)) {
                showAllFiles = true;
            }
            return showAllFiles;
        }

        /// <summary>
        /// Walks the project home directory and creates nodes for all of the files
        /// which are on disk but aren't part of the project.  We add these files and
        /// display them if the user enables Show All Files.
        /// </summary>
        private void MergeDiskNodes(HierarchyNode curParent, string dir) {
            IEnumerable<string> dirs;
            try {
                dirs = Directory.EnumerateDirectories(dir);
            } catch  {
                // directory was deleted, we don't have access, etc...
                return;
            }
            
            HashSet<HierarchyNode> missingChildren = new HashSet<HierarchyNode>(curParent.AllChildren);
            foreach (var curDir in dirs) {
                if (!Directory.Exists(dir)) {
                    // directory went away
                    break;
                }
                if (IsFileHidden(curDir)) {
                    continue;
                }

                if (IsFileSymLink(curDir)) {
                    if (IsRecursiveSymLink(dir, curDir)) {
                        // don't add recursive sym links
                        continue;
                    }

                    // track symlinks, we won't get events on the directory
                    CreateSymLinkWatcher(curDir);
                }
                var existing = AddAllFilesFolder(curParent, curDir + Path.DirectorySeparatorChar);
                missingChildren.Remove(existing);
                MergeDiskNodes(existing, curDir);
            }

            IEnumerable<string> files;
            try {
                files = Directory.EnumerateFiles(dir);
            } catch {
                // directory was deleted, we don't have access, etc...
                return;
            }

            foreach (var file in files) {
                if (!Directory.Exists(dir)) {
                    // file went away
                    break;
                }
                if (IsFileHidden(file)) {
                    continue;
                }
                missingChildren.Remove(AddAllFilesFile(curParent, file));
            }

            // remove the excluded children which are no longer there
            foreach (var child in missingChildren) {
                if (child.ItemNode.IsExcluded) {
                    RemoveSubTree(child);
                }
            }
        }

        private void RemoveSubTree(HierarchyNode node) {
            foreach (var child in node.AllChildren) {
                RemoveSubTree(child);
            }
            node.Parent.RemoveChild(node);
            _diskNodes.Remove(node.Url);
        }

        private static string GetFinalPathName(string dir) {
            using (var dirHandle = NativeMethods.CreateFile(
                dir,
                NativeMethods.FileDesiredAccess.FILE_LIST_DIRECTORY,
                NativeMethods.FileShareFlags.FILE_SHARE_DELETE |
                    NativeMethods.FileShareFlags.FILE_SHARE_READ |
                    NativeMethods.FileShareFlags.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.FileCreationDisposition.OPEN_EXISTING,
                NativeMethods.FileFlagsAndAttributes.FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero
            )) {
                if (!dirHandle.IsInvalid) {
                    uint pathLen = NativeMethods.MAX_PATH + 1;
                    uint res;
                    StringBuilder filePathBuilder;
                    for (; ; ) {
                        filePathBuilder = new StringBuilder(checked((int)pathLen));
                        res = NativeMethods.GetFinalPathNameByHandle(
                            dirHandle,
                            filePathBuilder,
                            pathLen,
                            0
                        );
                        if (res != 0 && res < pathLen) {
                            // we had enough space, and got the filename.
                            break;
                        }
                    }

                    if (res != 0) {
                        Debug.Assert(filePathBuilder.ToString().StartsWith("\\\\?\\"));
                        return filePathBuilder.ToString().Substring(4);
                    }
                }
            }
            return dir;
        }

        private static bool IsRecursiveSymLink(string parentDir, string childDir) {
            if (IsFileSymLink(parentDir)) {
                // figure out the real parent dir so the check below works w/ multiple
                // symlinks pointing at each other
                parentDir = GetFinalPathName(parentDir);
            }

            string finalPath = GetFinalPathName(childDir);
            // check and see if we're recursing infinitely...
            if (CommonUtils.IsSubpathOf(finalPath, parentDir)) {
                // skip this file
                return true;
            }
            return false;
        }

        private static bool IsFileSymLink(string path) {
            try {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            } catch (DirectoryNotFoundException) {
                return false;
            } catch (FileNotFoundException) {
                return false;
            }
        }

        private bool IsFileHidden(string path) {
            if (String.Equals(path, FileName, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(path, FileName + ".user", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if(!File.Exists(path) && !Directory.Exists(path)) {
                // if the file has disappeared avoid the exception...
                return false;
            }

            try {
                return (File.GetAttributes(path) & (FileAttributes.Hidden | FileAttributes.System)) != 0;
            } catch (DirectoryNotFoundException) {
                return false;
            } catch (FileNotFoundException) {
                return false;
            }
        }

        /// <summary>
        /// Adds a file which is displayed when Show All Files is enabled
        /// </summary>
        private HierarchyNode AddAllFilesFile(HierarchyNode curParent, string file) {
            var existing = FindNodeByFullPath(file);
            if (existing == null) {
                var newFile = CreateFileNode(new AllFilesProjectElement(file, GetItemType(file), this));
                AddAllFilesNode(curParent, newFile);
                return newFile;
            }
            return existing;
        }

        /// <summary>
        /// Adds a folder which is displayed when Show All files is enabled
        /// </summary>
        private HierarchyNode AddAllFilesFolder(HierarchyNode curParent, string curDir) {
            var folderNode = FindNodeByFullPath(curDir);
            if (folderNode == null) {
                folderNode = CreateFolderNode(new AllFilesProjectElement(curDir, "Folder", this));
                AddAllFilesNode(curParent, folderNode);

                // Solution Explorer will expand the parent when an item is
                // added, which we don't want
                folderNode.ExpandItem(EXPANDFLAGS.EXPF_CollapseFolder);
            }
            return folderNode;
        }

        /// <summary>
        /// Initializes and adds a file or folder visible only when Show All files is enabled
        /// </summary>
        private void AddAllFilesNode(HierarchyNode parent, HierarchyNode newNode) {
            newNode.IsVisible = IsShowingAllFiles;
            newNode.ID = ItemIdMap.Add(newNode);
            parent.AddChild(newNode);
        }

        private void FileContentsChanged(object sender, FileSystemEventArgs e) {
            if (IsClosed) {
                return;
            }

            FileSystemEventHandler handler;
            if (_fileChangedHandlers.TryGetValue(e.FullPath, out handler)) {
                handler(sender, e);
            }
        }

        private void FileAttributesChanged(object sender, FileSystemEventArgs e) {
            bool createdChanges;
            lock (this) {
                List<FileSystemChange> changes;
                GetFileChangeList(out changes, out createdChanges);

                if (changes.Count == 0 || changes[0]._type != WatcherChangeTypes.All) {
                    changes.Add(new FileSystemChange(this, WatcherChangeTypes.Changed, e.FullPath));
                }
            }

            QueueProcessingChanges();
        }

        internal void RegisterFileChangeNotification(FileNode node, FileSystemEventHandler handler) {
            _fileChangedHandlers[node.Url] = handler;
        }

        internal void UnregisterFileChangeNotification(FileNode node) {
            _fileChangedHandlers.Remove(node.Url);
        }

        protected override ReferenceContainerNode CreateReferenceContainerNode() {
            return new CommonReferenceContainerNode(this);
        }

        private void FileNameChanged(object sender, RenamedEventArgs e) {
            if (IsClosed) {
                return;
            }

            bool createdChanges;
            lock (this) {
                List<FileSystemChange> changes;
                GetFileChangeList(out changes, out createdChanges);

                // we just generate a delete and creation here - we're just updating the hierarchy
                // either changing icons or updating the non-project elements, so we don't need to
                // generate rename events or anything like that.  This saves us from having to 
                // handle updating the hierarchy in a special way for renames.
                if (changes.Count == 0 || changes[0]._type != WatcherChangeTypes.All) {
                    changes.Add(new FileSystemChange(this, WatcherChangeTypes.Deleted, e.OldFullPath, true));
                    changes.Add(new FileSystemChange(this, WatcherChangeTypes.Created, e.FullPath, true));
                }
            }

            QueueProcessingChanges();
        }

        private void FileExistanceChanged(object sender, FileSystemEventArgs e) {
            if (IsClosed) {
                return;
            }
            bool createdChanges;
            
            lock (this) {
                List<FileSystemChange> changes;
                GetFileChangeList(out changes, out createdChanges);

                if (changes.Count == 0 || changes[0]._type != WatcherChangeTypes.All) {
                    changes.Add(new FileSystemChange(this, e.ChangeType, e.FullPath));
                }
            }
            QueueProcessingChanges();
        }

        /// <summary>
        /// Gets or creates the current list of file change events to be processed.  We update the
        /// current list until the UI thread pulls it and starts processing it.  This keeps the
        /// response to the event fast even if we can't immediately make it back to the UI thread and
        /// keeps the # of posts low when the UI thread is busy.
        /// </summary>
        private void GetFileChangeList(out List<FileSystemChange> changes, out bool createdChanges) {
            createdChanges = false;
            lock (_fileSystemChangesLock) {
                if (_fileSystemChanges == null) {
                    _fileSystemChanges = new List<FileSystemChange>();
                    createdChanges = true;
                }
                changes = _fileSystemChanges;
            }
        }

        internal void CreateSymLinkWatcher(string curDir) {
            if (!CommonUtils.HasEndSeparator(curDir)) {
                curDir = curDir + Path.DirectorySeparatorChar;
            }
            _symlinkWatchers[curDir] = CreateFileSystemWatcher(curDir);
        }

        internal bool TryDeactivateSymLinkWatcher(HierarchyNode child) {
            FileSystemWatcher watcher;
            if (_symlinkWatchers.TryGetValue(child.Url, out watcher)) {
                _symlinkWatchers.Remove(child.Url);
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                return true;
            }
            return false;
        }

        private void ProcessFileChanges(object arg) {
            _uiSync.BeginInvoke(new Action(ProcessFileChangesUIThread), new object[0]);
        }

        /// <summary>
        /// Processes the current file system changes on the UI thread.
        /// </summary>
        private void ProcessFileChangesUIThread() {
            List<FileSystemChange> changes;
            lock (_fileSystemChangesLock) {
                changes = _fileSystemChanges;
                _fileSystemChanges = null;
            }

            using (new DebugTimer("ProcessFileChanges on UI Thread")) {
                foreach (var change in changes) {
                    if (IsClosed) {
                        return;
                    }

#if DEBUG
                    try {
#endif
                        change.ProcessChange();
#if DEBUG
                    } catch (Exception e) {
                        Debug.Fail("Unexpected exception while processing change: {0}", e.ToString());
                        throw;
                    }
#endif
                }
            }
        }

        /// <summary>
        /// Represents an individual change to the file system.  We process these in bulk on the
        /// UI thread.
        /// </summary>
        class FileSystemChange {
            private readonly CommonProjectNode _project;
            internal readonly WatcherChangeTypes _type;
            private readonly string _path;
            private readonly bool _isRename;

            public FileSystemChange(CommonProjectNode node, WatcherChangeTypes changeType, string path, bool isRename = false) {
                _project = node;
                _type = changeType;
                _path = path;
                _isRename = isRename;
            }

            public override string ToString() {
                return "FileSystemChange: " + _isRename + " " + _type + " " + _path;
            }

            private void RedrawIcon(HierarchyNode node) {
                _project.ReDrawNode(node, UIHierarchyElement.Icon);

                for (var child = node.FirstChild; child != null; child = child.NextSibling) {
                    RedrawIcon(child);
                }
            }

            public void ProcessChange() {
                if (_type == WatcherChangeTypes.All) {
                    WatcherError();
                    return;
                }

                var child = _project.FindNodeByFullPath(_path);
                if ((_type == WatcherChangeTypes.Deleted || _type == WatcherChangeTypes.Changed) && child == null) {
                    child = _project.FindNodeByFullPath(_path + Path.DirectorySeparatorChar);
                }
                switch (_type) {
                    case WatcherChangeTypes.Deleted:
                        ChildDeleted(child); 
                        break;
                    case WatcherChangeTypes.Created: ChildCreated(child); break;
                    case WatcherChangeTypes.Changed:
                        // we only care about the attributes
                        if (_project.IsFileHidden(_path)) {
                            if (child != null) {
                                // attributes must of changed to hidden, remove the file
                                goto case WatcherChangeTypes.Deleted;
                            }
                        } else {
                            if (child == null) {
                                // attributes must of changed from hidden, add the file
                                goto case WatcherChangeTypes.Created;
                            }
                        }
                        break;
                }
            }

            private void RemoveAllFilesChildren(HierarchyNode parent) {
                bool removed = false;

                for (var current = parent.FirstChild; current != null; current = current.NextSibling) {
                    // remove our children first
                    RemoveAllFilesChildren(current);

                    _project.TryDeactivateSymLinkWatcher(current);

                    // then remove us if we're an all files node
                    if (current.ItemNode is AllFilesProjectElement) {
                        parent.RemoveChild(current);
                        _project.OnItemDeleted(current);
                        removed = true;
                    }
                }

                if (removed) {
                    _project.OnInvalidateItems(parent);
                }
            }

            private void WatcherError() {
                // merge the current ones back in
                using (new DebugTimer("Merging on error")) {
                    var waitDialog = (IVsThreadedWaitDialog)_project.GetService(typeof(SVsThreadedWaitDialog));
                    int waitResult = waitDialog.StartWaitDialog(
                        "Updating project system...",
                        "Syncing project system with files on disk, this may take several seconds...",
                        null,
                        0,
                        null,
                        null
                    );
                    try {
                        _project.MergeDiskNodes(_project, _project.ProjectHome);
                    } finally {
                        if (ErrorHandler.Succeeded(waitResult)) {
                            int cancelled = 0;
                            waitDialog.EndWaitDialog(ref cancelled);
                        }
                    }
                }
            }

            private void ChildDeleted(HierarchyNode child) {
                if (child != null) {
                    _project.TryDeactivateSymLinkWatcher(child);

                    if (child.ItemNode.IsExcluded) {
                        RemoveAllFilesChildren(child);

                        // deleting a show all files item, remove the node.
                        child.Parent.RemoveChild(child);
                        _project.OnItemDeleted(child);
                    } else {
                        Debug.Assert(!child.IsNonMemberItem);
                        // deleting an item in the project, fix the icon, also
                        // fix the icon of all children which we may have not
                        // received delete notifications for
                        RedrawIcon(child);
                    }
                }
            }            

            private void ChildCreated(HierarchyNode child) {
                if (child != null) {
                    // creating an item which was in the project, fix the icon.
                    _project.ReDrawNode(child, UIHierarchyElement.Icon);
                } else {
                    if (_project.IsFileHidden(_path)) {
                        // don't add hidden files/folders
                        return;
                    }

                    // creating a new item, need to create the on all files node
                    string parentDir = Path.GetDirectoryName(CommonUtils.TrimEndSeparator(_path)) + Path.DirectorySeparatorChar;
                    HierarchyNode parent;
                    if (CommonUtils.IsSamePath(parentDir, _project.ProjectHome)) {
                        parent = _project;
                    } else {
                        parent = _project.FindNodeByFullPath(parentDir);
                    }

                    if (parent == null) {
                        // we've hit an error while adding too many files, the file system watcher
                        // couldn't keep up.  That's alright, we'll merge the files in correctly 
                        // in a little while...
                        return;
                    }

                    IVsUIHierarchyWindow uiHierarchy = UIHierarchyUtilities.GetUIHierarchyWindow(_project.Site, HierarchyNode.SolutionExplorer);
                    uint curState;
                    uiHierarchy.GetItemState(_project.GetOuterInterface<IVsUIHierarchy>(),
                        parent.ID,
                        (uint)__VSHIERARCHYITEMSTATE.HIS_Expanded,
                        out curState
                    );

                    if (Directory.Exists(_path)) {
                        if (IsFileSymLink(_path)) {
                            if (IsRecursiveSymLink(parentDir, _path)) {
                                // don't add recusrive sym link directory
                                return;
                            }

                            // otherwise we're going to need a new file system watcher
                            _project.CreateSymLinkWatcher(_path);
                        }

                        var folderNode = _project.AddAllFilesFolder(parent, _path + Path.DirectorySeparatorChar);
                        // we may have just moved a directory from another location (e.g. drag and
                        // and drop in explorer), in which case we also need to process the items
                        // which are in the folder that we won't receive create notifications for.

                        if (_isRename) {
                            // First, make sure we don't have any children
                            RemoveAllFilesChildren(folderNode);

                            // then add the folder nodes
                            _project.MergeDiskNodes(folderNode, _path);
                        }

                        _project.OnInvalidateItems(folderNode);
                    } else {
                        _project.AddAllFilesFile(parent, _path);
                    }

                    if ((curState & (uint)__VSHIERARCHYITEMSTATE.HIS_Expanded) == 0) {
                        // Solution Explorer will expand the parent when an item is
                        // added, which we don't want, so we check it's state before
                        // adding, and then collapse the folder if it was expanded.
                        parent.ExpandItem(EXPANDFLAGS.EXPF_CollapseFolder);
                    }
                }
            }
        }


        public override int GetGuidProperty(int propid, out Guid guid) {
            if ((__VSHPROPID)propid == __VSHPROPID.VSHPROPID_PreferredLanguageSID) {
                guid = new Guid("{EFB9A1D6-EA71-4F38-9BA7-368C33FCE8DC}");// GetLanguageServiceType().GUID;
            } else {
                return base.GetGuidProperty(propid, out guid);
            }
            return VSConstants.S_OK;
        }

        protected override bool IsItemTypeFileType(string type) {
            if (!base.IsItemTypeFileType(type)) {
                if (String.Compare(type, "Page", StringComparison.OrdinalIgnoreCase) == 0
                || String.Compare(type, "ApplicationDefinition", StringComparison.OrdinalIgnoreCase) == 0
                || String.Compare(type, "Resource", StringComparison.OrdinalIgnoreCase) == 0) {
                    return true;
                } else {
                    return false;
                }
            } else {
                //This is a well known item node type, so return true.
                return true;
            }
        }

        protected override NodeProperties CreatePropertiesObject() {
            return new CommonProjectNodeProperties(this);
        }

        public override int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider site) {
            base.SetSite(site);

            //Initialize a new object to track project document changes so that we can update the StartupFile Property accordingly
            _projectDocListenerForStartupFileUpdates = new ProjectDocumentsListenerForStartupFileUpdates((ServiceProvider)Site, this);
            _projectDocListenerForStartupFileUpdates.Init();

            return VSConstants.S_OK;
        }

        public override void Close() {
            if (null != _projectDocListenerForStartupFileUpdates) {
                _projectDocListenerForStartupFileUpdates.Dispose();
                _projectDocListenerForStartupFileUpdates = null;
            }
            LibraryManager libraryManager = ((IServiceContainer)Package).GetService(GetLibraryManagerType()) as LibraryManager;
            if (null != libraryManager) {
                libraryManager.UnregisterHierarchy(InteropSafeHierarchy);
            }
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;

            _attributesWatcher.EnableRaisingEvents = false;
            _attributesWatcher.Dispose();
            _attributesWatcher = null;

            _processFileChangesTimer.Dispose();

            base.Close();
        }

        public override void Load(string filename, string location, string name, uint flags, ref Guid iidProject, out int canceled) {
            base.Load(filename, location, name, flags, ref iidProject, out canceled);
            LibraryManager libraryManager = Site.GetService(GetLibraryManagerType()) as LibraryManager;
            if (null != libraryManager) {
                libraryManager.RegisterHierarchy(InteropSafeHierarchy);
            }


            //If this is a WPFFlavor-ed project, then add a project-level DesignerContext service to provide
            //event handler generation (EventBindingProvider) for the XAML designer.
            this.OleServiceProvider.AddService(typeof(DesignerContext), new OleServiceProvider.ServiceCreatorCallback(this.CreateServices), false);
        }

        internal void BoldStartupItem(HierarchyNode startupItem) {
            if (!_boldedStartupItem) {
                if (BoldItem(startupItem, true)) {
                    _boldedStartupItem = true;
                }
            }
        }

        public bool BoldItem(HierarchyNode node, bool isBold) {
            IVsUIHierarchyWindow2 windows = GetUIHierarchyWindow(
                Site as IServiceProvider,
                new Guid(ToolWindowGuids80.SolutionExplorer)) as IVsUIHierarchyWindow2;

            if (ErrorHandler.Succeeded(windows.SetItemAttribute(
                this.GetOuterInterface<IVsUIHierarchy>(),
                node.ID,
                (uint)__VSHIERITEMATTRIBUTE.VSHIERITEMATTRIBUTE_Bold,
                isBold
            ))) {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Overriding to provide project general property page
        /// </summary>
        /// <returns></returns>
        protected override Guid[] GetConfigurationIndependentPropertyPages() {
            var pageType = GetGeneralPropertyPageType();
            if (pageType != null) {
                return new[] { pageType.GUID };
        }
            return new Guid[0];
        }

        /// <summary>
        /// Create a file node based on an msbuild item.
        /// </summary>
        /// <param name="item">The msbuild item to be analyzed</param>        
        public override FileNode CreateFileNode(ProjectElement item) {
            Utilities.ArgumentNotNull("item", item);

            CommonFileNode newNode;
            if (IsCodeFile(item.GetFullPathForElement())) {
                newNode = CreateCodeFileNode(item);
            } else {
                newNode = CreateNonCodeFileNode(item);
            }

            string link = item.GetMetadata(ProjectFileConstants.Link);
            if (!String.IsNullOrWhiteSpace(link) ||
                !CommonUtils.IsSubpathOf(ProjectHome, item.GetFullPathForElement())) {
                newNode.SetIsLinkFile(true);
            }

            string include = item.GetMetadata(ProjectFileConstants.Include);

            newNode.OleServiceProvider.AddService(typeof(EnvDTE.Project),
                new OleServiceProvider.ServiceCreatorCallback(CreateServices), false);
            newNode.OleServiceProvider.AddService(typeof(EnvDTE.ProjectItem), newNode.ServiceCreator, false);

            if (!string.IsNullOrEmpty(include) && Path.GetExtension(include).Equals(".xaml", StringComparison.OrdinalIgnoreCase)) {
                //Create a DesignerContext for the XAML designer for this file
                newNode.OleServiceProvider.AddService(typeof(DesignerContext), newNode.ServiceCreator, false);
            }

            newNode.OleServiceProvider.AddService(typeof(VSLangProj.VSProject),
                new OleServiceProvider.ServiceCreatorCallback(CreateServices), false);
            return newNode;
        }

        /// <summary>
        /// Create a file node based on absolute file name.
        /// </summary>
        public override FileNode CreateFileNode(string absFileName) {
            // Avoid adding files to the project multiple times.  Ultimately           
            // we should not use project items and instead should have virtual items.       

            string path = CommonUtils.GetRelativeFilePath(ProjectHome, absFileName);
            var prjItem = BuildProject.AddItem(GetItemType(path), path)[0];

            return CreateFileNode(new MsBuildProjectElement(this, prjItem));
        }

        internal string GetItemType(string filename) {
            if (IsCodeFile(filename)) {
                return "Compile";
            } else {
                return "Content";
            }
        }

        public ProjectElement MakeProjectElement(string type, string path) {
            var item = BuildProject.AddItem(type, path)[0];
            return new MsBuildProjectElement(this, item);
        }

        public override int IsDirty(out int isDirty) {
            isDirty = 0;
            if (IsProjectFileDirty) {
                isDirty = 1;
                return VSConstants.S_OK;
            }

            isDirty = IsFlavorDirty();
            return VSConstants.S_OK;
        }

        protected override void AddNewFileNodeToHierarchy(HierarchyNode parentNode, string fileName) {
            base.AddNewFileNodeToHierarchy(parentNode, fileName);

            SetProjectFileDirty(true);
        }

        public override DependentFileNode CreateDependentFileNode(MsBuildProjectElement item) {
            DependentFileNode node = base.CreateDependentFileNode(item);
            if (null != node) {
                string include = item.GetMetadata(ProjectFileConstants.Include);
                if (IsCodeFile(include)) {
                    node.OleServiceProvider.AddService(
                        typeof(SVSMDCodeDomProvider), new OleServiceProvider.ServiceCreatorCallback(CreateServices), false);
                }
            }

            return node;
        }

        /// <summary>
        /// Creates the format list for the open file dialog
        /// </summary>
        /// <param name="formatlist">The formatlist to return</param>
        /// <returns>Success</returns>
        public override int GetFormatList(out string formatlist) {
            formatlist = GetFormatList();
            return VSConstants.S_OK;
        }

        protected override ConfigProvider CreateConfigProvider() {
            return new CommonConfigProvider(this);
        }
        #endregion

        #region Methods

        /// <summary>
        /// This method retrieves an instance of a service that 
        /// allows to start a project or a file with or without debugging.
        /// </summary>
        public abstract IProjectLauncher/*!*/ GetLauncher();

        /// <summary>
        /// Returns resolved value of the current working directory property.
        /// </summary>
        public string GetWorkingDirectory() {
            string workDir = GetProjectProperty(CommonConstants.WorkingDirectory, true);

            return CommonUtils.GetAbsoluteDirectoryPath(ProjectHome, workDir);
        }

        /// <summary>
        /// Returns resolved value of the startup file property.
        /// </summary>
        internal string GetStartupFile() {
            string startupFile = GetProjectProperty(CommonConstants.StartupFile, true);

            if (string.IsNullOrEmpty(startupFile)) {
                //No startup file is assigned
                return null;
            }

            return CommonUtils.GetAbsoluteFilePath(ProjectHome, startupFile);
        }

        /// <summary>
        /// Whenever project property has changed - refresh project hierarachy.
        /// </summary>
        private void CommonProjectNode_OnProjectPropertyChanged(object sender, ProjectPropertyChangedArgs e) {
            switch (e.PropertyName) {
                case CommonConstants.StartupFile:
                    RefreshStartupFile(this,
                        CommonUtils.GetAbsoluteFilePath(ProjectHome, e.OldValue),
                        CommonUtils.GetAbsoluteFilePath(ProjectHome, e.NewValue));
                    break;
            }
        }

        /// <summary>
        /// Same as VsShellUtilities.GetUIHierarchyWindow, but it doesn't contain a useless cast to IVsWindowPane
        /// which fails on Dev10 with the solution explorer window.
        /// </summary>
        private static IVsUIHierarchyWindow GetUIHierarchyWindow(IServiceProvider serviceProvider, Guid guidPersistenceSlot) {
            if (serviceProvider == null) {
                throw new ArgumentException("serviceProvider");
            }
            IVsUIShell service = serviceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (service == null) {
                throw new InvalidOperationException();
            }
            object pvar = null;
            IVsWindowFrame ppWindowFrame = null;
            IVsUIHierarchyWindow window = null;
            try {
                ErrorHandler.ThrowOnFailure(service.FindToolWindow(0, ref guidPersistenceSlot, out ppWindowFrame));
                ErrorHandler.ThrowOnFailure(ppWindowFrame.GetProperty(-3001, out pvar));
            } catch (COMException exception) {
                Trace.WriteLine("Exception :" + exception.Message);
            } finally {
                if (pvar != null) {
                    window = (IVsUIHierarchyWindow)pvar;
                }
            }
            return window;
        }

        /// <summary>
        /// Returns first immediate child node (non-recursive) of a given type.
        /// </summary>
        private void RefreshStartupFile(HierarchyNode parent, string oldFile, string newFile) {
            IVsUIHierarchyWindow2 windows = GetUIHierarchyWindow(
                Site,
                new Guid(ToolWindowGuids80.SolutionExplorer)) as IVsUIHierarchyWindow2;

            for (HierarchyNode n = parent.FirstChild; n != null; n = n.NextSibling) {
                // TODO: Distinguish between real Urls and fake ones (eg. "References")
                if (windows != null) {
                    var absUrl = CommonUtils.GetAbsoluteFilePath(parent.ProjectMgr.ProjectHome, n.Url);
                    if (CommonUtils.IsSamePath(oldFile, absUrl)) {
                        windows.SetItemAttribute(
                            this,
                            n.ID,
                            (uint)__VSHIERITEMATTRIBUTE.VSHIERITEMATTRIBUTE_Bold,
                            false
                        );
                        ReDrawNode(n, UIHierarchyElement.Icon);
                    } else if (CommonUtils.IsSamePath(newFile, absUrl)) {
                        windows.SetItemAttribute(
                            this,
                            n.ID,
                            (uint)__VSHIERITEMATTRIBUTE.VSHIERITEMATTRIBUTE_Bold,
                            true
                        );
                        ReDrawNode(n, UIHierarchyElement.Icon);
                    }
                }

                RefreshStartupFile(n, oldFile, newFile);
            }
        }

        /// <summary>
        /// Provide mapping from our browse objects and automation objects to our CATIDs
        /// </summary>
        private void InitializeCATIDs() {
            // The following properties classes are specific to current language so we can use their GUIDs directly
            AddCATIDMapping(typeof(OAProject), typeof(OAProject).GUID);
            // The following is not language specific and as such we need a separate GUID
            AddCATIDMapping(typeof(FolderNodeProperties), new Guid(CommonConstants.FolderNodePropertiesGuid));
            // These ones we use the same as language file nodes since both refer to files
            AddCATIDMapping(typeof(FileNodeProperties), typeof(FileNodeProperties).GUID);
            AddCATIDMapping(typeof(IncludedFileNodeProperties), typeof(IncludedFileNodeProperties).GUID);
            // Because our property page pass itself as the object to display in its grid, 
            // we need to make it have the same CATID
            // as the browse object of the project node so that filtering is possible.
            var genPropPage = GetGeneralPropertyPageType();
            if (genPropPage != null) {
            AddCATIDMapping(GetGeneralPropertyPageType(), GetGeneralPropertyPageType().GUID);
            }
            // We could also provide CATIDs for references and the references container node, if we wanted to.
        }

        /// <summary>
        /// Parses SearchPath property into a list of distinct absolute paths, preserving the order.
        /// </summary>
        protected IList<string> ParseSearchPath() {
            var searchPath = GetProjectProperty(CommonConstants.SearchPath, true);
            return ParseSearchPath(searchPath);
        }

        /// <summary>
        /// Parses SearchPath string into a list of distinct absolute paths, preserving the order.
        /// </summary>
        protected IList<string> ParseSearchPath(string searchPath) {
            var result = new List<string>();

            if (!string.IsNullOrEmpty(searchPath)) {
                var seen = new HashSet<string>();
                foreach (var path in searchPath.Split(';')) {
                    if (string.IsNullOrEmpty(path)) {
                        continue;
                    }

                    var absPath = CommonUtils.GetAbsoluteFilePath(ProjectHome, path);
                    if (seen.Add(absPath)) {
                        result.Add(absPath);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Saves list of paths back as SearchPath project property.
        /// </summary>
        private void SaveSearchPath(IList<string> value) {
            var valueStr = string.Join(";", value.Select(path => {
                var relPath = CommonUtils.GetRelativeFilePath(ProjectHome, path);
                if (string.IsNullOrEmpty(relPath)) {
                    relPath = ".";
            }
                return relPath;
            }));
            SetProjectProperty(CommonConstants.SearchPath, valueStr);
        }

        /// <summary>
        /// Adds new search path to the SearchPath project property.
        /// </summary>
        internal void AddSearchPathEntry(string newpath) {
            Utilities.ArgumentNotNull("newpath", newpath);

            IList<string> searchPath = ParseSearchPath();
            var absPath = CommonUtils.GetAbsoluteFilePath(ProjectHome, newpath);
            if (searchPath.Contains(absPath, StringComparer.OrdinalIgnoreCase)) {
                return;
            }
            searchPath.Add(absPath);
            SaveSearchPath(searchPath);
        }

        /// <summary>
        /// Removes a given path from the SearchPath property.
        /// </summary>
        internal void RemoveSearchPathEntry(string path) {
            IList<string> searchPath = ParseSearchPath();
            var absPath = CommonUtils.GetAbsoluteFilePath(ProjectHome, path);
            if (searchPath.Remove(path)) {
                SaveSearchPath(searchPath);
            }
        }

        /// <summary>
        /// Creates the services exposed by this project.
        /// </summary>
        private object CreateServices(Type serviceType) {
            object service = null;
            if (typeof(VSLangProj.VSProject) == serviceType) {
                service = VSProject;
            } else if (typeof(EnvDTE.Project) == serviceType) {
                service = GetAutomationObject();
            } else if (typeof(DesignerContext) == serviceType) {
                service = this.DesignerContext;
            }

            return service;
        }

        protected virtual internal Microsoft.Windows.Design.Host.DesignerContext DesignerContext {
            get {
                return null;
            }
        }

        /// <summary>
        /// Executes Add Search Path menu command.
        /// </summary>        
        internal int AddSearchPath() {
            string dirName = BrowseForFolder(
                DynamicProjectSR.GetString(DynamicProjectSR.SelectFolderForSearchPath), 
                ProjectHome);

            if (dirName != null) {
                AddSearchPathEntry(dirName);
            }
            
            return VSConstants.S_OK;
        }

        internal string BrowseForFolder(string title, string initialDir) {
            // Get a reference to the UIShell.
            IVsUIShell uiShell = GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (null == uiShell) {
                return null;
            }

            //Create a fill in a structure that defines Browse for folder dialog
            VSBROWSEINFOW[] browseInfo = new VSBROWSEINFOW[1];
            //Dialog title
            browseInfo[0].pwzDlgTitle = title;
            //Initial directory - project directory
            browseInfo[0].pwzInitialDir = initialDir;
            //Parent window
            uiShell.GetDialogOwnerHwnd(out browseInfo[0].hwndOwner);
            //Max path length
            //This is WCHARS not bytes
            browseInfo[0].nMaxDirName = (uint)NativeMethods.MAX_PATH;
            //This struct size
            browseInfo[0].lStructSize = (uint)Marshal.SizeOf(typeof(VSBROWSEINFOW));
            //Memory to write selected directory to.
            //Note: this one allocates unmanaged memory, which must be freed later
            //  Add 1 for the null terminator and double since we are WCHARs not bytes
            IntPtr pDirName = Marshal.AllocCoTaskMem((NativeMethods.MAX_PATH + 1)*2);
            browseInfo[0].pwzDirName = pDirName;
            try {
                //Show the dialog
                int hr = uiShell.GetDirectoryViaBrowseDlg(browseInfo);
                if (hr == VSConstants.OLE_E_PROMPTSAVECANCELLED) {
                    //User cancelled the dialog
                    return null;
                }
                //Check for any failures
                ErrorHandler.ThrowOnFailure(hr);
                //Get selected directory
                return Marshal.PtrToStringAuto(browseInfo[0].pwzDirName);
            } finally {
                //Free allocated unmanaged memory
                if (pDirName != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(pDirName);
                }
            }
        }

        #endregion

        #region IVsProjectSpecificEditorMap2 Members

        public int GetSpecificEditorProperty(string mkDocument, int propid, out object result) {
            // initialize output params
            result = null;

            //Validate input
            if (string.IsNullOrEmpty(mkDocument))
                throw new ArgumentException("Was null or empty", "mkDocument");

            // Make sure that the document moniker passed to us is part of this project
            // We also don't care if it is not a dynamic language file node
            uint itemid;
            int hr;
            if (ErrorHandler.Failed(hr = ParseCanonicalName(mkDocument, out itemid))) {
                return hr;
            }
            HierarchyNode hierNode = NodeFromItemId(itemid);
            if (hierNode == null || ((hierNode as CommonFileNode) == null))
                return VSConstants.E_NOTIMPL;

            switch (propid) {
                case (int)__VSPSEPROPID.VSPSEPROPID_UseGlobalEditorByDefault:
                    // don't show project default editor, every file supports Python.
                    result = false;
                    return VSConstants.E_FAIL;
                /*case (int)__VSPSEPROPID.VSPSEPROPID_ProjectDefaultEditorName:
                    result = "Python Editor";
                    break;*/
            }

            return VSConstants.S_OK;
        }

        public int GetSpecificEditorType(string mkDocument, out Guid guidEditorType) {
            // Ideally we should at this point initalize a File extension to EditorFactory guid Map e.g.
            // in the registry hive so that more editors can be added without changing this part of the
            // code. Dynamic languages only make usage of one Editor Factory and therefore we will return 
            // that guid
            guidEditorType = GetEditorFactoryType().GUID;
            return VSConstants.S_OK;
        }

        public int GetSpecificLanguageService(string mkDocument, out Guid guidLanguageService) {
            guidLanguageService = Guid.Empty;
            return VSConstants.E_NOTIMPL;
        }

        public int SetSpecificEditorProperty(string mkDocument, int propid, object value) {
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IVsDeferredSaveProject Members

        /// <summary>
        /// Implements deferred save support.  Enabled by unchecking Tools->Options->Solutions and Projects->Save New Projects Created.
        /// 
        /// In this mode we save the project when the user selects Save All.  We need to move all the files in the project
        /// over to the new location.
        /// </summary>
        public virtual int SaveProjectToLocation(string pszProjectFilename) {
            string oldName = Url;
            string basePath = CommonUtils.NormalizeDirectoryPath(Path.GetDirectoryName(this.FileName));
            string newName = Path.GetDirectoryName(pszProjectFilename);

            IVsUIShell shell = this.Site.GetService(typeof(SVsUIShell)) as IVsUIShell;
            IVsSolution vsSolution = (IVsSolution)this.GetService(typeof(SVsSolution));

            int canContinue;
            vsSolution.QueryRenameProject(this, FileName, pszProjectFilename, 0, out canContinue);
            if (canContinue == 0) {
                return VSConstants.OLE_E_PROMPTSAVECANCELLED;
            }

            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();

            // we don't use RenameProjectFile because it sends the OnAfterRenameProject event too soon
            // and causes VS to think the solution has changed on disk.  We need to send it after all 
            // updates are complete.

            // save the new project to to disk
            SaveMSBuildProjectFileAs(pszProjectFilename);

            if (CommonUtils.IsSameDirectory(ProjectHome, basePath)) {
                // ProjectHome was set by SaveMSBuildProjectFileAs to point to the temporary directory.
                BuildProject.SetProperty(CommonConstants.ProjectHome, ".");

                // save the project again w/ updated file info
                BuildProjectLocationChanged();

                // remove all the children, saving any dirty files, and collecting the list of open files
                MoveFilesForDeferredSave(this, basePath, newName);
            } else {
                // Project referenced external files only, so just update its location without moving
                // files around.
                BuildProjectLocationChanged();
            }

            BuildProject.Save();

            SetProjectFileDirty(false);

            // update VS that we've changed the project
            this.OnPropertyChanged(this, (int)__VSHPROPID.VSHPROPID_Caption, 0);

            // Update solution
            // Note we ignore the errors here because reporting them to the user isn't really helpful.
            // We've already completed all of the work to rename everything here.  If OnAfterNameProject
            // fails for some reason then telling the user it failed is just confusing because all of
            // the work is done.  And if someone wanted to prevent us from renaming the project file they
            // should have responded to QueryRenameProject.  Likewise if we can't refresh the property browser 
            // for any reason then that's not too interesting either - the users project has been saved to 
            // the new location.
            // http://pytools.codeplex.com/workitem/489
            vsSolution.OnAfterRenameProject((IVsProject)this, oldName, pszProjectFilename, 0);

            shell.RefreshPropertyBrowser(0);

            _watcher = CreateFileSystemWatcher(ProjectHome);
            _attributesWatcher = CreateAttributesWatcher(ProjectHome);

            return VSConstants.S_OK;
        }

        private void MoveFilesForDeferredSave(HierarchyNode node, string basePath, string baseNewPath) {
            if (node != null) {
                for (var child = node.FirstChild; child != null; child = child.NextSibling) {
                    bool isOpen, isDirty, isOpenedByUs;
                    uint docCookie;
                    IVsPersistDocData persist;
                    var docMgr = child.GetDocumentManager();
                    if (docMgr != null) {
                        docMgr.GetDocInfo(out isOpen, out isDirty, out isOpenedByUs, out docCookie, out persist);
                        int cancelled;
                        if (isDirty) {
                            child.ProjectMgr.SaveItem(VSSAVEFLAGS.VSSAVE_Save, null, docCookie, IntPtr.Zero, out cancelled);
                        }
                    }

                    IDiskBasedNode diskNode = child as IDiskBasedNode;
                    if (diskNode != null) {
                        diskNode.RenameForDeferredSave(basePath, baseNewPath);
                    }

                    MoveFilesForDeferredSave(child, basePath, baseNewPath);
                }
            }
        }

        #endregion

        internal void SuppressFileChangeNotifications() {
            _watcher.EnableRaisingEvents = false;
            _suppressFileWatcherCount++;
        }

        internal void RestoreFileChangeNotifications() {
            if (--_suppressFileWatcherCount == 0) {
                _watcher.EnableRaisingEvents = true;
            }
        }

#if DEV11_OR_LATER
        public override object GetProperty(int propId) {            
            CommonFolderNode.BoldStartupOnIcon(propId, this);

            return base.GetProperty(propId);
        }
#else
        public override int SetProperty(int propid, object value) {
            CommonFolderNode.BoldStartupOnExpand(propid, this);

            return base.SetProperty(propid, value);
        }
#endif
    }
}
