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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.Win32;

namespace Microsoft.PythonTools {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    class ConfigurablePythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider, IPythonConfigurableInterpreterFactoryProvider {
        private readonly Dictionary<Guid, IPythonInterpreterFactory> _interpreters = new Dictionary<Guid, IPythonInterpreterFactory>();

        private IDefaultInterpreterFactoryCreator _defaultCreator;

        // keys used for storing information about user defined interpreters
        const string PathKey = "InterpreterPath";
        const string WindowsPathKey = "WindowsInterpreterPath";
        const string ArchitectureKey = "Architecture";
        const string VersionKey = "Version";
        const string PathEnvVarKey = "PathEnvironmentVariable";
        const string DescriptionKey = "Description";
        const string PythonInterpreterKey = PythonCoreConstants.BaseRegistryKey + "\\Interpreters";

        static bool Created = false;

        public ConfigurablePythonInterpreterFactoryProvider() {
            Debug.Assert(!Created);
            Created = true;
            DiscoverInterpreterFactories();
        }

        private IPythonInterpreterFactory LoadUserDefinedInterpreter(RegistryKey userInterpreters, string guid) {
            // PythonInterpreters\
            //      Id\
            //          Description
            //          InterpreterPath
            //          WindowsInterpreterPath
            //          Architecture
            //          Version
            //          PathEnvironmentVariable

            Guid id;
            if (Guid.TryParse(guid, out id)) {
                using (var subkey = userInterpreters.OpenSubKey(guid)) {
                    var path = subkey.GetValue(PathKey) as string;
                    var winPath = subkey.GetValue(WindowsPathKey) as string;
                    var arch = subkey.GetValue(ArchitectureKey) as string;
                    var version = subkey.GetValue(VersionKey) as string;
                    var pathEnvVar = subkey.GetValue(PathEnvVarKey) as string;
                    var description = subkey.GetValue(DescriptionKey) as string;

                    ProcessorArchitecture archValue;
                    if (arch != null && version != null && guid != null) {
                        if (String.Equals(arch, "x64", StringComparison.OrdinalIgnoreCase)) {
                            archValue = ProcessorArchitecture.Amd64;
                        } else {
                            archValue = ProcessorArchitecture.X86;
                        }

                        Version ver;
                        if (!Version.TryParse(version, out ver)) {
                            ver = new Version(2, 7);
                        }

                        return CreateConfigurableInterpreterFactory(id, path, winPath, pathEnvVar, description, archValue, ver);
                    }
                }
            }
            return null;
        }

        public IPythonInterpreterFactory CreateConfigurableInterpreterFactory(Guid id, string path, string winPath, string pathEnvVar, string description, ProcessorArchitecture archValue, Version ver) {
            if (_defaultCreator == null) {
                _defaultCreator = PythonToolsPackage.ComponentModel.GetService<IDefaultInterpreterFactoryCreator>();
            }

            var fact = _defaultCreator.CreateInterpreterFactory(
                    new Dictionary<InterpreterFactoryOptions, object>() {
                        { InterpreterFactoryOptions.Version, ver },
                        { InterpreterFactoryOptions.Guid, id },
                        { InterpreterFactoryOptions.Description, description },
                        { InterpreterFactoryOptions.PythonPath, path },
                        { InterpreterFactoryOptions.PythonWindowsPath, winPath },
                        { InterpreterFactoryOptions.PathEnvVar, pathEnvVar },
                        { InterpreterFactoryOptions.ProcessorArchitecture, archValue }
                    }
                );

            return new ConfigurablePythonInterpreterFactory(fact);
        }

        void IPythonConfigurableInterpreterFactoryProvider.RemoveInterpreter(Guid id) {
            using (var userInterpreters = GetUserDefinedInterpreterKey()) {
                if (userInterpreters != null) {
                    userInterpreters.DeleteSubKey(id.ToString("B"), false);
                }

                _interpreters.Remove(id);
                OnInterpreterFactoriesChanged();
            }
        }

        IPythonInterpreterFactory IPythonConfigurableInterpreterFactoryProvider.SetOptions(Guid id, Dictionary<string, object> values) {
            using (var userInterpreters = GetUserDefinedInterpreterKey(true)) {
                using (var interpreterKey = userInterpreters.OpenSubKey(id.ToString("B"), true) ?? userInterpreters.CreateSubKey(id.ToString("B"))) {
                    interpreterKey.SetValue(PathKey, values[PathKey]);
                    interpreterKey.SetValue(WindowsPathKey, values[WindowsPathKey]);
                    interpreterKey.SetValue(ArchitectureKey, values[ArchitectureKey]);
                    interpreterKey.SetValue(VersionKey, values[VersionKey]);
                    interpreterKey.SetValue(PathEnvVarKey, values[PathEnvVarKey]);
                    interpreterKey.SetValue(DescriptionKey, values[DescriptionKey]);
                }

                var newInterp = LoadUserDefinedInterpreter(userInterpreters, id.ToString("B"));
                Debug.Assert(newInterp != null);

                _interpreters[newInterp.Id] = newInterp;
                OnInterpreterFactoriesChanged();
                return newInterp;
            }
        }

        private static RegistryKey GetUserDefinedInterpreterKey(bool create = false) {
            return PythonToolsPackage.UserRegistryRoot.OpenSubKey(PythonInterpreterKey, true) ??
                (create ? PythonToolsPackage.UserRegistryRoot.CreateSubKey(PythonInterpreterKey) : null);
        }

        #region IPythonInterpreterFactoryProvider Members

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            return _interpreters.Values;
        }


        private void DiscoverInterpreterFactories() {
            // look for custom configured interpreters
            bool anyChange = false;
            var notFound = new HashSet<Guid>(_interpreters.Keys);

            using (var userInterpreters = GetUserDefinedInterpreterKey()) {
                if (userInterpreters != null) {
                    foreach (var guid in userInterpreters.GetSubKeyNames()) {
                        var interp = LoadUserDefinedInterpreter(userInterpreters, guid);
                        if (interp != null) {
                            anyChange |= notFound.Remove(interp.Id);
                            if (!_interpreters.ContainsKey(interp.Id)) {
                                _interpreters[interp.Id] = interp;
                                anyChange = true;
                            }
                        }
                    }
                }
            }

            foreach (var id in notFound) {
                _interpreters.Remove(id);
            }

            if (anyChange) {
                OnInterpreterFactoriesChanged();
            }
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
