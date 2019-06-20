using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Projects;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudioTools.TestAdapter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.PythonTools.TestAdapter.Model {
    internal class ProjectInfo : IDisposable {
        private readonly PythonProject _pythonProject;
        private readonly IPythonWorkspaceContext _pythonWorkspace;
        private readonly string _projectHome;
        private readonly TestContainerDiscoverer _discoverer;
        private readonly Dictionary<string, TestContainer> _containers;

        public ProjectInfo(TestContainerDiscoverer discoverer, PythonProject project) {
            _pythonProject = project;
            _pythonWorkspace = null;
            _projectHome = _pythonProject.ProjectHome;
            _discoverer = discoverer;
            _containers = new Dictionary<string, TestContainer>(StringComparer.OrdinalIgnoreCase);
        }

        public ProjectInfo(TestContainerDiscoverer discoverer, IPythonWorkspaceContext workspace) {
            _pythonProject = null;
            _pythonWorkspace = workspace;
            _projectHome = workspace.Location;
            _discoverer = discoverer;
            _containers = new Dictionary<string, TestContainer>(StringComparer.OrdinalIgnoreCase);
        }

        public void Dispose() {
        }

        public bool IsWorkspace() => _pythonWorkspace != null;

        public TestContainer[] GetAllContainers() {
            return _containers.Select(x => x.Value).ToArray();
        }

        public bool TryGetContainer(string path, out TestContainer container) {
            return _containers.TryGetValue(path, out container);
        }

        public LaunchConfiguration GetLaunchConfigurationOrThrow() {
            if (IsWorkspace()) {
                if(!_pythonWorkspace.CurrentFactory.Configuration.IsAvailable()) {
                    throw new Exception("MissingEnvironment");
                }

                var config = new LaunchConfiguration(_pythonWorkspace.CurrentFactory.Configuration) {
                    WorkingDirectory = _pythonWorkspace.Location,
                    SearchPaths = _pythonWorkspace.GetAbsoluteSearchPaths().ToList(),
                    Environment = PathUtils.ParseEnvironment(_pythonWorkspace.GetStringProperty(PythonConstants.EnvironmentSetting) ?? "")
            };

                return config;
            }
            
            return _pythonProject.GetLaunchConfigurationOrThrow();
        }

        public string GetProperty(string name) {
            if (IsWorkspace()) {
                return _pythonWorkspace.GetStringProperty(name);
            }
            return _pythonProject.GetProperty(name);
        }


        public void AddTestContainer(string path) {
            if (!Path.GetExtension(path).Equals(PythonConstants.FileExtension, StringComparison.OrdinalIgnoreCase))
                return;

            TestContainer existing;
            if (!TryGetContainer(path, out existing)) {
            
                _containers[path] = new TestContainer(
                    _discoverer,
                    path,
                    _projectHome,
                    version:0,
                    Architecture,
                    null
                );
            } 
            else {
                RemoveTestContainer(path);

                _containers[path] = new TestContainer(
                   _discoverer,
                   path,
                   _projectHome,
                   version: existing.Version + 1,
                   Architecture,
                   null
               );
            }
        }

        public bool RemoveTestContainer(string path) {
            return _containers.Remove(path);
        }

        private Architecture Architecture => Architecture.Default;

        public string ProjectHome => _projectHome;
    }
}
