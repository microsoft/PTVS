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
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter {
    [Export(typeof(IInterpreterOptionsService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    sealed class InterpreterOptionsService : IInterpreterOptionsService {
        private readonly Lazy<IInterpreterRegistryService> _registryService;
        private readonly Lazy<CPythonInterpreterFactoryProvider> _cpythonProvider;
        private readonly Lazy<IPackageManagerProvider>[] _packageManagerProviders;
        private bool _defaultInterpreterWatched;
        private string _defaultInterpreterId;
        IPythonInterpreterFactory _defaultInterpreter;
        private readonly object _defaultInterpreterLock = new object();
        private EventHandler _defaultInterpreterChanged;

        private int _requireDefaultInterpreterChangeEvent;
        private int _suppressDefaultInterpreterChangeEvent;

        // The second is a static registry entry for the local machine and/or
        // the current user (HKCU takes precedence), intended for being set by
        // other installers.
        private const string DefaultInterpreterOptionsCollection = @"SOFTWARE\\Microsoft\\PythonTools\\Interpreters";
        private const string DefaultInterpreterSetting = "DefaultInterpreter";

        private const string PathKey = "ExecutablePath";
        private const string WindowsPathKey = "WindowedExecutablePath";
        private const string ArchitectureKey = "SysArchitecture";
        private const string VersionKey = "SysVersion";
        private const string PathEnvVarKey = "PathEnvironmentVariable";
        private const string DescriptionKey = "DisplayName";

        private const string CustomCompany = "VisualStudio";
        private const string CustomInterpreterKey = "SOFTWARE\\Python\\" + CustomCompany;

        [ImportingConstructor]
        public InterpreterOptionsService(
            [Import] Lazy<IInterpreterRegistryService> registryService,
            [Import] Lazy<CPythonInterpreterFactoryProvider> cpythonProvider,
            [ImportMany] Lazy<IPackageManagerProvider>[] packageManagerProviders
        ) {
            _registryService = registryService;
            _cpythonProvider = cpythonProvider;
            _packageManagerProviders = packageManagerProviders;
        }


        private void InitializeDefaultInterpreterWatcher() {
            Debug.Assert(!_defaultInterpreterWatched);

            _registryService.Value.InterpretersChanged += Provider_InterpreterFactoriesChanged;

            RegistryHive hive = RegistryHive.CurrentUser;
            RegistryView view = RegistryView.Default;
            if (RegistryWatcher.Instance.TryAdd(
                hive, view, DefaultInterpreterOptionsCollection,
                DefaultInterpreterRegistry_Changed,
                recursive: false, notifyValueChange: true, notifyKeyChange: false
            ) == null) {
                // DefaultInterpreterOptions subkey does not exist yet, so
                // create it and then start the watcher.
                SaveDefaultInterpreterId();

                RegistryWatcher.Instance.Add(
                    hive, view, DefaultInterpreterOptionsCollection,
                    DefaultInterpreterRegistry_Changed,
                    recursive: false, notifyValueChange: true, notifyKeyChange: false
                );
            }
            _defaultInterpreterWatched = true;
        }

        private void Provider_InterpreterFactoriesChanged(object sender, EventArgs e) {
            // reload the default interpreter ID and see if it changed...
            SetDefaultInterpreter(LoadDefaultInterpreterId(), null);
        }

        private void DefaultInterpreterRegistry_Changed(object sender, RegistryChangedEventArgs e) {
            try {
                SetDefaultInterpreter(LoadDefaultInterpreterId(), null);
            } catch (InvalidComObjectException) {
                // Race between VS closing and accessing the settings store.
            } catch (Exception ex) {
                try {
                    //ActivityLog.LogError(
                    //    "Python Tools for Visual Studio",
                    //    string.Format("Exception updating default interpreter: {0}", ex)
                    //);
                } catch (InvalidOperationException) {
                    // Can't get the activity log service either. This probably
                    // means we're being used from outside of VS, but also
                    // occurs during some unit tests. We want to debug this if
                    // possible, but generally avoid crashing.
                    Debug.Fail(ex.ToString());
                }
            }
        }

        private string LoadDefaultInterpreterId() {
            string id;
            using (var interpreterOptions = Registry.CurrentUser.OpenSubKey(DefaultInterpreterOptionsCollection)) {
                id = interpreterOptions?.GetValue(DefaultInterpreterSetting) as string;
            }

            InterpreterConfiguration newDefault = null;
            if (!string.IsNullOrEmpty(id)) {
                newDefault = _registryService.Value.FindConfiguration(id);
            }

            if (newDefault == null) {
                var defaultConfig = _registryService.Value.Configurations
                    .Where(PythonInterpreterFactoryExtensions.IsRunnable)
                    .LastOrDefault(c => c.CanBeAutoDefault());
                id = defaultConfig?.Id;

                if (!string.IsNullOrEmpty(id)) {
                    using (var interpreterOptions = Registry.CurrentUser.CreateSubKey(DefaultInterpreterOptionsCollection)) {
                        interpreterOptions?.SetValue(DefaultInterpreterSetting, id);
                    }
                }
            }

            return id ?? string.Empty;
        }

        private void SaveDefaultInterpreterId() {
            var id = _defaultInterpreterId;

            BeginSuppressDefaultInterpreterChangeEvent();
            try {
                using (var interpreterOptions = Registry.CurrentUser.CreateSubKey(DefaultInterpreterOptionsCollection, true)) {
                    if (string.IsNullOrEmpty(id)) {
                        interpreterOptions.DeleteValue(DefaultInterpreterSetting, false);
                    } else {
                        Debug.Assert(!InterpreterRegistryConstants.IsNoInterpretersFactory(id));
                        interpreterOptions.SetValue(DefaultInterpreterSetting, id);
                    }
                }
            } finally {
                EndSuppressDefaultInterpreterChangeEvent();
            }
        }

        private void SetDefaultInterpreter(string id, IPythonInterpreterFactory factory) {
            id = id ?? factory?.Configuration?.Id ?? string.Empty;

            BeginSuppressDefaultInterpreterChangeEvent();
            try {
                lock (_defaultInterpreterLock) {
                    if (_defaultInterpreterId == id) {
                        return;
                    }
                    if (string.IsNullOrEmpty(id) && _defaultInterpreterId == null) {
                        return;
                    }

                    if (string.IsNullOrEmpty(id)) {
                        _defaultInterpreter = null;
                    } else {
                        _defaultInterpreter = factory;
                    }
                    _defaultInterpreterId = id;
                    SaveDefaultInterpreterId();
                }
                OnDefaultInterpreterChanged();
            } finally {
                EndSuppressDefaultInterpreterChangeEvent();
            }
        }

        public string DefaultInterpreterId {
            get {
                if (_defaultInterpreterId == null) {
                    _defaultInterpreterId = LoadDefaultInterpreterId();
                }
                return _defaultInterpreterId;
            }
            set {
                SetDefaultInterpreter(value, null);
            }
        }

        public IPythonInterpreterFactory DefaultInterpreter {
            get {
                IPythonInterpreterFactory factory = _defaultInterpreter;
                if (factory != null) {
                    // We've loaded and found the factory already
                    return factory;
                }
                // FxCop won't let us compare to String.Empty, so we do
                // two comparisons for "performance reasons"
                if (_defaultInterpreterId != null && string.IsNullOrEmpty(_defaultInterpreterId)) {
                    // We've loaded, and there's nothing there
                    return _registryService.Value.NoInterpretersValue;
                }
                lock (_defaultInterpreterLock) {
                    if (_defaultInterpreterId == null) {
                        // We haven't loaded yet
                        _defaultInterpreterId = LoadDefaultInterpreterId();
                    }
                    if (_defaultInterpreter == null && !string.IsNullOrEmpty(_defaultInterpreterId)) {
                        // We've loaded but haven't found the factory yet
                        _defaultInterpreter = _registryService.Value.FindInterpreter(_defaultInterpreterId);
                    }
                }
                return _defaultInterpreter ?? _registryService.Value.NoInterpretersValue;
            }
            set {
                var newDefault = value;
                if (newDefault == _registryService.Value.NoInterpretersValue) {
                    newDefault = null;
                }
                SetDefaultInterpreter(null, newDefault);
            }
        }

        private void BeginSuppressDefaultInterpreterChangeEvent() {
            Interlocked.Increment(ref _suppressDefaultInterpreterChangeEvent);
        }

        private void EndSuppressDefaultInterpreterChangeEvent() {
            if (0 == Interlocked.Decrement(ref _suppressDefaultInterpreterChangeEvent) &&
                0 != Interlocked.Exchange(ref _requireDefaultInterpreterChangeEvent, 0)) {
                _defaultInterpreterChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnDefaultInterpreterChanged() {
            if (Volatile.Read(ref _suppressDefaultInterpreterChangeEvent) != 0) {
                Volatile.Write(ref _requireDefaultInterpreterChangeEvent, 1);
                return;
            }

            _defaultInterpreterChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler DefaultInterpreterChanged {
            add {
                if (!_defaultInterpreterWatched) {
                    InitializeDefaultInterpreterWatcher();
                }
                _defaultInterpreterChanged += value;
            }
            remove {
                _defaultInterpreterChanged -= value;
            }
        }

        public string AddConfigurableInterpreter(string name, InterpreterConfiguration config) {
            using (_cpythonProvider.Value.SuppressDiscoverFactories()) {
                var collection = CustomInterpreterKey + "\\" + name;
                using (var key = Registry.CurrentUser.CreateSubKey(CustomInterpreterKey, true)) {
                    key.SetValue(DescriptionKey, Strings.CustomEnvironmentLabel);
                }

                using (var key = Registry.CurrentUser.CreateSubKey(collection, true)) {
                    if (config.Architecture != InterpreterArchitecture.Unknown) {
                        key.SetValue(ArchitectureKey, config.Architecture.ToPEP514());
                    } else {
                        key.DeleteValue(ArchitectureKey, false);
                    }
                    if (config.Version != new Version()) {
                        key.SetValue(VersionKey, config.Version.ToString());
                    } else {
                        key.DeleteValue(VersionKey, false);
                    }
                    if (!string.IsNullOrEmpty(config.PathEnvironmentVariable)) {
                        key.SetValue(PathEnvVarKey, config.PathEnvironmentVariable);
                    } else {
                        key.DeleteValue(PathEnvVarKey, false);
                    }
                    if (!string.IsNullOrEmpty(config.Description)) {
                        key.SetValue(DescriptionKey, config.Description);
                    } else {
                        key.DeleteValue(DescriptionKey, false);
                    }

                    var vsConfig = (VisualStudioInterpreterConfiguration)config;
                    using (var installPath = key.CreateSubKey("InstallPath")) {
                        string exePath = config.InterpreterPath ?? vsConfig.WindowsInterpreterPath ?? "";
                        if (!string.IsNullOrEmpty(vsConfig.PrefixPath)) {
                            installPath.SetValue("", vsConfig.PrefixPath);
                        } else if (!string.IsNullOrWhiteSpace(exePath)) {
                            installPath.SetValue("", Path.GetDirectoryName(exePath));
                        }
                        installPath.SetValue(WindowsPathKey, config.GetWindowsInterpreterPath() ?? string.Empty);
                        installPath.SetValue(PathKey, config.InterpreterPath ?? string.Empty);
                    }
                }
            }

            return CPythonInterpreterFactoryConstants.GetInterpreterId(CustomCompany, name);
        }

        public void RemoveConfigurableInterpreter(string id) {
            string company, tag;
            if (CPythonInterpreterFactoryConstants.TryParseInterpreterId(id, out company, out tag) &&
                company == CustomCompany) {
                var collection = CustomInterpreterKey + "\\" + tag;
                using (_cpythonProvider.Value.SuppressDiscoverFactories()) {
                    try {
                        Registry.CurrentUser.DeleteSubKeyTree(collection);

                        _cpythonProvider.Value.DiscoverInterpreterFactories();
                    } catch (ArgumentException) {
                    }
                }
            }
        }

        public bool IsConfigurable(string id) {
            string company, tag;
            return CPythonInterpreterFactoryConstants.TryParseInterpreterId(id, out company, out tag) &&
                company == CustomCompany;
        }

        public IEnumerable<IPackageManager> GetPackageManagers(IPythonInterpreterFactory factory) {
            if (_packageManagerProviders == null || !_packageManagerProviders.Any() || factory == null) {
                return Enumerable.Empty<IPackageManager>();
            }

            return _packageManagerProviders.SelectMany(p => p.Value.GetPackageManagers(factory))
                .GroupBy(p => p.UniqueKey)
                .Select(g => g.OrderBy(p => p.Priority).FirstOrDefault())
                .Where(p => p != null)
                .OrderBy(p => p.Priority)
                .ToArray();
        }

    }
}
