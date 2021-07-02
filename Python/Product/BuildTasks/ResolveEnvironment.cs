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
#if !BUILDTASKS_CORE
using System.ComponentModel.Composition.Hosting;
#endif
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.BuildTasks {
    /// <summary>
    /// Resolves a project's active environment from the contents of the project
    /// file.
    /// </summary>
    public sealed class ResolveEnvironment : ITask {
        private readonly string _projectPath;
        internal readonly TaskLoggingHelper _log;

        internal ResolveEnvironment(string projectPath, IBuildEngine buildEngine) {
            BuildEngine = buildEngine;
            _projectPath = projectPath;
            _log = new TaskLoggingHelper(this);
        }

#if !BUILDTASKS_CORE
        class CatalogLog : ICatalogLog {
            private readonly TaskLoggingHelper _helper;
            public CatalogLog(TaskLoggingHelper helper) {
                _helper = helper;
            }

            public void Log(string msg) {
                _helper.LogWarning(msg);
            }
        }
#endif

        /// <summary>
        /// The interpreter ID to resolve.
        /// </summary>
        public string InterpreterId { get; set; }

        [Output]
        public string PrefixPath { get; private set; }

        [Output]
        public string ProjectRelativePrefixPath { get; private set; }

        [Output]
        public string InterpreterPath { get; private set; }

        [Output]
        public string WindowsInterpreterPath { get; private set; }

        [Output]
        public string Architecture { get; private set; }

        [Output]
        public string PathEnvironmentVariable { get; private set; }

        [Output]
        public string Description { get; private set; }

        [Output]
        public string MajorVersion { get; private set; }

        [Output]
        public string MinorVersion { get; private set; }

        internal string[] SearchPaths { get; private set; }

        public bool Execute() {
            string id = InterpreterId;

            ProjectCollection collection = null;
            Project project = null;

#if !BUILDTASKS_CORE
            var exports = GetExportProvider();
            if (exports == null) {
                _log.LogError("Unable to obtain interpreter service.");
                return false;
            }
#endif

            try {
                try {
                    project = ProjectCollection.GlobalProjectCollection.GetLoadedProjects(_projectPath).Single();
                } catch (InvalidOperationException) {
                    // Could not get exactly one project matching the path.
                }

                if (project == null) {
                    collection = new ProjectCollection();
                    project = collection.LoadProject(_projectPath);
                }

                if (id == null) {
                    id = project.GetPropertyValue("InterpreterId");
                    if (String.IsNullOrWhiteSpace(id)) {
#if !BUILDTASKS_CORE
                        var options = exports.GetExportedValueOrDefault<IInterpreterOptionsService>();
                        if (options != null) {
                            id = options.DefaultInterpreterId;
                        }
#endif
                    }
                }

                var projectHome = PathUtils.GetAbsoluteDirectoryPath(
                    project.DirectoryPath,
                    project.GetPropertyValue("ProjectHome")
                );

                var searchPath = project.GetPropertyValue("SearchPath");
                if (!string.IsNullOrEmpty(searchPath)) {
                    SearchPaths = searchPath.Split(';')
                        .Select(p => PathUtils.GetAbsoluteFilePath(projectHome, p))
                        .ToArray();
                } else {
                    SearchPaths = new string[0];
                }

#if BUILDTASKS_CORE
                ProjectItem item = null;
                InterpreterConfiguration config = null;

                if (string.IsNullOrEmpty(id)) {
                    id = project.GetItems(MSBuildConstants.InterpreterReferenceItem).Select(pi => pi.GetMetadataValue(MSBuildConstants.IdKey)).LastOrDefault(i => !string.IsNullOrEmpty(i));
                }
                if (string.IsNullOrEmpty(id)) {
                    item = project.GetItems(MSBuildConstants.InterpreterItem).FirstOrDefault();
                    if (item == null) {
                        var found = PythonRegistrySearch.PerformDefaultSearch().OrderByDescending(i => i.Configuration.Version).ToArray();
                        config = (
                            found.Where(i => CPythonInterpreterFactoryConstants.TryParseInterpreterId(i.Configuration.Id, out var co, out _) &&
                                        PythonRegistrySearch.PythonCoreCompany.Equals(co, StringComparison.OrdinalIgnoreCase)).FirstOrDefault()
                                ?? found.FirstOrDefault()
                        )?.Configuration;
                    }
                } else {
                    // Special case MSBuild environments
                    var m = Regex.Match(id, @"MSBuild\|(?<id>.+?)\|(?<moniker>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    if (m.Success && m.Groups["id"].Success) {
                        var subId = m.Groups["id"].Value;
                        item = project.GetItems(MSBuildConstants.InterpreterItem)
                            .FirstOrDefault(pi => subId.Equals(pi.GetMetadataValue(MSBuildConstants.IdKey), StringComparison.OrdinalIgnoreCase));
                    }
                    if (item == null) {
                        config = PythonRegistrySearch.PerformDefaultSearch()
                            .FirstOrDefault(pi => id.Equals(pi.Configuration.Id, StringComparison.OrdinalIgnoreCase))?.Configuration;
                    }
                }
                if (item != null) {
                    PrefixPath = PathUtils.GetAbsoluteDirectoryPath(projectHome, item.EvaluatedInclude);
                    if (PathUtils.IsSubpathOf(projectHome, PrefixPath)) {
                        ProjectRelativePrefixPath = PathUtils.GetRelativeDirectoryPath(projectHome, PrefixPath);
                    } else {
                        ProjectRelativePrefixPath = string.Empty;
                    }
                    InterpreterPath = PathUtils.GetAbsoluteFilePath(PrefixPath, item.GetMetadataValue(MSBuildConstants.InterpreterPathKey));
                    WindowsInterpreterPath = PathUtils.GetAbsoluteFilePath(PrefixPath, item.GetMetadataValue(MSBuildConstants.WindowsPathKey));
                    Architecture = InterpreterArchitecture.TryParse(item.GetMetadataValue(MSBuildConstants.ArchitectureKey)).ToString("X");
                    PathEnvironmentVariable = item.GetMetadataValue(MSBuildConstants.PathEnvVarKey).IfNullOrEmpty("PYTHONPATH");
                    Description = item.GetMetadataValue(MSBuildConstants.DescriptionKey).IfNullOrEmpty(PathUtils.CreateFriendlyDirectoryPath(projectHome, PrefixPath));
                    Version ver;
                    if (Version.TryParse(item.GetMetadataValue(MSBuildConstants.VersionKey) ?? "", out ver)) {
                        MajorVersion = ver.Major.ToString();
                        MinorVersion = ver.Minor.ToString();
                    } else {
                        MajorVersion = MinorVersion = "0";
                    }
                    return true;
                } else if (config != null) {
                    UpdateResultFromConfiguration(config, projectHome);
                    return true;
                }
#else
                // MsBuildProjectContextProvider isn't available in-proc, instead we rely upon the
                // already loaded VsProjectContextProvider which is loaded in proc and already
                // aware of the projects loaded in Solution Explorer.
                var projectContext = exports.GetExportedValueOrDefault<MsBuildProjectContextProvider>();
                if (projectContext != null) {
                    projectContext.AddContext(project);
                }
                try {
                    var config = exports.GetExportedValue<IInterpreterRegistryService>().FindConfiguration(id);

                    if (config != null) {
                        UpdateResultFromConfiguration(config, projectHome);
                        return true;
                    }
                } finally {
                    if (projectContext != null) {
                        projectContext.RemoveContext(project);
                    }
                }
#endif

                if (!string.IsNullOrEmpty(id)) {
                    _log.LogError(
                        "The environment '{0}' is not available. Check your project configuration and try again.",
                        id
                    );
                    return false;
                }
            } catch (Exception ex) {
                _log.LogErrorFromException(ex);
            } finally {
                if (collection != null) {
                    collection.UnloadAllProjects();
                    collection.Dispose();
                }
            }

            _log.LogError("Unable to resolve environment");
            return false;
        }

        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }

        private void UpdateResultFromConfiguration(InterpreterConfiguration config, string projectHome) {
            PrefixPath = PathUtils.EnsureEndSeparator(config.GetPrefixPath());
            if (PathUtils.IsSubpathOf(projectHome, PrefixPath)) {
                ProjectRelativePrefixPath = PathUtils.GetRelativeDirectoryPath(projectHome, PrefixPath);
            } else {
                ProjectRelativePrefixPath = string.Empty;
            }
            InterpreterPath = config.InterpreterPath;
            WindowsInterpreterPath = config.GetWindowsInterpreterPath();
            Architecture = config.Architecture.ToString("X");
            PathEnvironmentVariable = config.PathEnvironmentVariable;
            Description = config.Description;
            MajorVersion = config.Version.Major.ToString();
            MinorVersion = config.Version.Minor.ToString();
        }

#if !BUILDTASKS_CORE
        private ExportProvider GetExportProvider() {
            return InterpreterCatalog.CreateContainer(
                new CatalogLog(_log),
                typeof(MsBuildProjectContextProvider),
                typeof(IInterpreterRegistryService),
                typeof(IInterpreterOptionsService)
            );
        }
#endif
    }

    /// <summary>
    /// Constructs ResolveEnvironment task objects.
    /// </summary>
    public sealed class ResolveEnvironmentFactory : TaskFactory<ResolveEnvironment> {
        public override ITask CreateTask(IBuildEngine taskFactoryLoggingHost) {
            return new ResolveEnvironment(Properties["ProjectPath"], taskFactoryLoggingHost);
        }
    }
}
