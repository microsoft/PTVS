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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using MSBuild = Microsoft.Build.Evaluation;

namespace Microsoft.PythonTools.Uwp.Interpreter {

    [InterpreterFactoryId(InterpreterFactoryProviderId)]
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class PythonUwpInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
        private readonly Lazy<IProjectContextProvider>[] _contextProviders;
        private readonly Lazy<IInterpreterLog>[] _loggers;
        private bool _initialized;

        private readonly Dictionary<string, ProjectInfo> _projects = new Dictionary<string, ProjectInfo>();

        public const string InterpreterFactoryProviderId = "PythonUwpIoT";

        public event EventHandler InterpreterFactoriesChanged;

        [ImportingConstructor]
        public PythonUwpInterpreterFactoryProvider(
            [ImportMany]Lazy<IProjectContextProvider>[] contextProviders,
            [ImportMany]Lazy<IInterpreterLog>[] loggers) {
            _contextProviders = contextProviders;
            _loggers = loggers;
        }

        private void OnInterpreterFactoriesChanged() {
            InterpreterFactoriesChanged?.Invoke(this, EventArgs.Empty);
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

        private void EnsureInitialized() {
            lock (this) {
                if (!_initialized) {
                    foreach (var provider in _contextProviders) {
                        IProjectContextProvider providerValue;
                        try {
                            providerValue = provider.Value;
                        } catch (CompositionException ce) {
                            Log("Failed to get IProjectContextProvider {0}", ce);
                            continue;
                        }
                        providerValue.ProjectsChanaged += ProviderValue_ProjectsChanaged;
                        providerValue.ProjectChanged += ProviderValue_ProjectChanged;
                        ProviderValue_ProjectsChanaged(providerValue, EventArgs.Empty);
                    }
                    _initialized = true;
                }
            }
        }

        private void ProviderValue_ProjectChanged(object sender, ProjectChangedEventArgs e) {
            string filename = e.Project as string;
            if (filename == null) {
                var proj = e.Project as MSBuild.Project;
                if (proj != null) {
                    filename = proj.FullPath;
                }
            }

            RediscoverInterpreters(filename);
        }

        public void RediscoverInterpreters(string projectFullPath) {
            ProjectInfo projInfo;
            if (projectFullPath != null && _projects.TryGetValue(projectFullPath, out projInfo)) {
                if (projInfo.DiscoverInterpreters()) {
                    OnInterpreterFactoriesChanged();
                }
            }
        }

        private void ProviderValue_ProjectsChanaged(object sender, EventArgs e) {
            var contextProvider = (IProjectContextProvider)sender;
            if (contextProvider != null) {
                var hasInterpreterFactoriesChanged = false;
                var seen = new HashSet<string>();
                var removed = new HashSet<ProjectInfo>();
                lock (_projects) {

                    foreach (var context in contextProvider.Projects) {
                        var projectInfo = ProjectInfo.CreateFromProjectContext(context, contextProvider);
                        if (projectInfo != null && projectInfo.FullPath != null) {
                            seen.Add(projectInfo.FullPath);
                            if (!_projects.ContainsKey(projectInfo.FullPath)) {
                                _projects[projectInfo.FullPath] = projectInfo;
                            } else {
                                // reuse the old existing project info
                                projectInfo.Dispose();
                                projectInfo = _projects[projectInfo.FullPath];
                            }
                            hasInterpreterFactoriesChanged |= projectInfo.DiscoverInterpreters();
                        }
                    }

                    // Then remove any existing projects that are no longer there
                    var toRemove = _projects
                        .Where(x => x.Value.ContextProvider == contextProvider && !seen.Contains(x.Key))
                        .Select(x => x.Key)
                        .ToArray();

                    foreach (var proj in toRemove) {
                        var projInfo = _projects[proj];
                        _projects.Remove(proj);
                        if (projInfo.Factory != null) {
                            hasInterpreterFactoriesChanged = true;
                        }
                        projInfo.Dispose();
                    }
                }

                if (hasInterpreterFactoriesChanged) {
                    OnInterpreterFactoriesChanged();
                }
            }
        }

        /// <summary>
        /// Represents an MSBuild project file.  The file could have either been read from
        /// disk or it could be a project file running inside of the IDE which is being
        /// used for a Python project node.
        /// </summary>
        private class MSBuildProjectInfo : ProjectInfo {
            public readonly MSBuild.Project _project;

            public MSBuildProjectInfo(MSBuild.Project project, IProjectContextProvider contextProvider) 
                : base(contextProvider) {
                _project = project;
            }

            public override object Context {
                get {
                    return _project;
                }
            }

            public override string FullPath {
                get {
                    return _project.FullPath;
                }
            }

            public override string GetPropertyValue(string name) {
                return _project.GetPropertyValue(name);
            }
        }

        /// <summary>
        /// Gets information about an "in-memory" project.  Supports reading interpreters from
        /// a project when we're out of proc that haven't yet been committed to disk.
        /// </summary>
        private class InMemoryProjectInfo : ProjectInfo
        {
            private readonly InMemoryProject _project;

            public InMemoryProjectInfo(InMemoryProject project, IProjectContextProvider contextProvider) 
                : base(contextProvider) {
                _project = project;
            }

            public override object Context {
                get {
                    return _project;
                }
            }

            public override string FullPath {
                get {
                    return _project.FullPath;
                }
            }

            public override string GetPropertyValue(string name)
            {
                object res;
                if (_project.Properties.TryGetValue(name, out res) && res is string) {
                    return (string)res;
                }

                return String.Empty;
            }
        }

        /// <summary>
        /// Tracks data about a project.  Specific subclasses deal with how the underlying project
        /// is being stored.
        /// </summary>
        private abstract class ProjectInfo : IDisposable {
            public readonly IProjectContextProvider ContextProvider;
            private IPythonInterpreterFactory _factory;
            private static bool _skipMSBuild;

            public IPythonInterpreterFactory Factory {
                get {
                    return _factory;
                }
                private set {
                    if (_factory != value) {
                        var disp = _factory as IDisposable;
                        if (disp != null) {
                            disp.Dispose();
                        }
                        _factory = value;
                    }
                }
            }

            private static ProjectInfo CreateFromMSBuildProject(object context, IProjectContextProvider contextProvider) {
                var projContext = context as MSBuild.Project;
                if (projContext == null) {
                    var projectFile = context as string;
                    if (projectFile != null && projectFile.EndsWith(".pyproj", StringComparison.OrdinalIgnoreCase)) {
                        projContext = new MSBuild.Project(projectFile);
                    }
                }

                if (projContext != null) {
                    return new MSBuildProjectInfo(projContext, contextProvider);
                }
                return null;
            }

            static public ProjectInfo CreateFromProjectContext(object context, IProjectContextProvider contextProvider) {
                if (!_skipMSBuild) {
                    try {
                        var msBuild = CreateFromMSBuildProject(context, contextProvider);
                        if (msBuild != null) {
                            return msBuild;
                        }
                    } catch (FileNotFoundException) {
                        _skipMSBuild = true;
                    }
                }

                var inMemory = context as InMemoryProject;
                if (inMemory != null) {
                    return new InMemoryProjectInfo(inMemory, contextProvider);
                }

                return null;
            }

            private bool SetNotFoundInterpreterFactory(string interpreterId, Version ver) {
                var factory = Factory as NotFoundInterpreterFactory;
                if (factory != null && string.CompareOrdinal(factory.Configuration.Id, interpreterId) == 0 && factory.Configuration.Version == ver) {
                    // No updates.
                    return false;
                } else {
                    Factory = new NotFoundInterpreterFactory(interpreterId, ver, InterpreterFactoryProviderId);
                    return true;
                }
            }

            private bool SetPythonUwpInterpreterFactory(InterpreterConfiguration config) {
                var factory = Factory as PythonUwpInterpreterFactory;
                if (factory != null && factory.Configuration.Equals(config)) {
                    // No updates.
                    return false;
                } else {
                    Factory = new PythonUwpInterpreterFactory(config);
                    return true;
                }
            }

            /// <summary>
            /// Call to find interpreters in the associated project.
            /// </summary>
            public bool DiscoverInterpreters() {
                lock (this) {
                    // <InterpreterId>PythonUWP|3.5|$(MSBuildProjectFullPath)</InterpreterId>
                    var projectHome = Path.GetDirectoryName(FullPath);

                    var interpreterId = GetPropertyValue("InterpreterId");
                    if (string.IsNullOrEmpty(interpreterId)) {
                        return false;
                    }

                    var id = interpreterId.Split(new[] { '|' }, 3);
                    if (id.Length != 3) {
                        return false;
                    }

                    // Compare the tag name and the project full path
                    if (string.CompareOrdinal(id[0], InterpreterFactoryProviderId) != 0) {
                        return false;
                    }

                    // Get the Python version
                    Version ver;
                    if (!Version.TryParse(id[1], out ver)) {
                        return false;
                    }

                    // Msbuild will sometimes return a wrong "InterpreterId".  It will return the path from temp directory during project creation.
                    interpreterId = string.Join("|", InterpreterFactoryProviderId, ver.ToString(), FullPath);

                    if (InstalledPythonUwpInterpreter.GetDirectory(ver) == null) {
                        // We don't have that version of SDK installed.  Return "Not found interpreter factory".
                        return SetNotFoundInterpreterFactory(interpreterId, ver);
                    }

                    var interpreterPath = Path.GetFullPath(Path.Combine(projectHome, PythonUwpConstants.InterpreterRelativePath));
                    var prefixPath = new DirectoryInfo(interpreterPath);
                    if (!prefixPath.Exists) {
                        // Per-project interpreter doesn't.  Return "Not found interpreter factory".
                        return SetNotFoundInterpreterFactory(interpreterId, ver);
                    }

                    var targetsFile = prefixPath.GetFiles(PythonUwpConstants.InterpreterFile).FirstOrDefault();
                    var libPath = prefixPath.GetDirectories(PythonUwpConstants.InterpreterLibPath).FirstOrDefault();

                    if (targetsFile == null || libPath == null || !targetsFile.Exists || !libPath.Exists) {
                        return SetNotFoundInterpreterFactory(interpreterId, ver);
                    }

                    var projectName = Path.GetFileNameWithoutExtension(FullPath);
                    var descriptionSuffix = string.Format("({0})", projectName);

                    return SetPythonUwpInterpreterFactory(new InterpreterConfiguration(
                        interpreterId,
                        string.Format("{0} ({1})", InterpreterFactoryProviderId, descriptionSuffix),
                        prefixPath.FullName,
                        targetsFile.FullName,
                        "",
                        null,
                        InterpreterArchitecture.Unknown,
                        ver,
                        InterpreterUIMode.CannotBeDefault | InterpreterUIMode.SupportsDatabase
                    ));
                }
            }

            protected ProjectInfo(IProjectContextProvider context) {
                ContextProvider = context;
            }

            public abstract object Context { get; }
            public abstract string FullPath { get; }

            public abstract string GetPropertyValue(string name);

            public void Dispose() {
                IDisposable disp = Factory as IDisposable;
                if (disp != null) {
                    disp.Dispose();
                }
            }
        }

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            EnsureInitialized();
            lock (_projects) {
                return _projects.Where(x => x.Value.Factory != null).Select(x => x.Value.Factory).ToList();
            }
        }

        public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations() {
            EnsureInitialized();
            return GetInterpreterFactories().Select(x => x.Configuration);
        }

        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
            EnsureInitialized();
            return GetInterpreterFactories()
                .Where(x => x.Configuration.Id == id)
                .FirstOrDefault();
        }

        public object GetProperty(string id, string propName) {
            if (propName != "ProjectMoniker") {
                return null;
            }

            var moniker = id.Substring(id.LastIndexOf('|') + 1);
            if (string.IsNullOrEmpty(moniker) || moniker.IndexOfAny(Path.GetInvalidPathChars()) >= 0) {
                return null;
            }

            return moniker;
        }
    }
}