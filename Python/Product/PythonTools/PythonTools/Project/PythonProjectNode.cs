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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
using Microsoft.Build.Execution;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Navigation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Azure;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using IServiceProvider = System.IServiceProvider;
using MessageBox = System.Windows.Forms.MessageBox;
using MSBuild = Microsoft.Build.Evaluation;
using NativeMethods = Microsoft.VisualStudioTools.Project.NativeMethods;
using Task = System.Threading.Tasks.Task;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using VsMenus = Microsoft.VisualStudioTools.Project.VsMenus;

namespace Microsoft.PythonTools.Project {
    [Guid(PythonConstants.ProjectNodeGuid)]
    internal class PythonProjectNode :
        CommonProjectNode,
        IPythonProject3,
        IAzureRoleProject,
        IProjectInterpreterDbChanged,
        IPythonProjectLaunchProperties {
        // For files that are analyzed because they were directly or indirectly referenced in the search path, store the information
        // about the directory from the search path that referenced them in IProjectEntry.Properties[_searchPathEntryKey], so that
        // they can be located and removed when that directory is removed from the path.
        private static readonly object _searchPathEntryKey = new { Name = "SearchPathEntry" };

        private object _designerContext;
        private VsProjectAnalyzer _analyzer;
        private readonly HashSet<AnalysisEntry> _warnOnLaunchFiles = new HashSet<AnalysisEntry>();
        private PythonDebugPropertyPage _debugPropPage;
        private CommonSearchPathContainerNode _searchPathContainer;
        private InterpretersContainerNode _interpretersContainer;
        private readonly HashSet<string> _validFactories = new HashSet<string>();
        public IPythonInterpreterFactory _active;

        internal List<CustomCommand> _customCommands;
        private string _customCommandsDisplayLabel;
        private Dictionary<object, Action<object>> _actionsOnClose;

        public PythonProjectNode(IServiceProvider serviceProvider) : base(serviceProvider, null) {
            Type projectNodePropsType = typeof(PythonProjectNodeProperties);
            AddCATIDMapping(projectNodePropsType, projectNodePropsType.GUID);
            ActiveInterpreterChanged += OnActiveInterpreterChanged;
            InterpreterFactoriesChanged += OnInterpreterFactoriesChanged;
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

            // Remove old custom commands
            if (_customCommands != null) {
                foreach (var c in _customCommands) {
                    c.Dispose();
                }
            }
            _customCommands = null;

            var contextProvider = Site.GetComponentModel().GetService<VsProjectContextProvider>();
            contextProvider.UpdateProject(this, project);

            // Project has been cleared, so nothing else to do here
            if (project == null) {
                return;
            }

            // collect the valid interpreter factories for this project...
            _validFactories.Clear();
            foreach (var item in project.GetItems(MSBuildConstants.InterpreterItem)) {
                var id = item.GetMetadataValue(MSBuildConstants.IdKey);
                if (!String.IsNullOrWhiteSpace(id)) {
                    _validFactories.Add(MSBuildProjectInterpreterFactoryProvider.GetInterpreterId(BuildProject.FullPath, id));
                }
            }

            foreach (var item in project.GetItems(MSBuildConstants.InterpreterReferenceItem)) {
                var id = item.EvaluatedInclude;
                if (!String.IsNullOrWhiteSpace(id)) {
                    _validFactories.Add(id);
                }
            }
            
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

        private void OnInterpreterFactoriesChanged(object sender, EventArgs e) {
            Site.GetUIThread().Invoke(() => RefreshInterpreters());
        }

        public IPythonInterpreterFactory ActiveInterpreter {
            get {
                return _active ?? Site.GetPythonToolsService().DefaultInterpreter;
            }
            internal set {
                Debug.Assert(this.FileName != null);
                var oldActive = _active;

                lock (_validFactories) {
                    if (_validFactories.Count == 0) {
                        // No factories, so we must use the global default.
                        _active = null;
                    } else if (value == null || !_validFactories.Contains(value.Configuration.Id)) {
                        // Choose a factory and make it our default.
                        // TODO: We should have better ordering than this...
                        var compModel = Site.GetComponentModel();

                        _active = compModel.DefaultExportProvider.GetInterpreterFactory(
                                _validFactories.ToList().OrderBy(f => f).LastOrDefault()
                        );
                    } else {
                        _active = value;
                    }
                }

                if (_active != oldActive) {
                    if (oldActive == null) {
                        // No longer need to listen to this event
                        var defaultInterp = Site.GetPythonToolsService().DefaultInterpreter as PythonInterpreterFactoryWithDatabase;
                        if (defaultInterp != null) {
                            defaultInterp.NewDatabaseAvailable -= OnNewDatabaseAvailable;
                        }

                        Site.GetPythonToolsService().DefaultInterpreterChanged -= GlobalDefaultInterpreterChanged;
                    } else {
                        var oldInterpWithDb = oldActive as PythonInterpreterFactoryWithDatabase;
                        if (oldInterpWithDb != null) {
                            oldInterpWithDb.NewDatabaseAvailable -= OnNewDatabaseAvailable;
                        }
                    }

                    if (_active != null) {
                        var newInterpWithDb = _active as PythonInterpreterFactoryWithDatabase;
                        if (newInterpWithDb != null) {
                            newInterpWithDb.NewDatabaseAvailable += OnNewDatabaseAvailable;
                        }
                        BuildProject.SetProperty(
                            MSBuildConstants.InterpreterIdProperty,
                            ReplaceMSBuildPath(_active.Configuration.Id)
                        );
                    } else {
                        BuildProject.SetProperty(MSBuildConstants.InterpreterIdProperty, "");
                        // Need to start listening to this event

                        Site.GetPythonToolsService().DefaultInterpreterChanged += GlobalDefaultInterpreterChanged;

                        var defaultInterp = Site.GetPythonToolsService().DefaultInterpreter as PythonInterpreterFactoryWithDatabase;
                        if (defaultInterp != null) {
                            defaultInterp.NewDatabaseAvailable += OnNewDatabaseAvailable;
                        }
                    }
                    BuildProject.MarkDirty();

                    ActiveInterpreterChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private string ReplaceMSBuildPath(string id) {
            int index = id.IndexOf(BuildProject.FullPath, StringComparison.OrdinalIgnoreCase);
            if (index != -1) {
                id = id.Substring(0, index) + "$(MSBuildProjectFullPath)" + id.Substring(index + BuildProject.FullPath.Length);
            }
            return id;
        }

        private void OnNewDatabaseAvailable(object sender, EventArgs e) {
            InterpreterDbChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GlobalDefaultInterpreterChanged(object sender, EventArgs e) {
            // This event is only raised when our active interpreter is the
            // global default.
            var evt = ActiveInterpreterChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        public event EventHandler ActiveInterpreterChanged;

        internal event EventHandler InterpreterFactoriesChanged;

        /// <summary>
        /// Adds the specified factory to the project. If the factory was
        /// created by this provider, it will be added as an Interpreter element
        /// with full details. If the factory was not created by this provider,
        /// it will be added as an InterpreterReference element with only the
        /// ID and version.
        /// </summary>
        /// <param name="factory">The factory to add.</param>
        // TODO: Can this be entirely configuration based?
        public void AddInterpreter(IPythonInterpreterFactory factory, bool disposeInterpreter = false) {
            if (factory == null) {
                throw new ArgumentNullException("factory");
            }

            if (_validFactories.Contains(factory.Configuration.Id)) {
                return;
            }

            MSBuild.ProjectItem item;
            var compModel = Site.GetComponentModel();
            var derived = factory as DerivedInterpreterFactory;
            if (derived != null) {
                var projectHome = PathUtils.GetAbsoluteDirectoryPath(BuildProject.DirectoryPath, BuildProject.GetPropertyValue("ProjectHome"));
                var rootPath = PathUtils.EnsureEndSeparator(factory.Configuration.PrefixPath);

                item = BuildProject.AddItem(MSBuildConstants.InterpreterItem,
                    PathUtils.GetRelativeDirectoryPath(projectHome, rootPath),
                    new Dictionary<string, string> {
                        { MSBuildConstants.IdKey, MSBuildProjectInterpreterFactoryProvider.GetProjectiveRelativeId(derived.Configuration.Id) },
                        { MSBuildConstants.BaseInterpreterKey, derived.BaseInterpreter.Configuration.Id  },
                        { MSBuildConstants.VersionKey, derived.BaseInterpreter.Configuration.Version.ToString() },
                        { MSBuildConstants.DescriptionKey, derived.Configuration.Description },
                        { MSBuildConstants.InterpreterPathKey, PathUtils.GetRelativeFilePath(rootPath, derived.Configuration.InterpreterPath) },
                        { MSBuildConstants.WindowsPathKey, PathUtils.GetRelativeFilePath(rootPath, derived.Configuration.WindowsInterpreterPath) },
                        { MSBuildConstants.LibraryPathKey, PathUtils.GetRelativeDirectoryPath(rootPath, derived.Configuration.LibraryPath) },
                        { MSBuildConstants.PathEnvVarKey, derived.Configuration.PathEnvironmentVariable },
                        { MSBuildConstants.ArchitectureKey, derived.Configuration.Architecture.ToString() }
                    }).FirstOrDefault();
            } else if (compModel.DefaultExportProvider.GetInterpreterFactory(factory.Configuration.Id) != null) {
                // The interpreter exists globally, so add a reference.
                item = BuildProject.AddItem(MSBuildConstants.InterpreterReferenceItem,
                    string.Format("{0:B}\\{1}", factory.Configuration.Id, factory.Configuration.Version)
                    ).FirstOrDefault();
            } else {
                // Can't find the interpreter anywhere else, so add its
                // configuration to the project file.
                var projectHome = PathUtils.GetAbsoluteDirectoryPath(BuildProject.DirectoryPath, BuildProject.GetPropertyValue("ProjectHome"));
                var rootPath = PathUtils.EnsureEndSeparator(factory.Configuration.PrefixPath);

                item = BuildProject.AddItem(MSBuildConstants.InterpreterItem,
                    PathUtils.GetRelativeDirectoryPath(projectHome, rootPath),
                    new Dictionary<string, string> {
                        { MSBuildConstants.IdKey, MSBuildProjectInterpreterFactoryProvider.GetProjectiveRelativeId(factory.Configuration.Id) },
                        { MSBuildConstants.VersionKey, factory.Configuration.Version.ToString() },
                        { MSBuildConstants.DescriptionKey, factory.Configuration.Description },
                        { MSBuildConstants.InterpreterPathKey, PathUtils.GetRelativeFilePath(rootPath, factory.Configuration.InterpreterPath) },
                        { MSBuildConstants.WindowsPathKey, PathUtils.GetRelativeFilePath(rootPath, factory.Configuration.WindowsInterpreterPath) },
                        { MSBuildConstants.LibraryPathKey, PathUtils.GetRelativeDirectoryPath(rootPath, factory.Configuration.LibraryPath) },
                        { MSBuildConstants.PathEnvVarKey, factory.Configuration.PathEnvironmentVariable },
                        { MSBuildConstants.ArchitectureKey, factory.Configuration.Architecture.ToString() }
                    }).FirstOrDefault();
            }

            lock (_validFactories) {
                _validFactories.Add(factory.Configuration.Id);
            }

            Site.GetComponentModel().GetService<VsProjectContextProvider>().OnProjectChanged(
                BuildProject
            );
            UpdateActiveInterpreter();
            InterpreterFactoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Removes an interpreter factory from the project. This function will
        /// modify the project, but does not handle source control.
        /// </summary>
        /// <param name="factory">
        /// The id of the factory to remove. The function returns silently if
        /// the factory is not known by this provider.
        /// </param>
        public void RemoveInterpreterFactory(IPythonInterpreterFactory factory) {
            if (factory == null) {
                throw new ArgumentNullException("factory");
            }

            if (!_validFactories.Contains(factory.Configuration.Id)) {
                return;
            }

            foreach (var item in BuildProject.GetItems(MSBuildConstants.InterpreterItem)) {
                if (item.GetMetadataValue(MSBuildConstants.IdKey) == factory.Configuration.Id) {
                    BuildProject.RemoveItem(item);
                    BuildProject.MarkDirty();
                    break;
                }
            }

            foreach (var item in BuildProject.GetItems(MSBuildConstants.InterpreterReferenceItem)) {
                var id = item.EvaluatedInclude;
                if (id == factory.Configuration.Id) {
                    BuildProject.RemoveItem(item);
                    BuildProject.MarkDirty();
                    break;
                }
            }

            _validFactories.Remove(factory.Configuration.Id);
            UpdateActiveInterpreter();
            InterpreterFactoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateActiveInterpreter() {
            var newActive = _active;
            lock (_validFactories) {
                if (newActive == null ||
                    _validFactories.Count == 0 ||
                    !_validFactories.Contains(newActive.Configuration.Id)) {
                    newActive = Site.GetComponentModel().DefaultExportProvider.GetInterpreterFactory(
                        BuildProject.GetPropertyValue(MSBuildConstants.InterpreterIdProperty)
                    );
                }
            }
            ActiveInterpreter = newActive;
        }

        internal bool IsActiveInterpreterGlobalDefault {
            get {
                return _active == null;
            }
        }
        internal IEnumerable<InterpreterConfiguration> InterpreterConfigurations {
            get {
                var compModel = Site.GetComponentModel();
                var configs = compModel.DefaultExportProvider.GetConfigurations();
                if (_validFactories.Count == 0) {
                    // all non-project specific configs are valid...
                    var vsProjContext = compModel.GetService<VsProjectContextProvider>();
                    foreach (var config in configs) {
                        if (!vsProjContext.IsProjectSpecific(config.Value)) {
                            yield return config.Value;
                        }
                    }
                } else {
                    // we have a list of registered factories, only include those...
                    foreach (var config in _validFactories) {
                        var interp = compModel.DefaultExportProvider.GetInterpreterFactory(config);
                        InterpreterConfiguration value;
                        if (configs.TryGetValue(config, out value)) {
                            yield return value;
                        }
                    }
                }
            }
        }

        internal IEnumerable<IPythonInterpreterFactory> InterpreterFactories {
            get {
                var compModel = Site.GetComponentModel();
                if (_validFactories.Count == 0) {
                    // all non-project specific configs are valid...
                    var vsProjContext = compModel.GetService<VsProjectContextProvider>();
                    var configs = compModel.DefaultExportProvider.GetConfigurations();
                    foreach (var config in configs) {
                        if (!vsProjContext.IsProjectSpecific(config.Value)) {
                            var res = compModel.DefaultExportProvider.GetInterpreterFactory(config.Key);
                            if (res == null) {
                                continue;
                            }
                            yield return res;
                        }
                    }
                } else {
                    // we have a list of registered factories, only include those...
                    foreach (var config in _validFactories) {
                        var interp = compModel.DefaultExportProvider.GetInterpreterFactory(config);
                        if (interp != null) {
                            yield return interp;
                        }
                    }
                }
            }
        }

        protected override Stream ProjectIconsImageStripStream {
            get {
                throw new NotSupportedException("Python Tools does not support project image strip");
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

        protected override bool SupportsIconMonikers {
            get { return true; }
        }

        protected override ImageMoniker GetIconMoniker(bool open) {
            return KnownMonikers.PYProjectNode;
        }

        internal override string IssueTrackerUrl {
            get { return PythonConstants.IssueTrackerUrl; }
        }

        private static string GetSearchPathEntry(AnalysisEntry entry) {
            object result;
            entry.Properties.TryGetValue(_searchPathEntryKey, out result);
            return (string)result;
        }

        private static void SetSearchPathEntry(AnalysisEntry entry, string value) {
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
                    AddSearchPathEntry(PathUtils.EnsureEndSeparator(dirToAdd));
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
            if (MSBuildConstants.InterpreterReferenceItem.Equals(itemType, StringComparison.Ordinal) ||
                MSBuildConstants.InterpreterItem.Equals(itemType, StringComparison.Ordinal)) {
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

            UpdateActiveInterpreter();

            base.Reload();

            string id;
            if (IsActiveInterpreterGlobalDefault &&
                !string.IsNullOrEmpty(id = GetProjectProperty(MSBuildConstants.InterpreterIdProperty))) {
                // The interpreter in the project file has no reference, so
                // find and add it.
                var interpFact = Site.GetComponentModel().DefaultExportProvider.GetInterpreterFactory(
                    id
                );
                if (interpFact != null && QueryEditProjectFile(false)) {
                    AddInterpreter(interpFact);
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
                bool needCWD = !PathUtils.IsSameDirectory(projHome, workDir);
                var cwdNode = FindImmediateChild<CurrentWorkingDirectoryNode>(_searchPathContainer);
                if (needCWD) {
                    if (cwdNode == null) {
                        //No cwd node yet
                        _searchPathContainer.AddChild(new CurrentWorkingDirectoryNode(this, workDir));
                    } else if (!PathUtils.IsSameDirectory(cwdNode.Url, workDir)) {
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
            var vsProjectContext = Site.GetComponentModel().GetService<VsProjectContextProvider>();

            if (!IsActiveInterpreterGlobalDefault) {
                foreach (var fact in InterpreterFactories) {
                    if (!RemoveFirst(remaining, n => !n._isGlobalDefault && n._factory == fact)) {
                        bool isProjectSpecific = vsProjectContext.IsProjectSpecific(fact.Configuration);
                        node.AddChild(new InterpretersNode(
                            this,
                            fact,
                            isInterpreterReference: !isProjectSpecific,
                            canDelete:
                                isProjectSpecific &&
                                Directory.Exists(fact.Configuration.PrefixPath)
                        ));
                    }
                }
            } else {
                var fact = ActiveInterpreter;
                if (fact.IsRunnable() && !RemoveFirst(remaining, n => n._isGlobalDefault && n._factory == fact)) {
                    node.AddChild(new InterpretersNode(this, fact, true, false, true));
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
                    BoldItem(child, child._factory == ActiveInterpreter);
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
                if (PathUtils.IsSameDirectory(nodes[j].Url, path)) {
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

                    var absPath = PathUtils.GetAbsoluteFilePath(ProjectHome, path);
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
                var relPath = PathUtils.GetRelativeFilePath(ProjectHome, path);
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
            var absPath = PathUtils.GetAbsoluteFilePath(ProjectHome, newpath);
            // Ignore the end separator when determining whether the path has
            // already been added. Having both "C:\Fob" and "C:\Fob\" is not
            // legal.
            if (searchPath.Contains(PathUtils.EnsureEndSeparator(absPath), StringComparer.OrdinalIgnoreCase) ||
                searchPath.Contains(PathUtils.TrimEndSeparator(absPath), StringComparer.OrdinalIgnoreCase)) {
                return;
            }
            searchPath.Add(absPath);
            SaveSearchPath(searchPath);
        }

        /// <summary>
        /// Removes a given path from the SearchPath property.
        /// </summary>
        internal void RemoveSearchPathEntry(string path) {
            var absPath = PathUtils.TrimEndSeparator(PathUtils.GetAbsoluteFilePath(ProjectHome, path));
            var absPathWithEndSeparator = PathUtils.EnsureEndSeparator(absPath);
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
                Strings.SelectFolderForSearchPath
            );

            if (dirName != null) {
                AddSearchPathEntry(PathUtils.EnsureEndSeparator(dirName));
            }

            return VSConstants.S_OK;
        }


        private async void PythonProjectNode_OnProjectPropertyChanged(object sender, ProjectPropertyChangedArgs e) {
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
                        var projectEntry = fileProject.Value;
                        string searchPathEntry = fileProject.Value.SearchPathEntry;
                        if (projectEntry != null && 
                            searchPathEntry != null && 
                            !newDirs.Contains(searchPathEntry)) {
                            _analyzer.UnloadFileAsync(projectEntry);
                        }
                    }

                    // find the values only in the old list, and let the analyzer know it shouldn't be watching those dirs
                    oldDirs.ExceptWith(newDirs);
                    foreach (var dir in oldDirs) {
                        await _analyzer.StopAnalyzingDirectoryAsync(dir);
                    }

                    AnalyzeSearchPaths(newDirs);
                    break;
            }

            var debugProp = DebugPropertyPage;
            if (debugProp != null) {
                ((PythonDebugPropertyPageControl)debugProp.Control).ReloadSetting(e.PropertyName);
            }
        }

        private async void AnalyzeSearchPaths(IEnumerable<string> newDirs) {
            // now add all of the missing files, any dups will automatically not be re-analyzed
            foreach (var dir in newDirs) {
                if (File.Exists(dir)) {
                    // If it's a file and not a directory, parse it as a .zip
                    // file in accordance with PEP 273.
                    await _analyzer.AnalyzeZipArchiveAsync(dir);
                } else if (Directory.Exists(dir)) {
                    await _analyzer.AnalyzeDirectoryAsync(dir);
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

        /*
        public PythonAnalyzer GetProjectAnalyzer() {
            return GetAnalyzer().Project;
        }
        */
        VsProjectAnalyzer IPythonProject.GetProjectAnalyzer() {
            return GetAnalyzer();
        }

        public event EventHandler ProjectAnalyzerChanged;
        public event EventHandler<AnalyzerChangingEventArgs> ProjectAnalyzerChanging;
        public event EventHandler InterpreterDbChanged;

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

                if (_analyzer != null) {
                    UnHookErrorsAndWarnings(_analyzer);
                    _analyzer.ClearAllTasks();

                    if (_analyzer.RemoveUser()) {
                        _analyzer.Dispose();
                    }
                    _analyzer = null;
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
            if (factory != ActiveInterpreter) {
                //Make sure we can edit the project file
                if (!ProjectMgr.QueryEditProjectFile(false)) {
                    return VSConstants.OLE_E_PROMPTSAVECANCELLED;
                }

                ActiveInterpreter = factory;
            }
            return VSConstants.S_OK;
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
            var interpreterService = model.GetService<IInterpreterRegistryService>();
            var factory = GetInterpreterFactory();
            var res = new VsProjectAnalyzer(
                Site,
                factory.CreateInterpreter(),
                factory,
                false,
                BuildProject.FullPath
            );
            res.AbnormalAnalysisExit += AnalysisProcessExited;

            HookErrorsAndWarnings(res);
            return res;
        }

        private void AnalysisProcessExited(object sender, AbnormalAnalysisExitEventArgs e) {
            StringBuilder msg = new StringBuilder();
            msg.AppendFormat("Exit Code: {0}", e.ExitCode);
            msg.AppendLine();
            msg.AppendLine(" ------ STD ERR ------ ");
            msg.AppendLine(e.StdErr);
            msg.AppendLine(" ------ END STD ERR ------ ");
            Site.GetPythonToolsService().Logger.LogEvent(
                PythonLogEvent.AnalysisExitedAbnormally,
                msg.ToString()
            );
            EventLog.WriteEntry(Strings.ProductTitle, msg.ToString(), EventLogEntryType.Error, 9998);
            Site.GetUIThread().InvokeAsync(ReanalyzeProject).DoNotWait();
        }

        private void HookErrorsAndWarnings(VsProjectAnalyzer res) {
            res.ShouldWarnOnLaunchChanged += OnShouldWarnOnLaunchChanged;
        }

        private void UnHookErrorsAndWarnings(VsProjectAnalyzer res) {
            res.ShouldWarnOnLaunchChanged -= OnShouldWarnOnLaunchChanged;
            _warnOnLaunchFiles.Clear();
        }

        private void OnShouldWarnOnLaunchChanged(object sender, EntryEventArgs e) {
            if (_diskNodes.ContainsKey(e.Entry.Path ?? "")) {
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
            var fact = ActiveInterpreter;
            if (fact == null) {
                // May occur if we are racing with Dispose(), so the factory we
                // return isn't important, but it has to be non-null to fulfil
                // the contract.
                var service = Site.GetComponentModel().GetService<IInterpreterOptionsService>();
                return service.DefaultInterpreter;
            }


            Site.GetPythonToolsService().EnsureCompletionDb(fact);

            return fact;
        }

        public IPythonInterpreterFactory GetInterpreterFactoryOrThrow() {
            var fact = ActiveInterpreter;
            if (fact == null) {
                throw new NoInterpretersException();
            }

            if (!fact.Configuration.IsAvailable()) {
                throw new MissingInterpreterException(
                    Strings.MissingEnvironment.FormatUI(fact.Configuration.Description, fact.Configuration.Version)
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
        private void OnActiveInterpreterChanged(object sender, EventArgs e) {
            if (IsClosed) {
                return;
            }

            InterpreterDbChanged?.Invoke(this, EventArgs.Empty);
            Site.GetUIThread().InvokeAsync(ReanalyzeProject).DoNotWait();
        }

        private void ReanalyzeProject() {
            if (IsClosing || IsClosed) {
                // This deferred event is no longer important.
                return;
            }

            var statusBar = Site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            if (statusBar != null) {
                statusBar.SetText(Strings.AnalyzingProject);
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
                        _analyzer,
                        analyzer
                    ));
                }

                Reanalyze(analyzer);
                var oldAnalyzer = Interlocked.Exchange(ref _analyzer, analyzer);

                if (oldAnalyzer != null) {
                    if (analyzer != null) {
                        analyzer.SwitchAnalyzers(oldAnalyzer);
                    }
                    if (oldAnalyzer.RemoveUser()) {
                        oldAnalyzer.Dispose();
                    }
                }

                var searchPath = ParseSearchPath();
                if (searchPath != null && analyzer != null) {
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

        private async void Reanalyze(VsProjectAnalyzer newAnalyzer) {
            foreach (var child in AllVisibleDescendants.OfType<FileNode>()) {
                await newAnalyzer.AnalyzeFileAsync(child.Url);
            }

            var references = GetReferenceContainer();
            var interp = newAnalyzer;
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
            var interp = GetAnalyzer();
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
                return Strings.AddReferenceExtensions;
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
                var env = new Dictionary<string, string>(props.GetEnvironment(true), StringComparer.OrdinalIgnoreCase);

                var paths = new List<string>();
                var prefix = config.PrefixPath;
                if (factory == null) {
                    paths.Add(PathUtils.GetParent(props.GetInterpreterPath()));
                }
                paths.Add(PathUtils.GetParent(config.InterpreterPath));
                if (!string.IsNullOrEmpty(config.PrefixPath)) {
                    paths.Add(PathUtils.GetAbsoluteDirectoryPath(config.PrefixPath, "Scripts"));
                }
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
                    case PythonConstants.InstallRequirementsTxt:
                    case PythonConstants.GenerateRequirementsTxt:
                        var factory = GetInterpreterFactory();
                        if (factory.IsRunnable()) {
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
                        if (File.Exists(PathUtils.GetAbsoluteFilePath(ProjectHome, "requirements.txt"))) {
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
                MessageBox.Show(ex.Message, Strings.ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            } catch (NoInterpretersException ex) {
                PythonToolsPackage.OpenNoInterpretersHelpPage(Site, ex.HelpPage);
            } catch (Exception ex) {
                MessageBox.Show(
                    Strings.ErrorRunningCustomCommand.FormatUI(
                        command.DisplayLabelWithoutAccessKeys,
                        ex.Message
                    ),
                    Strings.ProductTitle
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
                var service = Site.GetComponentModel().GetService<IInterpreterRegistryService>();
                InterpreterConfiguration config;

                config = InterpreterConfigurations.FirstOrDefault(
                    // Description is a localized string, hence CCIC
                    c => description.Equals(c.Description, StringComparison.CurrentCultureIgnoreCase)
                );
                if (config != null) {
                    factory = service.FindInterpreter(config.Id);
                }

                if (factory == null) {
                    config = service.Configurations.FirstOrDefault(
                        c => description.Equals(c.Description, StringComparison.CurrentCultureIgnoreCase)
                    );
                    if (config != null) {
                        factory = service.FindInterpreter(config.Id);
                    }
                }
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
                ExecuteInReplCommand.EnsureReplWindow(Site, selectedInterpreterFactory.Configuration, this).Show(true);
            } catch (InvalidOperationException ex) {
                MessageBox.Show(Strings.ErrorOpeningInteractiveWindow.FormatUI(ex), Strings.ProductTitle);
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
            if (factory.Configuration.IsAvailable() &&
                Directory.Exists(factory.Configuration.PrefixPath) &&
                PathUtils.IsSubpathOf(ProjectHome, factory.Configuration.PrefixPath)
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
                                .Select(f => new PublishFile(f, PathUtils.GetRelativeFilePath(ProjectHome, f)))
                            ).ToArray(),
                            publishOptions.DestinationUrl
                        ),
                        Site,
                        Strings.FailedToCollectFilesForPublish,
                        Strings.FailedToCollectFilesForPublishMessage,
                        Strings.ErrorDetail,
                        Strings.Retry,
                        Strings.Cancel
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
                    .HandleAllExceptions(Site)
                    .DoNotWait();
            } else {
                // Prompt the user
                InstallNewPackageAsync(
                    selectedInterpreterFactory,
                    Site,
                    selectedInterpreter
                )
                    .SilenceException<OperationCanceledException>()
                    .HandleAllExceptions(Site)
                    .DoNotWait();
            }
            return VSConstants.S_OK;
        }

        private int ExecInstallRequirementsTxt(Dictionary<string, string> args, IList<HierarchyNode> selectedNodes) {
            InterpretersNode selectedInterpreter;
            IPythonInterpreterFactory selectedInterpreterFactory;
            GetSelectedInterpreterOrDefault(selectedNodes, args, out selectedInterpreter, out selectedInterpreterFactory);
            var txt = PathUtils.GetAbsoluteFilePath(ProjectHome, "requirements.txt");
            var elevated = Site.GetPythonToolsService().GeneralOptions.ElevatePip;
            var name = "-r " + ProcessOutput.QuoteSingleArgument(txt);
            if (args != null && !args.ContainsKey("y")) {
                if (!ShouldInstallRequirementsTxt(
                    selectedInterpreterFactory.Configuration.Description,
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
                .HandleAllExceptions(Site)
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
                .HandleAllExceptions(provider, typeof(PythonProjectNode))
                .DoNotWait();
        }

        internal static async Task InstallNewPackageAsync(
            IPythonInterpreterFactory factory,
            IServiceProvider provider,
            InterpretersNode node = null
        ) {
            var service = provider.GetComponentModel().GetService<IInterpreterRegistryService>();
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

            var service = provider.GetComponentModel().GetService<IInterpreterRegistryService>();
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
                statusBar.SetText(Strings.PackageInstallingSeeOutputWindow.FormatUI(name));

                Task<bool> task;
                if (action != null) {
                    task = action(name, elevate, redirector);
                } else {
                    task = Pip.Install(provider, factory, name, elevate, redirector);
                }

                bool success = await task;
                statusBar.SetText((success ? Strings.PackageInstallSucceeded : Strings.PackageInstallFailed).FormatUI(
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
                statusBar.SetText(Strings.PackageInstallFailed.FormatUI(name));
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

            var service = provider.GetComponentModel().GetService<IInterpreterRegistryService>();
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
                statusBar.SetText(Strings.PackageUninstallingSeeOutputWindow.FormatUI(name));

                bool elevate = provider.GetPythonToolsService().GeneralOptions.ElevatePip;

                bool success = await Pip.Uninstall(provider, factory, name, elevate, redirector);
                statusBar.SetText((success ? Strings.PackageUninstallSucceeded : Strings.PackageUninstallFailed).FormatUI(
                    name
                ));
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                statusBar.SetText(Strings.PackageUninstallFailed.FormatUI(name));
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
                Title = Strings.ProductTitle,
                MainInstruction = Strings.ShouldInstallRequirementsTxtHeader,
                Content = Strings.ShouldInstallRequirementsTxtContent,
                ExpandedByDefault = true,
                ExpandedControlText = Strings.ShouldInstallRequirementsTxtExpandedControl,
                CollapsedControlText = Strings.ShouldInstallRequirementsTxtCollapsedControl,
                ExpandedInformation = content,
                AllowCancellation = true
            };

            var install = new TaskDialogButton(Strings.ShouldInstallRequirementsTxtInstallInto.FormatUI(targetLabel)) {
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
                    .HandleAllExceptions(Site, GetType())
                    .DoNotWait();
            }
            return VSConstants.S_OK;
        }

        private async Task GenerateRequirementsTxtAsync(IPythonInterpreterFactory factory) {
            var projectHome = ProjectHome;
            var txt = PathUtils.GetAbsoluteFilePath(projectHome, "requirements.txt");

            HashSet<string> items = null;

            try {
                items = await Pip.Freeze(factory);
            } catch (FileNotFoundException ex) {
                // Other exceptions should not occur, so let them propagate
                var dlg = TaskDialog.ForException(
                    Site,
                    ex,
                    Strings.MissingEnvironment.FormatUI(factory.Configuration.Description, factory.Configuration.Version),
                    IssueTrackerUrl
                );
                dlg.Title = Strings.ProductTitle;
                dlg.ShowModal();
                return;
            }

            string[] existing = null;
            bool addNew = false;
            if (File.Exists(txt)) {
                existing = TaskDialog.CallWithRetry(
                    _ => File.ReadAllLines(txt),
                    Site,
                    Strings.ProductTitle,
                    Strings.RequirementsTxtFailedToRead,
                    Strings.ErrorDetail,
                    Strings.Retry,
                    Strings.Cancel
                );

                var td = new TaskDialog(Site) {
                    Title = Strings.ProductTitle,
                    MainInstruction = Strings.RequirementsTxtExists,
                    Content = Strings.RequirementsTxtExistsQuestion,
                    AllowCancellation = true,
                    CollapsedControlText = Strings.RequirementsTxtContentCollapsed,
                    ExpandedControlText = Strings.RequirementsTxtContentExpanded,
                    ExpandedInformation = string.Join(Environment.NewLine, existing)
                };
                var replace = new TaskDialogButton(
                    Strings.RequirementsTxtReplace,
                    Strings.RequirementsTxtReplaceHelp
                );
                var refresh = new TaskDialogButton(
                    Strings.RequirementsTxtRefresh,
                    Strings.RequirementsTxtRefreshHelp
                );
                var update = new TaskDialogButton(
                    Strings.RequirementsTxtUpdate,
                    Strings.RequirementsTxtUpdateHelp
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
                Strings.ProductTitle,
                Strings.RequirementsTxtFailedToWrite,
                Strings.ErrorDetail,
                Strings.Retry,
                Strings.Cancel
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
                    Strings.ProductTitle,
                    Strings.RequirementsTxtFailedToAddToProject,
                    Strings.ErrorDetail,
                    Strings.Retry,
                    Strings.Cancel
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

            var result = Project.AddInterpreter.ShowDialog(this, service);
            if (result == null) {
                return;
            }

            var toRemove = new HashSet<IPythonInterpreterFactory>(InterpreterFactories);
            var toAdd = new HashSet<IPythonInterpreterFactory>(result);
            toRemove.ExceptWith(toAdd);
            toAdd.ExceptWith(toRemove);

            if (toAdd.Any() || toRemove.Any()) {
                //Make sure we can edit the project file
                if (!QueryEditProjectFile(false)) {
                    throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
                }
                foreach (var factory in toAdd) {
                    AddInterpreter(factory);
                }
                foreach (var factory in toRemove) {
                    RemoveInterpreterFactory(factory);
                }
            }
        }

        private async void ShowAddVirtualEnvironmentWithErrorHandling(bool browseForExisting) {
            var service = Site.GetComponentModel().GetService<IInterpreterRegistryService>();
            var statusBar = (IVsStatusbar)GetService(typeof(SVsStatusbar));
            object index = (short)0;
            statusBar.Animation(1, ref index);
            try {
                await AddVirtualEnvironment.ShowDialog(this, service, browseForExisting);
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }

                statusBar.SetText(Strings.VirtualEnvAddFailed);

                Debug.WriteLine("Failed to add virtual environment.\r\n{0}", ex.InnerException ?? ex);

                try {
                    ActivityLog.LogError(Strings.ProductTitle, (ex.InnerException ?? ex).ToString());
                } catch (InvalidOperationException) {
                    // Activity log may be unavailable
                }
            } finally {
                statusBar.Animation(0, ref index);
            }
        }

        internal async Task<IPythonInterpreterFactory> CreateOrAddVirtualEnvironment(
            IInterpreterRegistryService service,
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

            var rootPath = PathUtils.GetAbsoluteDirectoryPath(ProjectHome, path);
            foreach (var fact in InterpreterFactories) {
                var config = fact.Configuration;
                var rootPrefix = PathUtils.EnsureEndSeparator(config.PrefixPath);

                if (rootPrefix.Equals(rootPath, StringComparison.OrdinalIgnoreCase)) {
                    return fact;
                }
            }

            var options = VirtualEnv.FindInterpreterOptions(path, service, baseInterp);
            if (options == null || !File.Exists(options.InterpreterPath)) {
                throw new InvalidOperationException(Strings.VirtualEnvAddFailed);
            }
            if (!create) {
                baseInterp = service.FindInterpreter(options.Id);
            }

            if (!QueryEditProjectFile(false)) {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            string id;
            try {
                id = CreateInterpreterFactory(options);
            } catch (ArgumentException ex) {
                TaskDialog.ForException(Site, ex, issueTrackerUrl: IssueTrackerUrl).ShowModal();
                return null;
            }
            return Site.GetComponentModel().DefaultExportProvider.GetInterpreterFactory(id);
        }


        /// <summary>
        /// Creates a derived interpreter factory from the specified set of
        /// options. This function will modify the project, raise the
        /// <see cref="InterpreterFactoriesChanged"/> event and potentially
        /// display UI.
        /// </summary>
        /// <param name="options">
        /// <para>The options for the new interpreter:</para>
        /// <para>Guid: ID of the base interpreter.</para>
        /// <para>Version: Version of the base interpreter. This will also be
        /// the version of the new interpreter.</para>
        /// <para>PythonPath: Either the path to the root of the virtual
        /// environment, or directly to the interpreter executable. If no file
        /// exists at the provided path, the name of the interpreter specified
        /// for the base interpreter is tried. If that is not found, "scripts"
        /// is added as the last directory. If that is not found, an exception
        /// is raised.</para>
        /// <para>PythonWindowsPath [optional]: The path to the interpreter
        /// executable for windowed applications. If omitted, an executable with
        /// the same name as the base interpreter's will be used if it exists.
        /// Otherwise, this will be set to the same as PythonPath.</para>
        /// <para>PathEnvVar [optional]: The name of the environment variable to
        /// set for search paths. If omitted, the value from the base
        /// interpreter will be used.</para>
        /// <para>Description [optional]: The user-friendly name of the
        /// interpreter. If omitted, the relative path from the project home to
        /// the directory containing the interpreter is used. If this path ends
        /// in "\\Scripts", the last segment is also removed.</para>
        /// </param>
        /// <returns>The ID of the created interpreter.</returns>
        public string CreateInterpreterFactory(InterpreterFactoryCreationOptions options) {
            var projectHome = ProjectHome;
            var rootPath = PathUtils.GetAbsoluteDirectoryPath(projectHome, options.PrefixPath);

            IPythonInterpreterFactory fact;

            string id = MSBuildProjectInterpreterFactoryProvider.GetInterpreterId(
                BuildProject.FullPath,
                Path.GetFileName(PathUtils.TrimEndSeparator(options.PrefixPath))
            );

            var interpReg = Site.GetComponentModel().GetService<IInterpreterRegistryService>();

            int counter = 1;
            while (interpReg.FindConfiguration(id) != null) {
                id = MSBuildProjectInterpreterFactoryProvider.GetInterpreterId(
                    BuildProject.FullPath,
                    Path.GetDirectoryName(rootPath) + counter++
                );
            }
            
            var baseInterp = Site.GetComponentModel().DefaultExportProvider
                .GetInterpreterFactory(options.Id) as PythonInterpreterFactoryWithDatabase;
            if (baseInterp != null) {
                var pathVar = options.PathEnvironmentVariableName;
                if (string.IsNullOrEmpty(pathVar)) {
                    pathVar = baseInterp.Configuration.PathEnvironmentVariable;
                }

                var description = options.Description;
                if (string.IsNullOrEmpty(description)) {
                    description = PathUtils.CreateFriendlyDirectoryPath(projectHome, rootPath);
                    int i = description.LastIndexOf("\\scripts", StringComparison.OrdinalIgnoreCase);
                    if (i > 0) {
                        description = description.Remove(i);
                    }
                }

                fact = new DerivedInterpreterFactory(
                    baseInterp,
                    new InterpreterConfiguration(
                        id,
                        description,
                        options.PrefixPath,
                        options.InterpreterPath,
                        options.WindowInterpreterPath,
                        options.LibraryPath,
                        options.PathEnvironmentVariableName,
                        baseInterp.Configuration.Architecture,
                        baseInterp.Configuration.Version,
                        InterpreterUIMode.CannotBeDefault | InterpreterUIMode.CannotBeConfigured | InterpreterUIMode.SupportsDatabase
                    ),
                    new InterpreterFactoryCreationOptions {
                        WatchLibraryForNewModules = true
                    }
                );
            } else {
                fact = InterpreterFactoryCreator.CreateInterpreterFactory(
                    new InterpreterConfiguration(
                        id,
                        options.Description,
                        options.PrefixPath,
                        options.InterpreterPath,
                        options.WindowInterpreterPath,
                        options.LibraryPath,
                        options.PathEnvironmentVariableName ?? "PYTHONPATH",
                        options.Architecture,
                        options.LanguageVersion
                    ),
                    new InterpreterFactoryCreationOptions {
                        WatchLibraryForNewModules = options.WatchLibraryForNewModules
                    }
                );
            }

            AddInterpreter(fact, true);

            return id;
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
            RemoveInterpreterFactory(factory);

            var path = factory.Configuration.PrefixPath;
            if (removeFromStorage && Directory.Exists(path)) {
                var t = Task.Run(() => {
                    try {
                        Directory.Delete(path, true);
                        return true;
                    } catch (IOException) {
                    } catch (UnauthorizedAccessException) {
                    }
                    return false;
                }).HandleAllExceptions(Site, GetType());

                if (!await t) {
                    MessageBox.Show(Strings.EnvironmentDeleteError.FormatUI(path), Strings.ProductTitle);
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
                str = PathUtils.GetAbsoluteFilePath(ProjectHome, str);
                if (!File.Exists(str)) {
                    throw new MissingInterpreterException(Strings.DebugLaunchInterpreterMissing_Path.FormatUI(str));
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
