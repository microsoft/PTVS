using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Projects;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudioTools.TestAdapter;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PythonTools.TestAdapter.Model {
    internal class ProjectInfo : IDisposable {
        private readonly PythonProject _pythonProject;
        private readonly string _projectHome;
        private readonly IEnumerable<string> _sources;
        private readonly TestContainerDiscoverer _discoverer;
        private readonly Dictionary<string, TestContainer> _containers;

        public ProjectInfo(TestContainerDiscoverer discoverer, PythonProject project, IEnumerable<string> sources) {
            _pythonProject = project;
            _projectHome = _pythonProject.ProjectHome;
            _discoverer = discoverer;
            _containers = new Dictionary<string, TestContainer>(StringComparer.OrdinalIgnoreCase);
            _sources = sources;
        }

        public void Dispose() {
        }

        public TestContainer[] GetAllContainers() {
            return _containers.Select(x => x.Value).ToArray();
        }

        public bool TryGetContainer(string path, out TestContainer container) {
            return _containers.TryGetValue(path, out container);
        }

        public LaunchConfiguration GetLaunchConfigurationOrThrow() {
            return _pythonProject.GetLaunchConfigurationOrThrow();
        }

        public void UpdateTestCases() {
            bool anythingToNotify = false;
            foreach (var path in _sources) {

                if (!path.EndsWith(".py")) {
                    continue;
                }

                if (!TryGetContainer(path, out TestContainer existing)) {
                    // we have a new entry or some of the tests changed
                    int version = (existing?.Version ?? 0) + 1;

                    _containers[path] = new TestContainer(
                        _discoverer,
                        path,
                        _projectHome,
                        version,
                        Architecture,
                        null
                    );

                    anythingToNotify = true;
                } else if (RemoveContainer(path)) {
                    // Raise containers changed event...
                    anythingToNotify = true;
                }
            }

            if (anythingToNotify) {
                ContainersChanged();
            }
        }
        private bool RemoveContainer(string path) {
            return _containers.Remove(path);
        }

        private Architecture Architecture => Architecture.Default;

        private void ContainersChanged() {
            _discoverer.NotifyChanged();
        }
    }
}
