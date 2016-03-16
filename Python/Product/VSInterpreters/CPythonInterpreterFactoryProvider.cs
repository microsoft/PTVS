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
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter {
    [InterpreterFactoryId(FactoryProviderName)]
    [Export(typeof(IPythonInterpreterFactoryProvider))]
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

        private static bool TryParsePythonVersion(string spec, out Version version, out ProcessorArchitecture? arch) {
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
            }

            return true;
        }

        private bool RegisterInterpreters(HashSet<string> registeredPaths, RegistryKey pythonKey, ProcessorArchitecture? arch) {
            bool anyAdded = false;
            foreach (var subKeyName in pythonKey.GetSubKeyNames()) {
                using (var python = pythonKey.OpenSubKey(subKeyName)) {
                    anyAdded |= RegisterVendor(registeredPaths, python, arch);
                }
            }

            return anyAdded;
        }

        private bool RegisterVendor(HashSet<string> registeredPaths, RegistryKey vendorKey, ProcessorArchitecture? arch) {
            bool anyAdded = false;
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
            if (subKeyNames == null) {
                return false;
            }

            foreach (var key in subKeyNames) {
                anyAdded |= TryRegisterInterpreter(registeredPaths, vendorKey, key, arch);

            }
            return anyAdded;
        }

        private bool TryRegisterInterpreter(HashSet<string> registeredPaths, RegistryKey vendorKey, string key, ProcessorArchitecture? arch) {
            Version version = null;
            ProcessorArchitecture? arch2;

            using (var interpKey = vendorKey.OpenSubKey(key)) {
                var versionValue = interpKey.GetValue("Version") as string;
                if (!TryParsePythonVersion(versionValue, out version, out arch2) &&
                    !TryParsePythonVersion(key, out version, out arch2)) {
                    version = new Version(2, 7);
                }

                var archStr = interpKey.GetValue("Architecture") as string;
                switch (archStr) {
                    case "x64": arch = ProcessorArchitecture.Amd64; break;
                    case "x86": arch = ProcessorArchitecture.X86; break;
                }

                if (version.Major == 2 && version.Minor <= 4) {
                    // 2.4 and below not supported.
                    return false;
                }

                var installPath = vendorKey.OpenSubKey(key + "\\InstallPath");
                if (installPath != null) {
                    var basePathObj = installPath.GetValue("");
                    if (basePathObj == null) {
                        // http://pytools.codeplex.com/discussions/301384
                        // messed up install, we don't know where it lives, we can't use it.
                        return false;
                    }
                    string basePath = basePathObj.ToString();
                    if (!PathUtils.IsValidPath(basePath)) {
                        // Invalid path in registry
                        return false;
                    }
                    if (!registeredPaths.Add(basePath)) {
                        // registered in both HCKU and HKLM
                        return false;
                    }

                    var actualArch = arch ?? arch2;
                    if (!actualArch.HasValue) {
                        actualArch = NativeMethods.GetBinaryType(Path.Combine(basePath, CPythonInterpreterFactoryConstants.ConsoleExecutable));
                    }

                    var id = CPythonInterpreterFactoryConstants.Guid32;
                    var description = CPythonInterpreterFactoryConstants.Description32;
                    if (actualArch == ProcessorArchitecture.Amd64) {
                        id = CPythonInterpreterFactoryConstants.Guid64;
                        description = CPythonInterpreterFactoryConstants.Description64;
                    }

                    string newId = GetIntepreterId(GetVendorName(vendorKey), arch ?? arch2, key);
                    if (!_factories.ContainsKey(newId)) {
                        try {
                            var interpPath = installPath.GetValue("ExecutablePath") as string ?? Path.Combine(basePath, CPythonInterpreterFactoryConstants.ConsoleExecutable);
                            var windowsPath = installPath.GetValue("WindowedExecutablePath") as string ?? Path.Combine(basePath, CPythonInterpreterFactoryConstants.WindowsExecutable);
                            var libraryPath = Path.Combine(basePath, CPythonInterpreterFactoryConstants.LibrarySubPath);
                            string prefixPath = Path.GetDirectoryName(interpPath);

                            _factories[newId] = new InterpreterInformation(
                                new InterpreterConfiguration(
                                    newId,
                                    string.Format("{0} {1}", description, version),
                                    prefixPath,
                                    interpPath,
                                    windowsPath,
                                    libraryPath,
                                    CPythonInterpreterFactoryConstants.PathEnvironmentVariableName,
                                    actualArch ?? ProcessorArchitecture.None,
                                    version
                                )
                            );
                        } catch (ArgumentException) {
                            return false;
                        }

                        return true;
                    }
                }
            }
            return false;
        }

        private static string GetVendorName(RegistryKey vendorKey) {
            return vendorKey.Name.Substring(vendorKey.Name.LastIndexOf('\\') + 1);
        }

        public static string GetIntepreterId(string vendor, ProcessorArchitecture? arch, string key) {
            string archStr;
            switch (arch) {
                case ProcessorArchitecture.Amd64: archStr = "x64"; break;
                case ProcessorArchitecture.X86: archStr = "x86"; break;
                default: archStr = "unknown"; break;
            }

            return FactoryProviderName + ";" + vendor + ";" + archStr + ";" + key;
        }

        private void DiscoverInterpreterFactories() {
            bool anyAdded = false;
            HashSet<string> registeredPaths = new HashSet<string>();
            var arch = Environment.Is64BitOperatingSystem ? null : (ProcessorArchitecture?)ProcessorArchitecture.X86;
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default)) {
                using (var python = baseKey.OpenSubKey(PythonPath)) {
                    if (python != null) {
                        anyAdded |= RegisterInterpreters(registeredPaths, python, arch);
                    }
                }
            }

            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var python = baseKey.OpenSubKey(PythonPath)) {
                if (python != null) {
                    anyAdded |= RegisterInterpreters(registeredPaths, python, ProcessorArchitecture.X86);
                }
            }

            if (Environment.Is64BitOperatingSystem) {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var python64 = baseKey.OpenSubKey(PythonPath)) {
                    if (python64 != null) {
                        anyAdded |= RegisterInterpreters(registeredPaths, python64, ProcessorArchitecture.Amd64);
                    }
                }
            }

            if (anyAdded) {
                OnInterpreterFactoriesChanged();
            }
        }


        #region IPythonInterpreterProvider Members

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            EnsureInitialized();

            InterpreterInformation[] infos;
            lock (_factories) {
                infos = _factories.Values.ToArray();
            }
            return infos.Select(x => x.EnsureFactory());
        }

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
