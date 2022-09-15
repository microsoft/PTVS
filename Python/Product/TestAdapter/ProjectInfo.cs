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
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Projects;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudioTools.TestAdapter;

namespace Microsoft.PythonTools.TestAdapter {
    internal class ProjectInfo : IDisposable {
        private readonly PythonProject _pythonProject;
        private readonly IPythonWorkspaceContext _pythonWorkspace;
        private readonly string _projectHome;
        private readonly string _projectName;
        private readonly ConcurrentDictionary<string, TestContainer> _containers;

        public ProjectInfo(PythonProject project) {
            _pythonProject = project;
            _pythonWorkspace = null;
            _projectHome = _pythonProject.ProjectHome;
            _projectName = _pythonProject.ProjectName;
            _containers = new ConcurrentDictionary<string, TestContainer>(StringComparer.OrdinalIgnoreCase);
        }

        public ProjectInfo(IPythonWorkspaceContext workspace) {
            _pythonProject = null;
            _pythonWorkspace = workspace;
            _projectHome = workspace.Location;
            _projectName = workspace.WorkspaceName;
            _containers = new ConcurrentDictionary<string, TestContainer>(StringComparer.OrdinalIgnoreCase);
        }

        public void Dispose() {
            _containers.Clear();
        }

        public bool IsWorkspace => _pythonWorkspace != null;

        public TestContainer[] GetAllContainers() {
            //ConcurrentDictionary.ToArray() locks before copying
            return _containers.Values.ToArray();
        }

        public bool TryGetContainer(string path, out TestContainer container) {
            return _containers.TryGetValue(path, out container);
        }

        public LaunchConfiguration GetLaunchConfigurationOrThrow() {
            if (IsWorkspace) {
                if (!_pythonWorkspace.CurrentFactory.Configuration.IsAvailable()) {
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
            if (IsWorkspace) {
                return _pythonWorkspace.GetStringProperty(name);
            }
            return _pythonProject.GetProperty(name);
        }

        public bool? GetBoolProperty(string name) {
            if (IsWorkspace) {
                return _pythonWorkspace.GetBoolProperty(name);
            }
            return _pythonProject.GetProperty(name).IsTrue();
        }

        public void AddTestContainer(ITestContainerDiscoverer discoverer, string path) {
            // check if the directory that's supplied as the ITestContainer.Source from ITestContainerDiscoverer exists or not.
            if (!Directory.Exists(path))
                return;

            _containers[path] = new TestContainer(
                discoverer,
                path,
                _projectHome,
                ProjectName,
                Architecture,
                IsWorkspace
            );
        }

        public bool RemoveTestContainer(string path) {
            return _containers.TryRemove(path, out _);
        }

        private Architecture Architecture => Architecture.Default;

        public string ProjectHome => _projectHome;

        public string ProjectName {
            get {
                if (IsWorkspace) {
                    return _pythonWorkspace.WorkspaceName;
                }

                return _projectName;
            }
        }
    }
}
