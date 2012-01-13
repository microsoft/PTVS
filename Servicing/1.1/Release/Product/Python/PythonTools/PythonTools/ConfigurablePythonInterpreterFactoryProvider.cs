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
using System.Reflection;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.Win32;

namespace Microsoft.PythonTools {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [Export(typeof(IPythonConfigurableInterpreterFactoryProvider))]
    class ConfigurablePythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider, IPythonConfigurableInterpreterFactoryProvider {
        private readonly List<IPythonInterpreterFactory> _interpreters = new List<IPythonInterpreterFactory>();

        private readonly IDefaultInterpreterFactoryCreator _defaultCreator;

        // keys used for storing information about user defined interpreters
        const string PathKey = "InterpreterPath";
        const string WindowsPathKey = "WindowsInterpreterPath";
        const string ArchitectureKey = "Architecture";
        const string VersionKey = "Version";
        const string PathEnvVarKey = "PathEnvironmentVariable";
        const string DescriptionKey = "Description";
        const string PythonInterpreterKey = PythonCoreConstants.BaseRegistryKey + "\\Interpreters";

        [ImportingConstructor]
        public ConfigurablePythonInterpreterFactoryProvider(IDefaultInterpreterFactoryCreator defaultCreator) {
            _defaultCreator = defaultCreator;

            // look for custom configured interpreters
            
            using (var userInterpreters = GetUserDefinedInterpreterKey()) {
                if (userInterpreters != null) {
                    foreach (var guid in userInterpreters.GetSubKeyNames()) {
                        var interp = LoadUserDefinedInterpreter(userInterpreters, guid);
                        if (interp != null) {
                            _interpreters.Add(interp);
                        }
                    }
                }
            }
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

                for (int i = 0; i < _interpreters.Count; i++) {
                    if (_interpreters[i].Id == id) {
                        _interpreters.RemoveAt(i);
                        break;
                    }
                }
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

                for (int i = 0; i < _interpreters.Count; i++) {
                    var interp = _interpreters[i];
                    if (interp.Id == id) {
                        _interpreters[i] = newInterp;
                        return newInterp;
                    }
                }

                // new interpreter, add it.
                _interpreters.Add(newInterp);
                return newInterp;
            }
        }

        private static RegistryKey GetUserDefinedInterpreterKey(bool create = false) {
            return PythonToolsPackage.UserRegistryRoot.OpenSubKey(PythonInterpreterKey, true) ??
                (create ? PythonToolsPackage.UserRegistryRoot.CreateSubKey(PythonInterpreterKey) : null);
        }

        #region IPythonInterpreterFactoryProvider Members

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            return _interpreters;
        }

        #endregion
    }
}
