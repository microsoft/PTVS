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
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    class ConfigurablePythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
        private readonly Dictionary<Guid, PythonInterpreterFactoryWithDatabase> _interpreters = new Dictionary<Guid, PythonInterpreterFactoryWithDatabase>();

        // keys used for storing information about user defined interpreters
        const string PathKey = "InterpreterPath";
        const string WindowsPathKey = "WindowsInterpreterPath";
        const string LibraryPathKey = "LibraryPath";
        const string ArchitectureKey = "Architecture";
        const string VersionKey = "Version";
        const string PathEnvVarKey = "PathEnvironmentVariable";
        const string DescriptionKey = "Description";
        const string PythonInterpreterKey = "SOFTWARE\\Python\\VisualStudio";

        [ImportingConstructor]
        private ConfigurablePythonInterpreterFactoryProvider() {
            DiscoverInterpreterFactories();
        }

        private PythonInterpreterFactoryWithDatabase LoadUserDefinedInterpreter(string guid) {
            // PythonInterpreters\
            //      Id\
            //          Description
            //          InterpreterPath
            //          WindowsInterpreterPath
            //          Architecture
            //          Version
            //          PathEnvironmentVariable

            var collection = PythonInterpreterKey + "\\" + guid;
            Guid id;
            if (Guid.TryParse(guid, out id)) {
                using (var key = Registry.CurrentUser.OpenSubKey(collection)) {
                    if (key != null) {
                        var path = key.GetValue(PathKey) as string ?? string.Empty;
                        var winPath = key.GetValue(WindowsPathKey) as string ?? string.Empty;
                        var libPath = key.GetValue(LibraryPathKey) as string ?? string.Empty;
                        var arch = key.GetValue(ArchitectureKey) as string ?? string.Empty;
                        var version = key.GetValue(VersionKey) as string ?? string.Empty;
                        var pathEnvVar = key.GetValue(PathEnvVarKey) as string ?? string.Empty;
                        var description = key.GetValue(DescriptionKey) as string ?? string.Empty;

                        return InterpreterFactoryCreator.CreateInterpreterFactory(
                            new InterpreterFactoryCreationOptions {
                                LanguageVersionString = version,
                                Id = id,
                                Description = description,
                                InterpreterPath = path,
                                WindowInterpreterPath = winPath,
                                LibraryPath = libPath,
                                PathEnvironmentVariableName = pathEnvVar,
                                ArchitectureString = arch,
                                WatchLibraryForNewModules = true
                            }
                        );
                    }
                }
            }
            return null;
        }

        public void RemoveInterpreter(Guid id) {
            PythonInterpreterFactoryWithDatabase fact;
            if (_interpreters.TryGetValue(id, out fact)) {
                var collection = PythonInterpreterKey + "\\" + id.ToString("B");
                Registry.CurrentUser.DeleteSubKeyTree(collection);

                _interpreters.Remove(id);
                OnInterpreterFactoriesChanged();
                fact.Dispose();
            }
        }

        public bool IsConfigurable(IPythonInterpreterFactory factory) {
            PythonInterpreterFactoryWithDatabase fact = factory as PythonInterpreterFactoryWithDatabase;
            if (fact == null) {
                return false;
            }
            return _interpreters.ContainsValue(fact);
        }

        public IPythonInterpreterFactory SetOptions(InterpreterFactoryCreationOptions options) {
            var collection = PythonInterpreterKey + "\\" + options.IdString;
            using (var key = Registry.CurrentUser.CreateSubKey(collection, true)) {
                key.SetValue(PathKey, options.InterpreterPath ?? string.Empty);
                key.SetValue(WindowsPathKey, options.WindowInterpreterPath ?? string.Empty);
                key.SetValue(LibraryPathKey, options.LibraryPath ?? string.Empty);
                key.SetValue(ArchitectureKey, options.ArchitectureString);
                key.SetValue(VersionKey, options.LanguageVersionString);
                key.SetValue(PathEnvVarKey, options.PathEnvironmentVariableName ?? string.Empty);
                key.SetValue(DescriptionKey, options.Description ?? string.Empty);
            }

            var newInterp = LoadUserDefinedInterpreter(options.IdString);
            if (newInterp == null) {
                throw new InvalidOperationException("Unable to load user defined interpreter");
            }

            PythonInterpreterFactoryWithDatabase existing;
            if (_interpreters.TryGetValue(newInterp.Id, out existing)) {
                existing.Dispose();
            }
            _interpreters[newInterp.Id] = newInterp;
            OnInterpreterFactoriesChanged();
            return newInterp;
        }

        #region IPythonInterpreterFactoryProvider Members

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            return _interpreters.Values;
        }


        private void DiscoverInterpreterFactories() {
            // look for custom configured interpreters
            bool anyChange = false;
            var notFound = new HashSet<Guid>(_interpreters.Keys);
            using (var key = Registry.CurrentUser.OpenSubKey(PythonInterpreterKey)) {
                if (key != null) {
                    foreach (var guid in key.GetSubKeyNames()) {
                        var interp = LoadUserDefinedInterpreter(guid);
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
                var existing = _interpreters[id];
                _interpreters.Remove(id);
                existing.Dispose();
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
