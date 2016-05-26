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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter {
    [InterpreterFactoryId(FactoryProviderName)]
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [Export(typeof(CPythonInterpreterFactoryProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class CPythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
        private readonly Dictionary<string, InterpreterInformation> _factories = new Dictionary<string, InterpreterInformation>();
        const string PythonPath = "Software\\Python";
        internal const string FactoryProviderName = "Global";
        private bool _initialized;

        public CPythonInterpreterFactoryProvider() {
        }

        private void EnsureInitialized() {
            lock (this) {
                if (!_initialized) {
                    _initialized = true;
                    DiscoverInterpreterFactories();

                    StartWatching(RegistryHive.CurrentUser, RegistryView.Default);
                    StartWatching(RegistryHive.LocalMachine, RegistryView.Registry32);
                    if (Environment.Is64BitOperatingSystem) {
                        StartWatching(RegistryHive.LocalMachine, RegistryView.Registry64);
                    }
                }
            }
        }

        private void StartWatching(RegistryHive hive, RegistryView view, int retries = 5) {
            var tag = RegistryWatcher.Instance.TryAdd(
                hive, view, PythonPath, Registry_PythonPath_Changed,
                recursive: true, notifyValueChange: true, notifyKeyChange: true
            ) ??
            RegistryWatcher.Instance.TryAdd(
                hive, view, "Software", Registry_Software_Changed,
                recursive: false, notifyValueChange: false, notifyKeyChange: true
            );

            if (tag == null && retries > 0) {
                Trace.TraceWarning("Failed to watch registry. Retrying {0} more times", retries);
                Thread.Sleep(100);
                StartWatching(hive, view, retries - 1);
            } else if (tag == null) {
                Trace.TraceError("Failed to watch registry");
            }
        }

        #region Registry Watching

        private static bool Exists(RegistryChangedEventArgs e) {
            using (var root = RegistryKey.OpenBaseKey(e.Hive, e.View))
            using (var key = root.OpenSubKey(e.Key)) {
                return key != null;
            }
        }

        private void Registry_PythonPath_Changed(object sender, RegistryChangedEventArgs e) {
            if (!Exists(e)) {
                // Python key no longer exists, so go back to watching
                // Software.
                e.CancelWatcher = true;
                StartWatching(e.Hive, e.View);
            } else {
                DiscoverInterpreterFactories();
            }
        }

        private void Registry_Software_Changed(object sender, RegistryChangedEventArgs e) {
            Registry_PythonPath_Changed(sender, e);
            if (e.CancelWatcher) {
                // Python key no longer exists, we're still watching Software
                return;
            }

            if (RegistryWatcher.Instance.TryAdd(
                e.Hive, e.View, PythonPath, Registry_PythonPath_Changed,
                recursive: true, notifyValueChange: true, notifyKeyChange: true
            ) != null) {
                // Python exists, we no longer need to watch Software
                e.CancelWatcher = true;
            }
        }

        #endregion

        private static bool TryParsePythonVersion(string spec, out Version version, out ProcessorArchitecture? arch, ref string id) {
            version = null;
            arch = null;

            if (string.IsNullOrEmpty(spec) || spec.Length < 3) {
                return false;
            }

            var m = Regex.Match(spec, @"^(?<ver>[23]\.[0-9]+)(?<suffix>.*)$");
            if (!m.Success) {
                return false;
            }

            if (!Version.TryParse(m.Groups["ver"].Value, out version)) {
                return false;
            }

            if (m.Groups["suffix"].Value == "-32") {
                arch = ProcessorArchitecture.X86;
                id = id.Substring(0, id.Length - 3);
            }

            return true;
        }

        private static void RegisterInterpreters(Dictionary<string, InterpreterInformation> factories, HashSet<string> registeredPaths, bool ignoreRegisteredPaths, RegistryKey pythonKey, ProcessorArchitecture? arch) {
            foreach (var subKeyName in pythonKey.GetSubKeyNames()) {
                using (var python = pythonKey.OpenSubKey(subKeyName)) {
                    RegisterVendor(factories, registeredPaths, ignoreRegisteredPaths, python, arch);
                }
            }
        }

        private static void RegisterVendor(Dictionary<string, InterpreterInformation> factories, HashSet<string> registeredPaths, bool ignoreRegisteredPaths, RegistryKey vendorKey, ProcessorArchitecture? arch) {
            string[] subKeyNames = null;
            for (int retries = 5; subKeyNames == null && retries > 0; --retries) {
                try {
                    subKeyNames = vendorKey.GetSubKeyNames();
                } catch (IOException) {
                    // Registry changed while enumerating subkeys. Give it a
                    // short period to settle down and try again.
                    // We are almost certainly being called from a background
                    // thread, so sleeping here is fine.
                    Thread.Sleep(100);
                }
            }

            foreach (var key in subKeyNames) {
                TryRegisterInterpreter(factories, registeredPaths, ignoreRegisteredPaths, vendorKey, key, arch);
            }
        }

        private static void TryRegisterInterpreter(Dictionary<string, InterpreterInformation> factories, HashSet<string> registeredPaths, bool ignoreRegisteredPaths, RegistryKey vendorKey, string key, ProcessorArchitecture? arch) {
            Version version = null;
            ProcessorArchitecture? arch2 = null;

            using (var interpKey = vendorKey.OpenSubKey(key)) {
                if (interpKey == null) {
                    // the key unexpectedly disappeared
                    return;
                }
                string id = key;
                var versionValue = interpKey.GetValue("SysVersion") as string;
                if ((versionValue == null || !Version.TryParse(versionValue, out version)) &&
                    !TryParsePythonVersion(key, out version, out arch2, ref id)) {
                    version = new Version(2, 7);
                }

                try {
                    var langVer = version.ToLanguageVersion();
                } catch (InvalidOperationException) {
                    // Version is not currently supported
                    return;
                }

                var archStr = interpKey.GetValue("Architecture") as string;
                switch (archStr) {
                    case "x64": arch = ProcessorArchitecture.Amd64; break;
                    case "x86": arch = ProcessorArchitecture.X86; break;
                }

                if (version.Major == 2 && version.Minor <= 4) {
                    // 2.4 and below not supported.
                    return;
                }

                var installPath = vendorKey.OpenSubKey(key + "\\InstallPath");
                if (installPath != null) {
                    var basePathObj = installPath.GetValue("");
                    if (basePathObj == null) {
                        // http://pytools.codeplex.com/discussions/301384
                        // messed up install, we don't know where it lives, we can't use it.
                        return;
                    }
                    string basePath = basePathObj.ToString();
                    if (!PathUtils.IsValidPath(basePath)) {
                        // Invalid path in registry
                        return;
                    }
                    if (!registeredPaths.Add(basePath) && !ignoreRegisteredPaths) {
                        // registered in both HCKU and HKLM (we allow duplicate paths in HKCU 
                        // which is why we have ignoreRegisteredPaths)
                        return;
                    }

                    var actualArch = arch ?? arch2;
                    if (!actualArch.HasValue) {
                        actualArch = NativeMethods.GetBinaryType(Path.Combine(basePath, CPythonInterpreterFactoryConstants.ConsoleExecutable));
                    }

                    string description = interpKey.GetValue("Description") as string;
                    if (description == null) {
                        description = "Python";
                    }

                    string newId = CPythonInterpreterFactoryConstants.GetInterpreterId(GetVendorName(vendorKey), actualArch, id);

                    try {
                        var interpPath = installPath.GetValue("ExecutablePath") as string ?? Path.Combine(basePath, CPythonInterpreterFactoryConstants.ConsoleExecutable);
                        var windowsPath = installPath.GetValue("WindowedExecutablePath") as string ?? Path.Combine(basePath, CPythonInterpreterFactoryConstants.WindowsExecutable);
                        var libraryPath = Path.Combine(basePath, CPythonInterpreterFactoryConstants.LibrarySubPath);
                        string prefixPath = Path.GetDirectoryName(interpPath);

                        var newConfig = new InterpreterConfiguration(
                            newId,
                            description,
                            prefixPath,
                            interpPath,
                            windowsPath,
                            libraryPath,
                            CPythonInterpreterFactoryConstants.PathEnvironmentVariableName,
                            actualArch ?? ProcessorArchitecture.None,
                            version
                        );
                        factories[newId] = new InterpreterInformation(newConfig);
                    } catch (ArgumentException) {
                    }
                }
            }
        }

        private static string GetVendorName(RegistryKey vendorKey) {
            return vendorKey.Name.Substring(vendorKey.Name.LastIndexOf('\\') + 1);
        }

        internal void DiscoverInterpreterFactories() {
            // Discover the available interpreters...
            bool anyChanged = false;
            HashSet<string> registeredPaths = new HashSet<string>();
            var arch = Environment.Is64BitOperatingSystem ? null : (ProcessorArchitecture?)ProcessorArchitecture.X86;

            Dictionary<string, InterpreterInformation> userFactories = new Dictionary<string, InterpreterInformation>();
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default)) {
                using (var python = baseKey.OpenSubKey(PythonPath)) {
                    if (python != null) {
                        RegisterInterpreters(userFactories, registeredPaths, true, python, arch);
                    }
                }
            }

            Dictionary<string, InterpreterInformation> machineFactories = new Dictionary<string, InterpreterInformation>();
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var python = baseKey.OpenSubKey(PythonPath)) {
                if (python != null) {
                    RegisterInterpreters(machineFactories, registeredPaths, false, python, ProcessorArchitecture.X86);
                }
            }

            if (Environment.Is64BitOperatingSystem) {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var python64 = baseKey.OpenSubKey(PythonPath)) {
                    if (python64 != null) {
                        RegisterInterpreters(machineFactories, registeredPaths, false, python64, ProcessorArchitecture.Amd64);
                    }
                }
            }

            // Then update our cached state with the lock held.
            lock (this) {
                var uniqueIds = new HashSet<string>(
                    userFactories.Keys
                       .Concat(machineFactories.Keys)
                );

                foreach (var id in uniqueIds) {
                    InterpreterInformation newInfo;
                    // User factories with the same ID take precedence over machine factories
                    if (!userFactories.TryGetValue(id, out newInfo)) {
                        newInfo = machineFactories[id];
                    }

                    InterpreterInformation existingInfo;
                    if (!_factories.TryGetValue(id, out existingInfo) ||
                        newInfo.Configuration != existingInfo.Configuration) {
                        _factories[id] = newInfo;
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


        #region IPythonInterpreterProvider Members

        public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations() {
            EnsureInitialized();

            lock (_factories) {
                return _factories.Values.Select(x => x.Configuration).ToArray();
            }
        }

        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
            EnsureInitialized();

            InterpreterInformation info;
            lock (_factories) {
                _factories.TryGetValue(id, out info);
            }

            return info?.EnsureFactory();
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

        public object GetProperty(string id, string propName) => null;

        #endregion

        class InterpreterInformation {
            IPythonInterpreterFactory Factory;
            public readonly InterpreterConfiguration Configuration;

            public InterpreterInformation(InterpreterConfiguration configuration) {
                Configuration = configuration;
            }

            public IPythonInterpreterFactory EnsureFactory() {
                if (Factory == null) {
                    lock (this) {
                        if (Factory == null) {
                            Factory = InterpreterFactoryCreator.CreateInterpreterFactory(
                                Configuration,
                                new InterpreterFactoryCreationOptions() {
                                    WatchLibraryForNewModules = true
                                }
                            );
                        }
                    }
                }
                return Factory;
            }
        }
    }
}
