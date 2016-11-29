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
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter {
    [Export(typeof(IInterpreterOptionsService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    sealed class InterpreterOptionsService : IInterpreterOptionsService {
        private readonly Lazy<IInterpreterRegistryService> _registryService;
        private readonly Lazy<CPythonInterpreterFactoryProvider> _cpythonProvider;
        private bool _defaultInterpreterWatched;
        private string _defaultInterpreterId;
        IPythonInterpreterFactory _defaultInterpreter;
        private readonly object _defaultInterpreterLock = new object();
        private EventHandler _defaultInterpreterChanged;

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
        private const string DescriptionKey = "Description";
        private const string PythonInterpreterKey = "SOFTWARE\\Python\\VisualStudio";

        [ImportingConstructor]
        public InterpreterOptionsService([Import]Lazy<IInterpreterRegistryService> registryService, Lazy<CPythonInterpreterFactoryProvider> cpythonProvider) {
            _registryService = registryService;
            _cpythonProvider = cpythonProvider;
        }


        private void InitializeDefaultInterpreterWatcher() {
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
                SaveDefaultInterpreter(_defaultInterpreter?.Configuration?.Id);

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
            string oldId = _defaultInterpreterId;
            _defaultInterpreterId = null;
            LoadDefaultInterpreterId();

            if (oldId != _defaultInterpreterId) {
                // it changed, invalidate the old interpreter ID.  If no one is watching then
                // we'll just load it on demand next time someone requests it.
                _defaultInterpreter = null;
                if (_defaultInterpreterWatched) {
                    // someone is watching it, so load it now and raise the changed event.
                    LoadDefaultInterpreter();
                }
            }
        }

        private void DefaultInterpreterRegistry_Changed(object sender, RegistryChangedEventArgs e) {
            try {
                LoadDefaultInterpreter();
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

        private void LoadDefaultInterpreterId() {
            if (_defaultInterpreterId != null) {
                return;
            }

            lock (_defaultInterpreterLock) {
                if (_defaultInterpreterId != null) {
                    return;
                }

                string id = null;
                using (var interpreterOptions = Registry.CurrentUser.OpenSubKey(DefaultInterpreterOptionsCollection)) {
                    if (interpreterOptions != null) {
                        id = interpreterOptions.GetValue(DefaultInterpreterSetting) as string ?? string.Empty;
                    }

                    var newDefault = _registryService.Value.FindInterpreter(id);

                    if (newDefault == null) {
                        var defaultConfig = _registryService.Value.Configurations.LastOrDefault(fact => fact.CanBeAutoDefault());
                        id = defaultConfig?.Id;
                    }

                    _defaultInterpreterId = id;
                }
            }
        }

        private void LoadDefaultInterpreter(bool suppressChangeEvent = false) {
            if (_defaultInterpreter != null) {
                return;
            }

            IPythonInterpreterFactory newDefault = null;
            lock (_defaultInterpreterLock) {
                if (_defaultInterpreter != null) {
                    return;
                }

                LoadDefaultInterpreterId();
                if (_defaultInterpreterId != null) {
                    newDefault = _registryService.Value.FindInterpreter(_defaultInterpreterId);
                }
            }

            if (newDefault != null) {
                if (suppressChangeEvent) {
                    _defaultInterpreter = newDefault;
                } else {
                    DefaultInterpreter = newDefault;
                }
            }
        }

        private void SaveDefaultInterpreter(string id) {
            using (var interpreterOptions = Registry.CurrentUser.CreateSubKey(DefaultInterpreterOptionsCollection, true)) {
                if (id == null) {
                    interpreterOptions.SetValue(DefaultInterpreterSetting, "");
                } else {
                    Debug.Assert(!InterpreterRegistryConstants.IsNoInterpretersFactory(id));

                    interpreterOptions.SetValue(DefaultInterpreterSetting, id);
                }
            }
        }

        public string DefaultInterpreterId {
            get {
                LoadDefaultInterpreterId();
                return _defaultInterpreterId;
            }
            set {
                if (_defaultInterpreterId != value) {
                    _defaultInterpreterId = value;
                    _defaultInterpreter = null; // cleared so we'll re-initialize if anyone cares about it.
                    SaveDefaultInterpreter(value);

                    _defaultInterpreterChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public IPythonInterpreterFactory DefaultInterpreter {
            get {
                LoadDefaultInterpreter(true);
                return _defaultInterpreter ?? _registryService.Value.NoInterpretersValue;
            }
            set {
                var newDefault = value;
                if (_defaultInterpreter == null && (newDefault == _registryService.Value.NoInterpretersValue || value == null)) {
                    // we may have not loaded the default interpreter yet.  Do so 
                    // now so we know if we need to raise the change event.
                    LoadDefaultInterpreter();
                }

                if (newDefault == _registryService.Value.NoInterpretersValue) {
                    newDefault = null;
                }
                if (newDefault != _defaultInterpreter) {
                    _defaultInterpreter = newDefault;
                    _defaultInterpreterId = _defaultInterpreter?.Configuration?.Id;
                    SaveDefaultInterpreter(_defaultInterpreter?.Configuration?.Id);

                    _defaultInterpreterChanged?.Invoke(this, EventArgs.Empty);
                }
            }
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
            var collection = PythonInterpreterKey + "\\" + name;
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
                using (var installPath = key.CreateSubKey("InstallPath")) {
                    string exePath = config.InterpreterPath ?? config.WindowsInterpreterPath ?? "";
                    if (!string.IsNullOrEmpty(config.PrefixPath)) {
                        installPath.SetValue("", config.PrefixPath);
                    } else if (!string.IsNullOrWhiteSpace(exePath)) {
                        installPath.SetValue("", Path.GetDirectoryName(exePath));
                    }
                    installPath.SetValue(WindowsPathKey, config.WindowsInterpreterPath ?? string.Empty);
                    installPath.SetValue(PathKey, config.InterpreterPath ?? string.Empty);
                }
            }

            // ensure we're up to date...
            _cpythonProvider.Value.DiscoverInterpreterFactories();

            return CPythonInterpreterFactoryConstants.GetInterpreterId("VisualStudio", name);

        }

        public void RemoveConfigurableInterpreter(string id) {
            string company, tag;
            if (CPythonInterpreterFactoryConstants.TryParseInterpreterId(id, out company, out tag) &&
                company == "VisualStudio") {
                var collection = PythonInterpreterKey + "\\" + tag;
                try {
                    Registry.CurrentUser.DeleteSubKeyTree(collection);

                    _cpythonProvider.Value.DiscoverInterpreterFactories();
                } catch (ArgumentException) {
                }
            }
        }

        public bool IsConfigurable(string id) {
            return id.StartsWith("Global|VisualStudio|");
        }
    }
}
