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
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools;

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
        /// The interpreter ID to resolve. If this and
        /// <see cref="InterpreterVersion"/>
        /// </summary>
        public string InterpreterId { get; set; }
        public string InterpreterVersion { get; set; }

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
            bool returnActive;
            Guid id;
            Version version;

            returnActive = !Guid.TryParse(InterpreterId, out id);

            if (!Version.TryParse(InterpreterVersion, out version)) {
                if (!returnActive) {
                    _log.LogError(
                        "Invalid values for InterpreterId (\"{0}\") and InterpreterVersion (\"{1}\")",
                        InterpreterId,
                        InterpreterVersion
                    );
                    return false;
                }
            }

            MSBuildProjectInterpreterFactoryProvider provider = null;
            ProjectCollection collection = null;
            Project project = null;

            var service = ServiceHolder.Create();
            if (service == null) {
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

                var projectHome = CommonUtils.GetAbsoluteDirectoryPath(
                    project.DirectoryPath,
                    project.GetPropertyValue("ProjectHome")
                );

                var searchPath = project.GetPropertyValue("SearchPath");
                if (!string.IsNullOrEmpty(searchPath)) {
                    SearchPaths = searchPath.Split(';')
                        .Select(p => CommonUtils.GetAbsoluteFilePath(projectHome, p))
                        .ToArray();
                } else {
                    SearchPaths = new string[0];
                }

                provider = new MSBuildProjectInterpreterFactoryProvider(service.Service, project);
                try {
                    provider.DiscoverInterpreters();
                } catch (InvalidDataException ex) {
                    _log.LogWarning("Errors while resolving environments: {0}", ex.Message);
                }

                IPythonInterpreterFactory factory = null;
                if (returnActive) {
                    factory = provider.ActiveInterpreter;
                } else {
                    factory = provider.FindInterpreter(id, version);
                }

                if (!provider.IsAvailable(factory)) {
                    _log.LogError(
                        "The environment '{0}' is not available. Check your project configuration and try again.",
                        factory.Description
                    );
                    return false;
                } else if (factory == service.Service.NoInterpretersValue) {
                    _log.LogError(
                        "No Python environments are configured. Please install or configure an environment and try " +
                        "again. See http://go.microsoft.com/fwlink/?LinkID=299429 for information on setting up a " +
                        "Python environment."
                    );
                    return false;
                } else if (factory != null) {
                    PrefixPath = CommonUtils.EnsureEndSeparator(factory.Configuration.PrefixPath);
                    if (CommonUtils.IsSubpathOf(projectHome, PrefixPath)) {
                        ProjectRelativePrefixPath = CommonUtils.GetRelativeDirectoryPath(projectHome, PrefixPath);
                    } else {
                        ProjectRelativePrefixPath = string.Empty;
                    }
                    InterpreterPath = factory.Configuration.InterpreterPath;
                    WindowsInterpreterPath = factory.Configuration.WindowsInterpreterPath;
                    LibraryPath = CommonUtils.EnsureEndSeparator(factory.Configuration.LibraryPath);
                    Architecture = factory.Configuration.Architecture.ToString();
                    PathEnvironmentVariable = factory.Configuration.PathEnvironmentVariable;
                    Description = factory.Description;
                    MajorVersion = factory.Configuration.Version.Major.ToString();
                    MinorVersion = factory.Configuration.Version.Minor.ToString();

                    return true;
                } else if (returnActive) {
                    _log.LogError("Unable to resolve active environment.");
                } else {
                    _log.LogError("Unable to resolve environment {0} {1}", InterpreterId, InterpreterVersion);
                }

            } catch (Exception ex) {
                _log.LogErrorFromException(ex);
            } finally {
                if (provider != null) {
                    provider.Dispose();
                }
                if (collection != null) {
                    collection.UnloadAllProjects();
                    collection.Dispose();
                }
                service.Dispose();
            }

            _log.LogError("Unable to resolve environment");
            return false;
        }

        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }

        private sealed class ServiceHolder : IDisposable {
            private readonly AssemblyCatalog _catalog;
            private readonly CompositionContainer _container;
            private readonly IInterpreterOptionsService _service;

            private static ServiceHolder FromInProc() {
                if (ServiceProvider.GlobalProvider != null) {
                    var model = ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel)) as IComponentModel;
                    if (model != null) {
                        var service = model.GetService<IInterpreterOptionsService>();
                        if (service != null) {
                            return new ServiceHolder(null, null, service);
                        }
                    }
                }
                return null;
            }

            private static ServiceHolder FromOutOfProc() {
                var catalog = new AssemblyCatalog(typeof(IInterpreterOptionsService).Assembly);
                var container = new CompositionContainer(catalog);
                var service = container.GetExportedValue<IInterpreterOptionsService>();
                if (service != null) {
                    return new ServiceHolder(catalog, container, service);
                }
                return null;
            }

            public static ServiceHolder Create() {
                return FromInProc() ?? FromOutOfProc();
            }

            private ServiceHolder(
                AssemblyCatalog catalog,
                CompositionContainer container,
                IInterpreterOptionsService service
            ) {
                _catalog = catalog;
                _container = container;
                _service = service;
            }

            public IInterpreterOptionsService Service { get { return _service; } }

            public void Dispose() {
                if (_container != null) {
                    _container.Dispose();
                }
                if (_catalog != null) {
                    _catalog.Dispose();
                }
            }
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
