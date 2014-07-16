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
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Build.Execution;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Azure;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using NativeMethods = Microsoft.VisualStudioTools.Project.NativeMethods;
using Task = System.Threading.Tasks.Task;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using VsMenus = Microsoft.VisualStudioTools.Project.VsMenus;

namespace Microsoft.PythonTools.Project {
    [Guid(PythonConstants.ProjectNodeGuid)]
    internal class PythonProjectNode : CommonProjectNode, IPythonProject2, IAzureRoleProject {
        // For files that are analyzed because they were directly or indirectly referenced in the search path, store the information
        // about the directory from the search path that referenced them in IProjectEntry.Properties[_searchPathEntryKey], so that
        // they can be located and removed when that directory is removed from the path.
        private static readonly object _searchPathEntryKey = new { Name = "SearchPathEntry" };

        private object _designerContext;
        private VsProjectAnalyzer _analyzer;
        private readonly HashSet<IProjectEntry> _warnOnLaunchFiles = new HashSet<IProjectEntry>();
        private PythonDebugPropertyPage _debugPropPage;
        private CommonSearchPathContainerNode _searchPathContainer;
        private InterpretersContainerNode _interpretersContainer;
        private MSBuildProjectInterpreterFactoryProvider _interpreters;

        internal List<CustomCommand> _customCommands;
        private string _customCommandsDisplayLabel;
        private Dictionary<object, Action<object>> _actionsOnClose;

        public PythonProjectNode(CommonProjectPackage package)
            : base(package, Utilities.GetImageList(typeof(PythonProjectNode).Assembly.GetManifestResourceStream(PythonConstants.ProjectImageList))) {

            Type projectNodePropsType = typeof(PythonProjectNodeProperties);
            AddCATIDMapping(projectNodePropsType, projectNodePropsType.GUID);
        }

        private static KeyValuePair<string, string>[] outputGroupNames = {
                                             // Name                     ItemGroup (MSBuild)
            new KeyValuePair<string, string>("Built",                 "BuiltProjectOutputGroupFast"),
            new KeyValuePair<string, string>("ContentFiles",          "ContentFilesProjectOutputGroup"),
            new KeyValuePair<string, string>("SourceFiles",           "SourceFilesProjectOutputGroup"),
        };

        protected internal override IList<KeyValuePair<string, string>> GetOutputGroupNames() {
            return outputGroupNames.ToList();
        }

        protected override void NewBuildProject(Build.Evaluation.Project project) {
            base.NewBuildProject(project);

            if (_interpreters != null) {
                _interpreters.ActiveInterpreterChanged -= ActiveInterpreterChanged;
                _interpreters.InterpreterFactoriesChanged -= InterpreterFactoriesChanged;
                _interpreters.Dispose();
                _interpreters = null;
            }

            // Remove old custom commands
            if (_customCommands != null) {
                foreach (var c in _customCommands) {
                    c.Dispose();
                }
            }
            _customCommands = null;

            // Project has been cleared, so nothing else to do here
            if (project == null) {
                return;
            }

            // Hook up the interpreter factory provider
            var interpreterService = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();
            _interpreters = new MSBuildProjectInterpreterFactoryProvider(interpreterService, project);
            try {
                _interpreters.DiscoverInterpreters();
            } catch (InvalidDataException ex) {
                OutputWindowRedirector.GetGeneral(Site).WriteErrorLine(ex.Message);
            }
            _interpreters.ActiveInterpreterChanged += ActiveInterpreterChanged;
            _interpreters.InterpreterFactoriesChanged += InterpreterFactoriesChanged;

            // Add any custom commands
            _customCommands = CustomCommand.GetCommands(project, this).ToList();
            _customCommandsDisplayLabel = CustomCommand.GetCommandsDisplayLabel(project, this);
        }

        IAsyncCommand IPythonProject2.FindCommand(string canonicalName) {
            return _customCommands.FirstOrDefault(cc => cc.Target == canonicalName);
        }

        ProjectInstance IPythonProject2.GetMSBuildProjectInstance() {
            if (CurrentConfig == null) {
                SetCurrentConfiguration();
                if (CurrentConfig == null) {
                    if (BuildProject == null) {
                        return null;
                    }
                    return BuildProject.CreateProjectInstance();
                }
            }
            return CurrentConfig;
        }

        private void InterpreterFactoriesChanged(object sender, EventArgs e) {
            UIThread.Invoke(() => RefreshInterpreters());
        }

        internal MSBuildProjectInterpreterFactoryProvider Interpreters {
            get {
                return _interpreters;
            }
        }

        protected override Stream ProjectIconsImageStripStream {
            get {
                return typeof(ProjectNode).Assembly.GetManifestResourceStream("Microsoft.PythonTools.Project.Resources.imagelis.bmp");
            }
        }

        internal int GetIconIndex(PythonProjectImageName name) {
            return ImageOffset + (int)name;
        }

        internal IntPtr GetIconHandleByName(PythonProjectImageName name) {
            return ImageHandler.GetIconHandle(GetIconIndex(name));
        }

        private static string GetSearchPathEntry(IProjectEntry entry) {
            object result;
            entry.Properties.TryGetValue(_searchPathEntryKey, out result);
            return (string)result;
        }

        private static void SetSearchPathEntry(IProjectEntry entry, string value) {
            entry.Properties[_searchPathEntryKey] = value;
        }

        public override CommonFileNode CreateCodeFileNode(ProjectElement item) {
            return new PythonFileNode(this, item);
        }

        public override CommonFileNode CreateNonCodeFileNode(ProjectElement item) {
            return new PythonNonCodeFileNode(this, item);
        }

        protected override ReferenceContainerNode CreateReferenceContainerNode() {
            return new PythonReferenceContainerNode(this);
        }

        protected override void LinkFileAdded(string filename) {
            if (PythonToolsPackage.Instance.DebuggingOptionsPage.UpdateSearchPathsWhenAddingLinkedFiles) {
                // update our search paths.
                string dirToAdd = Path.GetDirectoryName(filename);
                while (!String.IsNullOrEmpty(dirToAdd) && File.Exists(Path.Combine(dirToAdd, "__init__.py"))) {
                    dirToAdd = Path.GetDirectoryName(dirToAdd);
                }

                AddSearchPathEntry(CommonUtils.EnsureEndSeparator(dirToAdd));
            }

            base.LinkFileAdded(filename);
        }

        protected override Guid[] GetConfigurationIndependentPropertyPages() {
            return new[] { 
                GetGeneralPropertyPageType().GUID,
                typeof(PythonDebugPropertyPage).GUID,
                typeof(PublishPropertyPage).GUID
            };
        }

        /// <summary>
        /// Evaluates if a file is a current language code file based on is extension
        /// </summary>
        /// <param name="strFileName">The filename to be evaluated</param>
        /// <returns>true if is a code file</returns>
        public override bool IsCodeFile(string strFileName) {
            return IsPythonFile(strFileName);
        }

        public override string[] CodeFileExtensions {
            get {
                return new[] { PythonConstants.FileExtension, PythonConstants.WindowsFileExtension };
            }
        }

        internal static bool IsPythonFile(string strFileName) {
            var ext = Path.GetExtension(strFileName);

            return String.Equals(ext, PythonConstants.FileExtension, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(ext, PythonConstants.WindowsFileExtension, StringComparison.OrdinalIgnoreCase);
        }

        public override Type GetProjectFactoryType() {
            return typeof(PythonProjectFactory);
        }

        public override string GetProjectName() {
            return "PythonProject";
        }

        public override string GetFormatList() {
            return PythonConstants.ProjectFileFilter;
        }

        public override Type GetGeneralPropertyPageType() {
            return typeof(PythonGeneralPropertyPage);
        }

        public override Type GetEditorFactoryType() {
            return typeof(PythonEditorFactory);
        }

        public override Type GetLibraryManagerType() {
            return typeof(IPythonLibraryManager);
        }

        protected override NodeProperties CreatePropertiesObject() {
            return new PythonProjectNodeProperties(this);
        }

        public override CommonProjectConfig MakeConfiguration(string activeConfigName) {
            return new PythonProjectConfig(this, activeConfigName);
        }

        protected internal override FolderNode CreateFolderNode(ProjectElement element) {
            return new PythonFolderNode(this, element);
        }

        protected override bool FilterItemTypeToBeAddedToHierarchy(string itemType) {
            if (MSBuildProjectInterpreterFactoryProvider.InterpreterReferenceItem.Equals(itemType, StringComparison.Ordinal) ||
                MSBuildProjectInterpreterFactoryProvider.InterpreterItem.Equals(itemType, StringComparison.Ordinal)) {
                return true;
            }
            return base.FilterItemTypeToBeAddedToHierarchy(itemType);
        }

        public override int QueryService(ref Guid guidService, out object result) {
            if (XamlDesignerSupport.DesignerContextType != null &&
                guidService == XamlDesignerSupport.DesignerContextType.GUID) {
                result = DesignerContext;
                return VSConstants.S_OK;
            }
            return base.QueryService(ref guidService, out result);
        }

        public override int GenerateUniqueItemName(uint itemIdLoc, string ext, string suggestedRoot, out string itemName) {
            if ("bin".Equals(suggestedRoot, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(ext)) {
                // This item should not have a number added to the name.
                itemName = (suggestedRoot ?? "") + (ext ?? "").Trim();
                return VSConstants.S_OK;
            }
            return base.GenerateUniqueItemName(itemIdLoc, ext, suggestedRoot, out itemName);
        }

        protected override void Reload() {
            _searchPathContainer = new CommonSearchPathContainerNode(this);
            this.AddChild(_searchPathContainer);
            RefreshCurrentWorkingDirectory();
            RefreshSearchPaths();
            _interpretersContainer = new InterpretersContainerNode(this);
            this.AddChild(_interpretersContainer);
            RefreshInterpreters(alwaysCollapse: true);

            OnProjectPropertyChanged += PythonProjectNode_OnProjectPropertyChanged;
            base.Reload();

            string id;
            if (_interpreters.IsActiveInterpreterGlobalDefault &&
                !string.IsNullOrEmpty(id = GetProjectProperty(MSBuildProjectInterpreterFactoryProvider.InterpreterIdProperty))) {
                // The interpreter in the project file has no reference, so
                // find and add it.
                var interpreterService = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();
                if (interpreterService != null) {
                    var existing = interpreterService.FindInterpreter(
                        id,
                        GetProjectProperty(MSBuildProjectInterpreterFactoryProvider.InterpreterVersionProperty));
                    if (existing != null && QueryEditProjectFile(false)) {
                        _interpreters.AddInterpreter(existing);
                        Debug.Assert(_interpreters.ActiveInterpreter == existing);
                    }
                }
            }

            PythonToolsPackage.Instance.CheckSurveyNews(false);
        }

        private void RefreshCurrentWorkingDirectory() {
            try {
                IsRefreshing = true;
                string projHome = ProjectHome;
                string workDir = GetWorkingDirectory();

                //Refresh CWD node
                bool needCWD = !CommonUtils.IsSameDirectory(projHome, workDir);
                var cwdNode = FindImmediateChild<CurrentWorkingDirectoryNode>(_searchPathContainer);
                if (needCWD) {
                    if (cwdNode == null) {
                        //No cwd node yet
                        _searchPathContainer.AddChild(new CurrentWorkingDirectoryNode(this, workDir));
                    } else if (!CommonUtils.IsSameDirectory(cwdNode.Url, workDir)) {
                        //CWD has changed, recreate the node
                        cwdNode.Remove(false);
                        _searchPathContainer.AddChild(new CurrentWorkingDirectoryNode(this, workDir));
                    }
                } else {
                    //No need to show CWD, remove if exists
                    if (cwdNode != null) {
                        cwdNode.Remove(false);
                    }
                }
            } finally {
                IsRefreshing = false;
            }
        }

        private void RefreshSearchPaths() {
            try {
                IsRefreshing = true;

                string projHome = ProjectHome;
                string workDir = GetWorkingDirectory();
                IList<string> searchPath = ParseSearchPath();

                //Refresh regular search path nodes

                //We need to update search path nodes according to the search path property.
                //It's quite expensive to remove all and build all nodes from scratch, 
                //so we are going to perform some smarter update.
                //We are looping over paths in the search path and if a corresponding node
                //exists, we only update its index (sort order), creating new node otherwise.
                //At the end all nodes that haven't been updated have to be removed - they are
                //not in the search path anymore.
                var searchPathNodes = new List<CommonSearchPathNode>();
                this.FindNodesOfType<CommonSearchPathNode>(searchPathNodes);
                bool[] updatedNodes = new bool[searchPathNodes.Count];
                int index;
                for (int i = 0; i < searchPath.Count; i++) {
                    string path = searchPath[i];
                    //ParseSearchPath() must resolve all paths
                    Debug.Assert(Path.IsPathRooted(path));
                    var node = FindSearchPathNodeByPath(searchPathNodes, path, out index);
                    if (node != null) {
                        //existing path, update index (sort order)
                        node.Index = i;
                        updatedNodes[index] = true;
                    } else {
                        //new path - create new node
                        _searchPathContainer.AddChild(new CommonSearchPathNode(this, path, i));
                    }
                }

                //Refresh nodes and remove non-updated ones
                for (int i = 0; i < searchPathNodes.Count; i++) {
                    if (!updatedNodes[i]) {
                        searchPathNodes[i].Remove();
                    }
                }
            } finally {
                IsRefreshing = false;
            }
        }

        private void RefreshInterpreters(bool alwaysCollapse = false) {
            if (IsClosed) {
                return;
            }

            var node = _interpretersContainer;
            if (node == null) {
                return;
            }

            var remaining = node.AllChildren.OfType<InterpretersNode>().ToDictionary(n => n._factory);

            var interpreters = Interpreters;
            if (interpreters != null) {
                foreach (var fact in interpreters.GetInterpreterFactories()) {
                    if (!remaining.Remove(fact)) {
                        node.AddChild(new InterpretersNode(
                            this,
                            Interpreters.GetProjectItem(fact),
                            fact,
                            isInterpreterReference: !interpreters.IsProjectSpecific(fact),
                            canDelete:
                                interpreters.IsProjectSpecific(fact) &&
                                Directory.Exists(fact.Configuration.PrefixPath)
                        ));
                    }
                }
            }

            foreach (var child in remaining.Values) {
                node.RemoveChild(child);
            }

            if (alwaysCollapse || ParentHierarchy == null) {
                OnInvalidateItems(node);
            } else {
                bool wasExpanded = node.GetIsExpanded();
                var expandAfter = node.AllChildren.Where(n => n.GetIsExpanded()).ToArray();
                OnInvalidateItems(node);
                if (wasExpanded) {
                    node.ExpandItem(EXPANDFLAGS.EXPF_ExpandFolder);
                }
                foreach (var child in expandAfter) {
                    child.ExpandItem(EXPANDFLAGS.EXPF_ExpandFolder);
                }
            }
            BoldActiveEnvironment();
        }

        private void BoldActiveEnvironment() {
            var node = _interpretersContainer;
            if (node != null) {
                foreach (var child in node.AllChildren.OfType<InterpretersNode>()) {
                    BoldItem(child, child._factory == Interpreters.ActiveInterpreter);
                }
            }
        }

        /// <summary>
        /// Returns first immediate child node (non-recursive) of a given type.
        /// </summary>
        private static T FindImmediateChild<T>(HierarchyNode parent)
            where T : HierarchyNode {
            for (HierarchyNode n = parent.FirstChild; n != null; n = n.NextSibling) {
                if (n is T) {
                    return (T)n;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds Search Path node by a given search path and returns it along with the node's index. 
        /// </summary>
        private CommonSearchPathNode FindSearchPathNodeByPath(IList<CommonSearchPathNode> nodes, string path, out int index) {
            index = 0;
            for (int j = 0; j < nodes.Count; j++) {
                if (CommonUtils.IsSameDirectory(nodes[j].Url, path)) {
                    index = j;
                    return nodes[j];
                }
            }
            return null;
        }
        /// <summary>
        /// Parses SearchPath property into a list of distinct absolute paths, preserving the order.
        /// </summary>
        protected IList<string> ParseSearchPath() {
            var searchPath = GetProjectProperty(PythonConstants.SearchPathSetting, true);
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
            SetProjectProperty(PythonConstants.SearchPathSetting, valueStr);
        }

        /// <summary>
        /// Adds new search path to the SearchPath project property.
        /// </summary>
        internal void AddSearchPathEntry(string newpath) {
            Utilities.ArgumentNotNull("newpath", newpath);

            var searchPath = ParseSearchPath();
            var absPath = CommonUtils.GetAbsoluteFilePath(ProjectHome, newpath);
            // Ignore the end separator when determining whether the path has
            // already been added. Having both "C:\Fob" and "C:\Fob\" is not
            // legal.
            if (searchPath.Contains(CommonUtils.EnsureEndSeparator(absPath), StringComparer.OrdinalIgnoreCase) ||
                searchPath.Contains(CommonUtils.TrimEndSeparator(absPath), StringComparer.OrdinalIgnoreCase)) {
                return;
            }
            searchPath.Add(absPath);
            SaveSearchPath(searchPath);
        }

        /// <summary>
        /// Removes a given path from the SearchPath property.
        /// </summary>
        internal void RemoveSearchPathEntry(string path) {
            var absPath = CommonUtils.TrimEndSeparator(CommonUtils.GetAbsoluteFilePath(ProjectHome, path));
            var absPathWithEndSeparator = CommonUtils.EnsureEndSeparator(absPath);
            var searchPath = ParseSearchPath();

            var newSearchPath = searchPath
                // Ignore the end separator when determining paths to remove.
                .Where(p => !absPath.Equals(p, StringComparison.OrdinalIgnoreCase) &&
                            !absPathWithEndSeparator.Equals(p, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (searchPath.Count != newSearchPath.Count) {
                SaveSearchPath(newSearchPath);
            }
        }

        /// <summary>
        /// Executes Add Search Path menu command.
        /// </summary>        
        internal int AddSearchPath() {
            string dirName = Dialogs.BrowseForDirectory(
                IntPtr.Zero,
                ProjectHome,
                SR.GetString(SR.SelectFolderForSearchPath)
            );

            if (dirName != null) {
                AddSearchPathEntry(CommonUtils.EnsureEndSeparator(dirName));
            }

            return VSConstants.S_OK;
        }


        private void PythonProjectNode_OnProjectPropertyChanged(object sender, ProjectPropertyChangedArgs e) {
            switch (e.PropertyName) {
                case CommonConstants.StartupFile:
                    var genProp = GeneralPropertyPageControl;
                    if (genProp != null) {
                        genProp.StartupFile = e.NewValue;
                    }
                    break;
                case CommonConstants.WorkingDirectory:
                    RefreshCurrentWorkingDirectory();
                    genProp = GeneralPropertyPageControl;
                    if (genProp != null) {
                        genProp.WorkingDirectory = e.NewValue;
                    }
                    break;
                case PythonConstants.SearchPathSetting:
                    RefreshSearchPaths();

                    // we need to remove old files from the analyzer and add the new files
                    HashSet<string> oldDirs = new HashSet<string>(ParseSearchPath(e.OldValue), StringComparer.OrdinalIgnoreCase);
                    HashSet<string> newDirs = new HashSet<string>(ParseSearchPath(e.NewValue), StringComparer.OrdinalIgnoreCase);

                    // figure out all the possible directory names we could be removing...
                    foreach (var fileProject in _analyzer.LoadedFiles) {
                        string file = fileProject.Key;
                        IProjectEntry projectEntry = fileProject.Value;
                        string searchPathEntry = GetSearchPathEntry(fileProject.Value);
                        if (searchPathEntry != null && !newDirs.Contains(searchPathEntry)) {
                            _analyzer.UnloadFile(projectEntry);
                        }
                    }

                    // find the values only in the old list, and let the analyzer know it shouldn't be watching those dirs
                    oldDirs.ExceptWith(newDirs);
                    foreach (var dir in oldDirs) {
                        _analyzer.StopAnalyzingDirectory(dir);
                    }

                    AnalyzeSearchPaths(newDirs);
                    break;
            }

            var debugProp = DebugPropertyPage;
            if (debugProp != null) {
                ((PythonDebugPropertyPageControl)debugProp.Control).ReloadSetting(e.PropertyName);
            }
        }

        private void AnalyzeSearchPaths(IEnumerable<string> newDirs) {
            // now add all of the missing files, any dups will automatically not be re-analyzed
            foreach (var dir in newDirs) {
                if (File.Exists(dir)) {
                    // If it's a file and not a directory, parse it as a .zip
                    // file in accordance with PEP 273.
                    _analyzer.AnalyzeZipArchive(dir, onFileAnalyzed: entry => SetSearchPathEntry(entry, dir));
                } else if (Directory.Exists(dir)) {
                    _analyzer.AnalyzeDirectory(dir, onFileAnalyzed: entry => SetSearchPathEntry(entry, dir));
                }
            }
        }

        private PythonGeneralPropertyPageControl GeneralPropertyPageControl {
            get {
                if (PropertyPage != null && PropertyPage.Control != null) {
                    return (PythonGeneralPropertyPageControl)PropertyPage.Control;
                }

                return null;
            }
        }

        internal PythonDebugPropertyPage DebugPropertyPage {
            get {
                return _debugPropPage;
            }
            set {
                _debugPropPage = value;
            }
        }

        internal object DesignerContext {
            get {
                if (_designerContext == null) {
                    _designerContext = XamlDesignerSupport.CreateDesignerContext();
                }
                return _designerContext;
            }
        }

        public PythonAnalyzer GetProjectAnalyzer() {
            return GetAnalyzer().Project;
        }

        VsProjectAnalyzer IPythonProject.GetProjectAnalyzer() {
            return GetAnalyzer();
        }

        public event EventHandler ProjectAnalyzerChanged;
        public event EventHandler<AnalyzerChangingEventArgs> ProjectAnalyzerChanging;

        public override IProjectLauncher GetLauncher() {
            return PythonToolsPackage.GetLauncher(this);
        }

        public void AddActionOnClose(object key, Action<object> action) {
            Debug.Assert(key != null);
            Debug.Assert(action != null);
            if (key == null || action == null) {
                return;
            }

            if (_actionsOnClose == null) {
                _actionsOnClose = new Dictionary<object, Action<object>>();
            }
            _actionsOnClose[key] = action;
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                var actions = _actionsOnClose;
                _actionsOnClose = null;
                if (actions != null) {
                    foreach (var keyValue in actions) {
                        if (keyValue.Value != null) {
                            keyValue.Value(keyValue.Key);
                        }
                    }
                }

                if (_analyzer != null) {
                    UnHookErrorsAndWarnings(_analyzer);
                    _analyzer.ClearAllTasks();

                    if (_analyzer.RemoveUser()) {
                        _analyzer.Dispose();
                    }
                    _analyzer = null;
                }

                if (_interpreters != null) {
                    _interpreters.Dispose();
                    _interpreters = null;
                }
                if (_customCommands != null) {
                    foreach (var c in _customCommands) {
                        c.Dispose();
                    }
                    _customCommands = null;
                }
            }

            base.Dispose(disposing);
        }

        public int SetInterpreterFactory(IPythonInterpreterFactory factory) {
            if (Interpreters != null && factory != Interpreters.ActiveInterpreter) {
                //Make sure we can edit the project file
                if (!ProjectMgr.QueryEditProjectFile(false)) {
                    return VSConstants.OLE_E_PROMPTSAVECANCELLED;
                }

                Interpreters.ActiveInterpreter = factory;
            }
            return VSConstants.S_OK;
        }

        public IPythonInterpreter GetInterpreter() {
            return GetAnalyzer().Interpreter;
        }

        public VsProjectAnalyzer GetAnalyzer() {
            if (_analyzer == null) {
                _analyzer = CreateAnalyzer();
                AnalyzeSearchPaths(ParseSearchPath());
            }
            return _analyzer;
        }

        private VsProjectAnalyzer CreateAnalyzer() {
            // check to see if we should share our analyzer with another project in the same solution.  This enables
            // refactoring, find all refs, and intellisense across projects.
            var vsSolution = (IVsSolution)GetService(typeof(SVsSolution));
            if (vsSolution != null) {
                var guid = new Guid(PythonConstants.ProjectFactoryGuid);
                IEnumHierarchies hierarchies;
                ErrorHandler.ThrowOnFailure((vsSolution.GetProjectEnum((uint)(__VSENUMPROJFLAGS.EPF_MATCHTYPE | __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION), ref guid, out hierarchies)));
                IVsHierarchy[] hierarchy = new IVsHierarchy[1];
                uint fetched;
                var curFactory = GetInterpreterFactory();
                while (ErrorHandler.Succeeded(hierarchies.Next(1, hierarchy, out fetched)) && fetched == 1) {
                    var proj = hierarchy[0].GetProject();
                    Debug.Assert(proj != null);
                    if (proj != null) {
                        var pyProj = proj.GetPythonProject();
                        Debug.Assert(pyProj != null);

                        if (pyProj != this &&
                            pyProj._analyzer != null &&
                            pyProj._analyzer.InterpreterFactory == curFactory) {
                            // we have the same interpreter, we'll share analysis engines across projects.
                            pyProj._analyzer.AddUser();
                            HookErrorsAndWarnings(pyProj._analyzer);
                            return pyProj._analyzer;
                        }
                    }
                }
            }

            var model = PythonToolsPackage.ComponentModel;
            var interpreterService = model.GetService<IInterpreterOptionsService>();
            var factory = GetInterpreterFactory();
            var res = new VsProjectAnalyzer(
                factory.CreateInterpreter(),
                factory,
                interpreterService.Interpreters.ToArray(),
                false
            );

            HookErrorsAndWarnings(res);
            return res;
        }

        private void HookErrorsAndWarnings(VsProjectAnalyzer res) {
            res.ShouldWarnOnLaunchChanged += OnShouldWarnOnLaunchChanged;
        }

        private void UnHookErrorsAndWarnings(VsProjectAnalyzer res) {
            res.ShouldWarnOnLaunchChanged -= OnShouldWarnOnLaunchChanged;
        }

        private void OnShouldWarnOnLaunchChanged(object sender, EntryEventArgs e) {
            if (_diskNodes.ContainsKey(e.Entry.FilePath ?? "")) {
                if (((VsProjectAnalyzer)sender).ShouldWarnOnLaunch(e.Entry)) {
                    _warnOnLaunchFiles.Add(e.Entry);
                } else {
                    _warnOnLaunchFiles.Remove(e.Entry);
                }
            }
        }

        public bool ShouldWarnOnLaunch {
            get {
                return _warnOnLaunchFiles.Any();
            }
        }

        public IPythonInterpreterFactory GetInterpreterFactory() {
            var fact = _interpreters.ActiveInterpreter;
            
            if (!_interpreters.IsAvailable(fact)) {
                var service = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();
                if (service != null) {
                    fact = service.NoInterpretersValue;
                }
            }

            PythonToolsPackage.EnsureCompletionDb(fact);

            return fact;
        }

        /// <summary>
        /// Called when the active interpreter is changed.  A new interpreter
        /// will be created immediately unless another project in the solution
        /// can provide a matching analyzer.
        /// </summary>
        private void ActiveInterpreterChanged(object sender, EventArgs e) {
            if (IsClosed) {
                return;
            }

            UIThread.InvokeAsync(UpdateActiveInterpreter).DoNotWait();
        }

        private void UpdateActiveInterpreter() {
            RefreshInterpreters();

            if (_analyzer != null) {
                UnHookErrorsAndWarnings(_analyzer);
            }
            var analyzer = CreateAnalyzer();

            var analyzerChanging = ProjectAnalyzerChanging;
            if (analyzerChanging != null) {
                analyzerChanging(this, new AnalyzerChangingEventArgs(
                    _analyzer != null ? _analyzer.Project : null,
                    analyzer != null ? analyzer.Project : null
                ));
            }

            Reanalyze(this, analyzer);
            if (_analyzer != null) {
                analyzer.SwitchAnalyzers(_analyzer);
                if (_analyzer.RemoveUser()) {
                    _analyzer.Dispose();
                }
            }

            _analyzer = analyzer;
            var searchPath = ParseSearchPath();
            if (searchPath != null && _analyzer != null) {
                AnalyzeSearchPaths(searchPath);
            }

            var analyzerChanged = ProjectAnalyzerChanged;
            if (analyzerChanged != null) {
                analyzerChanged(this, EventArgs.Empty);
            }
        }

        private void Reanalyze(HierarchyNode node, VsProjectAnalyzer newAnalyzer) {
            if (node != null) {
                for (var child = node.FirstChild; child != null; child = child.NextSibling) {
                    if (child.IsNonMemberItem) {
                        continue;
                    }
                    if (child is FileNode) {
                        newAnalyzer.AnalyzeFile(child.Url);
                    }

                    Reanalyze(child, newAnalyzer);
                }
            }
        }

        public override ReferenceNode CreateReferenceNodeForFile(string filename) {
            var interp = this.GetInterpreter() as IPythonInterpreterWithProjectReferences;
            if (interp == null) {
                return null;
            }

            var cancelSource = new CancellationTokenSource();
            var task = interp.AddReferenceAsync(new ProjectReference(filename, ProjectReferenceKind.ExtensionModule), cancelSource.Token);

            // try to complete synchronously w/o flashing the dialog...
            if (!task.Wait(100)) {
                var progress = new TaskProgressBar(task, cancelSource, "Waiting for analysis of extension module to complete...");
                if (progress.ShowDialog() != true) {
                    // user cancelled.
                    return null;
                }
            }

            var exception = task.Exception;
            if (exception != null) {
                string msg = GetErrorMessage(exception);

                string fullMsg = String.Format("Cannot add reference to {0}:", filename);
                if (msg != null) {
                    fullMsg = fullMsg + "\r\n\r\n" + msg;
                }
                MessageBox.Show(fullMsg);
            } else {
                return new PythonExtensionReferenceNode(this, filename);
            }

            return null;
        }

        private static string GetErrorMessage(AggregateException exception) {
            string msg = null;
            foreach (var inner in exception.InnerExceptions) {
                if (inner is AggregateException) {
                    msg = GetErrorMessage((AggregateException)inner);
                } else if (msg == null || inner is CannotAnalyzeExtensionException) {
                    msg = inner.Message;
                }
            }
            return msg;
        }

        protected override string AddReferenceExtensions {
            get {
                return SR.GetString(SR.AddReferenceExtensions);
            }
        }

        internal int OpenCommandPrompt(string path, IPythonInterpreterFactory factory = null, string subtitle = null) {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"));
            psi.UseShellExecute = false;
            psi.WorkingDirectory = path;

            var config = (factory ?? GetInterpreterFactory()).Configuration;
            if (config != null) {
                psi.Arguments = string.Format("/K \"title {0} Command Prompt\"",
                    string.IsNullOrEmpty(subtitle) ? Caption : subtitle
                );

                var paths = new List<string>();
                if (File.Exists(config.InterpreterPath)) {
                    paths.Add(Path.GetDirectoryName(config.InterpreterPath));
                }
                paths.Add(Path.Combine(config.PrefixPath, "Scripts"));
                paths.AddRange(Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator));

                psi.EnvironmentVariables["PATH"] = string.Join(
                    new string(Path.PathSeparator, 1),
                    paths.Where(Directory.Exists).Distinct()
                );

                if (!string.IsNullOrWhiteSpace(config.PathEnvironmentVariable)) {
                    var searchPaths = this.GetSearchPaths().ToList();
                    searchPaths.Insert(0, GetWorkingDirectory());

                    if (!PythonToolsPackage.Instance.GeneralOptionsPage.ClearGlobalPythonPath) {
                        searchPaths.AddRange(
                            Environment.GetEnvironmentVariable(config.PathEnvironmentVariable).Split(';')
                        );
                    }

                    psi.EnvironmentVariables[config.PathEnvironmentVariable] = string.Join(
                        new string(Path.PathSeparator, 1),
                        searchPaths.Where(Directory.Exists).Distinct()
                    );
                }
            }

            Process.Start(psi);
            return VSConstants.S_OK;

        }

        internal override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == VsMenus.guidStandardCommandSet2K) {
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

            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch ((int)cmd) {
                    case PythonConstants.OpenInteractiveForEnvironment:
                        try {
                            var window = ExecuteInReplCommand.EnsureReplWindow(GetInterpreterFactory(), this);
                            var pane = window as ToolWindowPane;
                            if (pane != null) {
                                ErrorHandler.ThrowOnFailure(((IVsWindowFrame)pane.Frame).Show());
                                window.Focus();
                            }
                        } catch (InvalidOperationException ex) {
                            MessageBox.Show(SR.GetString(SR.ErrorOpeningInteractiveWindow, ex), SR.ProductName);
                        }
                        return VSConstants.S_OK;
                }
            }

            if (cmdGroup == ProjectMgr.SharedCommandGuid) {
                switch ((SharedCommands)cmd) {
                    case SharedCommands.OpenCommandPromptHere:
                        return OpenCommandPrompt(FullPathToChildren);
                }
            }

            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }

        internal override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch ((int)cmd) {
                    case PythonConstants.OpenInteractiveForEnvironment:
                        var factory = GetInterpreterFactory();
                        if (Interpreters.IsAvailable(factory) && File.Exists(factory.Configuration.InterpreterPath)) {
                            result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        } else {
                            result |= QueryStatusResult.INVISIBLE;
                        }
                        return VSConstants.S_OK;
                    
                    case PythonConstants.CustomProjectCommandsMenu:
                        if (_customCommands != null && _customCommands.Any()) {
                            result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        } else {
                            result |= QueryStatusResult.INVISIBLE;
                        }

                        if (pCmdText != IntPtr.Zero && NativeMethods.OLECMDTEXT.GetFlags(pCmdText) == NativeMethods.OLECMDTEXT.OLECMDTEXTF.OLECMDTEXTF_NAME) {
                            NativeMethods.OLECMDTEXT.SetText(pCmdText, _customCommandsDisplayLabel);
                        }
                        return VSConstants.S_OK;
                }

            }
            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        private CustomCommand GetCustomCommand(uint cmdId) {
            if ((int)cmdId >= PythonConstants.FirstCustomCmdId && (int)cmdId <= PythonConstants.LastCustomCmdId) {

                int i = (int)cmdId - PythonConstants.FirstCustomCmdId;
                if (_customCommands == null || i >= _customCommands.Count) {
                    return null;
                }

                return _customCommands[i];
            } else {
                return _customCommands.FirstOrDefault(c => c.AlternateCmdId == cmdId);
            }
        }

        protected override QueryStatusResult QueryStatusSelectionOnNodes(IList<HierarchyNode> selectedNodes, Guid cmdGroup, uint cmd, IntPtr pCmdText) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                var command = GetCustomCommand(cmd);
                if (command != null) {
                    // Update display text for the menu commands.
                    if ((int)cmd >= PythonConstants.FirstCustomCmdId && (int)cmd <= PythonConstants.LastCustomCmdId) {
                        if (pCmdText != IntPtr.Zero && NativeMethods.OLECMDTEXT.GetFlags(pCmdText) == NativeMethods.OLECMDTEXT.OLECMDTEXTF.OLECMDTEXTF_NAME) {
                            NativeMethods.OLECMDTEXT.SetText(pCmdText, command.DisplayLabel);
                        }
                    }

                    var result = QueryStatusResult.SUPPORTED;
                    if (command.CanExecute(null)) {
                        result |= QueryStatusResult.ENABLED;
                    }
                    return result;
                }

                if ((int)cmd >= PythonConstants.FirstCustomCmdId && (int)cmd <= PythonConstants.LastCustomCmdId) {
                    // All unspecified custom commands are hidden
                    return QueryStatusResult.INVISIBLE | QueryStatusResult.NOTSUPPORTED;
                }

                if ((int)cmd == PythonConstants.InstallRequirementsTxt) {
                    var status = base.QueryStatusSelectionOnNodes(selectedNodes, cmdGroup, cmd, pCmdText) |
                        QueryStatusResult.SUPPORTED;
                    if (File.Exists(CommonUtils.GetAbsoluteFilePath(ProjectHome, "requirements.txt"))) {
                        status |= QueryStatusResult.ENABLED;
                    }
                    return status;
                }
            }

            return base.QueryStatusSelectionOnNodes(selectedNodes, cmdGroup, cmd, pCmdText);
        }

        protected override int ExecCommandIndependentOfSelection(Guid cmdGroup, uint cmdId, uint cmdExecOpt, IntPtr vaIn, IntPtr vaOut, CommandOrigin commandOrigin, out bool handled) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                var command = GetCustomCommand(cmdId);

                if (command != null) {
                    handled = true;
                    if (command.CanExecute(null)) {
                        if (!Utilities.SaveDirtyFiles()) {
                            return VSConstants.S_OK;
                        }

                        command.ExecuteAsync(null).ContinueWith(t => {
                            if (t.Exception != null) {
                                var ex = t.Exception.InnerException ?? t.Exception;
                                if (ex is NoInterpretersException) {
                                    PythonToolsPackage.OpenNoInterpretersHelpPage();
                                } else {
                                    MessageBox.Show(
                                        SR.GetString(
                                            SR.ErrorRunningCustomCommand,
                                            command.DisplayLabelWithoutAccessKeys,
                                            ex.Message
                                        ),
                                        SR.ProductName
                                    );
                                }
                            }
                        });
                    }
                    return VSConstants.S_OK;
                }

                handled = true;
                switch((int)cmdId) {
                    case PythonConstants.AddEnvironment:
                        ShowAddInterpreter();
                        return VSConstants.S_OK;
                    case PythonConstants.AddExistingVirtualEnv:
                    case PythonConstants.AddVirtualEnv:
                        ShowAddVirtualEnvironmentWithErrorHandling((int)cmdId == PythonConstants.AddExistingVirtualEnv);
                        return VSConstants.S_OK;
                    case PythonConstants.ViewAllEnvironments:
                        PythonToolsPackage.Instance.ShowInterpreterList();
                        return VSConstants.S_OK;
                    default:
                        handled = false;
                        break;
                }
            }

            return base.ExecCommandIndependentOfSelection(cmdGroup, cmdId, cmdExecOpt, vaIn, vaOut, commandOrigin, out handled);
        }

        protected override int ExecCommandThatDependsOnSelectedNodes(Guid cmdGroup, uint cmdId, uint cmdExecOpt, IntPtr vaIn, IntPtr vaOut, CommandOrigin commandOrigin, IList<HierarchyNode> selectedNodes, out bool handled) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                if ((int)cmdId == PythonConstants.InstallRequirementsTxt) {
                    var txt = CommonUtils.GetAbsoluteFilePath(ProjectHome, "requirements.txt");
                    if (_interpretersContainer != null && 
                        File.Exists(txt) &&
                        !selectedNodes.Any(n => {
                        var status = QueryStatusResult.NOTSUPPORTED;
                        return ErrorHandler.Succeeded(n.QueryStatusOnNode(cmdGroup, cmdId, IntPtr.Zero, ref status)) &&
                            status.HasFlag(QueryStatusResult.SUPPORTED);
                    })) {
                        // No nodes support this command, so run it for the
                        // current active environment.
                        var active = _interpreters.ActiveInterpreter;
                        var node = _interpretersContainer.AllChildren.OfType<InterpretersNode>().FirstOrDefault(n => n._factory == active);
                        var name = "-r " + ProcessOutput.QuoteSingleArgument(txt);
                        var elevate = PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ElevatePip;

                        var alreadyConfirmed = Marshal.GetObjectForNativeVariant(vaIn) as bool? ?? false;
                        if (alreadyConfirmed || InterpretersPackageNode.ShouldInstallRequirementsTxt(
                            Site,
                            node == null ? active.Description : node.Caption,
                            txt,
                            elevate
                        )) {
                            if (node == null) {
                                InterpretersPackageNode.InstallNewPackage(active, Site, name, true, elevate)
                                    .HandleAllExceptions(SR.ProductName).DoNotWait();
                            } else {
                                InterpretersPackageNode.InstallNewPackage(node, name, elevate)
                                    .HandleAllExceptions(SR.ProductName).DoNotWait();
                            }
                        }
                        handled = true;
                        return VSConstants.S_OK;
                    }
                }
            }
            
            return base.ExecCommandThatDependsOnSelectedNodes(cmdGroup, cmdId, cmdExecOpt, vaIn, vaOut, commandOrigin, selectedNodes, out handled);
        }

        protected override bool DisableCmdInCurrentMode(Guid cmdGroup, uint cmd) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                if (IsCurrentStateASuppressCommandsMode()) {
                    switch ((int)cmd) {
                        case CommonConstants.StartDebuggingCmdId:
                        case CommonConstants.StartWithoutDebuggingCmdId:
                            return true;
                        case PythonConstants.ActivateEnvironment:
                        case PythonConstants.AddEnvironment:
                        case PythonConstants.AddExistingVirtualEnv:
                        case PythonConstants.AddVirtualEnv:
                        case PythonConstants.InstallPythonPackage:
                        case PythonConstants.InstallRequirementsTxt:
                        case PythonConstants.GenerateRequirementsTxt:
                        case PythonConstants.AddSearchPathCommandId:
                        case PythonConstants.AddSearchPathZipCommandId:
                        case PythonConstants.AddPythonPathToSearchPathCommandId:
                            return true;
                        default:
                            if (cmd >= PythonConstants.FirstCustomCmdId && cmd <= PythonConstants.LastCustomCmdId) {
                                return true;
                            }
                            break;
                    }
                }
            }

            return base.DisableCmdInCurrentMode(cmdGroup, cmd);
        }

        #region IPythonProject Members

        string IPythonProject.ProjectName {
            get {
                return Caption;
            }
        }

        string IPythonProject.ProjectDirectory {
            get {
                return ProjectHome;
            }
        }

        string IPythonProject.GetProperty(string name) {
            return GetProjectProperty(name, true);
        }

        void IPythonProject.SetProperty(string name, string value) {
            SetProjectProperty(name, value);
        }

        string IPythonProject.GetStartupFile() {
            return base.GetStartupFile();
        }

        IPythonInterpreterFactory IPythonProject.GetInterpreterFactory() {
            return this.GetInterpreterFactory();
        }

        bool IPythonProject.Publish(PublishProjectOptions options) {
            return base.Publish(options, false);
        }

        string IPythonProject.GetUnevaluatedProperty(string name) {
            return base.GetUnevaluatedProperty(name);
        }

        #endregion

        internal int AddSearchPathZip() {
            var fileName = PythonToolsPackage.Instance.BrowseForFileOpen(
                IntPtr.Zero,
                "Zip Archives (*.zip;*.egg)|*.zip;*.egg|All Files (*.*)|*.*",
                ProjectHome
            );
            if (!string.IsNullOrEmpty(fileName)) {
                AddSearchPathEntry(fileName);
            }
            return VSConstants.S_OK;
        }

        internal bool IsPythonPathSet() {
            return !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable(GetInterpreterFactory().Configuration.PathEnvironmentVariable ?? "")
            );
        }

        internal int AddPythonPathToSearchPath() {
            var value = Environment.GetEnvironmentVariable(GetInterpreterFactory().Configuration.PathEnvironmentVariable ?? "");
            if (string.IsNullOrEmpty(value)) {
                return VSConstants.S_OK;
            }

            foreach (var bit in value.Split(';')) {
                if (!string.IsNullOrEmpty(bit)) {
                    AddSearchPathEntry(bit);
                }
            }
            return VSConstants.S_OK;
        }



        #region Virtual Env support

        internal void ShowAddInterpreter() {
            var service = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();

            var result = AddInterpreter.ShowDialog(this, service);
            if (result == null) {
                return;
            }

            var toRemove = new HashSet<IPythonInterpreterFactory>(_interpreters.GetInterpreterFactories());
            var toAdd = new HashSet<IPythonInterpreterFactory>(result);
            toRemove.ExceptWith(toAdd);
            toAdd.ExceptWith(toRemove);

            if (toAdd.Any() || toRemove.Any()) {
                //Make sure we can edit the project file
                if (!QueryEditProjectFile(false)) {
                    throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
                }
                foreach (var factory in toAdd) {
                    _interpreters.AddInterpreter(factory);
                }
                foreach (var factory in toRemove) {
                    _interpreters.RemoveInterpreterFactory(factory);
                }
            }
        }

        private async void ShowAddVirtualEnvironmentWithErrorHandling(bool browseForExisting) {
            var service = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();
            var statusBar = (IVsStatusbar)GetService(typeof(SVsStatusbar));
            object index = (short)0;
            statusBar.Animation(1, ref index);
            try {
                await AddVirtualEnvironment.ShowDialog(this, service, browseForExisting);
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }

                statusBar.SetText(SR.GetString(SR.VirtualEnvAddFailed));

                Debug.WriteLine("Failed to add virtual environment.\r\n{0}", ex.InnerException ?? ex);

                try {
                    ActivityLog.LogError(SR.ProductName, (ex.InnerException ?? ex).ToString());
                } catch (InvalidOperationException) {
                    // Activity log may be unavailable
                }
            } finally {
                statusBar.Animation(0, ref index);
            }
        }

        internal async Task<IPythonInterpreterFactory> CreateOrAddVirtualEnvironment(
            IInterpreterOptionsService service,
            bool create,
            string path,
            IPythonInterpreterFactory baseInterp,
            bool preferVEnv = false
        ) {
            if (create && preferVEnv) {
                await VirtualEnv.CreateWithVEnv(
                    baseInterp,
                    path,
                    OutputWindowRedirector.GetGeneral(Site)
                );
            } else if (create) {
                await VirtualEnv.CreateAndInstallDependencies(
                    baseInterp,
                    path,
                    OutputWindowRedirector.GetGeneral(Site)
                );
            }

            var existing = _interpreters.FindInterpreter(path);
            if (existing != null) {
                return existing;
            }

            var options = VirtualEnv.FindInterpreterOptions(path, service, baseInterp);
            if (options == null || !File.Exists(options.InterpreterPath)) {
                throw new InvalidOperationException(SR.GetString(SR.VirtualEnvAddFailed));
            }
            if (!create) {
                baseInterp = service.FindInterpreter(options.Id, options.LanguageVersion);
            }

            if (!QueryEditProjectFile(false)) {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            var id = _interpreters.CreateInterpreterFactory(options);
            return _interpreters.FindInterpreter(id, options.LanguageVersion);
        }


        /// <summary>
        /// Removes a given interpreter from the project, optionally deleting
        /// its prefix path from disk.
        /// </summary>
        internal async void RemoveInterpreter(IPythonInterpreterFactory factory, bool removeFromStorage = false) {
            Utilities.ArgumentNotNull("factory", factory);

            //Make sure we can edit the project file
            if (!QueryEditProjectFile(false)) {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }
            _interpreters.RemoveInterpreterFactory(factory);

            var path = factory.Configuration.PrefixPath;
            if (removeFromStorage && Directory.Exists(path)) {
                var t = Task.Run(() => {
                    Directory.Delete(path, true);
                    return true;
                }).HandleAllExceptions(SR.ProductName, GetType());

                if (!await t) {
                    MessageBox.Show(SR.GetString(SR.EnvironmentDeleteError, path), SR.ProductName);
                }
            }
        }

        #endregion

        public override Guid SharedCommandGuid {
            get {
                return GuidList.guidPythonToolsCmdSet;
            }
        }

        protected internal override int ShowAllFiles() {
            int hr = base.ShowAllFiles();
            BoldActiveEnvironment();
            return hr;
        }

        public void AddedAsRole(object azureProjectHierarchy, string roleType) {
            var hier = azureProjectHierarchy as IVsHierarchy;
            Debug.Assert(hier != null);
            // TODO: modify the service definition appropriately
        }
    }
}
