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
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
using Microsoft.Build.Execution;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Common;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Environments;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Projects;
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
using NativeMethods = Microsoft.VisualStudioTools.Project.NativeMethods;
using Task = System.Threading.Tasks.Task;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using VsMenus = Microsoft.VisualStudioTools.Project.VsMenus;

namespace Microsoft.PythonTools.Project {
    [Guid(PythonConstants.ProjectNodeGuid)]
    internal partial class PythonProjectNode :
        CommonProjectNode,
        IPythonProject,
        IAzureRoleProject,
        IPythonProjectProvider {
        // For files that are analyzed because they were directly or indirectly referenced in the search path, store the information
        // about the directory from the search path that referenced them in IProjectEntry.Properties[_searchPathEntryKey], so that
        // they can be located and removed when that directory is removed from the path.
        private static readonly object _searchPathEntryKey = new { Name = "SearchPathEntry" };

        private readonly VsProjectContextProvider _vsProjectContext;

        private object _designerContext;
        private PythonDebugPropertyPage _debugPropPage;
        internal readonly SearchPathManager _searchPaths;
        private CommonSearchPathContainerNode _searchPathContainer;
        private InterpretersContainerNode _interpretersContainer;
        private readonly HashSet<string> _validFactories = new HashSet<string>();

        private IPythonInterpreterFactory _active;
        private readonly IPythonToolsLogger _logger;

        private IReadOnlyList<IPackageManager> _activePackageManagers;
        private readonly System.Threading.Timer _reanalyzeProjectNotification;

        private FileWatcher _projectFileWatcher;

        internal List<CustomCommand> _customCommands;
        private string _customCommandsDisplayLabel;
        private Dictionary<object, Action<object>> _actionsOnClose;
        private readonly PythonProject _pythonProject;

        private bool _infoBarCheckTriggered = false;
        private bool _asyncInfoBarCheckTriggered = false;
        private readonly CondaEnvCreateInfoBar _condaEnvCreateInfoBar;
        private readonly VirtualEnvCreateInfoBar _virtualEnvCreateInfoBar;
        private readonly PackageInstallInfoBar _packageInstallInfoBar;
        private readonly TestFrameworkInfoBar _testFrameworkInfoBar;
        private readonly PythonNotSupportedInfoBar _pythonVersionNotSupportedInfoBar;

        private readonly SemaphoreSlim _recreatingAnalyzer = new SemaphoreSlim(1);
        private bool _isRefreshingInterpreters = false;

        public event EventHandler LanguageServerInterpreterChanged;

        public event EventHandler LanguageServerSearchPathsChanged;

        public PythonProjectNode(IServiceProvider serviceProvider) : base(serviceProvider, null) {
            _logger = serviceProvider.GetPythonToolsService().Logger;
            _vsProjectContext = serviceProvider.GetComponentModel().GetService<VsProjectContextProvider>();

            InterpreterOptions = serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
            InterpreterRegistry = serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();

            _searchPaths = new SearchPathManager(serviceProvider);
            _searchPaths.Changed += SearchPaths_Changed;

            Type projectNodePropsType = typeof(PythonProjectNodeProperties);
            AddCATIDMapping(projectNodePropsType, projectNodePropsType.GUID);
            ActiveInterpreterChanged += OnActiveInterpreterChanged;
            InterpreterFactoriesChanged += OnInterpreterFactoriesChanged;
            // _active starts as null, so we need to start with this event
            // hooked up.
            InterpreterOptions.DefaultInterpreterChanged += GlobalDefaultInterpreterChanged;
            InterpreterRegistry.InterpretersChanged += OnInterpreterRegistryChanged;
            InterpreterRegistry.CondaInterpreterDiscoveryCompleted += OnInterpreterDiscoveryCompleted;
            _pythonProject = new VsPythonProject(this);

            _condaEnvCreateInfoBar = new CondaEnvCreateProjectInfoBar(Site, this);
            _virtualEnvCreateInfoBar = new VirtualEnvCreateProjectInfoBar(Site, this);
            _packageInstallInfoBar = new PackageInstallProjectInfoBar(Site, this);
            _testFrameworkInfoBar = new TestFrameworkProjectInfoBar(Site, this);
            _pythonVersionNotSupportedInfoBar = new PythonNotSupportedInfoBar(Site, InfoBarContexts.Project, () => ActiveInterpreter);
            _reanalyzeProjectNotification = new System.Threading.Timer(OnReanalyzeProject_Notify, state: null, Timeout.Infinite, Timeout.Infinite);
        }

        private static KeyValuePair<string, string>[] outputGroupNames = {
                                             // Name                     ItemGroup (MSBuild)
            new KeyValuePair<string, string>("Built",                 "BuiltProjectOutputGroup"),
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

            _vsProjectContext.UpdateProject(this, project);

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

            try {
                _logger?.LogEvent(PythonLogEvent.VirtualEnvironments, _validFactories.Count);
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
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

        public IAsyncCommand FindCommand(string canonicalName) {
            return _customCommands.FirstOrDefault(cc => cc.Target == canonicalName);
        }

        public ProjectInstance GetMSBuildProjectInstance() {
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

        // Called once all async interpreter factories have finished discovering interpreters
        private void OnInterpreterDiscoveryCompleted(object sender, EventArgs e) {
            if (!_asyncInfoBarCheckTriggered) {
                _asyncInfoBarCheckTriggered = true;

                // Check for any missing environments and show info bars for them
                _condaEnvCreateInfoBar.CheckAsync().HandleAllExceptions(Site, typeof(PythonProjectNode)).DoNotWait();
            }
        }

        private void OnInterpreterRegistryChanged(object sender, EventArgs e) {
            Site.GetUIThread().Invoke(() => {
                // Check whether the active interpreter factory has changed.
                var fact = InterpreterRegistry.FindInterpreter(ActiveInterpreter.Configuration.Id);
                if (fact != null && fact != ActiveInterpreter) {
                    ActiveInterpreter = fact;
                }
                InterpreterFactoriesChanged?.Invoke(this, EventArgs.Empty);
            });
        }

        public IInterpreterOptionsService InterpreterOptions { get; }

        public IInterpreterRegistryService InterpreterRegistry { get; }

        public IPythonInterpreterFactory ActiveInterpreter {
            get {
                return _active ?? InterpreterOptions.DefaultInterpreter;
            }
            internal set {
                Site.MustBeCalledFromUIThread();

                Debug.Assert(this.FileName != null);
                var oldActive = _active;

                // stop listening for installed files changed
                var oldPms = _activePackageManagers;
                _activePackageManagers = null;
                foreach (var pm in oldPms.MaybeEnumerate()) {
                    pm.InstalledFilesChanged -= PackageManager_InstalledFilesChanged;
                }

                lock (_validFactories) {
                    // if there are no valid factories,
                    // or the specified factory isn't in the valid list,
                    // use the global default
                    if (_validFactories.Count == 0 || value == null || !_validFactories.Contains(value.Configuration.Id)) {
                        _active = null;
                    } else {
                        _active = value;
                    }
                }

                // start listening for package changes on the active interpreter again if interpreter is outside our workspace.
                _activePackageManagers = InterpreterOptions.GetPackageManagers(_active).ToArray();
                if (_active != null && !PathUtils.IsSubpathOf(ProjectHome, _active.Configuration.InterpreterPath)) {
                    foreach (var pm in _activePackageManagers) {
                        pm.InstalledFilesChanged += PackageManager_InstalledFilesChanged;
                        pm.EnableNotifications();
                    }
                }

                // update the InterpreterId element in the pyproj with the new active interpreter
                if (_active != oldActive) {
                    if (_active != null) {
                        BuildProject.SetProperty(
                            MSBuildConstants.InterpreterIdProperty,
                            ReplaceMSBuildPath(_active.Configuration.Id)
                        );
                        var ver3 = new Version(3, 0);
                        var version = _active.Configuration.Version;
                        // show a warning if the python version is not supported
                        if (version.ToLanguageVersion() == PythonLanguageVersion.None) {
                            Utility.MessageBox.ShowWarningMessage(Site, Strings.PythonVersionNotSupportedInfoBarText.FormatUI(_active.Configuration.Description));
                        } else if (_active.Configuration.Version < ver3) {
                            Utility.MessageBox.ShowWarningMessage(Site, Strings.WarningPython2NotSupported);
                        }
                    } else {
                        BuildProject.SetProperty(MSBuildConstants.InterpreterIdProperty, "");
                    }
                    BuildProject.MarkDirty();
                }

                // https://github.com/Microsoft/PTVS/issues/1739
                // When we go from "no interpreters" to "global default", we see
                // _active == oldActive == null. Previously we would not trigger
                // new analysis in this case.
                if (_active != oldActive || oldActive == null) {
                    ActiveInterpreterChanged?.Invoke(this, EventArgs.Empty);
                }
                BoldActiveEnvironment();
            }
        }

        private void PackageManager_InstalledFilesChanged(object sender, EventArgs e) {
            try {
                _reanalyzeProjectNotification.Change(60000, Timeout.Infinite);
            } catch (ObjectDisposedException) {
            }
        }

        private string ReplaceMSBuildPath(string id) {
            int index = id.IndexOfOrdinal(BuildProject.FullPath, ignoreCase: true);
            if (index != -1) {
                id = id.Substring(0, index) + "$(MSBuildProjectFullPath)" + id.Substring(index + BuildProject.FullPath.Length);
            }
            return id;
        }

        private void GlobalDefaultInterpreterChanged(object sender, EventArgs e) {
            if (_active == null) {
                // This event is only raised when our active interpreter is the
                // global default.
                ActiveInterpreterChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler ActiveInterpreterChanged;
        public event EventHandler ReanalyzeProject_Notify;

        internal event EventHandler InterpreterFactoriesChanged;

        public void AddInterpreter(string id) {
            lock (_validFactories) {
                if (!_validFactories.Add(id)) {
                    return;
                }
            }

            BuildProject.AddItem(MSBuildConstants.InterpreterReferenceItem, id);
            if (IsActiveInterpreterGlobalDefault) {
                // force an update to 
                BuildProject.SetProperty(MSBuildConstants.InterpreterIdProperty, id);
            }

            _vsProjectContext.OnProjectChanged(BuildProject);
            UpdateActiveInterpreter();
            InterpreterFactoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddInterpreterDefinitionAndReference(Interpreter.InterpreterConfiguration config) {
            lock (_validFactories) {
                if (_validFactories.Contains(config.Id)) {
                    return;
                }
            }

            var projectHome = PathUtils.GetAbsoluteDirectoryPath(BuildProject.DirectoryPath, BuildProject.GetPropertyValue("ProjectHome"));
            var rootPath = PathUtils.EnsureEndSeparator(config.GetPrefixPath());

            var id = MSBuildProjectInterpreterFactoryProvider.GetProjectRelativeId(BuildProject.FullPath, config.Id);
            if (string.IsNullOrEmpty(id)) {
                throw new InvalidOperationException(Strings.AddingProjectEnvironmentToWrongProjectException.FormatUI(config.Id, BuildProject.FullPath));
            }

            BuildProject.AddItem(MSBuildConstants.InterpreterItem,
                PathUtils.GetRelativeDirectoryPath(projectHome, rootPath).IfNullOrEmpty("."),
                new Dictionary<string, string> {
                    { MSBuildConstants.IdKey, id },
                    { MSBuildConstants.VersionKey, config.Version.ToString() },
                    { MSBuildConstants.DescriptionKey, config.Description },
                    { MSBuildConstants.InterpreterPathKey, PathUtils.GetRelativeFilePath(rootPath, config.InterpreterPath) },
                    { MSBuildConstants.WindowsPathKey, PathUtils.GetRelativeFilePath(rootPath, config.GetWindowsInterpreterPath()) },
                    { MSBuildConstants.PathEnvVarKey, config.PathEnvironmentVariable },
                    { MSBuildConstants.ArchitectureKey, config.Architecture.ToString("X") }
                });

            lock (_validFactories) {
                _validFactories.Add(config.Id);
                if (IsActiveInterpreterGlobalDefault) {
                    // force an update to 
                    BuildProject.SetProperty(MSBuildConstants.InterpreterIdProperty, config.Id);
                }
            }
            _vsProjectContext.OnProjectChanged(BuildProject);
            UpdateActiveInterpreter();
            InterpreterFactoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void SaveMSBuildProjectFile(string filename) {
            base.SaveMSBuildProjectFile(filename);
            _vsProjectContext.UpdateProject(this, BuildProject);
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
                throw new ArgumentNullException(nameof(factory));
            }

            RemoveInterpreterFactory(factory.Configuration?.Id);
        }

        internal void RemoveInterpreterFactory(string id) {
            if (string.IsNullOrEmpty(id)) {
                throw new ArgumentNullException(nameof(id));
            }

            lock (_validFactories) {
                if (!_validFactories.Contains(id)) {
                    return;
                }
            }

            var subid = MSBuildProjectInterpreterFactoryProvider.GetProjectRelativeId(BuildProject.FullPath, id);
            bool projectChanged = false;

            if (!string.IsNullOrEmpty(subid)) {
                foreach (var item in BuildProject.GetItems(MSBuildConstants.InterpreterItem)) {
                    if (item.GetMetadataValue(MSBuildConstants.IdKey) == subid) {
                        try {
                            BuildProject.RemoveItem(item);
                            projectChanged = true;
                        } catch (InvalidOperationException) {
                        }
                        break;
                    }
                }
            }

            foreach (var item in BuildProject.GetItems(MSBuildConstants.InterpreterReferenceItem)) {
                if (id == item.EvaluatedInclude) {
                    try {
                        BuildProject.RemoveItem(item);
                        projectChanged = true;
                    } catch (InvalidOperationException) {
                    }
                    break;
                }
            }

            if (projectChanged) {
                BuildProject.MarkDirty();
                _vsProjectContext.OnProjectChanged(BuildProject);
            }

            lock (_validFactories) {
                if (!_validFactories.Remove(id)) {
                    // Wasn't removed, so don't update anything
                    return;
                }
            }
            UpdateActiveInterpreter();
            InterpreterFactoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateActiveInterpreter() {
            var newActive = _active;
            lock (_validFactories) {
                if (newActive == null ||
                    _validFactories.Count == 0 ||
                    !_validFactories.Contains(newActive.Configuration.Id)) {
                    newActive = InterpreterRegistry.FindInterpreter(
                        BuildProject.GetPropertyValue(MSBuildConstants.InterpreterIdProperty)
                    );
                }
            }
            ActiveInterpreter = newActive;
        }

        internal bool IsActiveInterpreterGlobalDefault => _active == null;

        internal IEnumerable<string> InterpreterIds => _validFactories.ToArray();

        internal IEnumerable<string> InvalidInterpreterIds {
            get {
                foreach (var id in _validFactories) {
                    if (InterpreterRegistry.FindConfiguration(id) == null) {
                        yield return id;
                    }
                }
            }
        }

        internal IEnumerable<Interpreter.InterpreterConfiguration> InterpreterConfigurations {
            get {
                foreach (var config in _validFactories) {
                    var value = InterpreterRegistry.FindConfiguration(config);
                    if (value != null) {
                        yield return value;
                    }
                }
            }
        }

        internal IEnumerable<IPythonInterpreterFactory> InterpreterFactories {
            get {
                return InterpreterConfigurations
                    .Select(x => InterpreterRegistry.FindInterpreter(x.Id))
                    .Where(x => x != null);
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

            var automationObject = (EnvDTE.Project)GetAutomationObject();

            try {
                EnvDTE.Configuration activeConfig = automationObject.ConfigurationManager.ActiveConfiguration;
                if (activeConfig != null) {
                    this.BuildProject.SetGlobalProperty(ProjectFileConstants.Platform, activeConfig.PlatformName);
                }
            } catch (COMException ex) {
                Debug.WriteLine("SetCurrentConfiguration(). Failed to get active configuration because of {0}", ex);
            }
        }

        protected override bool SupportsIconMonikers {
            get { return true; }
        }

        protected override ImageMoniker GetIconMoniker(bool open) {
            return KnownMonikers.PYProjectNode;
        }

        internal override string IssueTrackerUrl {
            get { return Strings.IssueTrackerUrl; }
        }

        public override CommonFileNode CreateCodeFileNode(ProjectElement item) {
            return new PythonFileNode(this, item);
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
                    _searchPaths.Add(dirToAdd, true);
                }
            }

            base.LinkFileAdded(filename);
        }

        protected override Guid[] GetConfigurationIndependentPropertyPages() {
            return new[] {
                GetGeneralPropertyPageType().GUID,
                typeof(PythonDebugPropertyPage).GUID,
                typeof(PublishPropertyPage).GUID,
                typeof(PythonTestPropertyPage).GUID
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
                return PythonConstants.SourceFileExtensionsArray;
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

        public override Type GetEditorFactoryType() => null;

        public override Type GetLibraryManagerType() => null;

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
            }

            _interpretersContainer = new InterpretersContainerNode(this);
            this.AddChild(_interpretersContainer);

            OnProjectPropertyChanged += PythonProjectNode_OnProjectPropertyChanged;

            // Defer reanalysis until after we have loaded the project
            ActiveInterpreterChanged -= OnActiveInterpreterChanged;
            try {
                UpdateActiveInterpreter();
            } finally {
                ActiveInterpreterChanged += OnActiveInterpreterChanged;
            }

            // Needs to be refreshed after the active interpreter has been updated
            RefreshInterpreters(alwaysCollapse: true);

            base.Reload();

            string id;
            if (IsActiveInterpreterGlobalDefault &&
                !string.IsNullOrEmpty(id = GetProjectProperty(MSBuildConstants.InterpreterIdProperty))) {
                // The interpreter in the project file has no reference, so
                // find and add it.
                var interpFact = InterpreterRegistry.FindInterpreter(id);
                if (interpFact != null && QueryEditProjectFile(false)) {
                    AddInterpreter(id);
                }
            }

            if (!this.IsAppxPackageableProject()) {
                _searchPaths.LoadPathsFromString(ProjectHome, GetProjectProperty(PythonConstants.SearchPathSetting, false));
            }
        }

        public override void OnOpenItem(string fullPathToSourceFile) {
            base.OnOpenItem(fullPathToSourceFile);

            if (!_infoBarCheckTriggered) {
                _infoBarCheckTriggered = true;
                TriggerInfoBarsAsync().HandleAllExceptions(Site, typeof(PythonProjectNode)).DoNotWait();
            }
        }

        private async Task TriggerInfoBarsAsync() {
            await Task.WhenAll(
                _virtualEnvCreateInfoBar.CheckAsync(),
                _packageInstallInfoBar.CheckAsync(),
                _testFrameworkInfoBar.CheckAsync(),
                _pythonVersionNotSupportedInfoBar.CheckAsync()
            );
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

                var searchPath = _searchPaths.GetAbsoluteSearchPaths();

                //Refresh regular search path nodes
                SetProjectProperty(PythonConstants.SearchPathSetting, _searchPaths.SavePathsToString(ProjectHome));

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

        // Refresh the interpreters under the "Python Environments" node.
        // This gets called from two places - once when the project loads,
        // and any time interpreter factories are changed after that.
        // For example, conda environments are discovered asynchronously and are
        // not available when the project it loaded. So once they are enumerated,
        // OnInterpreterFactoriesChanged is triggered, which calls this method.
        private void RefreshInterpreters(bool alwaysCollapse = false) {

            // This method is re-entrant the first time it's called because GetInterpreterConfigurations() calls EnsureInitialized(),
            // which triggers the OnInterpreterFactoriesChanged event. So only allow this method to run if it's not already running
            if (_isRefreshingInterpreters) {
                return;
            }
            _isRefreshingInterpreters = true;

            try {

                // if the project is closed, we're done
                if (IsClosed) {
                    return;
                }

                // if the "Python Environments" node doesn't exist, we're done
                var pythonEnvironmentsNode = _interpretersContainer;
                if (pythonEnvironmentsNode == null) {
                    return;
                }

                // clear out all interpreter nodes since we're going to re-add them
                var interpreterNodes = pythonEnvironmentsNode.AllChildren.OfType<InterpretersNode>().ToList();
                interpreterNodes.ForEach(pythonEnvironmentsNode.RemoveChild);

                // if we have no interpreter factories, and the active interpreter is the global default,
                // add a node for it
                if (!InterpreterFactories.Any() && IsActiveInterpreterGlobalDefault && ActiveInterpreter.IsRunnable()) {
                    var newNode = new InterpretersNode(
                        this,
                        ActiveInterpreter,
                        isInterpreterReference: true,
                        canDelete: false,
                        isGlobalDefault: true
                        );

                    pythonEnvironmentsNode.AddChild(newNode);
                }

                // add all the factories we have
                foreach (var interpreterFactory in InterpreterFactories) {
                    var isProjectSpecific = _vsProjectContext.IsProjectSpecific(interpreterFactory.Configuration);
                    var canRemove = !this.IsAppxPackageableProject(); // Do not allow change python environment for UWP
                    var canDelete = isProjectSpecific && Directory.Exists(interpreterFactory.Configuration.GetPrefixPath());

                    var newNode = new InterpretersNode(
                        this,
                        interpreterFactory,
                        isInterpreterReference: !isProjectSpecific,
                        canDelete,
                        isGlobalDefault: false,
                        canRemove
                    );

                    pythonEnvironmentsNode.AddChild(newNode);
                }

                // If the project is referencing interpreters that we can't find, add dummy nodes for them.
                // This can include virtual environments that have been deleted, interpreters that have been uninstalled,
                // or conda environments that are still being discovered asynchronously.
                foreach (var id in InvalidInterpreterIds) {
                    pythonEnvironmentsNode.AddChild(InterpretersNode.CreateAbsentInterpreterNode(this, id));
                }

                // Expand the Python Environments node, if appropriate
                OnInvalidateItems(pythonEnvironmentsNode);
                if (!alwaysCollapse && ParentHierarchy != null) {
                    pythonEnvironmentsNode.ExpandItem(EXPANDFLAGS.EXPF_ExpandFolder);
                } 

                // update the active interpreter based on the "InterpreterId" element in the pyproj
                UpdateActiveInterpreter();

                // finally, bold the active environment
                BoldActiveEnvironment();

            } finally {
                
                // allow the method to run again
                _isRefreshingInterpreters = false;
            }
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

        private void SearchPaths_Changed(object sender, EventArgs e) {
            // Update solution explorer
            Site.GetUIThread().Invoke(() => RefreshSearchPaths());

            LanguageServerSearchPathsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Returns a list of absolute search paths, optionally including those
        /// that are implied by other properties.
        /// </summary>
        public IList<string> GetSearchPaths(bool withImplied = true) {
            return withImplied ?
                _searchPaths.GetAbsoluteSearchPaths() :
                _searchPaths.GetAbsolutePersistedSearchPaths();
        }

        internal void OnInvalidateSearchPath(string absolutePath, object moniker) {
            if (string.IsNullOrEmpty(absolutePath)) {
                // Clear all paths associated with this moniker
                _searchPaths.RemoveByMoniker(moniker);
            } else if (!_searchPaths.AddOrReplace(moniker, absolutePath, false)) {
                // Didn't change a search path, so we need to trigger reanalysis
                // manually.
                LanguageServerSearchPathsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Executes Add Search Path menu command.
        /// </summary>        
        internal int AddSearchPath() {
            string dirName = Dialogs.BrowseForDirectory(IntPtr.Zero, ProjectHome, Strings.SelectFolderForSearchPath);

            if (dirName != null) {
                _searchPaths.Add(dirName, true);
            }

            return VSConstants.S_OK;
        }


        internal void RemoveSearchPath(string path) {
            _searchPaths.Remove(path);
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
            }

            var debugProp = DebugPropertyPage;
            if (debugProp != null) {
                ((PythonDebugPropertyPageControl)debugProp.Control).ReloadSetting(e.PropertyName);
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
                return _designerContext;
            }
            private set {
                Debug.Assert(_designerContext == null, "Should only set DesignerContext once");
                _designerContext = value;
            }
        }

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

                _condaEnvCreateInfoBar.Dispose();
                _virtualEnvCreateInfoBar.Dispose();
                _packageInstallInfoBar.Dispose();
                _testFrameworkInfoBar.Dispose();
                _pythonVersionNotSupportedInfoBar.Dispose();

                foreach (var pm in _activePackageManagers.MaybeEnumerate()) {
                    pm.InstalledFilesChanged -= PackageManager_InstalledFilesChanged;
                }

                InterpreterOptions.DefaultInterpreterChanged -= GlobalDefaultInterpreterChanged;
                InterpreterRegistry.InterpretersChanged -= OnInterpreterRegistryChanged;

                _searchPaths.Dispose();

                var watcher = _projectFileWatcher;
                _projectFileWatcher = null;
                watcher?.Dispose();

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

                _recreatingAnalyzer.Dispose();
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

        public bool ShouldWarnOnLaunch {
            get {
                // Warn on launch no longer supported
                return false;
            }
        }

        public IPythonInterpreterFactory GetInterpreterFactory() {
            var fact = ActiveInterpreter;
            if (fact == null) {
                // May occur if we are racing with Dispose(), so the factory we
                // return isn't important, but it has to be non-null to fulfil
                // the contract.
                return InterpreterOptions.DefaultInterpreter;
            }

            return fact;
        }

        /// <summary>
        /// Returns the active interpreter factory or throws an appropriate
        /// exception. These exceptions have localized strings that may be
        /// shown to the user.
        /// </summary>
        /// <returns>The active interpreter factory.</returns>
        /// <exception cref="NoInterpretersException">
        /// No interpreters are available at all.
        /// </exception>
        /// <exception cref="MissingInterpreterException">
        /// The specified interpreter is not suitable for use.
        /// </exception>
        public IPythonInterpreterFactory GetInterpreterFactoryOrThrow() {
            var fact = ActiveInterpreter;
            if (fact == null) {
                throw new NoInterpretersException();
            }

            if (!fact.Configuration.IsAvailable()) {
                throw new MissingInterpreterException(
                    Strings.MissingEnvironment.FormatUI(fact.Configuration.Description, fact.Configuration.Version)
                );
            } else if (IsActiveInterpreterGlobalDefault &&
                !String.IsNullOrWhiteSpace(BuildProject.GetPropertyValue(MSBuildConstants.InterpreterIdProperty))) {
                throw new MissingInterpreterException(
                    Strings.MissingEnvironmentUnknownVersion.FormatUI(
                        BuildProject.GetPropertyValue(MSBuildConstants.InterpreterIdProperty)
                    )
                );
            }

            return fact;
        }

        /// <summary>
        /// Returns the current launch configuration or throws an appropriate
        /// exception. These exceptions have localized strings that may be
        /// shown to the user.
        /// </summary>
        /// <returns>The active interpreter factory.</returns>
        /// <exception cref="NoInterpretersException">
        /// No interpreters are available at all.
        /// </exception>
        /// <exception cref="MissingInterpreterException">
        /// The specified interpreter is not suitable for use.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        /// The working directory specified by the project does not exist.
        /// </exception>
        public LaunchConfiguration GetLaunchConfigurationOrThrow() {
            var fact = GetInterpreterFactoryOrThrow();

            var intPath = GetProjectProperty(PythonConstants.InterpreterPathSetting, resetCache: false);
            if (string.IsNullOrEmpty(intPath)) {
                intPath = null;
            } else if (!Path.IsPathRooted(intPath)) {
                intPath = PathUtils.GetAbsoluteFilePath(ProjectHome, intPath);
            }

            var config = new LaunchConfiguration(fact.Configuration) {
                InterpreterPath = intPath,
                InterpreterArguments = GetProjectProperty(PythonConstants.InterpreterArgumentsSetting, resetCache: false),
                ScriptName = GetStartupFile(),
                ScriptArguments = GetProjectProperty(PythonConstants.CommandLineArgumentsSetting, resetCache: false),
                WorkingDirectory = GetWorkingDirectory(),
                SearchPaths = _searchPaths.GetAbsoluteSearchPaths().ToList()
            };

            var str = GetProjectProperty(PythonConstants.IsWindowsApplicationSetting);
            bool preferWindowed;
            config.PreferWindowedInterpreter = bool.TryParse(str, out preferWindowed) && preferWindowed;

            config.Environment = PathUtils.ParseEnvironment(GetProjectProperty(PythonConstants.EnvironmentSetting) ?? "");

            str = GetProjectProperty(PythonConstants.WebBrowserUrlSetting);
            if (!string.IsNullOrEmpty(str)) {
                config.LaunchOptions[PythonConstants.WebBrowserUrlSetting] = str;
            }
            str = GetProjectProperty(PythonConstants.WebBrowserPortSetting);
            if (!string.IsNullOrEmpty(str)) {
                config.LaunchOptions[PythonConstants.WebBrowserPortSetting] = str;
            }
            str = GetProjectProperty(PythonConstants.EnableNativeCodeDebugging);
            if (!string.IsNullOrEmpty(str)) {
                config.LaunchOptions[PythonConstants.EnableNativeCodeDebugging] = str;
            }

            if (!File.Exists(config.GetInterpreterPath())) {
                throw new MissingInterpreterException(
                    Strings.DebugLaunchInterpreterMissing_Path.FormatUI(config.GetInterpreterPath())
                );
            }

            if (!Directory.Exists(config.WorkingDirectory)) {
                throw new DirectoryNotFoundException(
                    Strings.DebugLaunchWorkingDirectoryMissing.FormatUI(config.WorkingDirectory)
                );
            }

            // Ensure working directory is a search path.
            config.SearchPaths.Insert(0, config.WorkingDirectory);
            config.SearchPaths.AddRange(Site.GetPythonToolsService().GetGlobalPythonSearchPaths(config.Interpreter));

            return config;
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

            LanguageServerInterpreterChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnReanalyzeProject_Notify(object state) {
            if (IsClosed) {
                return;
            }
            ReanalyzeProject_Notify?.Invoke(this, EventArgs.Empty);
        }

        protected override string AssemblyReferenceTargetMoniker {
            get {
                return GetProjectProperty("TargetFrameworkMoniker", false); // ?? ".NETFramework, version=4.5";
            }
        }

        protected override string AddReferenceExtensions {
            get {
                return null;
            }
        }

        internal int OpenCommandPrompt(string path, Interpreter.InterpreterConfiguration interpreterConfig = null, string subtitle = null) {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"));
            psi.UseShellExecute = false;
            psi.WorkingDirectory = path;

            LaunchConfiguration config = null;
            try {
                config = GetLaunchConfigurationOrThrow();
            } catch (NoInterpretersException ex) {
                PythonToolsPackage.OpenNoInterpretersHelpPage(Site, ex.HelpPage);
                return VSConstants.S_OK;
            } catch (MissingInterpreterException ex) {
                MessageBox.Show(ex.Message, Strings.ProductTitle);
                return VSConstants.S_OK;
            } catch (IOException ex) {
                MessageBox.Show(ex.Message, Strings.ProductTitle);
                return VSConstants.S_OK;
            }
            if (interpreterConfig != null) {
                config = config.Clone(interpreterConfig);
            }

            psi.Arguments = string.Format("/K \"title {0} Command Prompt\"",
                string.IsNullOrEmpty(subtitle) ? Caption : subtitle
            );


            var paths = config.Interpreter.GetPrefixPath();
            if (!Directory.Exists(paths)) {
                paths = PathUtils.GetParent(config.GetInterpreterPath());
            }
            string scripts;
            if (Directory.Exists(paths) &&
                Directory.Exists(scripts = PathUtils.GetAbsoluteDirectoryPath(paths, "Scripts"))) {
                paths += Path.PathSeparator + scripts;
            }

            var env = PathUtils.MergeEnvironments(
                Site.GetPythonToolsService().GetFullEnvironment(config),
                new KeyValuePair<string, string>[] {
                    new KeyValuePair<string, string>("PATH", paths),
                },
                "PATH"
            );

            foreach (var kv in env) {
                if (!string.IsNullOrEmpty(kv.Key)) {
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
            if (cmdGroup == CommonGuidList.guidPythonToolsCmdSet) {
                IPythonInterpreterFactory factory;
                switch ((int)cmd) {
                    case PythonConstants.OpenInteractiveForEnvironment:
                        factory = GetInterpreterFactory();
                        if (factory.IsRunnable()) {
                            result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        } else {
                            result |= QueryStatusResult.INVISIBLE;
                        }
                        return VSConstants.S_OK;

                    case PythonConstants.InstallPythonPackage:
                    case PythonConstants.InstallRequirementsTxt:
                    case PythonConstants.GenerateRequirementsTxt:
                        factory = GetInterpreterFactory();
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
            if (cmdGroup == CommonGuidList.guidPythonToolsCmdSet) {
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
            if (cmdGroup == CommonGuidList.guidPythonToolsCmdSet) {
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
                        return ShowAddEnvironment();
                    case PythonConstants.AddExistingEnv:
                        return ShowAddExistingEnvironment();
                    case PythonConstants.AddVirtualEnv:
                        return ShowAddVirtualEnvironment();
                    case PythonConstants.AddCondaEnv:
                        return ShowAddCondaEnvironment();
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

        private string GetStringArgument(IntPtr variantIn) {
            if (variantIn == IntPtr.Zero) {
                return null;
            }

            var obj = Marshal.GetObjectForNativeVariant(variantIn);
            return obj as string;
        }

        private void GetSelectedInterpreterOrDefault(
            IEnumerable<HierarchyNode> selectedNodes,
            Dictionary<string, string> args,
            out InterpretersNode node,
            out IPythonInterpreterFactory factory,
            bool useProjectByDefault = true
        ) {
            factory = null;

            // First try and get the factory from the parameter
            string description;
            if (args != null && args.TryGetValue("e", out description) && !string.IsNullOrEmpty(description)) {
                var service = Site.GetComponentModel().GetService<IInterpreterRegistryService>();
                Interpreter.InterpreterConfiguration config;

                config = InterpreterConfigurations.FirstOrDefault(
                    // Description is a localized string, hence CCIC
                    c => description.Equals(c.Description, StringComparison.CurrentCultureIgnoreCase)
                );
                if (config != null) {
                    factory = service.FindInterpreter(config.Id);
                }

                if (factory == null) {
                    config = service.Configurations
                        .Where(PythonInterpreterFactoryExtensions.IsRunnable)
                        .FirstOrDefault(c => description.Equals(c.Description, StringComparison.CurrentCultureIgnoreCase));
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

                if (useProjectByDefault) {
                    factory = GetInterpreterFactory();
                }
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
            if (cmdGroup == CommonGuidList.guidPythonToolsCmdSet) {
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
            if (cmdGroup == CommonGuidList.guidPythonToolsCmdSet) {
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
            if (cmdGroup == CommonGuidList.guidPythonToolsCmdSet) {
                if (IsCurrentStateASuppressCommandsMode()) {
                    switch ((int)cmd) {
                        case CommonConstants.StartDebuggingCmdId:
                        case CommonConstants.StartWithoutDebuggingCmdId:
                            return true;
                        case PythonConstants.ActivateEnvironment:
                        case PythonConstants.AddCondaEnv:
                        case PythonConstants.AddEnvironment:
                        case PythonConstants.AddExistingEnv:
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
                } else if (this.IsAppxPackageableProject()) {
                    // Disable adding environment for UWP projects
                    switch ((int)cmd) {
                        case PythonConstants.AddCondaEnv:
                        case PythonConstants.AddEnvironment:
                        case PythonConstants.AddExistingEnv:
                        case PythonConstants.AddVirtualEnv:
                            return true;
                    }
                }
            }

            return base.DisableCmdInCurrentMode(cmdGroup, cmd);
        }

        private int ExecActivateEnvironment(Dictionary<string, string> args, IList<HierarchyNode> selectedNodes) {
            InterpretersNode selectedInterpreter;
            IPythonInterpreterFactory selectedInterpreterFactory;
            GetSelectedInterpreterOrDefault(selectedNodes, args, out selectedInterpreter, out selectedInterpreterFactory,
                useProjectByDefault: false);
            if (selectedInterpreterFactory != null) {
                return SetInterpreterFactory(selectedInterpreterFactory);
            }
            return VSConstants.S_OK;
        }

        private int ExecOpenInteractiveForEnvironment(Dictionary<string, string> args, IList<HierarchyNode> selectedNodes) {
            InterpretersNode selectedInterpreter;
            IPythonInterpreterFactory selectedInterpreterFactory;
            GetSelectedInterpreterOrDefault(selectedNodes, args, out selectedInterpreter, out selectedInterpreterFactory,
                useProjectByDefault: false);
            try {
                ExecuteInReplCommand.EnsureReplWindow(Site, selectedInterpreterFactory?.Configuration, this, null).Show(true);
            } catch (InvalidOperationException ex) {
                MessageBox.Show(Strings.ErrorOpeningInteractiveWindow.FormatUI(ex), Strings.ProductTitle);
            } catch (MissingInterpreterException ex) {
                MessageBox.Show(ex.Message, Strings.ProductTitle);
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
            _logger?.LogEvent(PythonLogEvent.PythonSpecificPublish, null);

            var factory = GetInterpreterFactory();
            if (factory.Configuration.IsAvailable() &&
                Directory.Exists(factory.Configuration.GetPrefixPath()) &&
                PathUtils.IsSubpathOf(ProjectHome, factory.Configuration.GetPrefixPath())
            ) {
                try {
                    publishOptions = TaskDialog.CallWithRetry(
                        _ => new PublishProjectOptions(
                            publishOptions.AdditionalFiles.Concat(
                                PathUtils.EnumerateFiles(factory.Configuration.GetPrefixPath())
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
                Strings.ZipAndEggArchiveFileFilter,
                ProjectHome
            );
            if (!string.IsNullOrEmpty(fileName)) {
                _searchPaths.Add(fileName, true);
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
                    _searchPaths.Add(bit, true);
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

            var pm = InterpreterOptions.GetPackageManagers(selectedInterpreterFactory).FirstOrDefault();
            if (pm == null) {
                if (Utilities.IsInAutomationFunction(Site)) {
                    return VSConstants.E_INVALIDARG;
                }
                MessageBox.Show(Strings.PackageManagementNotSupported, Strings.ProductTitle, MessageBoxButtons.OK);
                return VSConstants.S_OK;
            }

            string name;
            if (args != null && args.TryGetValue("p", out name)) {
                // Don't prompt, just install
                bool elevated = args.ContainsKey("a");
                pm.InstallAsync(
                    PackageSpec.FromArguments(name),
                    new VsPackageManagerUI(Site, elevated),
                    CancellationToken.None
                )
                    .SilenceException<OperationCanceledException>()
                    .HandleAllExceptions(Site)
                    .DoNotWait();
            } else {
                // Open the install UI
                InterpreterList.InterpreterListToolWindow.OpenAtAsync(
                    Site,
                    selectedInterpreterFactory,
                    typeof(EnvironmentsList.PipExtensionProvider)
                ).DoNotWait();
            }
            return VSConstants.S_OK;
        }

        private int ExecInstallRequirementsTxt(Dictionary<string, string> args, IList<HierarchyNode> selectedNodes) {
            var txt = GetRequirementsTxtPath();

            InterpretersNode selectedInterpreter;
            IPythonInterpreterFactory selectedInterpreterFactory;
            GetSelectedInterpreterOrDefault(selectedNodes, args, out selectedInterpreter, out selectedInterpreterFactory);

            return InstallRequirements(args, txt, selectedInterpreterFactory);
        }

        private int InstallRequirements(Dictionary<string, string> args, string requirementsPath, IPythonInterpreterFactory selectedInterpreterFactory) {
            var pm = InterpreterOptions.GetPackageManagers(selectedInterpreterFactory).FirstOrDefault(p => p.UniqueKey == "pip");
            if (pm == null) {
                if (Utilities.IsInAutomationFunction(Site)) {
                    return VSConstants.E_INVALIDARG;
                }
                MessageBox.Show(Strings.PackageManagementNotSupported, Strings.ProductTitle, MessageBoxButtons.OK);
                return VSConstants.S_OK;
            }

            InstallRequirementsAsync(pm, args, requirementsPath, selectedInterpreterFactory)
                .SilenceException<OperationCanceledException>()
                .HandleAllExceptions(Site, GetType())
                .DoNotWait();

            return VSConstants.S_OK;
        }

        private async Task InstallRequirementsAsync(IPackageManager pm, Dictionary<string, string> args, string requirementsPath, IPythonInterpreterFactory selectedInterpreterFactory) {
            if (args != null && !args.ContainsKey("y")) {
                if (!ShouldInstallRequirementsTxt(
                    selectedInterpreterFactory.Configuration.Description,
                    requirementsPath,
                    Site.GetPythonToolsService().GeneralOptions.ElevatePip
                )) {
                    return;
                }
            }

            await InstallRequirementsAsync(Site, pm, requirementsPath);
        }

        internal static async Task InstallRequirementsAsync(IServiceProvider site, IPackageManager pm, string requirementsPath) {
            var operation = new InstallPackagesOperation(
                site,
                pm,
                requirementsPath,
                OutputWindowRedirector.GetGeneral(site)
            );

            await operation.RunAsync();
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
            var pm = InterpreterOptions.GetPackageManagers(selectedInterpreterFactory).FirstOrDefault(p => p.UniqueKey == "pip");
            if (pm == null) {
                if (Utilities.IsInAutomationFunction(Site)) {
                    return VSConstants.E_INVALIDARG;
                }
                MessageBox.Show(Strings.PackageManagementNotSupported, Strings.ProductTitle, MessageBoxButtons.OK);
                return VSConstants.S_OK;
            }

            GenerateRequirementsTxtAsync(pm)
                .SilenceException<OperationCanceledException>()
                .HandleAllExceptions(Site, GetType())
                .DoNotWait();
            return VSConstants.S_OK;
        }

        private async Task GenerateRequirementsTxtAsync(IPackageManager packageManager) {
            var projectHome = ProjectHome;
            var txt = PathUtils.GetAbsoluteFilePath(projectHome, "requirements.txt");

            IList<PackageSpec> items = null;

            try {
                items = await packageManager.GetInstalledPackagesAsync(CancellationToken.None);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                ex.ReportUnhandledException(Site, GetType(), allowUI: Utilities.IsInAutomationFunction(Site));
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
                        var merged = PipRequirementsUtils.MergeRequirements(existing, items, addNew);
                        File.WriteAllLines(txt, merged);
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

        #endregion

        #region Virtual Env support

        private int ShowAddEnvironment() {
            AddEnvironmentDialog.ShowAddEnvironmentDialogAsync(
                Site,
                this,
                null,
                null,
                GetEnvironmentYmlPath(),
                GetRequirementsTxtPath()
            ).HandleAllExceptions(Site, typeof(PythonProjectNode)).DoNotWait();
            return VSConstants.S_OK;
        }

        private int ShowAddExistingEnvironment() {
            AddEnvironmentDialog.ShowAddExistingEnvironmentDialogAsync(
                Site,
                this,
                null,
                null,
                GetEnvironmentYmlPath(),
                GetRequirementsTxtPath()
            ).HandleAllExceptions(Site, typeof(PythonProjectNode)).DoNotWait();
            return VSConstants.S_OK;
        }

        internal void ChangeInterpreters(IEnumerable<string> result) {
            var toRemove = new HashSet<string>(InterpreterIds);
            var toAdd = new HashSet<string>(result);
            toRemove.ExceptWith(toAdd);
            toAdd.ExceptWith(toRemove);

            if (toAdd.Any() || toRemove.Any()) {
                //Make sure we can edit the project file
                if (!QueryEditProjectFile(false)) {
                    throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
                }
                foreach (var id in toAdd) {
                    AddInterpreter(id);
                }
                foreach (var id in toRemove) {
                    RemoveInterpreterFactory(id);
                }
            }
        }

        private int ShowAddVirtualEnvironment() {
            ShowAddVirtualEnvironmentAsync(
                GetRequirementsTxtPath()
            ).HandleAllExceptions(Site, typeof(PythonProjectNode)).DoNotWait();
            return VSConstants.S_OK;
        }

        private async Task ShowAddVirtualEnvironmentAsync(string requirementsPath) {
            var service = Site.GetComponentModel().GetService<IInterpreterRegistryService>();
            var statusBar = (IVsStatusbar)GetService(typeof(SVsStatusbar));

            var solution = (IVsSolution3)GetService(typeof(SVsSolution));
            var saveResult = solution.CheckForAndSaveDeferredSaveSolution(0, Strings.VirtualEnvSaveDeferredSolution, Strings.ProductTitle, 0);
            if (saveResult != VSConstants.S_OK) {
                // The user cancelled out of the save project dialog
                return;
            }

            object index = (short)0;
            statusBar.Animation(1, ref index);
            try {
                await AddEnvironmentDialog.ShowAddVirtualEnvironmentDialogAsync(
                    Site,
                    this,
                    null,
                    null,
                    null,
                    requirementsPath
                );
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }

                statusBar.SetText(Strings.VirtualEnvAddFailed);

                Debug.WriteLine("Failed to add virtual environment.\r\n{0}", ex.InnerException ?? ex);

                CommonUtils.ActivityLogError(Strings.ProductTitle, (ex.InnerException ?? ex).ToString());
            } finally {
                statusBar.Animation(0, ref index);
            }
        }

        internal IPythonInterpreterFactory AddVirtualEnvironment(IInterpreterRegistryService service, string path, IPythonInterpreterFactory baseInterp) {
            var rootPath = PathUtils.GetAbsoluteDirectoryPath(ProjectHome, path);
            foreach (var existingConfig in InterpreterConfigurations) {
                var rootPrefix = PathUtils.EnsureEndSeparator(existingConfig.GetPrefixPath());

                if (rootPrefix.Equals(rootPath, StringComparison.OrdinalIgnoreCase)) {
                    return InterpreterRegistry.FindInterpreter(existingConfig.Id);
                }
            }

            string id = GetNewEnvironmentName(path);

            var config = VirtualEnv.FindInterpreterConfiguration(id, path, service, baseInterp);
            if (config == null || !File.Exists(config.InterpreterPath)) {
                throw new InvalidOperationException(Strings.VirtualEnvAddFailed);
            }

            if (!QueryEditProjectFile(false)) {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            try {
                AddInterpreterDefinitionAndReference(config);
            } catch (ArgumentException ex) {
                TaskDialog.ForException(Site, ex, issueTrackerUrl: IssueTrackerUrl).ShowModal();
                return null;
            }
            return InterpreterRegistry.FindInterpreter(id);
        }

        internal IPythonInterpreterFactory AddMSBuildEnvironment(
            IInterpreterRegistryService service,
            string path,
            string interpreterPath,
            string windowsInterpreterPath,
            string pathVar,
            Version languageVersion,
            InterpreterArchitecture architecture,
            string description
        ) {
            var rootPath = PathUtils.GetAbsoluteDirectoryPath(ProjectHome, path);
            foreach (var existingConfig in InterpreterConfigurations) {
                var rootPrefix = PathUtils.EnsureEndSeparator(existingConfig.GetPrefixPath());

                if (rootPrefix.Equals(rootPath, StringComparison.OrdinalIgnoreCase)) {
                    return InterpreterRegistry.FindInterpreter(existingConfig.Id);
                }
            }

            string id = GetNewEnvironmentName(path);

            var config = new VisualStudioInterpreterConfiguration(
                id,
                description,
                path,
                interpreterPath,
                windowsInterpreterPath,
                pathVar,
                architecture,
                languageVersion,
                Interpreter.InterpreterUIMode.CannotBeDefault | Interpreter.InterpreterUIMode.CannotBeConfigured
            );

            if (!QueryEditProjectFile(false)) {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            try {
                AddInterpreterDefinitionAndReference(config);
            } catch (ArgumentException ex) {
                TaskDialog.ForException(Site, ex, issueTrackerUrl: IssueTrackerUrl).ShowModal();
                return null;
            }
            return InterpreterRegistry.FindInterpreter(id);
        }

        private string GetNewEnvironmentName(string path) {
            string id = MSBuildProjectInterpreterFactoryProvider.GetInterpreterId(
                BuildProject.FullPath,
                Path.GetFileName(PathUtils.TrimEndSeparator(path))
            );

            var interpReg = Site.GetComponentModel().GetService<IInterpreterRegistryService>();

            int counter = 1;
            while (interpReg.FindConfiguration(id) != null) {
                id = MSBuildProjectInterpreterFactoryProvider.GetInterpreterId(
                    BuildProject.FullPath,
                    Path.GetFileName(PathUtils.TrimEndSeparator(path)) + counter++
                );
            }

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

            var path = factory.Configuration.GetPrefixPath();
            if (removeFromStorage && Directory.Exists(path)) {
                var t = Task.Run(() => {
                    for (int retries = 5; Directory.Exists(path) && retries > 0; --retries) {
                        try {
                            Directory.Delete(path, true);
                            return true;
                        } catch (DirectoryNotFoundException) {
                            return true;
                        } catch (IOException) {
                        } catch (UnauthorizedAccessException) {
                        }
                        // Allow some time for operations to fail and release
                        // locks before trying to delete the directory again.
                        Thread.Sleep(100);
                    }
                    return !Directory.Exists(path);
                }).HandleAllExceptions(Site, GetType());

                if (!await t) {
                    MessageBox.Show(Strings.EnvironmentDeleteError.FormatUI(path), Strings.ProductTitle);
                }
            }
        }

        #endregion

        internal string GetRequirementsTxtPath() {
            var reqsPath = PathUtils.GetAbsoluteFilePath(ProjectHome, "requirements.txt");
            return File.Exists(reqsPath) ? reqsPath : null;
        }

        internal string GetPyTestConfigFilePath() {
            string fileName = PythonConstants.PyTestFrameworkConfigFiles
                .FirstOrDefault(x => File.Exists(PathUtils.GetAbsoluteFilePath(ProjectHome, x)));

            return String.IsNullOrEmpty(fileName) ? "" : Path.Combine(ProjectHome, fileName);
        }

        internal string GetEnvironmentYmlPath() {
            var yamlPath = PathUtils.GetAbsoluteFilePath(ProjectHome, "environment.yml");
            return File.Exists(yamlPath) ? yamlPath : null;
        }

        internal int ShowAddCondaEnvironment() {
            return ShowAddCondaEnvironment(null, GetEnvironmentYmlPath());
        }

        internal int ShowAddCondaEnvironment(string existingName, string yamlPath) {
            //Make sure we can edit the project file
            if (!QueryEditProjectFile(false)) {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            AddEnvironmentDialog.ShowAddCondaEnvironmentDialogAsync(
                Site,
                this,
                null,
                existingName,
                yamlPath,
                null
            ).HandleAllExceptions(Site, typeof(PythonProjectNode)).DoNotWait();
            return VSConstants.S_OK;
        }

        public override Guid SharedCommandGuid {
            get {
                return CommonGuidList.guidPythonToolsCmdSet;
            }
        }

        public PythonProject Project {
            get {
                return _pythonProject;
            }
        }

        protected internal override int ShowAllFiles() {
            int hr = base.ShowAllFiles();
            BoldActiveEnvironment();
            return hr;
        }

        void IAzureRoleProject.AddedAsRole(object azureProjectHierarchy, string roleType) {
            var hier = azureProjectHierarchy as IVsHierarchy;

            if (hier == null) {
                return;
            }

            Site.GetUIThread().Invoke(() => {
                UpdateServiceDefinition(hier, roleType, Caption, Site);
                SetProjectProperty(PythonConstants.SuppressCollectPythonCloudServiceFiles, "false");
            });
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
                    "ServiceDefinition.csdef".Equals(obj as string, StringComparison.OrdinalIgnoreCase) &&
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security.Xml", "CA3053:UseXmlSecureResolver")]
        private static void UpdateServiceDefinition(IVsTextLines lines, string roleType, string projectName) {
            if (lines == null) {
                throw new ArgumentException("lines");
            }

            int lastLine, lastIndex;
            string text;

            ErrorHandler.ThrowOnFailure(lines.GetLastLineIndex(out lastLine, out lastIndex));
            ErrorHandler.ThrowOnFailure(lines.GetLineText(0, 0, lastLine, lastIndex, out text));

            var doc = new XmlDocument { XmlResolver = null };
            var settings = new XmlReaderSettings { XmlResolver = null };
            using (var reader = XmlReader.Create(new StringReader(text), settings))
                doc.Load(reader);

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

        private class VsPythonProject : PythonProject {
            private readonly PythonProjectNode _node;
            public VsPythonProject(PythonProjectNode node) {
                _node = node;
                _node.OnProjectPropertyChanged += OnProjectPropertyChanged;
            }

            private void OnProjectPropertyChanged(object sender, ProjectPropertyChangedArgs e) {
                ProjectPropertyChanged?.Invoke(this, new PythonProjectPropertyChangedArgs(e.PropertyName, e.OldValue, e.NewValue));
            }

            public override string ProjectHome {
                get {
                    return _node.ProjectHome;
                }
            }

            public override string ProjectName {
                get {
                    return _node.Caption;
                }
            }

            public override event EventHandler<PythonProjectPropertyChangedArgs> ProjectPropertyChanged;

            public override event EventHandler ActiveInterpreterChanged {
                add { _node.ActiveInterpreterChanged += value; }
                remove { _node.ActiveInterpreterChanged -= value; }
            }

            public override IPythonInterpreterFactory GetInterpreterFactory() {
                return _node.GetInterpreterFactory();
            }

            public override LaunchConfiguration GetLaunchConfigurationOrThrow() {
                return _node.GetLaunchConfigurationOrThrow();
            }

            public override string GetProperty(string name) {
                return _node.GetProjectProperty(name);
            }

            public override string GetUnevaluatedProperty(string name) {
                return _node.GetUnevaluatedProperty(name);
            }

            public override void SetProperty(string name, string value) {
                _node.SetProjectProperty(name, value);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security.Xml", "CA3053:UseXmlSecureResolver")]
        private static void UpdateServiceDefinition(string path, string roleType, string projectName) {
            var doc = new XmlDocument { XmlResolver = null };
            var settings = new XmlReaderSettings { XmlResolver = null };
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = XmlReader.Create(stream, settings)) {
                doc.Load(reader);
            }

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
                throw new ArgumentException(Strings.UnknownRoleTypeException.FormatUI(roleType ?? "(null)"), nameof(roleType));
            }

            var nav = doc.CreateNavigator();

            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("sd", "http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition");

            var role = nav.SelectSingleNode(string.Format(
                "/sd:ServiceDefinition/sd:{0}Role[@name='{1}']", roleType, projectName
            ), ns);

            if (role == null) {
                throw new InvalidOperationException(Strings.MissingRoleEntryException);
            }

            var startup = role.SelectSingleNode("sd:Startup", ns);
            if (startup != null) {
                startup.DeleteSelf();
            }

            role.AppendChildElement(null, "Startup", null, null);
            startup = role.SelectSingleNode("sd:Startup", ns);
            if (startup == null) {
                throw new InvalidOperationException(Strings.MissingStartupEntryException);
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
                pep.CreateAttribute(null, "commandLine", null, "bin\\ps.cmd LaunchWorker.ps1 worker.py");
                pep.CreateAttribute(null, "setReadyOnProcessStart", null, "true");
            }
        }

        private static void AddEnvironmentNode(XPathNavigator nav, IXmlNamespaceResolver ns) {
            nav.AppendChildElement(null, "Environment", null, null);
            nav = nav.SelectSingleNode("sd:Environment", ns);
            nav.AppendChildElement(null, "Variable", null, null);
            var children = nav.SelectChildren(XPathNodeType.Element);
            if (children.MoveNext()) {
                var emulatedNode = children.Current;
                emulatedNode.CreateAttribute(null, "name", null, "EMULATED");
                emulatedNode.AppendChildElement(null, "RoleInstanceValue", null, null);
                emulatedNode = emulatedNode.SelectSingleNode("sd:RoleInstanceValue", ns);
                emulatedNode.CreateAttribute(null, "xpath", null, "/RoleEnvironment/Deployment/@emulated");
            }
        }
    }
}
