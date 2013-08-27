/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reflection;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Win32;

namespace Microsoft.IronPythonTools.Interpreter {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class IronPythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
        private IPythonInterpreterFactory _interpreter;
        private IPythonInterpreterFactory _interpreterX64;
        const string IronPythonCorePath = "Software\\IronPython";

        public IronPythonInterpreterFactoryProvider() {
            DiscoverInterpreterFactories();
            if (_interpreter == null) {
                try {
                    RegistryWatcher.Instance.Add(RegistryHive.LocalMachine, RegistryView.Registry32, IronPythonCorePath,
                        Registry_Changed,
                        recursive: true, notifyValueChange: true, notifyKeyChange: true);
                } catch (ArgumentException) {
                    RegistryWatcher.Instance.Add(RegistryHive.LocalMachine, RegistryView.Registry32, "Software",
                        Registry_Software_Changed,
                        recursive: false, notifyValueChange: false, notifyKeyChange: true);
                }
            }
        }

        private void Registry_Changed(object sender, RegistryChangedEventArgs e) {
            if (!Exists(e)) {
                // IronPython key no longer exists, so go back to watching
                // Software.
                RegistryWatcher.Instance.Add(RegistryHive.LocalMachine, RegistryView.Registry32, "Software",
                    Registry_Software_Changed,
                    recursive: false, notifyValueChange: false, notifyKeyChange: true);
                e.CancelWatcher = true;
            } else {
                DiscoverInterpreterFactories();
                if (_interpreter != null) {
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
            using (var root = RegistryKey.OpenBaseKey(e.Hive, e.View))
            using (var key = root.OpenSubKey(IronPythonCorePath)) {
                if (key != null) {
                    RegistryWatcher.Instance.Add(e.Hive, e.View, IronPythonCorePath, Registry_Changed,
                        recursive: true, notifyValueChange: true, notifyKeyChange: true);
                    e.CancelWatcher = true;
                    Registry_Changed(sender, e);
                }
            }
        }

        #region IPythonInterpreterProvider Members

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            if (_interpreter != null) {
                yield return _interpreter;
            }
            if (_interpreterX64 != null) {
                yield return _interpreterX64;
            }
        }

        private void DiscoverInterpreterFactories() {
            if (_interpreter == null && IronPythonResolver.GetPythonInstallDir() != null) {
                _interpreter = new IronPythonInterpreterFactory(ProcessorArchitecture.X86);
                if (Environment.Is64BitOperatingSystem) {
                    _interpreterX64 = new IronPythonInterpreterFactory(ProcessorArchitecture.Amd64);
                }
                var evt = InterpreterFactoriesChanged;
                if (evt != null) {
                    evt(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler InterpreterFactoriesChanged;


        #endregion

    }
}
