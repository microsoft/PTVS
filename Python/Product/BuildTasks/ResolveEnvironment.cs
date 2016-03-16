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
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;

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
        public string LibraryPath { get; private set; }

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

            var exports = FromInProc() ?? FromOutOfProc();
            if (exports == null) {
                _log.LogError("Unable to obtain interpreter service.");
                return false;
            }

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

                var projectContext = exports.GetExportedValue<MsBuildProjectContextProvider>();
                projectContext.AddContext(project);
                try {
                    var factoryProviders = exports.GetExports<IPythonInterpreterFactoryProvider, Dictionary<string, object>>();

                    var factory = factoryProviders.GetInterpreterFactory(id);

                    if (factory == null) {
                        _log.LogError(
                            "The environment '{0}' is not available. Check your project configuration and try again.",
                            factory.Description
                        );
                        return false;
                    } else {
                        PrefixPath = PathUtils.EnsureEndSeparator(factory.Configuration.PrefixPath);
                        if (PathUtils.IsSubpathOf(projectHome, PrefixPath)) {
                            ProjectRelativePrefixPath = PathUtils.GetRelativeDirectoryPath(projectHome, PrefixPath);
                        } else {
                            ProjectRelativePrefixPath = string.Empty;
                        }
                        InterpreterPath = factory.Configuration.InterpreterPath;
                        WindowsInterpreterPath = factory.Configuration.WindowsInterpreterPath;
                        LibraryPath = PathUtils.EnsureEndSeparator(factory.Configuration.LibraryPath);
                        Architecture = factory.Configuration.Architecture.ToString();
                        PathEnvironmentVariable = factory.Configuration.PathEnvironmentVariable;
                        Description = factory.Description;
                        MajorVersion = factory.Configuration.Version.Major.ToString();
                        MinorVersion = factory.Configuration.Version.Minor.ToString();

                        return true;
                    }
                } finally {
                    projectContext.RemoveContext(project);
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


        private static ExportProvider FromInProc() {
            if (ServiceProvider.GlobalProvider != null) {
                var model = ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel)) as IComponentModel;
                if (model != null) {
                    return model.DefaultExportProvider;
                }
            }
            return null;
        }

        private static ExportProvider FromOutOfProc() {
            return InterpreterCatalog.CreateContainer<MsBuildProjectContextProvider>();
        }
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
