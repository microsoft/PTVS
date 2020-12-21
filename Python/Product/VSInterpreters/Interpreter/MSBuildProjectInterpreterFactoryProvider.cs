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
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Shell;
using MSBuild = Microsoft.Build.Evaluation;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// MSBuild factory provider.
    /// 
    /// The MSBuild factory provider consuems the relevant IProjectContextProvider to find locations
    /// for MSBuild projects.  The IProjectContextProvider can provide either MSBuild.Project items
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
    sealed class MSBuildProjectInterpreterFactoryProvider : IPythonInterpreterFactoryProvider, IDisposable {
        private readonly IServiceProvider _site;
        private readonly Dictionary<string, ProjectInfo> _projects = new Dictionary<string, ProjectInfo>();
        private readonly Lazy<IInterpreterLog>[] _loggers;
        private readonly Lazy<IProjectContextProvider>[] _contextProviders;
        private readonly Lazy<IPythonInterpreterFactoryProvider, Dictionary<string, object>>[] _factoryProviders;
        public const string MSBuildProviderName = "MSBuild";
        private const string InterpreterFactoryIdMetadata = "InterpreterFactoryId";
        private bool _initialized;
        private bool _skipMSBuild;

        private static readonly Regex InterpreterIdRegex = new Regex(
            @"MSBuild\|(?<id>.+?)\|(?<moniker>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        [ImportingConstructor]
        public MSBuildProjectInterpreterFactoryProvider(
            [ImportMany] Lazy<IProjectContextProvider>[] contextProviders,
            [ImportMany] Lazy<IPythonInterpreterFactoryProvider, Dictionary<string, object>>[] factoryProviders,
            [ImportMany] Lazy<IInterpreterLog>[] loggers,
            [Import(typeof(SVsServiceProvider), AllowDefault = true)] IServiceProvider site = null) {
            _site = site;
            _factoryProviders = factoryProviders;
            _loggers = loggers;
            _contextProviders = contextProviders;
        }

        private void EnsureInitialized() {
            if (!_initialized) {
                _initialized = true;

                foreach (var provider in _contextProviders) {
                    IProjectContextProvider providerValue;
                    try {
                        providerValue = provider.Value;
                    } catch (CompositionException ce) {
                        Log("Failed to get IProjectContextProvider {0}", ce);
                        continue;
                    }
                    providerValue.ProjectsChanaged += Provider_ProjectContextsChanged;
                    providerValue.ProjectChanged += Provider_ProjectChanged;
                    Provider_ProjectContextsChanged(providerValue, EventArgs.Empty);
                }
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
            EnsureInitialized();

            foreach (var project in _projects) {
                if (project.Value.Factories != null) {
                    foreach (var fact in project.Value.Factories) {
                        yield return fact.Value.Config;
                    }
                }
            }
        }

        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
            EnsureInitialized();

            var m = InterpreterIdRegex.Match(id);
            if (!m.Success) {
                return null;
            }

            // see if the project is loaded
            ProjectInfo project;
            FactoryInfo factInfo;
            if (_projects.TryGetValue(m.Groups["moniker"].Value, out project) &&
                project.Factories != null &&
                project.Factories.TryGetValue(id, out factInfo)) {
                return factInfo.Factory;
            }
            return null;
        }

        public object GetProperty(string id, string propName) {
            Match m;
            string moniker;

            switch (propName) {
                case "ProjectMoniker":
                    m = InterpreterIdRegex.Match(id);
                    if (m.Success && PathUtils.IsValidPath(moniker = m.Groups["moniker"].Value)) {
                        return moniker;
                    }
                    break;
                case PythonRegistrySearch.CompanyPropertyKey:
                    m = InterpreterIdRegex.Match(id);
                    if (m.Success && PathUtils.IsValidPath(moniker = m.Groups["moniker"].Value)) {
                        try {
                            return Path.GetFileNameWithoutExtension(moniker);
                        } catch (ArgumentException) {
                            return PathUtils.GetFileOrDirectoryName(moniker);
                        }
                    }
                    break;
            }
            return null;
        }

        public static string GetInterpreterId(string file, string id) {
            return String.Join("|", MSBuildProviderName, id, file);
        }

        public static string GetProjectRelativeId(string file, string id) {
            var m = InterpreterIdRegex.Match(id);
            if (m.Success && (m.Groups["moniker"].Value?.Equals(file, StringComparison.OrdinalIgnoreCase) ?? false)) {
                return m.Groups["id"].Value;
            }
            return null;
        }

        private void HandleMSBuildProject(
            object context,
            IProjectContextProvider contextProvider,
            HashSet<ProjectInfo> added,
            HashSet<string> seen
        ) {
            var projContext = context as MSBuild.Project;
            if (projContext == null) {
                var projectFile = context as string;
                if (projectFile != null && projectFile.EndsWithOrdinal(".pyproj", ignoreCase: true)) {
                    projContext = new MSBuild.Project(projectFile);
                }
            }

            if (projContext != null) {
                if (!_projects.ContainsKey(projContext.FullPath)) {
                    var projInfo = new MSBuildProjectInfo(projContext, projContext.FullPath, contextProvider);
                    _projects[projContext.FullPath] = projInfo;
                    added.Add(projInfo);
                }
                seen.Add(projContext.FullPath);
            }
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
                        if (!_skipMSBuild) {
                            try {
                                HandleMSBuildProject(context, contextProvider, added, seen);
                            } catch (FileNotFoundException) {
                                _skipMSBuild = true;
                            }
                        }

                        var inMemory = context as InMemoryProject;
                        if (inMemory != null) {
                            if (!_projects.ContainsKey(inMemory.FullPath)) {
                                var projInfo = new InMemoryProjectInfo(inMemory, inMemory.FullPath, contextProvider);
                                _projects[inMemory.FullPath] = projInfo;
                                added.Add(projInfo);
                            }
                            seen.Add(inMemory.FullPath);
                        }
                    }

                    // Then remove any existing projects that are no longer there
                    var toRemove = _projects
                        .Where(x => x.Value.ContextProvider == contextProvider && !seen.Contains(x.Key))
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
                IInterpreterLog loggerValue;
                try {
                    loggerValue = logger.Value;
                } catch (CompositionException) {
                    continue;
                }
                loggerValue.Log(msg);
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
            //   <Version>...</Version>
            //   <InterpreterPath>...</InterpreterPath>
            //   <WindowsInterpreterPath>...</WindowsInterpreterPath>
            //   <PathEnvironmentVariable>...</PathEnvironmentVariable>
            //   <Description>...</Description>
            // </Interpreter>
            var projectHome = PathUtils.GetAbsoluteDirectoryPath(
                Path.GetDirectoryName(projectInfo.FullPath),
                projectInfo.GetPropertyValue("ProjectHome")
            );
            var factories = new Dictionary<string, FactoryInfo>();
            foreach (var item in projectInfo.GetInterpreters()) {
                // Errors in these options are fatal, so we set anyError and
                // continue with the next entry.
                var dir = GetValue(item, "EvaluatedInclude");
                if (!PathUtils.IsValidPath(dir)) {
                    Log("Interpreter has invalid path: {0}", dir ?? "(null)");
                    continue;
                }
                dir = PathUtils.GetAbsoluteDirectoryPath(projectHome, dir);

                var id = GetValue(item, MSBuildConstants.IdKey);
                if (string.IsNullOrEmpty(id)) {
                    Log("Interpreter {0} has invalid value for '{1}': {2}", dir, MSBuildConstants.IdKey, id);
                    continue;
                }
                if (factories.ContainsKey(id)) {
                    Log("Interpreter {0} has a non-unique id: {1}", dir, id);
                    continue;
                }

                var verStr = GetValue(item, MSBuildConstants.VersionKey);
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

                var description = GetValue(item, MSBuildConstants.DescriptionKey);
                if (string.IsNullOrEmpty(description)) {
                    description = PathUtils.CreateFriendlyDirectoryPath(projectHome, dir);
                }

                var path = GetValue(item, MSBuildConstants.InterpreterPathKey);
                if (!PathUtils.IsValidPath(path)) {
                    Log("Interpreter {0} has invalid value for '{1}': {2}", dir, MSBuildConstants.InterpreterPathKey, path);
                    hasError = true;
                } else if (!hasError) {
                    path = PathUtils.GetAbsoluteFilePath(dir, path);
                }

                var winPath = GetValue(item, MSBuildConstants.WindowsPathKey);
                if (!PathUtils.IsValidPath(winPath)) {
                    Log("Interpreter {0} has invalid value for '{1}': {2}", dir, MSBuildConstants.WindowsPathKey, winPath);
                    hasError = true;
                } else if (!hasError) {
                    winPath = PathUtils.GetAbsoluteFilePath(dir, winPath);
                }

                var pathVar = GetValue(item, MSBuildConstants.PathEnvVarKey);
                if (string.IsNullOrEmpty(pathVar)) {
                    pathVar = "PYTHONPATH";
                }

                var arch = InterpreterArchitecture.TryParse(GetValue(item, MSBuildConstants.ArchitectureKey));

                string fullId = GetInterpreterId(projectInfo.FullPath, id);

                FactoryInfo info;
                if (hasError) {
                    info = new ErrorFactoryInfo(fullId, ver, description, dir);
                } else {
                    info = new ConfiguredFactoryInfo(this, new VisualStudioInterpreterConfiguration(
                        fullId,
                        description,
                        dir,
                        path,
                        winPath,
                        pathVar,
                        arch,
                        ver,
                        InterpreterUIMode.CannotBeDefault | InterpreterUIMode.CannotBeConfigured
                    ));
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
                    projectInfo.ContextProvider.InterpreterUnloaded(
                        projectInfo.Context,
                        removed.Config
                    );

                    IDisposable disp = removed as IDisposable;
                    if (disp != null) {
                        disp.Dispose();
                    }
                }

                foreach (var added in newFactories.Except(previousFactories)) {
                    foreach (var factory in factories) {
                        projectInfo.ContextProvider.InterpreterLoaded(
                            projectInfo.Context,
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

        private static string GetValue(Dictionary<string, string> from, string name) {
            string res;
            if (!from.TryGetValue(name, out res)) {
                return String.Empty;
            }
            return res;
        }

        class FactoryInfo {
            public readonly InterpreterConfiguration Config;
            protected IPythonInterpreterFactory _factory;

            public FactoryInfo(InterpreterConfiguration configuration) {
                Config = configuration;
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
            private readonly MSBuildProjectInterpreterFactoryProvider _factoryProvider;

            public ConfiguredFactoryInfo(MSBuildProjectInterpreterFactoryProvider factoryProvider, InterpreterConfiguration config) : base(config) {
                _factoryProvider = factoryProvider;
            }

            protected override void CreateFactory() {
                _factory = InterpreterFactoryCreator.CreateInterpreterFactory(
                    Config,
                    new InterpreterFactoryCreationOptions {
                        WatchFileSystem = true,
                    }
                );
            }

            public override bool Equals(object obj) {
                ConfiguredFactoryInfo other = obj as ConfiguredFactoryInfo;
                if (other != null) {
                    return other.Config == Config;
                }
                return false;
            }

            public override int GetHashCode() {
                return Config.GetHashCode();
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

            public ErrorFactoryInfo(string id, Version ver, string description, string dir) :
                base(new VisualStudioInterpreterConfiguration(id, "{0} (unavailable)".FormatInvariant(description), version: ver)) {
                _dir = dir;
            }

            protected override void CreateFactory() {
                _factory = new NotFoundInterpreterFactory(
                    Config.Id,
                    Config.Version,
                    Config.Description,
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

        /// <summary>
        /// Represents an MSBuild project file.  The file could have either been read from 
        /// disk or it could be a project file running inside of the IDE which is being
        /// used for a Python project node.
        /// </summary>
        sealed class MSBuildProjectInfo : ProjectInfo {
            public readonly MSBuild.Project Project;

            public MSBuildProjectInfo(MSBuild.Project project, string filename, IProjectContextProvider context) : base(filename, context) {
                Project = project;
            }

            public override object Context {
                get {
                    return Project;
                }
            }

            public override string GetPropertyValue(string name) {
                return Project.GetPropertyValue(name);
            }

            internal override IEnumerable<Dictionary<string, string>> GetInterpreters() {
                return Project.GetItems(MSBuildConstants.InterpreterItem).Select(
                    interp => new Dictionary<string, string>() {
                        { "EvaluatedInclude", interp.EvaluatedInclude },
                        { MSBuildConstants.IdKey,              interp.GetMetadataValue(MSBuildConstants.IdKey) },
                        { MSBuildConstants.VersionKey,         interp.GetMetadataValue(MSBuildConstants.VersionKey) },
                        { MSBuildConstants.DescriptionKey,     interp.GetMetadataValue(MSBuildConstants.DescriptionKey) },
                        { MSBuildConstants.InterpreterPathKey, interp.GetMetadataValue(MSBuildConstants.InterpreterPathKey) },
                        { MSBuildConstants.WindowsPathKey,     interp.GetMetadataValue(MSBuildConstants.WindowsPathKey) },
                        { MSBuildConstants.PathEnvVarKey,      interp.GetMetadataValue(MSBuildConstants.PathEnvVarKey) },
                        { MSBuildConstants.ArchitectureKey,    interp.GetMetadataValue(MSBuildConstants.ArchitectureKey) }
                    }
                );
            }
        }

        /// <summary>
        /// Gets information about an "in-memory" project.  Supports reading interpreters from
        /// a project when we're out of proc that haven't yet been committed to disk.
        /// </summary>
        sealed class InMemoryProjectInfo : ProjectInfo {
            public readonly InMemoryProject Project;

            public InMemoryProjectInfo(InMemoryProject project, string filename, IProjectContextProvider context) : base(filename, context) {
                Project = project;
            }

            public override object Context {
                get {
                    return Project;
                }
            }

            public override string GetPropertyValue(string name) {
                object res;
                if (Project.Properties.TryGetValue(name, out res) && res is string) {
                    return (string)res;
                }

                return String.Empty;
            }

            internal override IEnumerable<Dictionary<string, string>> GetInterpreters() {
                object interps;
                if (Project.Properties.TryGetValue("Interpreters", out interps) &&
                    interps is IEnumerable<Dictionary<string, string>>) {
                    return (IEnumerable<Dictionary<string, string>>)interps;
                }

                return Array.Empty<Dictionary<string, string>>();
            }
        }

        /// <summary>
        /// Tracks data about a project.  Specific subclasses deal with how the underlying project
        /// is being stored. 
        /// </summary>
        abstract class ProjectInfo : IDisposable {
            public readonly IProjectContextProvider ContextProvider;
            public readonly string FullPath;
            public Dictionary<string, FactoryInfo> Factories;
            public readonly Dictionary<string, string> RootPaths = new Dictionary<string, string>();

            public ProjectInfo(string filename, IProjectContextProvider context) {
                FullPath = filename;
                ContextProvider = context;
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

            public abstract object Context {
                get;
            }

            public abstract string GetPropertyValue(string name);

            internal abstract IEnumerable<Dictionary<string, string>> GetInterpreters();
        }

        public void Dispose() {
            if (_projects != null) {
                foreach (var project in _projects) {
                    project.Value.Dispose();
                }
            }
        }

        /* We can't use IInterpreterRegistryService here because we need to do
           this during initilization, and we don't have access to it until after
           our ctor has run.  So we do our own interpreter discovery */
        private IPythonInterpreterFactory FindInterpreter(string id) {
            return GetFactoryProvider(id)?.GetInterpreterFactory(id);
        }

        private InterpreterConfiguration FindConfiguration(string id) {
            var factoryProvider = GetFactoryProvider(id);
            if (factoryProvider != null) {
                return factoryProvider
                    .GetInterpreterConfigurations()
                    .Where(x => x.Id == id)
                    .FirstOrDefault();
            }
            return null;
        }


        private IPythonInterpreterFactoryProvider GetFactoryProvider(string id) {
            if (string.IsNullOrEmpty(id)) {
                return null;
            }
            var interpAndId = id.Split(new[] { '|' }, 2);
            if (interpAndId.Length == 2) {
                foreach (var provider in GetProvidersAndMetadata()) {
                    object value;
                    if (provider.Value.TryGetValue(InterpreterFactoryIdMetadata, out value) &&
                        value is string &&
                        (string)value == interpAndId[0]) {
                        return provider.Key;
                    }
                }
            }
            return null;
        }

        private IEnumerable<KeyValuePair<IPythonInterpreterFactoryProvider, Dictionary<string, object>>> GetProvidersAndMetadata() {
            for (int i = 0; i < _factoryProviders.Length; i++) {
                IPythonInterpreterFactoryProvider value = null;
                try {
                    var provider = _factoryProviders[i];
                    if (provider != null) {
                        value = provider.Value;
                    }
                } catch (CompositionException ce) {
                    Log("Failed to get interpreter factory value: {0}", ce);
                    _factoryProviders[i] = null;
                }
                if (value != null) {
                    yield return new KeyValuePair<IPythonInterpreterFactoryProvider, Dictionary<string, object>>(value, _factoryProviders[i].Metadata);
                }
            }
        }

    }
}
