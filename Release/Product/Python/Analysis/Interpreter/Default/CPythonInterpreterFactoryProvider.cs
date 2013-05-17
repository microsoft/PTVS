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
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools.Analysis;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter.Default {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class CPythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
        private readonly List<IPythonInterpreterFactory> _interpreters;
        private static readonly Guid _cpyInterpreterGuid = new Guid("{2AF0F10D-7135-4994-9156-5D01C9C11B7E}");
        private static readonly Guid _cpy64InterpreterGuid = new Guid("{9A7A9026-48C1-4688-9D5D-E5699D47D074}");
        const string PythonCorePath = "SOFTWARE\\Python\\PythonCore";

        public CPythonInterpreterFactoryProvider() {
            _interpreters = new List<IPythonInterpreterFactory>();
            DiscoverInterpreterFactories();

            try {
                RegistryWatcher.Instance.Add(RegistryHive.CurrentUser, RegistryView.Default,
                    PythonCorePath,
                    Registry_Changed,
                    recursive: true, notifyValueChange: true, notifyKeyChange: true);
            } catch (ArgumentException) {
                RegistryWatcher.Instance.Add(RegistryHive.CurrentUser, RegistryView.Default,
                    "SOFTWARE",
                    Registry_Software_Changed,
                    recursive: false, notifyValueChange: false, notifyKeyChange: true);
            }

            try {
                RegistryWatcher.Instance.Add(RegistryHive.LocalMachine, RegistryView.Registry32,
                    PythonCorePath,
                    Registry_Changed,
                    recursive: true, notifyValueChange: true, notifyKeyChange: true);
            } catch (ArgumentException) {
                RegistryWatcher.Instance.Add(RegistryHive.LocalMachine, RegistryView.Registry32,
                    "SOFTWARE",
                    Registry_Software_Changed,
                    recursive: false, notifyValueChange: false, notifyKeyChange: true);
            }

            if (Environment.Is64BitOperatingSystem) {
                try {
                    RegistryWatcher.Instance.Add(RegistryHive.LocalMachine, RegistryView.Registry64,
                        PythonCorePath,
                        Registry_Changed,
                        recursive: true, notifyValueChange: true, notifyKeyChange: true);
                } catch (ArgumentException) {
                    RegistryWatcher.Instance.Add(RegistryHive.LocalMachine, RegistryView.Registry64,
                        "SOFTWARE",
                        Registry_Software_Changed,
                        recursive: false, notifyValueChange: false, notifyKeyChange: true);
                }
            }
        }

        private void Registry_Changed(object sender, RegistryChangedEventArgs e) {
            DiscoverInterpreterFactories();
        }

        private void Registry_Software_Changed(object sender, RegistryChangedEventArgs e) {
            using (var root = RegistryKey.OpenBaseKey(e.Hive, e.View))
            using (var key = root.OpenSubKey(PythonCorePath)) {
                if (key != null) {
                    Registry_Changed(sender, e);
                    e.CancelWatcher = true;
                    RegistryWatcher.Instance.Add(e.Hive, e.View, PythonCorePath, Registry_Changed,
                        recursive: true, notifyValueChange: true, notifyKeyChange: true);
                }
            }
        }

        private bool RegisterInterpreters(HashSet<string> registeredPaths, RegistryKey python, ProcessorArchitecture? arch) {
            bool anyAdded = false;

            foreach (var key in python.GetSubKeyNames()) {
                Version version;
                if (Version.TryParse(key, out version)) {
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
                        if (basePath.IndexOfAny(Path.GetInvalidPathChars()) != -1) {
                            // Invalid path in registry
                            continue;
                        }
                        if (!registeredPaths.Add(basePath)) {
                            // registered in both HCKU and HKLM
                            continue;
                        }

                        var actualArch = arch;
                        if (!actualArch.HasValue) {
                            actualArch = NativeMethods.GetBinaryType(Path.Combine(basePath, "python.exe"));
                        }

                        var id = _cpyInterpreterGuid;
                        var description = "Python";
                        if (actualArch == ProcessorArchitecture.Amd64) {
                            id = _cpy64InterpreterGuid;
                            description = "Python 64-bit";
                        }

                        if (!_interpreters.Any(f => f.Id == id && f.Configuration.Version == version)) {
                            _interpreters.Add(new CPythonInterpreterFactory(
                                version,
                                id,
                                description,
                                Path.Combine(basePath, "python.exe"),
                                Path.Combine(basePath, "pythonw.exe"),
                                "PYTHONPATH",
                                actualArch ?? ProcessorArchitecture.None
                            ));
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

            var arch = Environment.Is64BitOperatingSystem ? null : (ProcessorArchitecture?)ProcessorArchitecture.X86;
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
            using (var python = baseKey.OpenSubKey(PythonCorePath)) {
                if (python != null) {
                    anyAdded |= RegisterInterpreters(registeredPaths, python, arch);
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
