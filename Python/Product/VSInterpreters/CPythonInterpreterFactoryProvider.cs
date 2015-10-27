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
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudioTools;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class CPythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
        private readonly List<IPythonInterpreterFactory> _interpreters;
        const string PythonPath = "Software\\Python";
        const string PythonCorePath = "Software\\Python\\PythonCore";

        public CPythonInterpreterFactoryProvider() {
            _interpreters = new List<IPythonInterpreterFactory>();
            DiscoverInterpreterFactories();

            StartWatching(RegistryHive.CurrentUser, RegistryView.Default);
            StartWatching(RegistryHive.LocalMachine, RegistryView.Registry32);
            if (Environment.Is64BitOperatingSystem) {
                StartWatching(RegistryHive.LocalMachine, RegistryView.Registry64);
            }
        }

        private void StartWatching(RegistryHive hive, RegistryView view, int retries = 5) {
            var tag = RegistryWatcher.Instance.TryAdd(
                hive, view, PythonCorePath, Registry_PythonCorePath_Changed,
                recursive: true, notifyValueChange: true, notifyKeyChange: true
            ) ??
            RegistryWatcher.Instance.TryAdd(
                hive, view, PythonPath, Registry_PythonPath_Changed,
                recursive: false, notifyValueChange: false, notifyKeyChange: true
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

        private void Registry_PythonCorePath_Changed(object sender, RegistryChangedEventArgs e) {
            if (!Exists(e)) {
                // PythonCore key no longer exists, so go back to watching
                // Python.
                e.CancelWatcher = true;
                StartWatching(e.Hive, e.View);
            } else {
                DiscoverInterpreterFactories();
            }
        }

        private void Registry_PythonPath_Changed(object sender, RegistryChangedEventArgs e) {
            if (Exists(e)) {
                if (RegistryWatcher.Instance.TryAdd(
                    e.Hive, e.View, PythonCorePath, Registry_PythonCorePath_Changed,
                    recursive: true, notifyValueChange: true, notifyKeyChange: true
                ) != null) {
                    // PythonCore key now exists, so start watching it,
                    // discover any interpreters, and cancel this watcher.
                    e.CancelWatcher = true;
                    DiscoverInterpreterFactories();
                }
            } else {
                // Python key no longer exists, so go back to watching
                // Software.
                e.CancelWatcher = true;
                StartWatching(e.Hive, e.View);
            }
        }

        private void Registry_Software_Changed(object sender, RegistryChangedEventArgs e) {
            Registry_PythonPath_Changed(sender, e);
            if (e.CancelWatcher) {
                // PythonCore key also exists and is now being watched, so just
                // return.
                return;
            }

            if (RegistryWatcher.Instance.TryAdd(
                e.Hive, e.View, PythonPath, Registry_PythonPath_Changed,
                recursive: false, notifyValueChange: false, notifyKeyChange: true
            ) != null) {
                // Python exists, but not PythonCore, so watch Python until
                // PythonCore is created.
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

        private bool RegisterInterpreters(HashSet<string> registeredPaths, RegistryKey python, ProcessorArchitecture? arch) {
            bool anyAdded = false;

            string[] subKeyNames = null;
            for (int retries = 5; subKeyNames == null && retries > 0; --retries) {
                try {
                    subKeyNames = python.GetSubKeyNames();
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
                Version version;
                ProcessorArchitecture? arch2;
                if (TryParsePythonVersion(key, out version, out arch2)) {
                    if (version.Major == 2 && version.Minor <= 4) {
                        // 2.4 and below not supported.
                        continue;
                    }

                    var installPath = python.OpenSubKey(key + "\\InstallPath");
                    if (installPath != null) {
                        var basePathObj = installPath.GetValue("");
                        if (basePathObj == null) {
                            // http://pytools.codeplex.com/discussions/301384
                            // messed up install, we don't know where it lives, we can't use it.
                            continue;
                        }
                        string basePath = basePathObj.ToString();
                        if (!CommonUtils.IsValidPath(basePath)) {
                            // Invalid path in registry
                            continue;
                        }
                        if (!registeredPaths.Add(basePath)) {
                            // registered in both HCKU and HKLM
                            continue;
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

                        if (!_interpreters.Any(f => f.Id == id && f.Configuration.Version == version)) {
                            IPythonInterpreterFactory fact;
                            try {
                                fact = InterpreterFactoryCreator.CreateInterpreterFactory(
                                    new InterpreterFactoryCreationOptions {
                                        LanguageVersion = version,
                                        Id = id,
                                        Description = string.Format("{0} {1}", description, version),
                                        InterpreterPath = Path.Combine(basePath, CPythonInterpreterFactoryConstants.ConsoleExecutable),
                                        WindowInterpreterPath = Path.Combine(basePath, CPythonInterpreterFactoryConstants.WindowsExecutable),
                                        LibraryPath = Path.Combine(basePath, CPythonInterpreterFactoryConstants.LibrarySubPath),
                                        PathEnvironmentVariableName = CPythonInterpreterFactoryConstants.PathEnvironmentVariableName,
                                        Architecture = actualArch ?? ProcessorArchitecture.None,
                                        WatchLibraryForNewModules = true
                                    }
                                );
                            } catch (ArgumentException) {
                                continue;
                            }

                            _interpreters.Add(fact);
                            anyAdded = true;
                        }
                    }
                }
            }

            return anyAdded;
        }

        private void DiscoverInterpreterFactories() {
            bool anyAdded = false;
            HashSet<string> registeredPaths = new HashSet<string>();
            var arch = Environment.Is64BitOperatingSystem ? null : (ProcessorArchitecture?)ProcessorArchitecture.X86;
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
            using (var python = baseKey.OpenSubKey(PythonCorePath)) {
                if (python != null) {
                    anyAdded |= RegisterInterpreters(registeredPaths, python, arch);
                }
            }

            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var python = baseKey.OpenSubKey(PythonCorePath)) {
                if (python != null) {
                    anyAdded |= RegisterInterpreters(registeredPaths, python, ProcessorArchitecture.X86);
                }
            }

            if (Environment.Is64BitOperatingSystem) {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var python64 = baseKey.OpenSubKey(PythonCorePath)) {
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
            return _interpreters;
        }

        public event EventHandler InterpreterFactoriesChanged;

        private void OnInterpreterFactoriesChanged() {
            var evt = InterpreterFactoriesChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}
