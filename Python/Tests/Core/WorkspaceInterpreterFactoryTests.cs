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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Intellisense;
using Microsoft.VisualStudio.Workspace.Settings;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using TestUtilities;

namespace PythonToolsTests {
    [TestClass]
    public class WorkspaceInterpreterFactoryTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(0)]
        public void WatchWorkspaceFolderChanged() {
            var workspaceFolder1 = TestData.GetTempPath();
            Directory.CreateDirectory(workspaceFolder1);
            File.WriteAllText(Path.Combine(workspaceFolder1, "app1.py"), string.Empty);

            var workspaceFolder2 = TestData.GetTempPath();
            Directory.CreateDirectory(workspaceFolder2);
            File.WriteAllText(Path.Combine(workspaceFolder2, "app2.py"), string.Empty);

            var workspace1 = new MockWorkspace(workspaceFolder1);
            var workspace2 = new MockWorkspace(workspaceFolder2);

            var workspaceService = new MockWorkspaceService(workspace1);

            // Load a different workspace
            Action triggerDiscovery = () => {
                workspaceService.ChangeWorkspace(workspace2);
            };

            TestTriggerDiscovery(workspaceService, triggerDiscovery);
        }

        [TestMethod, Priority(0)]
        public void WatchWorkspaceSettingsChanged() {
            var workspaceFolder = TestData.GetTempPath();
            Directory.CreateDirectory(workspaceFolder);
            File.WriteAllText(Path.Combine(workspaceFolder, "app.py"), string.Empty);

            var aggregatedSettings1 = new MockWorkspaceSettings(
                new Dictionary<string, string> { { "Interpreter", null } }
            );
            var aggregatedSettings2 = new MockWorkspaceSettings(
                new Dictionary<string, string> { { "Interpreter", "Global|PythonCore|3.7" } }
            );
            var settingsManager = new MockWorkspaceSettingsManager(aggregatedSettings1);
            var workspace = new MockWorkspace(workspaceFolder, settingsManager);
            var workspaceService = new MockWorkspaceService(workspace);

            // Modify settings
            Action triggerDiscovery = () => {
                settingsManager.ChangeSettings(aggregatedSettings2);
            };

            TestTriggerDiscovery(workspaceService, triggerDiscovery);
        }

        [TestMethod, Priority(0)]
        public void WatchWorkspaceVirtualEnvCreated() {
            var python = PythonPaths.Python37_x64 ?? PythonPaths.Python37;

            var workspaceFolder = TestData.GetTempPath();
            Directory.CreateDirectory(workspaceFolder);
            File.WriteAllText(Path.Combine(workspaceFolder, "app.py"), string.Empty);

            string envFolder = Path.Combine(workspaceFolder, "env");

            var workspace = new MockWorkspace(workspaceFolder);
            var workspaceService = new MockWorkspaceService(workspace);

            // Create virtual env inside the workspace folder (one level from root)
            Action triggerDiscovery = () => {
                using (var p = ProcessOutput.RunHiddenAndCapture(python.InterpreterPath, "-m", "venv", envFolder)) {
                    Console.WriteLine(p.Arguments);
                    p.Wait();
                    Console.WriteLine(string.Join(Environment.NewLine, p.StandardOutputLines.Concat(p.StandardErrorLines)));
                    Assert.AreEqual(0, p.ExitCode);
                }
            };

            var configs = TestTriggerDiscovery(workspaceService, triggerDiscovery).ToArray();

            Assert.AreEqual(1, configs.Length);
            Assert.IsTrue(PathUtils.IsSamePath(
                Path.Combine(envFolder, "scripts", "python.exe"),
                configs[0].InterpreterPath
            ));
            Assert.AreEqual("Workspace|Workspace|env", configs[0].Id);
        }

        [TestMethod, Priority(0)]
        public void DetectLocalEnvOutsideWorkspace() {
            var python = PythonPaths.Python37_x64 ?? PythonPaths.Python37;

            var tempFolder = TestData.GetTempPath();
            var workspaceFolder = Path.Combine(tempFolder, "workspace");
            var envFolder = Path.Combine(tempFolder, "outside");

            Directory.CreateDirectory(workspaceFolder);
            File.WriteAllText(Path.Combine(workspaceFolder, "app.py"), string.Empty);

            // Create virtual env outside the workspace folder
            using (var p = ProcessOutput.RunHiddenAndCapture(python.InterpreterPath, "-m", "venv", envFolder)) {
                Console.WriteLine(p.Arguments);
                p.Wait();
                Console.WriteLine(string.Join(Environment.NewLine, p.StandardOutputLines.Concat(p.StandardErrorLines)));
                Assert.AreEqual(0, p.ExitCode);
            }

            // Normally a local virtual environment outside the workspace
            // wouldn't be detected, but it is when it's referenced from
            // the workspace python settings.
            var aggregatedSettings = new MockWorkspaceSettings(
                new Dictionary<string, string> { { "Interpreter", @"..\outside\scripts\python.exe" } }
            );
            var settingsManager = new MockWorkspaceSettingsManager(aggregatedSettings);
            var workspace = new MockWorkspace(workspaceFolder, settingsManager);
            var workspaceService = new MockWorkspaceService(workspace);

            using (var provider = new WorkspaceInterpreterFactoryProvider(workspaceService)) {
                var configs = provider.GetInterpreterConfigurations().ToArray();

                Assert.AreEqual(1, configs.Length);
                Assert.IsTrue(PathUtils.IsSamePath(
                    Path.Combine(envFolder, "scripts", "python.exe"),
                    configs[0].InterpreterPath
                ));
                Assert.AreEqual("Workspace|Workspace|outside", configs[0].Id);
            }
        }

        private static IEnumerable<InterpreterConfiguration> TestTriggerDiscovery(IVsFolderWorkspaceService workspaceService, Action triggerDiscovery) {
            using (var evt = new AutoResetEvent(false))
            using (var provider = new WorkspaceInterpreterFactoryProvider(workspaceService)) {
                // This initializes the provider, discovers the initial set
                // of factories and starts watching the filesystem.
                var configs = provider.GetInterpreterConfigurations();
                provider.DiscoveryStarted += (sender, e) => {
                    evt.Set();
                };
                triggerDiscovery();
                Assert.IsTrue(evt.WaitOne(5000), "Failed to trigger discovery.");
                return provider.GetInterpreterConfigurations();
            }
        }

        class MockWorkspaceService : IVsFolderWorkspaceService {
            public MockWorkspaceService(IWorkspace workspace) {
                CurrentWorkspace = workspace;
            }

            public IWorkspace CurrentWorkspace { get; private set; }

            public AsyncEvent<EventArgs> OnActiveWorkspaceChanged { get; set; }

            public void ChangeWorkspace(IWorkspace workspace) {
                CurrentWorkspace = workspace;
                OnActiveWorkspaceChanged?.InvokeAsync(this, EventArgs.Empty).DoNotWait();
            }
        }

        class MockWorkspace : IWorkspace {
            private readonly MockWorkspaceSettingsManager _settingsManager;

            public MockWorkspace(string location) {
                Location = location;
                _settingsManager = new MockWorkspaceSettingsManager();
            }

            public MockWorkspace(string location, MockWorkspaceSettingsManager settingsManager) {
                Location = location;
                _settingsManager = settingsManager;
            }

            public string Location { get; private set; }

            public JoinableTaskFactory JTF => throw new NotImplementedException();

            public Task DisposeAsync() {
                throw new NotImplementedException();
            }

            public Task<IReadOnlyList<IGrouping<Lazy<IFileContextActionProvider, IFileContextActionProviderMetadata>, IFileContextAction>>> GetActionsForContextsAsync(string filePath, IEnumerable<FileContext> fileContexts, CancellationToken cancellationToken = default(CancellationToken)) {
                throw new NotImplementedException();
            }

            public Task<IReadOnlyCollection<string>> GetDirectoriesAsync(string subPath, bool recursive, CancellationToken cancellationToken = default(CancellationToken)) {
                throw new NotImplementedException();
            }

            public Task<IReadOnlyList<IGrouping<Lazy<IFileContextActionProvider, IFileContextActionProviderMetadata>, IFileContextAction>>> GetFileContextActionsAsync(string path, IEnumerable<Guid> fileContextTypes, CancellationToken cancellationToken = default(CancellationToken)) {
                throw new NotImplementedException();
            }

            public Task<IReadOnlyList<IGrouping<Lazy<IFileContextActionProvider, IFileContextActionProviderMetadata>, IFileContextAction>>> GetFileContextActionsAsync<T>(string path, T context, IEnumerable<Guid> fileContextTypes, CancellationToken cancellationToken = default(CancellationToken)) {
                throw new NotImplementedException();
            }

            public Task<IReadOnlyList<IGrouping<Lazy<IFileContextProvider, IFileContextProviderMetadata>, FileContext>>> GetFileContextsAsync(string path, IEnumerable<Guid> fileContextTypes, CancellationToken cancellationToken = default(CancellationToken)) {
                throw new NotImplementedException();
            }

            public Task<IReadOnlyList<IGrouping<Lazy<IFileContextProvider, IFileContextProviderMetadata>, FileContext>>> GetFileContextsAsync<T>(string path, T context, IEnumerable<Guid> fileContextTypes, CancellationToken cancellationToken = default(CancellationToken)) {
                throw new NotImplementedException();
            }

            public Task<IReadOnlyList<Tuple<Lazy<ILanguageServiceProvider, ILanguageServiceProviderMetadata>, IReadOnlyCollection<FileContext>>>> GetFileContextsForLanguageServicesAsync(string filePath, CancellationToken cancellationToken = default(CancellationToken)) {
                throw new NotImplementedException();
            }

            public Task<IReadOnlyCollection<string>> GetFilesAsync(string subPath, bool recursive, CancellationToken cancellationToken = default(CancellationToken)) {
                throw new NotImplementedException();
            }

            public object GetService(Type serviceType) {
                if (serviceType == typeof(IWorkspaceSettingsManager)) {
                    return _settingsManager;
                }

                throw new NotImplementedException();
            }

            public Task<object> GetServiceAsync(Type serviceType) {
                if (serviceType == typeof(IWorkspaceSettingsManager)) {
                    return Task.FromResult<object>(_settingsManager);
                }

                throw new NotImplementedException();
            }

            public string MakeRelative(string path) {
                return PathUtils.GetRelativeFilePath(Location, path);
            }

            public string MakeRooted(string subPath) {
                return PathUtils.GetAbsoluteFilePath(Location, subPath);
            }
        }

        class MockWorkspaceSettingsManager : IWorkspaceSettingsManager {
            private IWorkspaceSettings _aggregatedSettings;

            public MockWorkspaceSettingsManager() {
                _aggregatedSettings = new MockWorkspaceSettings();
            }

            public MockWorkspaceSettingsManager(IWorkspaceSettings aggregatedSettings) {
                _aggregatedSettings = aggregatedSettings;
            }

            public AsyncEvent<WorkspaceSettingsChangedEventArgs> OnWorkspaceSettingsChanged { get; set; }

            public IWorkspaceSettings GetAggregatedSettings(string type, string scopePath = null) {
                return _aggregatedSettings;
            }

            public Task<IWorkspaceSettingsPersistance> GetPersistanceAsync(bool autoCommit) {
                throw new NotImplementedException();
            }

            public IWorkspaceSettingsSource GetSettings(string settingsFile) {
                throw new NotImplementedException();
            }

            public void ChangeSettings(IWorkspaceSettings settings) {
                _aggregatedSettings = settings;
                OnWorkspaceSettingsChanged.InvokeAsync(this, new WorkspaceSettingsChangedEventArgs(string.Empty, string.Empty)).DoNotWait();
            }
        }

        class MockWorkspaceSettings : IWorkspaceSettings {
            private readonly Dictionary<string, string> _keyValuePairs;

            public MockWorkspaceSettings() {
                _keyValuePairs = new Dictionary<string, string>();
            }

            public MockWorkspaceSettings(Dictionary<string, string> keyValuePairs) {
                _keyValuePairs = keyValuePairs;
            }

            public string ScopePath => throw new NotImplementedException();

            public IWorkspaceSettings Parent => null;

            public IEnumerable<string> GetKeys() => _keyValuePairs.Keys;

            public WorkspaceSettingsResult GetProperty<T>(string key, out T value, out IWorkspaceSettings originator, T defaultValue = default(T)) {
                value = defaultValue;
                originator = this;

                if (typeof(T) != typeof(string)) {
                    return WorkspaceSettingsResult.Error;
                }

                if (_keyValuePairs.TryGetValue(key, out string v)) {
                    value = (T)(object)v;
                }

                return WorkspaceSettingsResult.Success;
            }

            public WorkspaceSettingsResult GetProperty<T>(string key, out T value, T defaultValue = default(T)) {
                return GetProperty(key, out value, out _, defaultValue);
            }
        }
    }
}
