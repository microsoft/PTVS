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
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Interpreter {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    class ConfigurablePythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
        private readonly Dictionary<Guid, PythonInterpreterFactoryWithDatabase> _interpreters = new Dictionary<Guid, PythonInterpreterFactoryWithDatabase>();
        private readonly SettingsManager _settings;

        // keys used for storing information about user defined interpreters
        const string PathKey = "InterpreterPath";
        const string WindowsPathKey = "WindowsInterpreterPath";
        const string LibraryPathKey = "LibraryPath";
        const string ArchitectureKey = "Architecture";
        const string VersionKey = "Version";
        const string PathEnvVarKey = "PathEnvironmentVariable";
        const string DescriptionKey = "Description";
        const string PythonInterpreterKey = "PythonTools\\Interpreters";

        [ImportingConstructor]
        private ConfigurablePythonInterpreterFactoryProvider([Import(typeof(SVsServiceProvider), AllowDefault = true)] IServiceProvider provider) {
            _settings = SettingsManagerCreator.GetSettingsManager(provider);
            DiscoverInterpreterFactories();
        }

        private PythonInterpreterFactoryWithDatabase LoadUserDefinedInterpreter(SettingsStore store, string guid) {
            // PythonInterpreters\
            //      Id\
            //          Description
            //          InterpreterPath
            //          WindowsInterpreterPath
            //          Architecture
            //          Version
            //          PathEnvironmentVariable

            Guid id;
            string collection;
            if (Guid.TryParse(guid, out id) && store.CollectionExists((collection = PythonInterpreterKey + "\\" + id.ToString("B")))) {
                var path = store.GetString(collection, PathKey, string.Empty);
                var winPath = store.GetString(collection, WindowsPathKey, string.Empty);
                var libPath = store.GetString(collection, LibraryPathKey, string.Empty);
                var arch = store.GetString(collection, ArchitectureKey, string.Empty);
                var version = store.GetString(collection, VersionKey, string.Empty);
                var pathEnvVar = store.GetString(collection, PathEnvVarKey, string.Empty);
                var description = store.GetString(collection, DescriptionKey, string.Empty);

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
            return null;
        }

        public void RemoveInterpreter(Guid id) {
            PythonInterpreterFactoryWithDatabase fact;
            if (_interpreters.TryGetValue(id, out fact)) {
                var collection = PythonInterpreterKey + "\\" + id.ToString("B");
                var store = _settings.GetWritableSettingsStore(SettingsScope.UserSettings);

                store.DeleteCollection(collection);

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
            var store = _settings.GetWritableSettingsStore(SettingsScope.UserSettings);
            store.CreateCollection(collection);
            store.SetString(collection, PathKey, options.InterpreterPath ?? string.Empty);
            store.SetString(collection, WindowsPathKey, options.WindowInterpreterPath ?? string.Empty);
            store.SetString(collection, LibraryPathKey, options.LibraryPath ?? string.Empty);
            store.SetString(collection, ArchitectureKey, options.ArchitectureString);
            store.SetString(collection, VersionKey, options.LanguageVersionString);
            store.SetString(collection, PathEnvVarKey, options.PathEnvironmentVariableName ?? string.Empty);
            store.SetString(collection, DescriptionKey, options.Description ?? string.Empty);

            var newInterp = LoadUserDefinedInterpreter(store, options.IdString);
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

            var store = _settings.GetReadOnlySettingsStore(SettingsScope.UserSettings);
            if (store.CollectionExists(PythonInterpreterKey)) {
                foreach (var guid in store.GetSubCollectionNames(PythonInterpreterKey)) {
                    var interp = LoadUserDefinedInterpreter(store, guid);
                    if (interp != null) {
                        anyChange |= notFound.Remove(interp.Id);
                        if (!_interpreters.ContainsKey(interp.Id)) {
                            _interpreters[interp.Id] = interp;
                            anyChange = true;
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
