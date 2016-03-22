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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using MSBuild = Microsoft.Build.Evaluation;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// MSBuild factory provider.
    /// 
    /// The MSBuild factory provider consuems the relevant IProjectContextProvider to find locations
    /// for MSBuild proejcts.  The IProjectContextProvider can provide either MSBuild.Project items
    /// or strings which are paths to MSBuild project files.
    /// 
    /// The MSBuild interpreter factory provider ID is "MSBuild".  The interpreter IDs are in the
    /// format: id_in_project_file;path_to_project_file
    /// 
    /// 
    /// </summary>
    [InterpreterFactoryId(MSBuildProviderName)]
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class MSBuildProjectInterpreterFactoryProvider : IPythonInterpreterFactoryProvider, IDisposable {
        private readonly Dictionary<string, ProjectInfo> _projects = new Dictionary<string, ProjectInfo>();
        private readonly Lazy<IInterpreterLog>[] _loggers;
        private readonly Lazy<IProjectContextProvider>[] _contextProviders;
        private readonly Lazy<IPythonInterpreterFactoryProvider, Dictionary<string, object>>[] _factoryProviders;
        public const string MSBuildProviderName = "MSBuild";

        [ImportingConstructor]
        public MSBuildProjectInterpreterFactoryProvider(
            [ImportMany]Lazy<IProjectContextProvider>[] contextProviders, 
            [ImportMany]Lazy<IPythonInterpreterFactoryProvider, Dictionary<string, object>>[] factoryProviders,
            [ImportMany]Lazy<IInterpreterLog>[] loggers) {
            _factoryProviders = factoryProviders;
            _loggers = loggers;
            _contextProviders = contextProviders;
            InitializeContexts();
        }

        private void InitializeContexts() {
            foreach (var provider in _contextProviders) {
                provider.Value.ProjectsChanaged += Provider_ProjectContextsChanged;
                provider.Value.ProjectChanged += Provider_ProjectChanged;
                Provider_ProjectContextsChanged(provider.Value, EventArgs.Empty);
            }
        }

        private void Provider_ProjectChanged(object sender, ProjectChangedEventArgs e) {
            string filename = e.Project as string;
            if (filename == null) {
                var proj = e.Project as MSBuild.Project;
                if (proj != null) {
                    filename = proj.FullPath;
                }
            }

            ProjectInfo projInfo;
            if (filename != null && _projects.TryGetValue(filename, out projInfo)) {
                if (DiscoverInterpreters(projInfo)) {
                    OnInterpreterFactoriesChanged();
                }
            }
        }

        public event EventHandler InterpreterFactoriesChanged;

        public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations() {
            foreach (var project in _projects) {
                if (project.Value.Factories != null) {
                    foreach (var fact in project.Value.Factories) {
                        yield return fact.Value.Config;
                    }
                }
            }
        }

        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
            var pathAndId = id.Split(new[] { '|' }, 3);
            if (pathAndId.Length == 3) {
                var path = pathAndId[2];

                // see if the project is loaded
                ProjectInfo project;
                FactoryInfo factInfo;
                if (_projects.TryGetValue(path, out project) &&
                    project.Factories != null &&
                    project.Factories.TryGetValue(id, out factInfo)) {
                    return factInfo.Factory;
                }
            }
            return null;
        }

        public static string GetInterpreterId(string file, string id) {
            return String.Join("|", MSBuildProviderName, id, file);
        }

        public static string GetProjectiveRelativeId(string interpreterId) {
            return interpreterId.Split(new[] { '|' }, 3)[1];
        }

        private void Provider_ProjectContextsChanged(object sender, EventArgs e) {
            var contextProvider = (IProjectContextProvider)sender;
            bool discovered = false;
            if (contextProvider != null) {
                // Run through and and get the new interpreters to add...
                HashSet<string> seen = new HashSet<string>();
                HashSet<ProjectInfo> added = new HashSet<ProjectInfo>();
                HashSet<ProjectInfo> removed = new HashSet<ProjectInfo>();
                var contexts = contextProvider.Projects;
                lock (_projects) {
                    foreach (var context in contextProvider.Projects) {
                        var projContext = context as MSBuild.Project;
                        if (projContext == null) {
                            var projectFile = context as string;
                            if (projectFile != null && projectFile.EndsWith(".pyproj", StringComparison.OrdinalIgnoreCase)) {
                                projContext = new MSBuild.Project(projectFile);
                            }
                        }

                        if (projContext != null) {
                            if (!_projects.ContainsKey(projContext.FullPath)) {
                                var projInfo = new ProjectInfo(projContext, contextProvider);
                                _projects[projContext.FullPath] = projInfo;
                                added.Add(projInfo);
                            }
                            seen.Add(projContext.FullPath);
                        }
                    }

                    // Then remove any existing projects that are no longer there
                    var toRemove = _projects
                        .Where(x => x.Value.Context == contextProvider && !seen.Contains(x.Value.Project.FullPath))
                        .Select(x => x.Key)
                        .ToArray();

                    foreach (var projInfo in toRemove) {
                        var value = _projects[projInfo];
                        _projects.Remove(projInfo);
                        removed.Add(value);
                        value.Dispose();
                    }
                }

                // apply what we discovered without the projects lock...
                foreach (var projInfo in added) {
                    discovered |= DiscoverInterpreters(projInfo);
                }

                foreach (var projInfo in removed) {
                    projInfo.Dispose();
                    if (projInfo.Factories.Count > 0) {
                        discovered = true;
                    }
                }
            }

            if (discovered) {
                OnInterpreterFactoriesChanged();
            }
        }

        private void OnInterpreterFactoriesChanged() {
            var evt = InterpreterFactoriesChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        private void Log(string format, params object[] args) {
            Log(String.Format(format, args));
        }

        private void Log(string msg) {
            foreach (var logger in _loggers) {
                IInterpreterLog loggerValue = null;
                try {
                    loggerValue = logger.Value;
                } catch (CompositionException) {
                }
                if (loggerValue != null) {
                    loggerValue.Log(msg);
                }
            }
        }

        /// <summary>
        /// Call to find interpreters in the associated project. Separated from
        /// the constructor to allow exceptions to be handled without causing
        /// the project node to be invalid.
        /// </summary>
        private bool DiscoverInterpreters(ProjectInfo projectInfo) {
            // <Interpreter Include="InterpreterDirectory">
            //   <Id>factoryProviderId;interpreterFactoryId</Id>
            //   <BaseInterpreter>factoryProviderId;interpreterFactoryId</BaseInterpreter>
            //   <Version>...</Version>
            //   <InterpreterPath>...</InterpreterPath>
            //   <WindowsInterpreterPath>...</WindowsInterpreterPath>
            //   <LibraryPath>...</LibraryPath>
            //   <PathEnvironmentVariable>...</PathEnvironmentVariable>
            //   <Description>...</Description>
            // </Interpreter>
            var project = projectInfo.Project;

            var projectHome = PathUtils.GetAbsoluteDirectoryPath(project.DirectoryPath, project.GetPropertyValue("ProjectHome"));
            var factories = new Dictionary<string, FactoryInfo>();
            foreach (var item in project.GetItems(MSBuildConstants.InterpreterItem)) {
                // Errors in these options are fatal, so we set anyError and
                // continue with the next entry.
                var dir = item.EvaluatedInclude;
                if (!PathUtils.IsValidPath(dir)) {
                    Log("Interpreter has invalid path: {0}", dir ?? "(null)");
                    continue;
                }
                dir = PathUtils.GetAbsoluteDirectoryPath(projectHome, dir);

                var id = item.GetMetadataValue(MSBuildConstants.IdKey);
                if (string.IsNullOrEmpty(id)) {
                    Log("Interpreter {0} has invalid value for '{1}': {2}", dir, MSBuildConstants.IdKey, id);
                    continue;
                }
                if (factories.ContainsKey(id)) {
                    Log("Interpreter {0} has a non-unique id: {1}", dir, id);
                    continue;
                }

                var verStr = item.GetMetadataValue(MSBuildConstants.VersionKey);
                Version ver;
                if (string.IsNullOrEmpty(verStr) || !Version.TryParse(verStr, out ver)) {
                    Log("Interpreter {0} has invalid value for '{1}': {2}", dir, MSBuildConstants.VersionKey, verStr);
                    continue;
                }

                // The rest of the options are non-fatal. We create an instance
                // of NotFoundError with an amended description, which will
                // allow the user to remove the entry from the project file
                // later.
                bool hasError = false;

                bool hasDescription = true;
                var description = item.GetMetadataValue(MSBuildConstants.DescriptionKey);
                if (string.IsNullOrEmpty(description)) {
                    hasDescription = false;
                    description = PathUtils.CreateFriendlyDirectoryPath(projectHome, dir);
                }

                var value = item.GetMetadataValue(MSBuildConstants.BaseInterpreterKey);
                InterpreterConfiguration baseInterp = null;
                if (!string.IsNullOrEmpty(value)) {
                    // It's a valid GUID, so find a suitable base. If we
                    // don't find one now, we'll try and figure it out from
                    // the pyvenv.cfg/orig-prefix.txt files later.
                    // Using an empty GUID will always go straight to the
                    // later lookup.
                    baseInterp = _factoryProviders.GetConfiguration(value);
                }

                var path = item.GetMetadataValue(MSBuildConstants.InterpreterPathKey);
                if (!PathUtils.IsValidPath(path)) {
                    Log("Interpreter {0} has invalid value for '{1}': {2}", dir, MSBuildConstants.InterpreterPathKey, path);
                    hasError = true;
                } else if (!hasError) {
                    path = PathUtils.GetAbsoluteFilePath(dir, path);
                }

                var winPath = item.GetMetadataValue(MSBuildConstants.WindowsPathKey);
                if (!PathUtils.IsValidPath(winPath)) {
                    Log("Interpreter {0} has invalid value for '{1}': {2}", dir, MSBuildConstants.WindowsPathKey, winPath);
                    hasError = true;
                } else if (!hasError) {
                    winPath = PathUtils.GetAbsoluteFilePath(dir, winPath);
                }

                var libPath = item.GetMetadataValue(MSBuildConstants.LibraryPathKey);
                if (string.IsNullOrEmpty(libPath)) {
                    libPath = "lib";
                }
                if (!PathUtils.IsValidPath(libPath)) {
                    Log("Interpreter {0} has invalid value for '{1}': {2}", dir, MSBuildConstants.LibraryPathKey, libPath);
                    hasError = true;
                } else if (!hasError) {
                    libPath = PathUtils.GetAbsoluteDirectoryPath(dir, libPath);
                }

                var pathVar = item.GetMetadataValue(MSBuildConstants.PathEnvVarKey);
                if (string.IsNullOrEmpty(pathVar)) {
                    if (baseInterp != null) {
                        pathVar = baseInterp.PathEnvironmentVariable;
                    } else {
                        pathVar = "PYTHONPATH";
                    }
                }

                string arch = null;
                if (baseInterp == null) {
                    arch = item.GetMetadataValue(MSBuildConstants.ArchitectureKey);
                    if (string.IsNullOrEmpty(arch)) {
                        arch = "x86";
                    }
                }

                if (baseInterp == null && !hasError) {
                    // Only thing missing is the base interpreter, so let's try
                    // to find it using paths
                    baseInterp = FindBaseInterpreterFromVirtualEnv(dir, libPath);

                    if (baseInterp == null) {
                        Log("Interpreter {0} has invalid value for '{1}': {2}", dir, MSBuildConstants.BaseInterpreterKey, value ?? "(null)");
                        hasError = true;
                    }
                }

                string fullId = GetInterpreterId(project.FullPath, id);

                FactoryInfo info;
                if (hasError) {
                    info = new ErrorFactoryInfo(item, fullId, ver, description, dir);
                } else {
                    Debug.Assert(baseInterp != null, "we reported an error if we didn't have a base interpreter");

                    if (!hasDescription) {
                        description = string.Format("{0} ({1})", description, baseInterp.Description);
                    }

                    info = new ConfiguredFactoryInfo(
                        this,
                        item,
                        baseInterp,
                        new InterpreterConfiguration(
                            fullId,
                            description,
                            dir,
                            path,
                            winPath,
                            libPath,
                            pathVar,
                            baseInterp.Architecture,
                            baseInterp.Version,
                            InterpreterUIMode.CannotBeDefault | InterpreterUIMode.CannotBeConfigured | InterpreterUIMode.SupportsDatabase
                        )
                    );
                }

                MergeFactory(projectInfo, factories, info);
            }

            // <InterpreterReference Include="{factoryProviderId}|{interpreterId}" />
            foreach (var item in project.GetItems(MSBuildConstants.InterpreterReferenceItem)) {
                string id = item.EvaluatedInclude;

                var config = _factoryProviders.GetConfiguration(id);
                FactoryInfo info;
                if (config == null) {
                    info = new ErrorFactoryInfo(item, id, new Version(0, 0), "Missing interpreter", "");
                } else {
                    info = new ReferenceFactoryInfo(this, config, item);
                }

                MergeFactory(projectInfo, factories, info);
            }

            HashSet<FactoryInfo> previousFactories = new HashSet<FactoryInfo>();
            if (projectInfo.Factories != null) {
                previousFactories.UnionWith(projectInfo.Factories.Values);
            }
            HashSet<FactoryInfo> newFactories = new HashSet<FactoryInfo>(factories.Values);

            bool anyChange = !newFactories.SetEquals(previousFactories);
            if (anyChange || projectInfo.Factories == null) {
                // Lock here mainly to ensure that any searches complete before
                // we trigger the changed event.
                lock (projectInfo) {
                    projectInfo.Factories = factories;
                }

                foreach (var removed in previousFactories.Except(newFactories)) {
                    projectInfo.Context.InterpreterUnloaded(
                        projectInfo.Project,
                        removed.Config
                    );

                    IDisposable disp = removed as IDisposable;
                    if (disp != null) {
                        disp.Dispose();
                    }
                }

                foreach (var added in newFactories.Except(previousFactories)) {
                    foreach (var factory in factories) {
                        projectInfo.Context.InterpreterLoaded(
                            projectInfo.Project,
                            factory.Value.Config
                        );
                    }
                }
            }

            return anyChange;
        }

        private static void MergeFactory(ProjectInfo projectInfo, Dictionary<string, FactoryInfo> factories, FactoryInfo info) {
            FactoryInfo existing;
            if (projectInfo.Factories != null &&
                projectInfo.Factories.TryGetValue(info.Config.Id, out existing) &&
                existing.Equals(info)) {
                // keep the existing factory, we may have already created it's IPythonInterpreterFactory instance
                factories[info.Config.Id] = existing;
            } else {
                factories[info.Config.Id] = info;
            }
        }

        private static ProcessorArchitecture ParseArchitecture(string value) {
            if (string.IsNullOrEmpty(value)) {
                return ProcessorArchitecture.None;
            } else if (value.Equals("x64", StringComparison.InvariantCultureIgnoreCase)) {
                return ProcessorArchitecture.Amd64;
            } else {
                return ProcessorArchitecture.X86;
            }
        }

        public InterpreterConfiguration FindBaseInterpreterFromVirtualEnv(
            string prefixPath,
            string libPath
        ) {
            string basePath = DerivedInterpreterFactory.GetOrigPrefixPath(prefixPath, libPath);

            if (Directory.Exists(basePath)) {
                foreach (var provider in _factoryProviders) {
                    var value = provider.Value;

                    foreach (var config in value.GetInterpreterConfigurations()) {
                        if (PathUtils.IsSamePath(config.PrefixPath, basePath)) {
                            return config;
                        }
                    }
                }
            }
            return null;
        }

        class NotFoundInterpreter : IPythonInterpreter {
            public void Initialize(PythonAnalyzer state) { }
            public IPythonType GetBuiltinType(BuiltinTypeId id) { throw new KeyNotFoundException(); }
            public IList<string> GetModuleNames() { return new string[0]; }
            public event EventHandler ModuleNamesChanged { add { } remove { } }
            public IPythonModule ImportModule(string name) { return null; }
            public IModuleContext CreateModuleContext() { return null; }
        }

        internal class NotFoundInterpreterFactory : IPythonInterpreterFactory {
            public NotFoundInterpreterFactory(
                string id,
                Version version,
                string description = null,
                string prefixPath = null
            ) {
                Configuration = new InterpreterConfiguration(
                    id,
                    string.IsNullOrEmpty(description) ? string.Format("Unknown Python {0}", version) : description,
                    prefixPath,
                    null,
                    null,
                    null,
                    null,
                    ProcessorArchitecture.None,
                    version
                );
            }

            public string Description { get; private set; }
            public InterpreterConfiguration Configuration { get; private set; }
            public Guid Id { get; private set; }

            public IPythonInterpreter CreateInterpreter() {
                return new NotFoundInterpreter();
            }
        }

        class FactoryInfo {
            public readonly MSBuild.ProjectItem ProjectItem;
            public readonly InterpreterConfiguration Config;
            protected IPythonInterpreterFactory _factory;

            public FactoryInfo(MSBuild.ProjectItem projectItem, InterpreterConfiguration configuration) {
                Config = configuration;
                ProjectItem = projectItem;
            }

            protected virtual void CreateFactory() {
            }

            public IPythonInterpreterFactory Factory {
                get {
                    if (_factory == null) {
                        CreateFactory();
                    }
                    return _factory;
                }
            }
        }

        sealed class ConfiguredFactoryInfo : FactoryInfo, IDisposable {
            private readonly InterpreterConfiguration _baseConfig;
            private readonly MSBuildProjectInterpreterFactoryProvider _factoryProvider;

            public ConfiguredFactoryInfo(MSBuildProjectInterpreterFactoryProvider factoryProvider, MSBuild.ProjectItem projectItem, InterpreterConfiguration baseConfig, InterpreterConfiguration config) : base(projectItem, config) {
                _factoryProvider = factoryProvider;
                _baseConfig = baseConfig;
            }

            protected override void CreateFactory() {
                if (_baseConfig != null) {
                    var baseInterp = _factoryProvider._factoryProviders.GetInterpreterFactory(_baseConfig.Id) as PythonInterpreterFactoryWithDatabase;
                    if (baseInterp != null) {
                        _factory = new DerivedInterpreterFactory(
                            baseInterp,
                            Config,
                            new InterpreterFactoryCreationOptions {
                                WatchLibraryForNewModules = true,
                            }
                        );
                    }
                }
                if (_factory == null) {
                    _factory = InterpreterFactoryCreator.CreateInterpreterFactory(
                        Config,
                        new InterpreterFactoryCreationOptions {
                            WatchLibraryForNewModules = true
                        }
                    );
                }
            }

            public override bool Equals(object obj) {
                ConfiguredFactoryInfo other = obj as ConfiguredFactoryInfo;
                if (other != null) {
                    return other.Config == Config && other._baseConfig == _baseConfig;
                }
                return false;
            }

            public override int GetHashCode() {
                return Config.GetHashCode() ^ _baseConfig?.GetHashCode() ?? 0;
            }

            public void Dispose() {
                IDisposable fact = _factory as IDisposable;
                if (fact != null) {
                    fact.Dispose();
                }
            }
        }

        sealed class ErrorFactoryInfo : FactoryInfo {
            private string _dir;

            public ErrorFactoryInfo(MSBuild.ProjectItem projectItem, string id, Version ver, string description, string dir) : base(projectItem, new InterpreterConfiguration(id, description, ver)) {
                _dir = dir;
            }

            protected override void CreateFactory() {
                _factory = new NotFoundInterpreterFactory(
                    Config.Id,
                    Config.Version,
                    string.Format("{0} (unavailable)", Config.Description),
                    Directory.Exists(_dir) ? _dir : null
                );
            }

            public override bool Equals(object obj) {
                ErrorFactoryInfo other = obj as ErrorFactoryInfo;
                if (other != null) {
                    return other.Config == Config &&
                        other._dir == _dir;
                }
                return false;
            }

            public override int GetHashCode() {
                return Config.GetHashCode() ^ _dir?.GetHashCode() ?? 0;
            }
        }

        sealed class ReferenceFactoryInfo : FactoryInfo {
            private readonly MSBuildProjectInterpreterFactoryProvider _owner;

            public ReferenceFactoryInfo(MSBuildProjectInterpreterFactoryProvider owner, InterpreterConfiguration config, MSBuild.ProjectItem projectItem) : base(projectItem, config) {
                _owner = owner;
            }

            protected override void CreateFactory() {
                var existing = _owner._factoryProviders.GetInterpreterFactory(Config.Id);

                if (existing != null) {
                    _factory = existing;
                } else {
                    _factory = new NotFoundInterpreterFactory(Config.Id, new Version(0, 0));
                }
            }

            public override bool Equals(object obj) {
                ReferenceFactoryInfo other = obj as ReferenceFactoryInfo;
                if (other != null) {
                    return other.Config == Config;
                }
                return false;
            }

            public override int GetHashCode() {
                return Config.GetHashCode();
            }
        }


        sealed class ProjectInfo : IDisposable {
            public readonly MSBuild.Project Project;
            public readonly IProjectContextProvider Context;
            public Dictionary<string, FactoryInfo> Factories;
            public readonly Dictionary<string, string> RootPaths = new Dictionary<string, string>();

            public ProjectInfo(MSBuild.Project project, IProjectContextProvider context) {
                Context = context;
                Project = project;
            }

            public void Dispose() {
                if (Factories != null) {
                    foreach (var keyValue in Factories) {
                        IDisposable disp = keyValue.Value as IDisposable;
                        if (disp != null) {
                            disp.Dispose();
                        }
                    }
                }
            }
        }

        public void Dispose() {
            if (_projects != null) {
                foreach (var project in _projects) {
                    project.Value.Dispose();
                }
            }
        }
    }
}
