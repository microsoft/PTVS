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
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Evaluator;
using Microsoft.VisualStudio.Workspace.Intellisense;
using Microsoft.VisualStudio.Workspace.Settings;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsTests {
    internal class WorkspaceTestHelper {
        public const string PythonNoId = null;
        public const string Python27Id = "Global|PythonCore|2.7";
        public const string Python36Id = "Global|PythonCore|3.6";
        public const string Python37Id = "Global|PythonCore|3.7";
        public const string PythonUnavailableId = "Global|PythonCore|2.8";
        public const string Evaluated_Result = "Evaluated_Result";

        private static InterpreterConfiguration Python27Config = new InterpreterConfiguration(Python27Id, "Mock 2.7");
        private static InterpreterConfiguration Python36Config = new InterpreterConfiguration(Python36Id, "Mock 3.6");
        private static InterpreterConfiguration Python37Config = new InterpreterConfiguration(Python37Id, "Mock 3.7");

        public static IPythonInterpreterFactory Python27Factory = new MockPythonInterpreterFactory(Python27Config);
        public static IPythonInterpreterFactory Python36Factory = new MockPythonInterpreterFactory(Python36Config);
        public static IPythonInterpreterFactory Python37Factory = new MockPythonInterpreterFactory(Python37Config);

        public static IPythonInterpreterFactory DefaultFactory = Python36Factory;

        public static IEnumerable<IPythonInterpreterFactory> AllFactories = new[] {
            Python27Factory,
            Python36Factory,
            Python37Factory,
        };

        public static MockWorkspace CreateMockWorkspace(string workspaceFolder, string interpreterSetting) {
            var aggregatedSettings = new MockWorkspaceSettings(
                new Dictionary<string, object> { 
                    { "Interpreter", interpreterSetting },
                    { "SearchPaths", new String[] {"${workspaceRoot}" } }
                }
            );
            var settingsManager = new MockWorkspaceSettingsManager(aggregatedSettings);
            return new MockWorkspace(workspaceFolder, settingsManager);
        }

        public static string CreateWorkspaceFolder() {
            var workspaceFolder = TestData.GetTempPath();
            Directory.CreateDirectory(workspaceFolder);
            File.WriteAllText(Path.Combine(workspaceFolder, "app.py"), string.Empty);
            return workspaceFolder;
        }

        public class MockOptionsService : IInterpreterOptionsService {
            public MockOptionsService(IPythonInterpreterFactory factory) {
                DefaultInterpreter = factory;
            }

            public IPythonInterpreterFactory DefaultInterpreter { get; set; }

            public string DefaultInterpreterId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public event EventHandler DefaultInterpreterChanged;

            public string AddConfigurableInterpreter(string name, InterpreterConfiguration config) {
                throw new NotImplementedException();
            }

            public IEnumerable<IPackageManager> GetPackageManagers(IPythonInterpreterFactory factory) {
                throw new NotImplementedException();
            }

            public bool IsConfigurable(string id) {
                throw new NotImplementedException();
            }

            public void RemoveConfigurableInterpreter(string id) {
                throw new NotImplementedException();
            }

            internal void SimulateChangeDefaultInterpreter(IPythonInterpreterFactory factory) {
                DefaultInterpreter = factory;
                DefaultInterpreterChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public class MockRegistryService : IInterpreterRegistryService {
            public MockRegistryService(IEnumerable<IPythonInterpreterFactory> interpreters) {
                Interpreters = interpreters ?? Enumerable.Empty<IPythonInterpreterFactory>();

            }

            public IEnumerable<IPythonInterpreterFactory> Interpreters { get; private set; }

            public IEnumerable<InterpreterConfiguration> Configurations {
                get {
                    return Interpreters.Select(x => x.Configuration);
                }
            }

            public IEnumerable<IPythonInterpreterFactory> InterpretersOrDefault => throw new NotImplementedException();

            public IPythonInterpreterFactory NoInterpretersValue => throw new NotImplementedException();

            public event EventHandler InterpretersChanged;
            public event EventHandler CondaInterpreterDiscoveryCompleted {
                add { }
                remove { }
            }

            public void BeginSuppressInterpretersChangedEvent() {
                throw new NotImplementedException();
            }

            public void EndSuppressInterpretersChangedEvent() {
                throw new NotImplementedException();
            }

            public InterpreterConfiguration FindConfiguration(string id) {
                throw new NotImplementedException();
            }

            public IPythonInterpreterFactory FindInterpreter(string id) {
                return Interpreters.SingleOrDefault(f => f.Configuration.Id == id);
            }

            public object GetProperty(string id, string propName) {
                throw new NotImplementedException();
            }

            internal void SimulateChangeInterpreters(IEnumerable<IPythonInterpreterFactory> interpreters) {
                Interpreters = interpreters ?? Enumerable.Empty<IPythonInterpreterFactory>();
                InterpretersChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public class MockWorkspaceService : IVsFolderWorkspaceService {
            public MockWorkspaceService(IWorkspace workspace) {
                CurrentWorkspace = workspace;
            }

            public IWorkspace CurrentWorkspace { get; private set; }

            public AsyncEvent<EventArgs> OnActiveWorkspaceChanged { get; set; }

            public void SimulateChangeWorkspace(IWorkspace workspace) {
                CurrentWorkspace = workspace;
                OnActiveWorkspaceChanged?.InvokeAsync(this, EventArgs.Empty).DoNotWait();
            }
        }

        public class MockWorkspace : IWorkspace {
            public MockWorkspace(string location) : this(location, new MockWorkspaceSettingsManager()){
            }

            public MockWorkspace(string location, MockWorkspaceSettingsManager settingsManager) {
                Location = location;
                SettingsManager = settingsManager;
                PropertyEvaluator = new MockPropertyEvaluatorService();
            }

            internal MockWorkspaceSettingsManager SettingsManager { get; }

            internal MockPropertyEvaluatorService PropertyEvaluator { get; }

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
                    return SettingsManager;
                }

                throw new NotImplementedException();
            }

            public Task<object> GetServiceAsync(Type serviceType) {
                if (serviceType == typeof(IWorkspaceSettingsManager)) {
                    return Task.FromResult<object>(SettingsManager);
                }
                if (serviceType == typeof(IPropertyEvaluatorService)) {
                    return Task.FromResult<object>(PropertyEvaluator);
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

        public class MockWorkspaceSettingsManager : IWorkspaceSettingsManager {
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

            public void SimulateChangeSettings(IWorkspaceSettings settings) {
                _aggregatedSettings = settings;
                OnWorkspaceSettingsChanged.InvokeAsync(this, new WorkspaceSettingsChangedEventArgs(string.Empty, string.Empty)).DoNotWait();
            }
        }

        public class MockPropertyEvaluatorService : IPropertyEvaluatorService {
            public AsyncEvent<PropertyVariablesChangedEventArgs> OnPropertyVariablesChanged { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public IConfiguredPropertyEvaluator CreateConfiguredPropertyEvaluator(string fileScopePath, IReadOnlyCollection<string> inheritEnvironments, IEnumerable<Tuple<IPropertyEvaluator, IPropertyEvaluatorMetadata>> localPropertyEvaluators) => throw new NotImplementedException();
            public Tuple<IPropertyEvaluator, IPropertyEvaluatorMetadata> CreatePropertyGroupEvaluator(IReadOnlyCollection<PropertyGroup> propertyGroups) => throw new NotImplementedException();
            public Tuple<IPropertyEvaluator, IPropertyEvaluatorMetadata> CreatePropertySettingsEvaluator(IPropertySettings source, int groupPriority, int priority) => throw new NotImplementedException();
            public Tuple<IPropertyEvaluator, IPropertyEvaluatorMetadata> CreateWorkspaceSettingsSourceEvaluator(IWorkspaceSettingsSource source, int groupPriority, int priority) => throw new NotImplementedException();
            public EvaluationResult Evaluate(string content, string fileScopePath, IReadOnlyCollection<string> inheritEnvironments, IEnumerable<Tuple<IPropertyEvaluator, IPropertyEvaluatorMetadata>> localPropertyEvaluators) {
                return new EvaluationResult(true, content.Contains("${") ? Evaluated_Result : content, new EvaluationError[0]);
            }
            public IReadOnlyCollection<string> GetProperties(string prefixNamespace, IReadOnlyCollection<string> inheritEnvironments) => throw new NotImplementedException();
            public IReadOnlyCollection<Tuple<IPropertyEvaluator, IPropertyEvaluatorMetadata>> GetPropertyEvaluators(string prefixNamespace) => throw new NotImplementedException();
            public IReadOnlyCollection<PropertyGroup> ParsePropertyGroups(string jsonPropertyGroups, GroupDefaultOptions defaultOptions) => throw new NotImplementedException();
            public IReadOnlyCollection<PropertyGroup> ParsePropertyGroups(IWorkspaceSettingsSource[] propertyGroups, GroupDefaultOptions defaultOptions) => throw new NotImplementedException();
            public IReadOnlyCollection<PropertyGroup> ParsePropertyGroups(IPropertySettings[] propertyGroupArray, GroupDefaultOptions defaultOptions) => throw new NotImplementedException();
            public IReadOnlyCollection<MatchPropertyResult> SelectPropertyEvaluators(PropertyContext propertyContext, IEnumerable<Tuple<IPropertyEvaluator, IPropertyEvaluatorMetadata>> localPropertyEvaluators) => throw new NotImplementedException();
        }

        public class MockWorkspaceSettings : IWorkspaceSettings {
            private readonly Dictionary<string, object> _keyValuePairs;

            public MockWorkspaceSettings() {
                _keyValuePairs = new Dictionary<string, object>();
            }

            public MockWorkspaceSettings(Dictionary<string, object> keyValuePairs) {
                _keyValuePairs = keyValuePairs;
            }

            public string ScopePath => throw new NotImplementedException();

            public IWorkspaceSettings Parent => null;

            public IEnumerable<string> GetKeys() => _keyValuePairs.Keys;

            public WorkspaceSettingsResult GetProperty<T>(string key, out T value, out IWorkspaceSettings originator, T defaultValue = default(T)) {
                value = defaultValue;
                originator = this;

                if (typeof(T) != typeof(string) && typeof(T) != typeof(String [])) {
                    return WorkspaceSettingsResult.Error;
                }

                if (_keyValuePairs.TryGetValue(key, out object v)) {
                    value = (T)(object)v;
                }

                return value != null ? WorkspaceSettingsResult.Success : WorkspaceSettingsResult.Error;
            }

            public WorkspaceSettingsResult GetProperty<T>(string key, out T value, T defaultValue = default(T)) {
                return GetProperty(key, out value, out _, defaultValue);
            }
        }

        public class MockWorkspaceContextProvider : IPythonWorkspaceContextProvider {
            public MockWorkspaceContextProvider(IPythonWorkspaceContext workspaceContext) {
                Workspace = workspaceContext;
            }

            public void SimulateChangeWorkspace(IPythonWorkspaceContext context) {
                WorkspaceClosing?.Invoke(this, new PythonWorkspaceContextEventArgs(Workspace));
                WorkspaceClosed?.Invoke(this, new PythonWorkspaceContextEventArgs(Workspace));
                Workspace = context;
                WorkspaceOpening?.Invoke(this, new PythonWorkspaceContextEventArgs(Workspace));
                WorkspaceInitialized?.Invoke(this, new PythonWorkspaceContextEventArgs(Workspace));
            }

            public IPythonWorkspaceContext Workspace { get; private set; }

            public event EventHandler<PythonWorkspaceContextEventArgs> WorkspaceInitialized;
            public event EventHandler<PythonWorkspaceContextEventArgs> WorkspaceOpening;
            public event EventHandler<PythonWorkspaceContextEventArgs> WorkspaceClosing;
            public event EventHandler<PythonWorkspaceContextEventArgs> WorkspaceClosed;
        }

        public class MockWorkspaceContext : IPythonWorkspaceContext {
            private readonly WorkspaceTestHelper.MockWorkspace _workspace;
            private string _interpreterSetting;

            public MockWorkspaceContext(WorkspaceTestHelper.MockWorkspace workspace, string interpreterSetting = null) {
                _workspace = workspace;
                _interpreterSetting = interpreterSetting;
            }

            public string WorkspaceName => throw new NotImplementedException();

            public string Location => _workspace.Location;

            public IPythonInterpreterFactory CurrentFactory => throw new NotImplementedException();

            public bool IsCurrentFactoryDefault => throw new NotImplementedException();

#pragma warning disable CS0067
            public event EventHandler InterpreterSettingChanged;
            public event EventHandler SearchPathsSettingChanged;
            public event EventHandler ActiveInterpreterChanged;
            public event EventHandler TestSettingChanged;
            public event EventHandler IsTrustedChanged;
            public event EventHandler IsTrustedQueried;
#pragma warning restore CS0067

            public void Dispose() {
            }

            public void SimulateChangeInterpreterSetting(string setting) {
                _interpreterSetting = setting;
                InterpreterSettingChanged?.Invoke(this, EventArgs.Empty);
            }
            public IEnumerable<string> GetAbsoluteSearchPaths() {
                throw new NotImplementedException();
            }

            public string GetEnvironmentYmlPath() {
                throw new NotImplementedException();
            }

            public string GetRequirementsTxtPath() {
                throw new NotImplementedException();
            }

            public string MakeRooted(string path) => PathUtils.GetAbsoluteFilePath(Location, path);

            public string ReadInterpreterSetting() => _interpreterSetting;

            public Task SetInterpreterAsync(string interpreter) {
                throw new NotImplementedException();
            }

            public Task SetInterpreterFactoryAsync(IPythonInterpreterFactory factory) {
                throw new NotImplementedException();
            }

            public void AddActionOnClose(object key, Action<object> action) {
                throw new NotImplementedException();
            }

            public string GetStringProperty(string propertyName) {
                throw new NotImplementedException();
            }

            public bool? GetBoolProperty(string propertyName) {
                throw new NotImplementedException();
            }

            public Task SetPropertyAsync(string propertyName, string propertyVal) {
                throw new NotImplementedException();
            }

            public Task SetPropertyAsync(string propertyName, bool? propertyVal) {
                throw new NotImplementedException();
            }

            public bool IsTrusted {
                get => true;
                set {
                }
            }

            public IEnumerable<string> EnumerateUserFiles(Predicate<string> predicate) => throw new NotImplementedException();
        }
    }
}
