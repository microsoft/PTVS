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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Detects interpreters in user-created conda environments.
    /// </summary>
    /// <remarks>
    /// Uses %HOMEPATH%/.conda/environments.txt and `conda info --envs`.
    /// </remarks>
    [InterpreterFactoryId(FactoryProviderName)]
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [Export(typeof(CondaEnvironmentFactoryProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class CondaEnvironmentFactoryProvider : IPythonInterpreterFactoryProvider, IDisposable {
        private readonly Dictionary<string, PythonInterpreterInformation> _factories = new Dictionary<string, PythonInterpreterInformation>();
        internal const string FactoryProviderName = "CondaEnv";
        internal const string EnvironmentCompanyName = "CondaEnv";

        private bool _isDisposed;
        private int _ignoreNotifications;
        private bool _initialized;
        private readonly CPythonInterpreterFactoryProvider _globalProvider;
        private readonly ICondaLocatorProvider _condaLocatorProvider;
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly bool _watchFileSystem;
        private FileSystemWatcher _envsTxtWatcher;
        private FileSystemWatcher _condaFolderWatcher;
        private Timer _envsWatcherTimer;
        private string _userProfileFolder;
        private string _environmentsTxtFolder;
        private string _environmentsTxtPath;

        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] {
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        internal event EventHandler DiscoveryStarted;

        [ImportingConstructor]
        public CondaEnvironmentFactoryProvider(
            [Import] CPythonInterpreterFactoryProvider globalProvider,
            [Import] ICondaLocatorProvider condaLocatorProvider,
            [Import("Microsoft.VisualStudioTools.MockVsTests.IsMockVs", AllowDefault = true)] object isMockVs = null
        ) : this(globalProvider, condaLocatorProvider, ThreadHelper.JoinableTaskFactory, isMockVs == null) {
        }

        public CondaEnvironmentFactoryProvider(
            CPythonInterpreterFactoryProvider globalProvider,
            ICondaLocatorProvider condaLocatorProvider,
            JoinableTaskFactory joinableTaskFactory,
            bool watchFileSystem,
            string userProfileFolder = null) {
            _watchFileSystem = watchFileSystem;
            _globalProvider = globalProvider;
            _condaLocatorProvider = condaLocatorProvider;
            _joinableTaskFactory = joinableTaskFactory;
            _userProfileFolder = userProfileFolder;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CondaEnvironmentFactoryProvider() {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                _isDisposed = true;
                lock (_factories) {
                    if (_envsTxtWatcher != null) {
                        _envsTxtWatcher.Dispose();
                    }
                    if (_condaFolderWatcher != null) {
                        _condaFolderWatcher.Dispose();
                    }
                    if (_envsWatcherTimer != null) {
                        _envsWatcherTimer.Dispose();
                    }
                }
            }
        }

        private void EnsureInitialized() {
            if (_initialized) {
                return;
            }

            bool doDiscover = false;
            lock (_factories) {
                if (!_initialized) {
                    _initialized = true;
                    doDiscover = true;
                    try {
                        if (_userProfileFolder == null) {
                            _userProfileFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        }
                        _environmentsTxtFolder = Path.Combine(
                            _userProfileFolder,
                            ".conda"
                        );
                        _environmentsTxtPath = Path.Combine(
                            _environmentsTxtFolder,
                            "environments.txt"
                        );
                    } catch (ArgumentException) {
                    }

                    if (_watchFileSystem && !string.IsNullOrEmpty(_environmentsTxtPath)) {
                        _envsWatcherTimer = new Timer(EnvironmentsWatcherTimer_Elapsed);

                        if (!WatchForEnvironmentsTxtChanges()) {
                            WatchForCondaFolderCreation();
                        }
                    }
                }
            }

            if (doDiscover) {
                DiscoverInterpreterFactories();
            }
        }

        private bool WatchForEnvironmentsTxtChanges() {
            // Watch the file %HOMEPATH%/.conda/Environments.txt which
            // is updated by conda after a new environment is created/deleted.
            if (Directory.Exists(_environmentsTxtFolder)) {
                try {
                    _envsTxtWatcher = new FileSystemWatcher(_environmentsTxtFolder, "environments.txt");
                    _envsTxtWatcher.Changed += EnvironmentsTxtWatcher_Changed;
                    _envsTxtWatcher.Created += EnvironmentsTxtWatcher_Changed;
                    _envsTxtWatcher.EnableRaisingEvents = true;
                    return true;
                } catch (ArgumentException) {
                } catch (IOException) {
                }
            }

            return false;
        }

        private void WatchForCondaFolderCreation() {
            // When .conda does not exist, we watch for its creation
            // then watch for environments.txt changes once it's created.
            // The simpler alternative of using a recursive watcher on user
            // folder could lead to poor performance if there are lots of
            // files under the user folder.
            var watchedPath = Path.GetDirectoryName(_environmentsTxtFolder);
            if (Directory.Exists(watchedPath)) {
                try {
                    _condaFolderWatcher = new FileSystemWatcher(watchedPath, ".conda");
                    _condaFolderWatcher.Created += CondaFolderWatcher_Created;
                    _condaFolderWatcher.EnableRaisingEvents = true;
                } catch (ArgumentException) {
                } catch (IOException) {
                }
            }
        }

        private void EnvironmentsWatcherTimer_Elapsed(object state) {
            try {
                lock (_factories) {
                    _envsWatcherTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }

                DiscoverInterpreterFactories();
            } catch (ObjectDisposedException) {
            }
        }

        private void EnvironmentsTxtWatcher_Changed(object sender, FileSystemEventArgs e) {
            lock (_factories) {
                try {
                    _envsWatcherTimer.Change(1000, Timeout.Infinite);
                } catch (ObjectDisposedException) {
                }
            }
        }

        private void CondaFolderWatcher_Created(object sender, FileSystemEventArgs e) {
            lock (_factories) {
                try {
                    _envsWatcherTimer.Change(1000, Timeout.Infinite);
                } catch (ObjectDisposedException) {
                }

                if (_envsTxtWatcher == null) {
                    WatchForEnvironmentsTxtChanges();
                }
            }
        }

        private void DiscoverInterpreterFactories() {
            if (Volatile.Read(ref _ignoreNotifications) > 0) {
                return;
            }

            ForceDiscoverInterpreterFactories();
        }

        private void ForceDiscoverInterpreterFactories() {
            DiscoveryStarted?.Invoke(this, EventArgs.Empty);

            // Discover the available interpreters...
            bool anyChanged = false;

            var found = new List<PythonInterpreterInformation>();

            try {
                FindCondaEnvironments(found);
            } catch (ObjectDisposedException) {
                // We are aborting, so silently return with no results.
                return;
            }

            var uniqueIds = new HashSet<string>(found.Select(i => i.Configuration.Id));

            // Then update our cached state with the lock held.
            lock (_factories) {
                foreach (var info in found) {
                    PythonInterpreterInformation existingInfo;
                    if (!_factories.TryGetValue(info.Configuration.Id, out existingInfo) ||
                        info.Configuration != existingInfo.Configuration) {
                        _factories[info.Configuration.Id] = info;
                        anyChanged = true;
                    }
                }

                // Remove any factories we had before and no longer see...
                foreach (var unregistered in _factories.Keys.Except(uniqueIds).ToArray()) {
                    _factories.Remove(unregistered);
                    anyChanged = true;
                }
            }

            if (anyChanged) {
                OnInterpreterFactoriesChanged();
            }
        }

        internal async static Task<CondaInfoResult> ExecuteCondaInfoAsync(string condaPath) {
            var activationVars = await CondaUtils.GetActivationEnvironmentVariablesForRootAsync(condaPath);
            var envVars = activationVars.Union(UnbufferedEnv).ToArray();

            var args = new[] { "info", "--json" };
            using (var output = ProcessOutput.Run(condaPath, args, null, envVars, false, null)) {
                output.Wait();
                if (output.ExitCode == 0) {
                    var json = string.Join(Environment.NewLine, output.StandardOutputLines);
                    try {
                        return JsonConvert.DeserializeObject<CondaInfoResult>(json);
                    } catch (JsonException ex) {
                        Debug.WriteLine("Failed to parse: {0}".FormatInvariant(ex.Message));
                        Debug.WriteLine(json);
                        return null;
                    }
                }
                return null;
            }
        }

        internal class CondaInfoResult {
            [JsonProperty("envs")]
            public string[] EnvironmentFolders = null;

            [JsonProperty("envs_dirs")]
            public string[] EnvironmentRootFolders = null;

            [JsonProperty("root_prefix")]
            public string RootPrefixFolder = null;
        }

        private void FindCondaEnvironments(List<PythonInterpreterInformation> envs) {
            var mainCondaExePath = _condaLocatorProvider?.FindLocator()?.CondaExecutablePath;
            if (!string.IsNullOrEmpty(mainCondaExePath)) {
                envs.AddRange(FindCondaEnvironments(mainCondaExePath));
            }
        }

        private IReadOnlyList<PythonInterpreterInformation> FindCondaEnvironments(string condaPath) {
            var condaInfoResult = _joinableTaskFactory.Run(() => ExecuteCondaInfoAsync(condaPath));
            if (condaInfoResult != null) {
                // We skip the root to avoid duplicate entries, root is
                // discovered by CPythonInterpreterFactoryProvider already.
                // Older versions of `conda info` used to not return the root.
                return condaInfoResult.EnvironmentFolders
                    .AsParallel()
                    .Where(folder =>
                        Directory.Exists(folder) &&
                        !PathUtils.IsSameDirectory(folder, condaInfoResult.RootPrefixFolder)
                    )
                    .Select(folder => CreateEnvironmentInfo(folder))
                    .Where(env => env != null)
                    .ToList();
            }

            return Enumerable.Empty<PythonInterpreterInformation>().ToList();
        }

        private static PythonInterpreterInformation CreateEnvironmentInfo(string prefixPath) {
            var name = Path.GetFileName(prefixPath);
            var description = name;
            var vendor = Strings.CondaEnvironmentDescription;
            var vendorUrl = string.Empty;
            var supportUrl = string.Empty;
            var interpreterPath = Path.Combine(prefixPath, CondaEnvironmentFactoryConstants.ConsoleExecutable);
            var windowsInterpreterPath = Path.Combine(prefixPath, CondaEnvironmentFactoryConstants.WindowsExecutable);

            if (!File.Exists(interpreterPath)) {
                return null;
            }

            var arch = CPythonInterpreterFactoryProvider.ArchitectureFromExe(interpreterPath);
            var version = CPythonInterpreterFactoryProvider.VersionFromSysVersionInfo(interpreterPath);

            var config = new VisualStudioInterpreterConfiguration(
                CondaEnvironmentFactoryConstants.GetInterpreterId(CondaEnvironmentFactoryProvider.EnvironmentCompanyName, name),
                description,
                prefixPath,
                interpreterPath,
                windowsInterpreterPath,
                CondaEnvironmentFactoryConstants.PathEnvironmentVariableName,
                arch,
                version
            );

            config.SwitchToFullDescription();

            var unique = new PythonInterpreterInformation(
                config,
                vendor,
                vendorUrl,
                supportUrl
            );
            return unique;
        }

        #region IPythonInterpreterProvider Members

        public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations() {
            EnsureInitialized();

            lock (_factories) {
                return _factories.Values.Select(x => x.Configuration).ToArray();
            }
        }

        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
            EnsureInitialized();

            PythonInterpreterInformation info;
            lock (_factories) {
                _factories.TryGetValue(id, out info);
            }

            return info?.GetOrCreateFactory(CreateFactory);
        }

        private IPythonInterpreterFactory CreateFactory(PythonInterpreterInformation info) {
            return InterpreterFactoryCreator.CreateInterpreterFactory(
                info.Configuration,
                new InterpreterFactoryCreationOptions {
                    WatchFileSystem = true,
                }
            );
        }

        private EventHandler _interpFactoriesChanged;
        public event EventHandler InterpreterFactoriesChanged {
            add {
                EnsureInitialized();
                _interpFactoriesChanged += value;
            }
            remove {
                _interpFactoriesChanged -= value;
            }
        }

        private void OnInterpreterFactoriesChanged() {
            _interpFactoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public object GetProperty(string id, string propName) {
            PythonInterpreterInformation info;

            switch (propName) {
                case PythonRegistrySearch.CompanyPropertyKey:
                    lock (_factories) {
                        if (_factories.TryGetValue(id, out info)) {
                            return info.Vendor;
                        }
                    }
                    break;
                case PythonRegistrySearch.SupportUrlPropertyKey:
                    lock (_factories) {
                        if (_factories.TryGetValue(id, out info)) {
                            return info.SupportUrl;
                        }
                    }
                    break;
                case "PersistInteractive":
                    return true;
            }

            return null;
        }

        internal static bool IsCondaEnv(IPythonInterpreterFactory factory) {
            return factory.Configuration.Id.StartsWithOrdinal(CondaEnvironmentFactoryProvider.FactoryProviderName + "|");
        }

        internal static bool IsCondaEnv(string id) {
            return id.StartsWithOrdinal(CondaEnvironmentFactoryProvider.FactoryProviderName + "|");
        }

        internal static bool IsCondaEnv(string id, string expectedName) {
            if (IsCondaEnv(id)) {
                return false;
            }

            string name = NameFromId(id);
            return string.CompareOrdinal(name, expectedName) == 0;
        }

        internal static bool IsCondaEnv(IPythonInterpreterFactory factory, string expectedName) {
            if (!IsCondaEnv(factory)) {
                return false;
            }

            string name = NameFromId(factory.Configuration.Id);
            return string.CompareOrdinal(name, expectedName) == 0;
        }

        internal static string NameFromId(string id) {
            if (CondaEnvironmentFactoryConstants.TryParseInterpreterId(id, out _, out string name)) {
                return name;
            }
            return null;
        }

        #endregion

        private sealed class DiscoverOnDispose : IDisposable {
            private readonly CondaEnvironmentFactoryProvider _provider;
            private readonly bool _forceDiscovery;

            public DiscoverOnDispose(CondaEnvironmentFactoryProvider provider, bool forceDiscovery) {
                _provider = provider;
                _forceDiscovery = forceDiscovery;
                Interlocked.Increment(ref _provider._ignoreNotifications);
            }

            public void Dispose() {
                Interlocked.Decrement(ref _provider._ignoreNotifications);
                if (_forceDiscovery) {
                    _provider.ForceDiscoverInterpreterFactories();
                } else {
                    _provider.DiscoverInterpreterFactories();
                }
            }
        }

        internal IDisposable SuppressDiscoverFactories(bool forceDiscoveryOnDispose) {
            return new DiscoverOnDispose(this, forceDiscoveryOnDispose);
        }
    }
}
