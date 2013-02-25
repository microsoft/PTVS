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
using System.Diagnostics.Contracts;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Project.Automation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Windows.Design.Host;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using VSConstants = Microsoft.VisualStudio.VSConstants;

namespace Microsoft.PythonTools.Project {

    public enum CommonImageName {
        File = 0,
        Project = 1,
        SearchPathContainer,
        SearchPath,
        MissingSearchPath,
        StartupFile,
        VirtualEnvContainer = SearchPathContainer,
        VirtualEnv = SearchPath,
        VirtualEnvPackage = SearchPath
    }

    public abstract class CommonProjectNode : ProjectNode, IVsProjectSpecificEditorMap2, IVsDeferredSaveProject {
        private CommonProjectPackage/*!*/ _package;
        private Guid _mruPageGuid = new Guid(CommonConstants.AddReferenceMRUPageGuid);
        private VSLangProj.VSProject _vsProject = null;
        private static ImageList _imageList;
        private ProjectDocumentsListenerForStartupFileUpdates _projectDocListenerForStartupFileUpdates;
        private static int _imageOffset;
        private FileSystemWatcher _watcher;
        private int _suppressFileWatcherCount;
        private bool _isRefreshing;
        internal bool _boldedStartupItem;
        private object _automationObject;
        private CommonPropertyPage _propPage;

        public CommonProjectNode(CommonProjectPackage/*!*/ package, ImageList/*!*/ imageList) {
            Contract.Assert(package != null);
            Contract.Assert(imageList != null);

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
        }

        #region abstract methods

        public abstract Type GetProjectFactoryType();
        public abstract Type GetEditorFactoryType();
        public abstract string GetProjectName();

        public virtual CommonFileNode CreateCodeFileNode(MsBuildProjectElement item) {
            return new CommonFileNode(this, item);
        }
        public virtual CommonFileNode CreateNonCodeFileNode(MsBuildProjectElement item) {
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

        protected override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == CommonConstants.Std97CmdGroupGuid) {
                switch ((VSConstants.VSStd97CmdID)cmd) {
                    case VSConstants.VSStd97CmdID.BuildCtx:
                    case VSConstants.VSStd97CmdID.RebuildCtx:
                    case VSConstants.VSStd97CmdID.CleanCtx:
                        result = QueryStatusResult.SUPPORTED | QueryStatusResult.INVISIBLE;
                        return VSConstants.S_OK;
                }
            } else if (cmdGroup == Microsoft.PythonTools.Project.VsMenus.guidStandardCommandSet2K) {
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
            }

            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        private bool IsPublishingEnabled {
            get {
                return !String.IsNullOrWhiteSpace(GetProjectProperty(CommonConstants.PublishUrl));
            }
        }

        protected override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == Microsoft.PythonTools.Project.VsMenus.guidStandardCommandSet2K) {
                switch ((VsCommands2K)cmd) {
                    case VsCommands2K.ECMD_PUBLISHSELECTION:
                    case VsCommands2K.ECMD_PUBLISHSLNCTX:
                        Publish(PublishProjectOptions.Default, true);
                        return VSConstants.S_OK;
                    case CommonConstants.OpenFolderInExplorerCmdId:
                        Process.Start(this.ProjectHome);
                        return VSConstants.S_OK;
                }
            }
            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
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

            _watcher = new FileSystemWatcher(ProjectHome);
            _watcher.IncludeSubdirectories = true;
            _watcher.Created += new FileSystemEventHandler(FileExistanceChanged);
            _watcher.Deleted += new FileSystemEventHandler(FileExistanceChanged);
            _watcher.Renamed += new RenamedEventHandler(FileNameChanged);
            _watcher.SynchronizingObject = new UIThreadSynchronizer();
            _watcher.EnableRaisingEvents = true;
        }

        protected override ReferenceContainerNode CreateReferenceContainerNode() {
            return new CommonReferenceContainerNode(this);
        }

        private void FileNameChanged(object sender, RenamedEventArgs e) {
            string oldPath = e.OldFullPath;
            if (!File.Exists(oldPath) && Directory.Exists(oldPath)) {
                oldPath = oldPath + "\\";
            }

            var child = FindChild(oldPath);
            if (child != null) {
                child.ReDraw(UIHierarchyElement.Icon);
            }

            string newPath = e.FullPath;
            if (!File.Exists(newPath) && Directory.Exists(newPath)) {
                newPath = newPath + "\\";
            }
            child = FindChild(newPath);
            if (child != null) {
                child.ReDraw(UIHierarchyElement.Icon);
            }
        }

        private void FileExistanceChanged(object sender, FileSystemEventArgs e) {
            var child = FindChild(e.FullPath);
            if (child != null) {
                child.ReDraw(UIHierarchyElement.Icon);
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

        public override int Close() {
            if (null != _projectDocListenerForStartupFileUpdates) {
                _projectDocListenerForStartupFileUpdates.Dispose();
                _projectDocListenerForStartupFileUpdates = null;
            }
            if (null != Site) {
                LibraryManager libraryManager = Site.GetService(GetLibraryManagerType()) as LibraryManager;
                if (null != libraryManager) {
                    libraryManager.UnregisterHierarchy(InteropSafeHierarchy);
                }
            }
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;

            return base.Close();
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
            if (!_boldedStartupItem && BoldItem(startupItem, true)) {
                _boldedStartupItem = true;
            }
        }

        internal bool BoldItem(HierarchyNode item, bool bolded) {
            IVsUIHierarchyWindow2 windows = GetUIHierarchyWindow(
                ProjectMgr.Site as IServiceProvider,
                new Guid(ToolWindowGuids80.SolutionExplorer)) as IVsUIHierarchyWindow2;

            return ErrorHandler.Succeeded(windows.SetItemAttribute(this, item.ID, (uint)__VSHIERITEMATTRIBUTE.VSHIERITEMATTRIBUTE_Bold, bolded));
        }

        /// <summary>
        /// Overriding to provide project general property page
        /// </summary>
        /// <returns></returns>
        protected override Guid[] GetConfigurationIndependentPropertyPages() {
            return new[] { 
                GetGeneralPropertyPageType().GUID
            };
        }

        /// <summary>
        /// Create a file node based on an msbuild item.
        /// </summary>
        /// <param name="item">The msbuild item to be analyzed</param>        
        public override FileNode CreateFileNode(MsBuildProjectElement item) {
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

            var prjItem = GetExistingItem(absFileName);

            if (prjItem == null) {
                string path = CommonUtils.GetRelativeFilePath(ProjectHome, absFileName);
                if (IsCodeFile(absFileName)) {
                    prjItem = BuildProject.AddItem("Compile", path)[0];
                } else {
                    prjItem = BuildProject.AddItem("Content", path)[0];
                }
            }

            return CreateFileNode(new MsBuildProjectElement(this, prjItem));
        }

        protected Microsoft.Build.Evaluation.ProjectItem GetExistingItem(string absFileName) {
            foreach (var item in BuildProject.Items) {
                string absItemPath = CommonUtils.GetAbsoluteFilePath(ProjectHome, item.EvaluatedInclude);
                if (CommonUtils.IsSamePath(absItemPath, absFileName)) {
                    return item;
                }
            }
            return null;
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
            string workDir = this.ProjectMgr.GetProjectProperty(CommonConstants.WorkingDirectory, true);

            return CommonUtils.GetAbsoluteDirectoryPath(ProjectHome, workDir);
        }

        /// <summary>
        /// Returns resolved value of the startup file property.
        /// </summary>
        internal string GetStartupFile() {
            string startupFile = ProjectMgr.GetProjectProperty(CommonConstants.StartupFile, true);

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
                        n.ReDraw(UIHierarchyElement.Icon);
                    } else if (CommonUtils.IsSamePath(newFile, absUrl)) {
                        windows.SetItemAttribute(
                            this,
                            n.ID,
                            (uint)__VSHIERITEMATTRIBUTE.VSHIERITEMATTRIBUTE_Bold,
                            true
                        );
                        n.ReDraw(UIHierarchyElement.Icon);
                    }
                }

                RefreshStartupFile(n, oldFile, newFile);
            }
        }

        /// <summary>
        /// Provide mapping from our browse objects and automation objects to our CATIDs
        /// </summary>
        private void InitializeCATIDs() {
            Type fileNodePropsType = typeof(FileNodeProperties);
            // The following properties classes are specific to current language so we can use their GUIDs directly
            AddCATIDMapping(typeof(OAProject), typeof(OAProject).GUID);
            // The following is not language specific and as such we need a separate GUID
            AddCATIDMapping(typeof(FolderNodeProperties), new Guid(CommonConstants.FolderNodePropertiesGuid));
            // This one we use the same as language file nodes since both refer to files
            AddCATIDMapping(typeof(FileNodeProperties), fileNodePropsType.GUID);
            // Because our property page pass itself as the object to display in its grid, 
            // we need to make it have the same CATID
            // as the browse object of the project node so that filtering is possible.
            AddCATIDMapping(GetGeneralPropertyPageType(), GetGeneralPropertyPageType().GUID);
            // We could also provide CATIDs for references and the references container node, if we wanted to.
        }

        /// <summary>
        /// Parses SearchPath property into a list of distinct absolute paths, preserving the order.
        /// </summary>
        protected IList<string> ParseSearchPath() {
            string searchPath = this.ProjectMgr.GetProjectProperty(CommonConstants.SearchPath, true);
            return ParseSearchPath(searchPath);
        }

        /// <summary>
        /// Parses SearchPath string into a list of distinct absolute paths, preserving the order.
        /// </summary>
        protected IList<string> ParseSearchPath(string searchPath) {
            List<string> parsedPaths = new List<string>();
            if (!string.IsNullOrEmpty(searchPath)) {
                foreach (string path in searchPath.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
                    string resolvedPath = CommonUtils.GetAbsoluteFilePath(ProjectHome, path);
                    if (!parsedPaths.Contains(resolvedPath)) {
                        parsedPaths.Add(resolvedPath);
                    }
                }
            }
            return parsedPaths;
        }

        /// <summary>
        /// Saves list of paths back as SearchPath project property.
        /// </summary>
        private void SaveSearchPath(IList<string> value) {
            string valueStr = "";
            if (value != null && value.Count > 0) {
                valueStr = value.Aggregate((joined, path) => joined + ';' + path);
            }
            this.ProjectMgr.SetProjectProperty(CommonConstants.SearchPath, valueStr);
        }

        /// <summary>
        /// Adds new search path to the SearchPath project property.
        /// </summary>
        internal void AddSearchPathEntry(string newpath) {
            Utilities.ArgumentNotNull("newpath", newpath);

            IList<string> searchPath = ParseSearchPath();
            var relativePath = CommonUtils.GetRelativeFilePath(ProjectHome, CommonUtils.GetAbsoluteFilePath(ProjectHome, newpath));
            if (searchPath.Contains(newpath, StringComparer.OrdinalIgnoreCase) ||
                searchPath.Contains(relativePath, StringComparer.OrdinalIgnoreCase)) {
                return;
            }
            searchPath.Add(relativePath);
            SaveSearchPath(searchPath);
        }

        /// <summary>
        /// Removes a given path from the SearchPath property.
        /// </summary>
        internal void RemoveSearchPathEntry(string path) {
            IList<string> searchPath = ParseSearchPath();
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
            // Get a reference to the UIShell.
            IVsUIShell uiShell = GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (null == uiShell) {
                return VSConstants.S_FALSE;
            }
            //Create a fill in a structure that defines Browse for folder dialog
            VSBROWSEINFOW[] browseInfo = new VSBROWSEINFOW[1];
            //Dialog title
            browseInfo[0].pwzDlgTitle = DynamicProjectSR.GetString(DynamicProjectSR.SelectFolderForSearchPath);
            //Initial directory - project directory
            browseInfo[0].pwzInitialDir = ProjectHome;
            //Parent window
            uiShell.GetDialogOwnerHwnd(out browseInfo[0].hwndOwner);
            //Max path length
            browseInfo[0].nMaxDirName = NativeMethods.MAX_PATH;
            //This struct size
            browseInfo[0].lStructSize = (uint)Marshal.SizeOf(typeof(VSBROWSEINFOW));
            //Memory to write selected directory to.
            //Note: this one allocates unmanaged memory, which must be freed later
            IntPtr pDirName = Marshal.AllocCoTaskMem(NativeMethods.MAX_PATH);
            browseInfo[0].pwzDirName = pDirName;
            try {
                //Show the dialog
                int hr = uiShell.GetDirectoryViaBrowseDlg(browseInfo);
                if (hr == VSConstants.OLE_E_PROMPTSAVECANCELLED) {
                    //User cancelled the dialog
                    return VSConstants.S_OK;
                }
                //Check for any failures
                ErrorHandler.ThrowOnFailure(hr);
                //Get selected directory
                string dirName = Marshal.PtrToStringAuto(browseInfo[0].pwzDirName);
                AddSearchPathEntry(dirName);
            } finally {
                //Free allocated unmanaged memory
                if (pDirName != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(pDirName);
                }
            }
            return VSConstants.S_OK;
        }

        internal unsafe int AddSearchPathZip() {
            var uiShell = GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (uiShell == null) {
                return VSConstants.S_FALSE;
            }

            var fileNameBuf = stackalloc char[NativeMethods.MAX_PATH];
            var ofn = new[] {
                new VSOPENFILENAMEW {
                    lStructSize = (uint)Marshal.SizeOf(typeof(VSOPENFILENAMEW)),
                    pwzDlgTitle = DynamicProjectSR.GetString(DynamicProjectSR.SelectZipFileForSearchPath),
                    nMaxFileName = NativeMethods.MAX_PATH,
                    pwzFileName = (IntPtr)fileNameBuf,
                    pwzInitialDir = ProjectHome,
                    pwzFilter = "Zip Archives\0*.zip\0All Files\0*.*\0"
                }
            };
            uiShell.GetDialogOwnerHwnd(out ofn[0].hwndOwner);

            var hr = uiShell.GetOpenFileNameViaDlg(ofn);
            if (hr == VSConstants.OLE_E_PROMPTSAVECANCELLED) {
                return VSConstants.S_OK;
            }
            ErrorHandler.ThrowOnFailure(hr);

            string fileName = new string(fileNameBuf);
            AddSearchPathEntry(fileName);
            return VSConstants.S_OK;
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
            ErrorHandler.ThrowOnFailure(ParseCanonicalName(mkDocument, out itemid));
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
                            child.SaveItem(VSSAVEFLAGS.VSSAVE_Save, null, docCookie, IntPtr.Zero, out cancelled);
                        }

                        FileNode fn = child as FileNode;
                        if (fn != null) {
                            string oldLoc = CommonUtils.GetAbsoluteFilePath(basePath, child.ItemNode.GetMetadata(ProjectFileConstants.Include));
                            string newLoc = CommonUtils.GetAbsoluteFilePath(baseNewPath, child.ItemNode.GetMetadata(ProjectFileConstants.Include));

                            // make sure the directory is there
                            Directory.CreateDirectory(Path.GetDirectoryName(newLoc));
                            fn.RenameDocument(oldLoc, newLoc);
                        }

                        FolderNode folder = child as FolderNode;
                        if (folder != null) {
                            folder.VirtualNodeName = CommonUtils.GetAbsoluteDirectoryPath(baseNewPath, child.ItemNode.GetMetadata(ProjectFileConstants.Include));
                        }
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

#if DEV11
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
