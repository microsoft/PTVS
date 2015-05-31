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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
using Microsoft.Build.Execution;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Azure;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using IServiceProvider = System.IServiceProvider;
using MessageBox = System.Windows.Forms.MessageBox;
using NativeMethods = Microsoft.VisualStudioTools.Project.NativeMethods;
using Task = System.Threading.Tasks.Task;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using VsMenus = Microsoft.VisualStudioTools.Project.VsMenus;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
#endif

namespace Microsoft.PythonTools.Project {
    [Guid(PythonConstants.ProjectNodeGuid)]
    internal class PythonProjectNode :
        CommonProjectNode,
        IPythonProject3,
        IAzureRoleProject,
        IPythonProjectLaunchProperties {
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
        private readonly CommentTaskProvider _commentTaskProvider;

        public PythonProjectNode(IServiceProvider serviceProvider)
            : base(
                  serviceProvider,
#if DEV14_OR_LATER
                  null
#else
                  Utilities.GetImageList(typeof(PythonProjectNode).Assembly.GetManifestResourceStream(PythonConstants.ProjectImageList))
#endif
        ) {

            Type projectNodePropsType = typeof(PythonProjectNodeProperties);
            AddCATIDMapping(projectNodePropsType, projectNodePropsType.GUID);

            _commentTaskProvider = ((CommentTaskProvider)serviceProvider.GetService(typeof(CommentTaskProvider)));
            if (_commentTaskProvider != null) {
                _commentTaskProvider.TokensChanged += CommentTaskTokensChanged;
            }
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
            var interpreterService = Site.GetComponentModel().GetService<IInterpreterOptionsService>();
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
            Site.GetUIThread().Invoke(() => RefreshInterpreters());
        }

        internal MSBuildProjectInterpreterFactoryProvider Interpreters {
            get {
                return _interpreters;
            }
        }

        protected override Stream ProjectIconsImageStripStream {
            get {
#if DEV14_OR_LATER
                throw new NotSupportedException("Python Tools does not support project image strip");
#else
                return typeof(ProjectNode).Assembly.GetManifestResourceStream("Microsoft.PythonTools.Project.Resources.imagelis.bmp");
#endif
            }
        }

        protected internal override void SetCurrentConfiguration() {
            base.SetCurrentConfiguration();

            if (!IsProjectOpened)
                return;

            if (this.IsAppxPackageableProject()) {
                EnvDTE.Project automationObject = (EnvDTE.Project)GetAutomationObject();

                this.BuildProject.SetGlobalProperty(ProjectFileConstants.Platform, automationObject.ConfigurationManager.ActiveConfiguration.PlatformName);
            }
        }

#if DEV14_OR_LATER
        protected override bool SupportsIconMonikers {
            get { return true; }
        }

        protected override ImageMoniker GetIconMoniker(bool open) {
            return KnownMonikers.PYProjectNode;
        }
#else
        internal int GetIconIndex(PythonProjectImageName name) {
            return ImageOffset + (int)name;
        }

        internal IntPtr GetIconHandleByName(PythonProjectImageName name) {
            return ImageHandler.GetIconHandle(GetIconIndex(name));
        }
#endif

        internal override string IssueTrackerUrl {
            get { return PythonConstants.IssueTrackerUrl; }
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

        protected override ConfigProvider CreateConfigProvider() {
            return new CommonConfigProvider(this);
        }
        protected override ReferenceContainerNode CreateReferenceContainerNode() {
            return new PythonReferenceContainerNode(this);
        }

        protected override void LinkFileAdded(string filename) {
            if (Site.GetPythonToolsService().GeneralOptions.UpdateSearchPathsWhenAddingLinkedFiles) {
                // update our search paths.
                string dirToAdd;
                try {
                    dirToAdd = ModulePath.FromFullPath(filename).LibraryPath;
                } catch (ArgumentException) {
                    dirToAdd = null;
                }
                if (!string.IsNullOrEmpty(dirToAdd)) {
                    AddSearchPathEntry(CommonUtils.EnsureEndSeparator(dirToAdd));
                }
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
            return ModulePath.IsPythonSourceFile(strFileName);
        }

        public override string[] CodeFileExtensions {
            get {
                return new[] { PythonConstants.FileExtension, PythonConstants.WindowsFileExtension };
            }
        }

        public override Type GetProjectFactoryType() {
            return typeof(PythonProjectFactory);
        }

        public override string GetProjectName() {
            return "PythonProject";
        }

        protected override string ProjectCapabilities {
            get { return "Python"; }
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

        public override MSBuildResult Build(string config, string target) {
            if (this.IsAppxPackageableProject()) {
                // Ensure that AnyCPU is not the default Platform if this is an AppX project
                // Use x86 instead
                var platform = this.BuildProject.GetPropertyValue(GlobalProperty.Platform.ToString());

                if (platform == ProjectConfig.AnyCPU) {
                    this.BuildProject.SetGlobalProperty(GlobalProperty.Platform.ToString(), ConfigProvider.x86Platform);
                }
            }
            return base.Build(config, target);
        }

        protected override void Reload() {
            if (!this.IsAppxPackageableProject()) {
                _searchPathContainer = new CommonSearchPathContainerNode(this);
                this.AddChild(_searchPathContainer);
                RefreshCurrentWorkingDirectory();
                RefreshSearchPaths();
                _interpretersContainer = new InterpretersContainerNode(this);
                this.AddChild(_interpretersContainer);
                RefreshInterpreters(alwaysCollapse: true);
            }

            OnProjectPropertyChanged += PythonProjectNode_OnProjectPropertyChanged;
            base.Reload();

            string id;
            if (_interpreters.IsActiveInterpreterGlobalDefault &&
                !string.IsNullOrEmpty(id = GetProjectProperty(MSBuildProjectInterpreterFactoryProvider.InterpreterIdProperty))) {
                // The interpreter in the project file has no reference, so
                // find and add it.
                var interpreterService = Site.GetComponentModel().GetService<IInterpreterOptionsService>();
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

            Site.GetPythonToolsService().SurveyNews.CheckSurveyNews(false);
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

        private static bool RemoveFirst<T>(List<T> list, Func<T, bool> condition) {
            for (int i = 0; i < list.Count; ++i) {
                if (condition(list[i])) {
                    list.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        private void RefreshInterpreters(bool alwaysCollapse = false) {
            if (IsClosed) {
                return;
            }

            var node = _interpretersContainer;
            if (node == null) {
                return;
            }

            var remaining = node.AllChildren.OfType<InterpretersNode>().ToList();

            var interpreters = Interpreters;
            if (interpreters != null) {
                if (!interpreters.IsActiveInterpreterGlobalDefault) {
                    foreach (var fact in interpreters.GetInterpreterFactories()) {
                        if (!RemoveFirst(remaining, n => !n._isGlobalDefault && n._factory == fact)) {
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
                } else {
                    var fact = interpreters.ActiveInterpreter;
                    if (fact.IsRunnable() && !RemoveFirst(remaining, n => n._isGlobalDefault && n._factory == fact)) {
                        node.AddChild(new InterpretersNode(this, null, fact, true, false, true));
                    }
                }
            }

            foreach (var child in remaining) {
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
            return PythonToolsPackage.GetLauncher(Site, this);
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

                if (_commentTaskProvider != null) {
                    _commentTaskProvider.TokensChanged -= CommentTaskTokensChanged;
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
                if (_interpretersContainer != null) {
                    _interpretersContainer.Dispose();
                    _interpretersContainer = null;
                }
                if (_searchPathContainer != null) {
                    _searchPathContainer.Dispose();
                    _searchPathContainer = null;
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
            if (IsClosed) {
                Debug.Fail("GetAnalyzer() called on closed project");
                var service = (PythonToolsService)PythonToolsPackage.GetGlobalService(typeof(PythonToolsService));
                if (service == null) {
                    throw new InvalidOperationException("Called GetAnalyzer() with no Python Tools service available");
                }
                return service.DefaultAnalyzer;
            } else if (_analyzer == null) {
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

            var model = Site.GetComponentModel();
            var interpreterService = model.GetService<IInterpreterOptionsService>();
            var factory = GetInterpreterFactory();
            var res = new VsProjectAnalyzer(
                Site,
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
            var interpreters = _interpreters;
            if (interpreters == null) {
                // May occur if we are racing with Dispose(), so the factory we
                // return isn't important, but it has to be non-null to fulfil
                // the contract.
                var service = Site.GetComponentModel().GetService<IInterpreterOptionsService>();
                return service.DefaultInterpreter;
            }

            var fact = _interpreters.ActiveInterpreter;

            Site.GetPythonToolsService().EnsureCompletionDb(fact);

            return fact;
        }

        public IPythonInterpreterFactory GetInterpreterFactoryOrThrow() {
            var interpreters = _interpreters;
            var service = Site.GetComponentModel().GetService<IInterpreterOptionsService>();

            if (interpreters == null) {
                // May occur if we are racing with Dispose(), so the factory we
                // return isn't important, but it has to be non-null to fulfil
                // the contract.
                return service.DefaultInterpreter;
            }

            var fact = _interpreters.ActiveInterpreter;
            if (fact == service.NoInterpretersValue) {
                throw new NoInterpretersException();
            }
            if (!interpreters.IsAvailable(fact) || !File.Exists(fact.Configuration.InterpreterPath)) {
                throw new MissingInterpreterException(
                    SR.GetString(SR.MissingEnvironment, fact.Description, fact.Configuration.Version)
                );
            }

            Site.GetPythonToolsService().EnsureCompletionDb(fact);

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

            Site.GetUIThread().InvokeAsync(ReanalyzeProject).DoNotWait();
        }

        private void CommentTaskTokensChanged(object sender, EventArgs e) {
            ReanalyzeProject();
        }

        private void ReanalyzeProject() {
            if (IsClosing || IsClosed) {
                // This deferred event is no longer important.
                return;
            }

            var statusBar = Site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            if (statusBar != null) {
                statusBar.SetText(SR.GetString(SR.AnalyzingProject));
                object index = (short)0;
                statusBar.Animation(1, ref index);
                statusBar.FreezeOutput(1);
            }

            try {
                RefreshInterpreters();

                if (_analyzer != null) {
                    UnHookErrorsAndWarnings(_analyzer);
                }
                var analyzer = CreateAnalyzer();
                Debug.Assert(analyzer != null);

                var analyzerChanging = ProjectAnalyzerChanging;
                if (analyzerChanging != null) {
                    analyzerChanging(this, new AnalyzerChangingEventArgs(
                        _analyzer != null ? _analyzer.Project : null,
                        analyzer != null ? analyzer.Project : null
                    ));
                }

                Reanalyze(analyzer);
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
            } catch (ObjectDisposedException) {
                // Raced with project disposal
            } finally {
                if (statusBar != null) {
                    statusBar.FreezeOutput(0);
                    object index = (short)0;
                    statusBar.Animation(0, ref index);
                    statusBar.Clear();
                }
            }
        }

        private void Reanalyze(VsProjectAnalyzer newAnalyzer) {
            foreach (var child in AllVisibleDescendants.OfType<FileNode>()) {
                newAnalyzer.AnalyzeFile(child.Url);
            }

            var references = GetReferenceContainer();
            var interp = newAnalyzer.Interpreter as IPythonInterpreterWithProjectReferences;
            if (references != null && interp != null) {
                foreach (var child in GetReferenceContainer().EnumReferences()) {
                    var pyd = child as PythonExtensionReferenceNode;
                    if (pyd != null) {
                        pyd.AnalyzeReference(interp);
                    }
                    var pyproj = child as PythonProjectReferenceNode;
                    if (pyproj != null) {
                        pyproj.AddAnalyzedAssembly(interp).DoNotWait();
                    }
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

            factory = factory ?? GetInterpreterFactory();
            var config = factory == null ? null : factory.Configuration;
            if (config != null) {
                psi.Arguments = string.Format("/K \"title {0} Command Prompt\"",
                    string.IsNullOrEmpty(subtitle) ? Caption : subtitle
                );

                var props = PythonProjectLaunchProperties.Create(this);
                var env = new Dictionary<string, string>(props.GetEnvironment(true));

                var paths = new List<string>();
                paths.Add(CommonUtils.GetParent(((IPythonProjectLaunchProperties)this).GetInterpreterPath()));
                paths.Add(CommonUtils.GetParent(config.InterpreterPath));
                paths.Add(CommonUtils.GetAbsoluteDirectoryPath(config.PrefixPath, "Scripts"));
                if (psi.EnvironmentVariables.ContainsKey("PATH")) {
                    paths.AddRange(psi.EnvironmentVariables["PATH"].Split(Path.PathSeparator));
                }
                paths.AddRange(Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator));

                PythonProjectLaunchProperties.MergeEnvironmentBelow(env, new[]{ new KeyValuePair<string, string>(
                    "PATH", string.Join(new string(Path.PathSeparator, 1), paths.Where(Directory.Exists).Distinct())
                )}, true);

                foreach (var kv in env) {
                    psi.EnvironmentVariables[kv.Key] = kv.Value;
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
                    case PythonConstants.InstallPythonPackage:
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

                QueryStatusResult status;
                switch ((int)cmd) {
                    case PythonConstants.InstallRequirementsTxt:
                        status = base.QueryStatusSelectionOnNodes(selectedNodes, cmdGroup, cmd, pCmdText) |
                            QueryStatusResult.SUPPORTED;
                        if (File.Exists(CommonUtils.GetAbsoluteFilePath(ProjectHome, "requirements.txt"))) {
                            status |= QueryStatusResult.ENABLED;
                        }
                        return status;
                    case PythonConstants.ActivateEnvironment:
                        status = base.QueryStatusSelectionOnNodes(selectedNodes, cmdGroup, cmd, pCmdText);
                        if (!status.HasFlag(QueryStatusResult.SUPPORTED)) {
                            // Command is supported if an environment is
                            // selected, so only force enable if nobody has
                            // claimed it.
                            status = QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        }
                        return status;
                }
            }

            return base.QueryStatusSelectionOnNodes(selectedNodes, cmdGroup, cmd, pCmdText);
        }

        private async Task ExecuteCustomCommandAsync(CustomCommand command) {
            try {
                await command.ExecuteAsync(null);
            } catch (MissingInterpreterException ex) {
                MessageBox.Show(ex.Message, SR.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            } catch (NoInterpretersException ex) {
                PythonToolsPackage.OpenNoInterpretersHelpPage(Site, ex.HelpPage);
            } catch (Exception ex) {
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

        protected override int ExecCommandIndependentOfSelection(Guid cmdGroup, uint cmdId, uint cmdExecOpt, IntPtr vaIn, IntPtr vaOut, CommandOrigin commandOrigin, out bool handled) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                var command = GetCustomCommand(cmdId);
                handled = true;

                if (command != null) {
                    if (command.CanExecute(null)) {
                        if (!Utilities.SaveDirtyFiles()) {
                            return VSConstants.S_OK;
                        }

                        ExecuteCustomCommandAsync(command).DoNotWait();
                    }
                    return VSConstants.S_OK;
                }

                switch ((int)cmdId) {
                    case PythonConstants.AddEnvironment:
                        ShowAddInterpreter();
                        return VSConstants.S_OK;
                    case PythonConstants.AddExistingVirtualEnv:
                    case PythonConstants.AddVirtualEnv:
                        ShowAddVirtualEnvironmentWithErrorHandling((int)cmdId == PythonConstants.AddExistingVirtualEnv);
                        return VSConstants.S_OK;
                    case PythonConstants.ViewAllEnvironments:
                        Site.ShowInterpreterList();
                        return VSConstants.S_OK;
                    case PythonConstants.AddSearchPathCommandId:
                        return AddSearchPath();
                    case PythonConstants.AddSearchPathZipCommandId:
                        return AddSearchPathZip();
                    case PythonConstants.AddPythonPathToSearchPathCommandId:
                        return AddPythonPathToSearchPath();
                    default:
                        handled = false;
                        break;
                }
            }

            return base.ExecCommandIndependentOfSelection(cmdGroup, cmdId, cmdExecOpt, vaIn, vaOut, commandOrigin, out handled);
        }

        private void GetSelectedInterpreterOrDefault(
            IEnumerable<HierarchyNode> selectedNodes,
            Dictionary<string, string> args,
            out InterpretersNode node,
            out IPythonInterpreterFactory factory
        ) {
            factory = null;

            // First try and get the factory from the parameter
            string description;
            if (args != null && args.TryGetValue("e", out description) && !string.IsNullOrEmpty(description)) {
                var service = Site.GetComponentModel().GetService<IInterpreterOptionsService>();

                factory = _interpreters.GetInterpreterFactories().FirstOrDefault(
                    // Description is a localized string, hence CCIC
                    f => description.Equals(f.Description, StringComparison.CurrentCultureIgnoreCase)
                ) ?? service.Interpreters.FirstOrDefault(
                    f => description.Equals(f.Description, StringComparison.CurrentCultureIgnoreCase)
                );
            }

            if (factory == null) {
                var candidates = selectedNodes
                    .OfType<InterpretersNode>()
                    .Where(n => n._factory != null && n._factory.IsRunnable())
                    .Distinct()
                    .ToList();

                if (candidates.Count == 1) {
                    node = candidates[0];
                    factory = node._factory;
                    return;
                }

                factory = GetInterpreterFactory();
            }

            if (_interpretersContainer != null && factory != null) {
                var active = factory;
                node = _interpretersContainer.AllChildren
                    .OfType<InterpretersNode>()
                    .FirstOrDefault(n => n._factory == active);
            } else {
                node = null;
            }
        }

        protected internal override string QueryCommandArguments(Guid cmdGroup, uint cmdId, CommandOrigin commandOrigin) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch ((int)cmdId) {
                    case PythonConstants.ActivateEnvironment:
                        return "e,env,environment:";
                    case PythonConstants.InstallRequirementsTxt:
                        return "e,env,environment: a,admin y";
                    case PythonConstants.OpenInteractiveForEnvironment:
                        return "e,env,environment:";
                    case PythonConstants.InstallPythonPackage:
                        return "e,env,environment: p,package: a,admin";
                    case PythonConstants.GenerateRequirementsTxt:
                        return "e,env,environment:";
                }
            }
            return base.QueryCommandArguments(cmdGroup, cmdId, commandOrigin);
        }

        protected override int ExecCommandThatDependsOnSelectedNodes(Guid cmdGroup, uint cmdId, uint cmdExecOpt, IntPtr vaIn, IntPtr vaOut, CommandOrigin commandOrigin, IList<HierarchyNode> selectedNodes, out bool handled) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                handled = true;
                switch ((int)cmdId) {
                    case PythonConstants.ActivateEnvironment:
                        return ExecActivateEnvironment(ParseCommandArgs(vaIn, cmdGroup, cmdId), selectedNodes);
                    case PythonConstants.InstallRequirementsTxt:
                        return ExecInstallRequirementsTxt(ParseCommandArgs(vaIn, cmdGroup, cmdId), selectedNodes);
                    case PythonConstants.OpenInteractiveForEnvironment:
                        return ExecOpenInteractiveForEnvironment(ParseCommandArgs(vaIn, cmdGroup, cmdId), selectedNodes);
                    case PythonConstants.InstallPythonPackage:
                        return ExecInstallPythonPackage(ParseCommandArgs(vaIn, cmdGroup, cmdId), selectedNodes);
                    case PythonConstants.GenerateRequirementsTxt:
                        return ExecGenerateRequirementsTxt(ParseCommandArgs(vaIn, cmdGroup, cmdId), selectedNodes);
                    default:
                        handled = false;
                        break;
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
        private int ExecActivateEnvironment(Dictionary<string, string> args, IList<HierarchyNode> selectedNodes) {
            InterpretersNode selectedInterpreter;
            IPythonInterpreterFactory selectedInterpreterFactory;
            GetSelectedInterpreterOrDefault(selectedNodes, args, out selectedInterpreter, out selectedInterpreterFactory);
            if (selectedInterpreterFactory != null) {
                return SetInterpreterFactory(selectedInterpreterFactory);
            }
            return VSConstants.S_OK;
        }

        private int ExecOpenInteractiveForEnvironment(Dictionary<string, string> args, IList<HierarchyNode> selectedNodes) {
            InterpretersNode selectedInterpreter;
            IPythonInterpreterFactory selectedInterpreterFactory;
            GetSelectedInterpreterOrDefault(selectedNodes, args, out selectedInterpreter, out selectedInterpreterFactory);
            try {
                var window = ExecuteInReplCommand.EnsureReplWindow(Site, selectedInterpreterFactory, this);
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
            return Publish(options, false);
        }

        public override bool Publish(PublishProjectOptions publishOptions, bool async) {
            var factory = GetInterpreterFactory();
            if (_interpreters.IsAvailable(factory) &&
                Directory.Exists(factory.Configuration.PrefixPath) &&
                CommonUtils.IsSubpathOf(ProjectHome, factory.Configuration.PrefixPath)
            ) {
                try {
                    publishOptions = TaskDialog.CallWithRetry(
                        _ => new PublishProjectOptions(
                            publishOptions.AdditionalFiles.Concat(
                                Directory.EnumerateFiles(
                                    factory.Configuration.PrefixPath,
                                    "*",
                                    SearchOption.AllDirectories
                                )
                            // Exclude the completion DB
                                .Where(f => !f.Contains("\\.ptvs\\"))
                                .Select(f => new PublishFile(f, CommonUtils.GetRelativeFilePath(ProjectHome, f)))
                            ).ToArray(),
                            publishOptions.DestinationUrl
                        ),
                        Site,
                        SR.GetString(SR.FailedToCollectFilesForPublish),
                        SR.GetString(SR.FailedToCollectFilesForPublishMessage),
                        SR.GetString(SR.ErrorDetail),
                        SR.GetString(SR.Retry),
                        SR.GetString(SR.Cancel)
                    );
                } catch (OperationCanceledException) {
                    return false;
                }
            }
            return base.Publish(publishOptions, async);
        }

        string IPythonProject.GetUnevaluatedProperty(string name) {
            return base.GetUnevaluatedProperty(name);
        }

#endregion

#region Search Path support

        internal int AddSearchPathZip() {
            var fileName = Site.BrowseForFileOpen(
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

#endregion

#region Package Installation support

        private int ExecInstallPythonPackage(Dictionary<string, string> args, IList<HierarchyNode> selectedNodes) {
            InterpretersNode selectedInterpreter;
            IPythonInterpreterFactory selectedInterpreterFactory;
            GetSelectedInterpreterOrDefault(selectedNodes, args, out selectedInterpreter, out selectedInterpreterFactory);

            string name;
            if (args != null && args.TryGetValue("p", out name)) {
                // Don't prompt, just install
                bool elevated = args.ContainsKey("a");
                InstallNewPackageAsync(
                    selectedInterpreterFactory,
                    Site,
                    name,
                    elevated,
                    node: selectedInterpreter
                )
                    .SilenceException<OperationCanceledException>()
                    .HandleAllExceptions(SR.ProductName)
                    .DoNotWait();
            } else {
                // Prompt the user
                InstallNewPackageAsync(
                    selectedInterpreterFactory,
                    Site,
                    selectedInterpreter
                )
                    .SilenceException<OperationCanceledException>()
                    .HandleAllExceptions(SR.ProductName)
                    .DoNotWait();
            }
            return VSConstants.S_OK;
        }

        private int ExecInstallRequirementsTxt(Dictionary<string, string> args, IList<HierarchyNode> selectedNodes) {
            InterpretersNode selectedInterpreter;
            IPythonInterpreterFactory selectedInterpreterFactory;
            GetSelectedInterpreterOrDefault(selectedNodes, args, out selectedInterpreter, out selectedInterpreterFactory);
            var txt = CommonUtils.GetAbsoluteFilePath(ProjectHome, "requirements.txt");
            var elevated = Site.GetPythonToolsService().GeneralOptions.ElevatePip;
            var name = "-r " + ProcessOutput.QuoteSingleArgument(txt);
            if (args != null && !args.ContainsKey("y")) {
                if (!ShouldInstallRequirementsTxt(
                    selectedInterpreterFactory.Description,
                    txt,
                    elevated
                )) {
                    return VSConstants.S_OK;
                }
            }

            InstallNewPackageAsync(
                selectedInterpreterFactory,
                Site,
                name,
                elevated,
                node: selectedInterpreter
            )
                .SilenceException<OperationCanceledException>()
                .HandleAllExceptions(SR.ProductName)
                .DoNotWait();

            return VSConstants.S_OK;
        }


        internal static void BeginUninstallPackage(
            IPythonInterpreterFactory factory,
            IServiceProvider provider,
            string name,
            InterpretersNode node = null
        ) {
            UninstallPackageAsync(factory, provider, name, node)
                .SilenceException<OperationCanceledException>()
                .HandleAllExceptions(SR.ProductName)
                .DoNotWait();
        }

        internal static async Task InstallNewPackageAsync(
            IPythonInterpreterFactory factory,
            IServiceProvider provider,
            InterpretersNode node = null
        ) {
            var service = provider.GetComponentModel().GetService<IInterpreterOptionsService>();
            var view = InstallPythonPackage.ShowDialog(provider, factory, service);
            if (view == null) {
                throw new OperationCanceledException();
            }

            Func<string, bool, Redirector, Task<bool>> f;
            if (view.InstallUsingConda) {
                f = (n, e, r) => Conda.Install(provider, factory, service, n, r);
            } else if (view.InstallUsingEasyInstall) {
                f = (n, e, r) => EasyInstall.Install(provider, factory, n, provider, e, r);
            } else {
                f = (n, e, r) => Pip.Install(provider, factory, n, provider, e, r);
            }

            await InstallNewPackageAsync(factory, provider, view.Name, view.InstallElevated, f, node);
        }

        internal static async Task InstallNewPackageAsync(
            IPythonInterpreterFactory factory,
            IServiceProvider provider,
            string name,
            bool elevate,
            Func<string, bool, Redirector, Task<bool>> action = null,
            InterpretersNode node = null
        ) {
            var statusBar = (IVsStatusbar)provider.GetService(typeof(SVsStatusbar));

            var service = provider.GetComponentModel().GetService<IInterpreterOptionsService>() as IInterpreterOptionsService2;
            object cookie = null;
            if (service != null) {
                cookie = await service.LockInterpreterAsync(
                    factory,
                    InterpretersNode.InstallPackageLockMoniker,
                    TimeSpan.MaxValue
                );
            }

            try {
                if (cookie != null && node != null) {
                    // don't process events while we're installing, we'll
                    // rescan once we're done
                    node.StopWatching();
                }

                var redirector = OutputWindowRedirector.GetGeneral(provider);
                statusBar.SetText(SR.GetString(SR.PackageInstallingSeeOutputWindow, name));

                Task<bool> task;
                if (action != null) {
                    task = action(name, elevate, redirector);
                } else {
                    task = Pip.Install(provider, factory, name, elevate, redirector);
                }

                bool success = await task;
                statusBar.SetText(SR.GetString(
                    success ? SR.PackageInstallSucceeded : SR.PackageInstallFailed,
                    name
                ));

                var packageInfo = FindRequirementRegex.Match(name.ToLower());

                //If we fail to parse the package name then just skip logging it
                if (packageInfo.Groups["name"].Success) {
                    //Log the details of the Installation
                    var packageDetails = new Logging.PackageInstallDetails(
                        packageInfo.Groups["name"].Value,
                        packageInfo.Groups["ver"].Success ? packageInfo.Groups["ver"].Value : String.Empty,
                        factory.GetType().Name,
                        factory.Configuration.Version.ToString(),
                        factory.Configuration.Architecture.ToString(),
                        String.Empty, //Installer if we tracked it
                        elevate,
                        success ? 0 : 1);

                    provider.GetPythonToolsService().Logger.LogEvent(Logging.PythonLogEvent.PackageInstalled, packageDetails);
                }

            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                statusBar.SetText(SR.GetString(SR.PackageInstallFailed, name));
            } finally {
                if (service != null && cookie != null) {
                    bool lastOne = service.UnlockInterpreter(cookie);
                    if (lastOne && node != null) {
                        node.ResumeWatching();
                    }
                }
            }
        }

        internal static async Task UninstallPackageAsync(
            IPythonInterpreterFactory factory,
            IServiceProvider provider,
            string name,
            InterpretersNode node = null) {
            var statusBar = (IVsStatusbar)provider.GetService(typeof(SVsStatusbar));

            var service = provider.GetComponentModel().GetService<IInterpreterOptionsService>() as IInterpreterOptionsService2;
            object cookie = null;
            if (service != null) {
                cookie = await service.LockInterpreterAsync(
                    factory,
                    InterpretersNode.InstallPackageLockMoniker,
                    TimeSpan.MaxValue
                );
            }

            try {
                if (cookie != null && node != null) {
                    // don't process events while we're installing, we'll
                    // rescan once we're done
                    node.StopWatching();
                }

                var redirector = OutputWindowRedirector.GetGeneral(provider);
                statusBar.SetText(SR.GetString(SR.PackageUninstallingSeeOutputWindow, name));

                bool elevate = provider.GetPythonToolsService().GeneralOptions.ElevatePip;

                bool success = await Pip.Uninstall(provider, factory, name, elevate, redirector);
                statusBar.SetText(SR.GetString(
                    success ? SR.PackageUninstallSucceeded : SR.PackageUninstallFailed,
                    name
                ));
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                statusBar.SetText(SR.GetString(SR.PackageUninstallFailed, name));
            } finally {
                if (service != null && cookie != null) {
                    bool lastOne = service.UnlockInterpreter(cookie);
                    if (lastOne && node != null) {
                        node.ResumeWatching();
                    }
                }
            }
        }


        private bool ShouldInstallRequirementsTxt(
            string targetLabel,
            string txt,
            bool elevate
        ) {
            if (!File.Exists(txt)) {
                return false;
            }
            string content;
            try {
                content = File.ReadAllText(txt);
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                return false;
            }

            var td = new TaskDialog(Site) {
                Title = SR.ProductName,
                MainInstruction = SR.GetString(SR.ShouldInstallRequirementsTxtHeader),
                Content = SR.GetString(SR.ShouldInstallRequirementsTxtContent),
                ExpandedByDefault = true,
                ExpandedControlText = SR.GetString(SR.ShouldInstallRequirementsTxtExpandedControl),
                CollapsedControlText = SR.GetString(SR.ShouldInstallRequirementsTxtCollapsedControl),
                ExpandedInformation = content,
                AllowCancellation = true
            };

            var install = new TaskDialogButton(SR.GetString(SR.ShouldInstallRequirementsTxtInstallInto, targetLabel)) {
                ElevationRequired = elevate
            };

            td.Buttons.Add(install);
            td.Buttons.Add(TaskDialogButton.Cancel);

            return td.ShowModal() == install;
        }

        private int ExecGenerateRequirementsTxt(Dictionary<string, string> args, IList<HierarchyNode> selectedNodes) {
            InterpretersNode selectedInterpreter;
            IPythonInterpreterFactory selectedInterpreterFactory;
            GetSelectedInterpreterOrDefault(selectedNodes, args, out selectedInterpreter, out selectedInterpreterFactory);
            if (selectedInterpreterFactory != null) {
                GenerateRequirementsTxtAsync(selectedInterpreterFactory)
                    .SilenceException<OperationCanceledException>()
                    .HandleAllExceptions(SR.ProductName)
                    .DoNotWait();
            }
            return VSConstants.S_OK;
        }

        private async Task GenerateRequirementsTxtAsync(IPythonInterpreterFactory factory) {
            var projectHome = ProjectHome;
            var txt = CommonUtils.GetAbsoluteFilePath(projectHome, "requirements.txt");

            string[] existing = null;
            bool addNew = false;
            if (File.Exists(txt)) {
                existing = TaskDialog.CallWithRetry(
                    _ => File.ReadAllLines(txt),
                    Site,
                    SR.ProductName,
                    SR.GetString(SR.RequirementsTxtFailedToRead),
                    SR.GetString(SR.ErrorDetail),
                    SR.GetString(SR.Retry),
                    SR.GetString(SR.Cancel)
                );

                var td = new TaskDialog(Site) {
                    Title = SR.ProductName,
                    MainInstruction = SR.GetString(SR.RequirementsTxtExists),
                    Content = SR.GetString(SR.RequirementsTxtExistsQuestion),
                    AllowCancellation = true,
                    CollapsedControlText = SR.GetString(SR.RequirementsTxtContentCollapsed),
                    ExpandedControlText = SR.GetString(SR.RequirementsTxtContentExpanded),
                    ExpandedInformation = string.Join(Environment.NewLine, existing)
                };
                var replace = new TaskDialogButton(
                    SR.GetString(SR.RequirementsTxtReplace),
                    SR.GetString(SR.RequirementsTxtReplaceHelp)
                );
                var refresh = new TaskDialogButton(
                    SR.GetString(SR.RequirementsTxtRefresh),
                    SR.GetString(SR.RequirementsTxtRefreshHelp)
                );
                var update = new TaskDialogButton(
                    SR.GetString(SR.RequirementsTxtUpdate),
                    SR.GetString(SR.RequirementsTxtUpdateHelp)
                );
                td.Buttons.Add(replace);
                td.Buttons.Add(refresh);
                td.Buttons.Add(update);
                td.Buttons.Add(TaskDialogButton.Cancel);
                var selection = td.ShowModal();
                if (selection == TaskDialogButton.Cancel) {
                    return;
                } else if (selection == replace) {
                    existing = null;
                } else if (selection == update) {
                    addNew = true;
                }
            }

            var items = await Pip.Freeze(factory);

            if (File.Exists(txt) && !QueryEditFiles(false, txt)) {
                return;
            }

            TaskDialog.CallWithRetry(
                _ => {
                    if (items.Any()) {
                        File.WriteAllLines(txt, MergeRequirements(existing, items, addNew));
                    } else if (existing == null) {
                        File.WriteAllText(txt, "");
                    }
                },
                Site,
                SR.ProductName,
                SR.GetString(SR.RequirementsTxtFailedToWrite),
                SR.GetString(SR.ErrorDetail),
                SR.GetString(SR.Retry),
                SR.GetString(SR.Cancel)
            );

            var existingNode = FindNodeByFullPath(txt);
            if (existingNode == null || existingNode.IsNonMemberItem) {
                if (!QueryEditProjectFile(false)) {
                    return;
                }
                existingNode = TaskDialog.CallWithRetry(
                    _ => {
                        ErrorHandler.ThrowOnFailure(AddItem(
                            ID,
                            VSADDITEMOPERATION.VSADDITEMOP_LINKTOFILE,
                            Path.GetFileName(txt),
                            1,
                            new[] { txt },
                            IntPtr.Zero,
                            new VSADDRESULT[1]
                        ));

                        return FindNodeByFullPath(txt);
                    },
                    Site,
                    SR.ProductName,
                    SR.GetString(SR.RequirementsTxtFailedToAddToProject),
                    SR.GetString(SR.ErrorDetail),
                    SR.GetString(SR.Retry),
                    SR.GetString(SR.Cancel)
                );
            }
        }

        internal static readonly Regex FindRequirementRegex = new Regex(@"
            (?<!\#.*)       # ensure we are not in a comment
            (?<=\s|\A)      # ensure we are preceded by a space/start of the line
            (?<spec>        # <spec> includes name, version and whitespace
                (?<name>[^\s\#<>=!\-][^\s\#<>=!]*)  # just the name, no whitespace
                (\s*(?<cmp><=|>=|<|>|!=|==)\s*
                    (?<ver>[^\s\#]+)
                )?          # cmp and ver are optional
            )", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace
        );

        internal static IEnumerable<string> MergeRequirements(
            IEnumerable<string> original,
            IEnumerable<string> updates,
            bool addNew
        ) {
            if (original == null) {
                foreach (var req in updates.OrderBy(r => r)) {
                    yield return req;
                }
                yield break;
            }

            var existing = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var m in updates.SelectMany(req => FindRequirementRegex.Matches(req).Cast<Match>())) {
                existing[m.Groups["name"].Value] = m.Value;
            }

            var seen = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var _line in original) {
                var line = _line;
                foreach (var m in FindRequirementRegex.Matches(line).Cast<Match>()) {
                    string newReq;
                    var name = m.Groups["name"].Value;
                    if (existing.TryGetValue(name, out newReq)) {
                        line = FindRequirementRegex.Replace(line, m2 =>
                            name.Equals(m2.Groups["name"].Value, StringComparison.InvariantCultureIgnoreCase) ?
                                newReq :
                                m2.Value
                        );
                        seen.Add(name);
                    }
                }
                yield return line;
            }

            if (addNew) {
                foreach (var req in existing
                    .Where(kv => !seen.Contains(kv.Key))
                    .Select(kv => kv.Value)
                    .OrderBy(v => v)
                ) {
                    yield return req;
                }
            }
        }

#endregion

#region Virtual Env support

        private void ShowAddInterpreter() {
            var service = Site.GetComponentModel().GetService<IInterpreterOptionsService>();

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
            var service = Site.GetComponentModel().GetService<IInterpreterOptionsService>();
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
                    Site,
                    baseInterp,
                    path,
                    OutputWindowRedirector.GetGeneral(Site)
                );
            } else if (create) {
                await VirtualEnv.CreateAndInstallDependencies(
                    Site,
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

            if (hier == null) {
                return;
            }

            Site.GetUIThread().Invoke(() => UpdateServiceDefinition(hier, roleType, Caption, Site));
        }

        private static bool TryGetItemId(object obj, out uint id) {
            const uint nil = (uint)VSConstants.VSITEMID.Nil;
            id = obj as uint? ?? nil;
            if (id == nil) {
                var asInt = obj as int?;
                if (asInt.HasValue) {
                    id = unchecked((uint)asInt.Value);
                }
            }
            return id != nil;
        }

        /// <summary>
        /// Updates the ServiceDefinition.csdef file in
        /// <paramref name="project"/> to include the default startup and
        /// runtime tasks for Python projects.
        /// </summary>
        /// <param name="project">
        /// The Cloud Service project to update.
        /// </param>
        /// <param name="roleType">
        /// The type of role being added, either "Web" or "Worker".
        /// </param>
        /// <param name="projectName">
        /// The name of the role. This typically matches the Caption property.
        /// </param>
        /// <param name="site">
        /// VS service provider.
        /// </param>
        internal static void UpdateServiceDefinition(
            IVsHierarchy project,
            string roleType,
            string projectName,
            IServiceProvider site
        ) {
            Utilities.ArgumentNotNull("project", project);

            object obj;
            ErrorHandler.ThrowOnFailure(project.GetProperty(
                (uint)VSConstants.VSITEMID.Root,
                (int)__VSHPROPID.VSHPROPID_FirstChild,
                out obj
            ));

            uint id;
            while (TryGetItemId(obj, out id)) {
                Guid itemType;
                string mkDoc;

                if (ErrorHandler.Succeeded(project.GetGuidProperty(id, (int)__VSHPROPID.VSHPROPID_TypeGuid, out itemType)) &&
                    itemType == VSConstants.GUID_ItemType_PhysicalFile &&
                    ErrorHandler.Succeeded(project.GetProperty(id, (int)__VSHPROPID.VSHPROPID_Name, out obj)) &&
                    "ServiceDefinition.csdef".Equals(obj as string, StringComparison.InvariantCultureIgnoreCase) &&
                    ErrorHandler.Succeeded(project.GetCanonicalName(id, out mkDoc)) &&
                    !string.IsNullOrEmpty(mkDoc)
                ) {
                    // We have found the file
                    var rdt = site.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;

                    IVsHierarchy docHier;
                    uint docId, docCookie;
                    IntPtr pDocData;

                    bool updateFileOnDisk = true;

                    if (ErrorHandler.Succeeded(rdt.FindAndLockDocument(
                        (uint)_VSRDTFLAGS.RDT_EditLock,
                        mkDoc,
                        out docHier,
                        out docId,
                        out pDocData,
                        out docCookie
                    ))) {
                        try {
                            if (pDocData != IntPtr.Zero) {
                                try {
                                    // File is open, so edit it through the document
                                    UpdateServiceDefinition(
                                        Marshal.GetObjectForIUnknown(pDocData) as IVsTextLines,
                                        roleType,
                                        projectName
                                    );

                                    ErrorHandler.ThrowOnFailure(rdt.SaveDocuments(
                                        (uint)__VSRDTSAVEOPTIONS.RDTSAVEOPT_ForceSave,
                                        docHier,
                                        docId,
                                        docCookie
                                    ));

                                    updateFileOnDisk = false;
                                } catch (ArgumentException) {
                                } catch (InvalidOperationException) {
                                } finally {
                                    Marshal.Release(pDocData);
                                }
                            }
                        } finally {
                            ErrorHandler.ThrowOnFailure(rdt.UnlockDocument(
                                (uint)_VSRDTFLAGS.RDT_Unlock_SaveIfDirty | (uint)_VSRDTFLAGS.RDT_RequestUnlock,
                                docCookie
                            ));
                        }
                    }

                    if (updateFileOnDisk) {
                        // File is not open, so edit it on disk
                        FileStream stream = null;
                        try {
                            UpdateServiceDefinition(mkDoc, roleType, projectName);
                        } finally {
                            if (stream != null) {
                                stream.Close();
                            }
                        }
                    }

                    break;
                }

                if (ErrorHandler.Failed(project.GetProperty(id, (int)__VSHPROPID.VSHPROPID_NextSibling, out obj))) {
                    break;
                }
            }
        }

        private class StringWriterWithEncoding : StringWriter {
            private readonly Encoding _encoding;

            public StringWriterWithEncoding(Encoding encoding) {
                _encoding = encoding;
            }

            public override Encoding Encoding {
                get { return _encoding; }
            }
        }

        private static void UpdateServiceDefinition(IVsTextLines lines, string roleType, string projectName) {
            if (lines == null) {
                throw new ArgumentException("lines");
            }

            int lastLine, lastIndex;
            string text;

            ErrorHandler.ThrowOnFailure(lines.GetLastLineIndex(out lastLine, out lastIndex));
            ErrorHandler.ThrowOnFailure(lines.GetLineText(0, 0, lastLine, lastIndex, out text));

            var doc = new XmlDocument();
            doc.LoadXml(text);

            UpdateServiceDefinition(doc, roleType, projectName);

            var encoding = Encoding.UTF8;

            var userData = lines as IVsUserData;
            if (userData != null) {
                var guid = VSConstants.VsTextBufferUserDataGuid.VsBufferEncodingVSTFF_guid;
                object data;
                int cp;
                if (ErrorHandler.Succeeded(userData.GetData(ref guid, out data)) &&
                    (cp = (data as int? ?? (int)(data as uint? ?? 0)) & (int)__VSTFF.VSTFF_CPMASK) != 0) {
                    try {
                        encoding = Encoding.GetEncoding(cp);
                    } catch (NotSupportedException) {
                    } catch (ArgumentException) {
                    }
                }
            }

            var sw = new StringWriterWithEncoding(encoding);
            doc.Save(XmlWriter.Create(
                sw,
                new XmlWriterSettings {
                    Indent = true,
                    IndentChars = " ",
                    NewLineHandling = NewLineHandling.Entitize,
                    Encoding = encoding
                }
            ));

            var sb = sw.GetStringBuilder();
            var len = sb.Length;
            var pStr = Marshal.StringToCoTaskMemUni(sb.ToString());

            try {
                ErrorHandler.ThrowOnFailure(lines.ReplaceLines(0, 0, lastLine, lastIndex, pStr, len, new TextSpan[1]));
            } finally {
                Marshal.FreeCoTaskMem(pStr);
            }
        }

        private static void UpdateServiceDefinition(string path, string roleType, string projectName) {
            var doc = new XmlDocument();
            doc.Load(path);

            UpdateServiceDefinition(doc, roleType, projectName);

            doc.Save(XmlWriter.Create(
                path,
                new XmlWriterSettings {
                    Indent = true,
                    IndentChars = " ",
                    NewLineHandling = NewLineHandling.Entitize,
                    Encoding = Encoding.UTF8
                }
            ));
        }

        /// <summary>
        /// Modifies the provided XML document to contain the service definition
        /// nodes needed for the specified project.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// <paramref name="roleType"/> is not one of "Web" or "Worker".
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// A required element is missing from the document.
        /// </exception>
        internal static void UpdateServiceDefinition(XmlDocument doc, string roleType, string projectName) {
            bool isWeb = roleType == "Web";
            bool isWorker = roleType == "Worker";
            if (isWeb == isWorker) {
                throw new ArgumentException("Unknown role type: " + (roleType ?? "(null)"), "roleType");
            }

            var nav = doc.CreateNavigator();

            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("sd", "http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition");

            var role = nav.SelectSingleNode(string.Format(
                "/sd:ServiceDefinition/sd:{0}Role[@name='{1}']", roleType, projectName
            ), ns);

            if (role == null) {
                throw new InvalidOperationException("Missing role entry");
            }

            var startup = role.SelectSingleNode("sd:Startup", ns);
            if (startup != null) {
                startup.DeleteSelf();
            }

            role.AppendChildElement(null, "Startup", null, null);
            startup = role.SelectSingleNode("sd:Startup", ns);
            if (startup == null) {
                throw new InvalidOperationException("Missing Startup entry");
            }

            startup.AppendChildElement(null, "Task", null, null);
            var task = startup.SelectSingleNode("sd:Task", ns);
            AddEnvironmentNode(task, ns);
            task.CreateAttribute(null, "executionContext", null, "elevated");
            task.CreateAttribute(null, "taskType", null, "simple");

            if (isWeb) {
                task.CreateAttribute(null, "commandLine", null, "ps.cmd ConfigureCloudService.ps1");
            } else if (isWorker) {
                task.CreateAttribute(null, "commandLine", null, "bin\\ps.cmd ConfigureCloudService.ps1");

                var runtime = role.SelectSingleNode("sd:Runtime", ns);
                if (runtime != null) {
                    runtime.DeleteSelf();
                }
                role.AppendChildElement(null, "Runtime", null, null);
                runtime = role.SelectSingleNode("sd:Runtime", ns);
                AddEnvironmentNode(runtime, ns);
                runtime.AppendChildElement(null, "EntryPoint", null, null);
                var ep = runtime.SelectSingleNode("sd:EntryPoint", ns);
                ep.AppendChildElement(null, "ProgramEntryPoint", null, null);
                var pep = ep.SelectSingleNode("sd:ProgramEntryPoint", ns);
                pep.CreateAttribute(null, "commandLine", null, "bin\\ps.cmd LaunchWorker.ps1");
                pep.CreateAttribute(null, "setReadyOnProcessStart", null, "true");
            }
        }

        private static void AddEnvironmentNode(XPathNavigator nav, IXmlNamespaceResolver ns) {
            nav.AppendChildElement(null, "Environment", null, null);
            nav = nav.SelectSingleNode("sd:Environment", ns);
            nav.AppendChildElement(null, "Variable", null, null);
            nav = nav.SelectSingleNode("sd:Variable", ns);
            nav.CreateAttribute(null, "name", null, "EMULATED");
            nav.AppendChildElement(null, "RoleInstanceValue", null, null);
            nav = nav.SelectSingleNode("sd:RoleInstanceValue", ns);
            nav.CreateAttribute(null, "xpath", null, "/RoleEnvironment/Deployment/@emulated");
        }

        string IProjectLaunchProperties.GetArguments() {
            return GetProjectProperty(CommonConstants.CommandLineArguments);
        }

        string IProjectLaunchProperties.GetWorkingDirectory() {
            return GetWorkingDirectory();
        }

        IDictionary<string, string> IProjectLaunchProperties.GetEnvironment(bool includeSearchPaths) {
            var res = PythonProjectLaunchProperties.ParseEnvironment(GetProjectProperty(PythonConstants.EnvironmentSetting));

            if (includeSearchPaths) {
                PythonProjectLaunchProperties.AddSearchPaths(res, this, Site);
            }

            return res;
        }

        string IPythonProjectLaunchProperties.GetInterpreterPath() {
            var str = GetProjectProperty(PythonConstants.InterpreterPathSetting);
            if (!string.IsNullOrEmpty(str)) {
                str = CommonUtils.GetAbsoluteFilePath(ProjectHome, str);
                if (!File.Exists(str)) {
                    throw new MissingInterpreterException(SR.GetString(SR.DebugLaunchInterpreterMissing_Path, str));
                }
                return str;
            }

            var factory = GetInterpreterFactoryOrThrow();
            if (((IPythonProjectLaunchProperties)this).GetIsWindowsApplication() ?? false) {
                return factory.Configuration.WindowsInterpreterPath;
            }
            return factory.Configuration.InterpreterPath;
        }

        string IPythonProjectLaunchProperties.GetInterpreterArguments() {
            return GetProjectProperty(PythonConstants.InterpreterArgumentsSetting);
        }

        bool? IPythonProjectLaunchProperties.GetIsWindowsApplication() {
            var str = GetProjectProperty(PythonConstants.IsWindowsApplicationSetting);
            bool isWindowsApp;
            return bool.TryParse(str, out isWindowsApp) ? (bool?)isWindowsApp : null;
        }

        bool? IPythonProjectLaunchProperties.GetIsNativeDebuggingEnabled() {
            var str = GetProjectProperty(PythonConstants.EnableNativeCodeDebugging);
            bool isNativeDebug;
            return bool.TryParse(str, out isNativeDebug) ? (bool?)isNativeDebug : null;
        }
    }
}
