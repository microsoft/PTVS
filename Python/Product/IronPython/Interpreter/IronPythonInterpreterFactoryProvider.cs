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
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Win32;

namespace Microsoft.IronPythonTools.Interpreter {
    [InterpreterFactoryId("IronPython")]
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    sealed class IronPythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider, IDisposable {
        private IPythonInterpreterFactory _interpreter;
        private IPythonInterpreterFactory _interpreterX64;
        private InterpreterConfiguration _config, _configX64;
        const string IronPythonCorePath = "Software\\IronPython";

        public IronPythonInterpreterFactoryProvider() {
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
            if (_config != null) {
                yield return GetInterpreterFactory(_config.Id);
            }
            if (_configX64 != null) {
                yield return GetInterpreterFactory(_configX64.Id);
            }
        }

        public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations() {
            if (_config != null) {
                yield return _config;
            }
            if (_configX64 != null) {
                yield return _configX64;
            }
        }

        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
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
                        _interpreterX64 = new IronPythonInterpreterFactory(_configX64.Architecture);
                    }
                }
            }
        }

        private void EnsureInterpreter() {
            if (_interpreter == null) {
                lock (this) {
                    if (_interpreter == null) {
                        _interpreter = new IronPythonInterpreterFactory(_config.Architecture);
                    }
                }
            }
        }

        private void DiscoverInterpreterFactories() {
            if (_config == null && IronPythonResolver.GetPythonInstallDir() != null) {
                _config = IronPythonInterpreterFactory.GetConfiguration(ProcessorArchitecture.X86);
                if (Environment.Is64BitOperatingSystem) {
                    _configX64 = IronPythonInterpreterFactory.GetConfiguration(ProcessorArchitecture.Amd64);
                }
                var evt = InterpreterFactoriesChanged;
                if (evt != null) {
                    evt(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler InterpreterFactoriesChanged;

        public string GetAssociatedProjectMoniker(string id) => null;

        #endregion

    }
}
