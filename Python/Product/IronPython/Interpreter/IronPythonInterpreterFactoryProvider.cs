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
using System.Threading;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;

namespace Microsoft.IronPythonTools.Interpreter {
    [InterpreterFactoryId("IronPython")]
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    sealed class IronPythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider, IDisposable {
        private readonly IServiceProvider _site;
        private bool _initialized;
        private IPythonInterpreterFactory _interpreter;
        private IPythonInterpreterFactory _interpreterX64;
        private PythonTools.Interpreter.InterpreterConfiguration _config, _configX64;
        const string IronPythonCorePath = "Software\\IronPython";

        [ImportingConstructor]
        public IronPythonInterpreterFactoryProvider([Import(typeof(SVsServiceProvider), AllowDefault = true)] IServiceProvider site = null) {
            _site = site;
        }

        private void EnsureInitialized() {
            if (_initialized) {
                return;
            }

            _initialized = true;
            DiscoverInterpreterFactories();
            if (_config == null) {
                StartWatching(RegistryHive.LocalMachine, RegistryView.Registry32);
            }
        }

        public void Dispose() {
            (_interpreter as IDisposable)?.Dispose();
            (_interpreterX64 as IDisposable)?.Dispose();
        }


        private void StartWatching(RegistryHive hive, RegistryView view, int retries = 5) {
            var tag = RegistryWatcher.Instance.TryAdd(
                hive, view, IronPythonCorePath,
                Registry_Changed,
                recursive: true, notifyValueChange: true, notifyKeyChange: true
            ) ?? RegistryWatcher.Instance.TryAdd(
                hive, view, "Software",
                Registry_Software_Changed,
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

        private void Registry_Changed(object sender, RegistryChangedEventArgs e) {
            if (!Exists(e)) {
                // IronPython key no longer exists, so go back to watching
                // Software.
                RegistryWatcher.Instance.Add(
                    RegistryHive.LocalMachine, RegistryView.Registry32, "Software",
                    Registry_Software_Changed,
                    recursive: false, notifyValueChange: false, notifyKeyChange: true
                );
                e.CancelWatcher = true;
            } else {
                DiscoverInterpreterFactories();
                if (_config != null) {
                    e.CancelWatcher = true;
                }
            }
        }

        private static bool Exists(RegistryChangedEventArgs e) {
            using (var root = RegistryKey.OpenBaseKey(e.Hive, e.View))
            using (var key = root.OpenSubKey(e.Key)) {
                return key != null;
            }
        }

        private void Registry_Software_Changed(object sender, RegistryChangedEventArgs e) {
            if (RegistryWatcher.Instance.TryAdd(
                e.Hive, e.View, IronPythonCorePath, Registry_Changed,
                recursive: true, notifyValueChange: true, notifyKeyChange: true
            ) != null) {
                e.CancelWatcher = true;
                Registry_Changed(sender, e);
            }
        }

        #region IPythonInterpreterProvider Members

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            EnsureInitialized();

            if (_config != null) {
                yield return GetInterpreterFactory(_config.Id);
            }
            if (_configX64 != null) {
                yield return GetInterpreterFactory(_configX64.Id);
            }
        }

        public IEnumerable<PythonTools.Interpreter.InterpreterConfiguration> GetInterpreterConfigurations() {
            EnsureInitialized();

            if (_config != null) {
                yield return _config;
            }
            if (_configX64 != null) {
                yield return _configX64;
            }
        }

        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
            EnsureInitialized();

            if (_config != null && id == _config.Id) {
                EnsureInterpreter();

                return _interpreter;
            } else if (_configX64 != null && id == _configX64.Id) {
                EnsureInterpreterX64();

                return _interpreterX64;
            }
            return null;
        }

        private void EnsureInterpreterX64() {
            if (_interpreterX64 == null) {
                lock (this) {
                    if (_interpreterX64 == null) {
                        var config = GetConfiguration(InterpreterArchitecture.x64);
                        var opts = GetCreationOptions(_site, config);
                        _interpreterX64 = new IronPythonAstInterpreterFactory(config, opts);
                    }
                }
            }
        }

        private void EnsureInterpreter() {
            if (_interpreter == null) {
                lock (this) {
                    if (_interpreter == null) {
                        var config = GetConfiguration(InterpreterArchitecture.x86);
                        var opts = GetCreationOptions(_site, config);
                        _interpreter = new IronPythonAstInterpreterFactory(config, opts);
                    }
                }
            }
        }

        private void DiscoverInterpreterFactories() {
            if (_config == null && IronPythonResolver.GetPythonInstallDir() != null) {
                _config = GetConfiguration(InterpreterArchitecture.x86);
                if (Environment.Is64BitOperatingSystem) {
                    _configX64 = GetConfiguration(InterpreterArchitecture.x64);
                }
                InterpreterFactoriesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler InterpreterFactoriesChanged;

        public object GetProperty(string id, string propName) {
            switch (propName) {
                // Should match PythonRegistrySearch.CompanyPropertyKey
                case "Company":
                    return "IronPython team";
                // Should match PythonRegistrySearch.SupportUrlPropertyKey
                case "SupportUrl":
                    return "http://ironpython.net/";
                case "PersistInteractive":
                    return true;
            }
            return null;
        }

        #endregion

        internal static string GetInterpreterId(InterpreterArchitecture arch) {
            if (arch == InterpreterArchitecture.x64) {
                return "IronPython|2.7-64";
            } else {
                return "IronPython|2.7-32";
            }
        }

        internal static VisualStudioInterpreterConfiguration GetConfiguration(InterpreterArchitecture arch) {
            var prefixPath = IronPythonResolver.GetPythonInstallDir();
            if (string.IsNullOrEmpty(prefixPath)) {
                return null;
            }

            // IronPython 2.7.8 changed the executable names for 64-bit vs 32-bit
            var ipyExe = arch == InterpreterArchitecture.x64 ? "ipy64.exe" : "ipy.exe";
            var ipywExe = arch == InterpreterArchitecture.x64 ? "ipyw64.exe" : "ipyw.exe";
            if (File.Exists(Path.Combine(prefixPath, "ipy32.exe"))) {
                ipyExe = arch == InterpreterArchitecture.x64 ? "ipy.exe" : "ipy32.exe";
                ipywExe = arch == InterpreterArchitecture.x64 ? "ipyw.exe" : "ipyw32.exe";
            }

            return new VisualStudioInterpreterConfiguration(
                GetInterpreterId(arch),
                string.Format("IronPython 2.7{0: ()}", arch),
                prefixPath,
                Path.Combine(prefixPath, ipyExe),
                Path.Combine(prefixPath, ipywExe),
                "IRONPYTHONPATH",
                arch,
                new Version(2, 7),
                PythonTools.Interpreter.InterpreterUIMode.SupportsDatabase
            );
        }

        internal static InterpreterFactoryCreationOptions GetCreationOptions(IServiceProvider site, PythonTools.Interpreter.InterpreterConfiguration config) {
            return new InterpreterFactoryCreationOptions {};
        }
    }
}
