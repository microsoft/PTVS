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
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;

namespace Microsoft.PythonTools {
    [Export(typeof(IInterpreterOptionsService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class InterpreterOptionsService : IInterpreterOptionsService {
        private static Guid NoInterpretersFactoryGuid = new Guid("{15CEBB59-1008-4305-97A9-CF5E2CB04711}");

        private const string FactoryProvidersSubKey = PythonCoreConstants.BaseRegistryKey + "\\InterpreterFactories";
        private const string FactoryProviderCodeBaseSetting = "CodeBase";

        private const string DefaultInterpreterOptionsSubKey = PythonCoreConstants.BaseRegistryKey + "\\Options\\Interpreters";
        private const string DefaultInterpreterSetting = "DefaultInterpreter";
        private const string DefaultInterpreterVersionSetting = "DefaultInterpreterVersion";

        private IPythonInterpreterFactoryProvider[] _providers;
        private readonly RegistryView _configRegView;
        private readonly RegistryHive _configRegHive;
        private readonly string _configRegSubkey;
        private readonly RegistryView _userRegView;
        private readonly RegistryHive _userRegHive;
        private readonly string _userRegSubkey;

        private readonly object _suppressInterpretersChangedLock = new object();
        private int _suppressInterpretersChanged;
        private bool _raiseInterpretersChanged;

        IPythonInterpreterFactory _defaultInterpreter;
        IPythonInterpreterFactory _noInterpretersValue;

        static bool Created = false;

        public InterpreterOptionsService() {
            Debug.Assert(!Created);
            Created = true;
            if (!PythonToolsPackage.GetRegistryKeyLocation(PythonToolsPackage.UserRegistryRoot, out _userRegHive, out _userRegView, out _userRegSubkey)) {
                _userRegSubkey = "Software\\Microsoft\\VisualStudio\\" + AssemblyVersionInfo.VSVersion;
                _userRegView = RegistryView.Registry32;
                _userRegHive = RegistryHive.CurrentUser;
            }

            if (!PythonToolsPackage.GetRegistryKeyLocation(PythonToolsPackage.ConfigurationRegistryRoot, out _configRegHive, out _configRegView, out _configRegSubkey)) {
                _configRegSubkey = "Software\\Microsoft\\VisualStudio\\" + AssemblyVersionInfo.VSVersion + "_Config";
                _configRegView = RegistryView.Registry32;
                _configRegHive = RegistryHive.CurrentUser;
            }

            BeginSuppressInterpretersChangedEvent();
            try {
                using (var key = OpenProvidersRegistryKey()) {
                    _providers = (key != null) ?
                        LoadProviders(key).Where(provider => provider != null).ToArray() :
                        new IPythonInterpreterFactoryProvider[0];
                }
            } finally {
                EndSuppressInterpretersChangedEvent();
            }

            foreach (var provider in _providers) {
                provider.InterpreterFactoriesChanged += Provider_InterpreterFactoriesChanged;
            }

            LoadDefaultInterpreter(suppressChangeEvent: true);

            try {
                RegistryWatcher.Instance.Add(_userRegHive, _userRegView, _userRegSubkey + "\\" + DefaultInterpreterOptionsSubKey,
                    DefaultInterpreterRegistry_Changed,
                    recursive: false, notifyValueChange: true, notifyKeyChange: false);
            } catch (ArgumentException) {
                // DefaultInterpreterOptions subkey does not exist yet, so
                // create it and then start the watcher.
                SaveDefaultInterpreter();

                RegistryWatcher.Instance.Add(_userRegHive, _userRegView, _userRegSubkey + "\\" + DefaultInterpreterOptionsSubKey,
                    DefaultInterpreterRegistry_Changed,
                    recursive: false, notifyValueChange: true, notifyKeyChange: false);
            }
        }

        private RegistryKey OpenDefaultInterpreterRegistryKey(bool writable = false) {
            using (var root = RegistryKey.OpenBaseKey(_userRegHive, _userRegView))
            using (var key = root.OpenSubKey(_userRegSubkey, writable)) {
                if (key == null) {
                    return null;
                } else if (writable) {
                    return key.CreateSubKey(DefaultInterpreterOptionsSubKey);
                } else {
                    return key.OpenSubKey(DefaultInterpreterOptionsSubKey);
                }
            }
        }

        private RegistryKey OpenProvidersRegistryKey(bool writable = false) {
            using (var root = RegistryKey.OpenBaseKey(_configRegHive, _configRegView))
            using (var key = root.OpenSubKey(_configRegSubkey, writable)) {
                if (key == null) {
                    return null;
                } else if (writable) {
                    return key.CreateSubKey(FactoryProvidersSubKey);
                } else {
                    return key.OpenSubKey(FactoryProvidersSubKey);
                }
            }
        }

        private void Provider_InterpreterFactoriesChanged(object sender, EventArgs e) {
            lock (_suppressInterpretersChangedLock) {
                if (_suppressInterpretersChanged > 0) {
                    _raiseInterpretersChanged = true;
                    return;
                }
            }
            
            // May have removed the default interpreter, so select a new default
            if (FindInterpreter(DefaultInterpreter.Id, DefaultInterpreter.Configuration.Version) == null) {
                DefaultInterpreter = Interpreters.LastOrDefault();
            }

            var evt = InterpretersChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        private static IEnumerable<IPythonInterpreterFactoryProvider> LoadProviders(RegistryKey key) {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var catalog = new AggregateCatalog();

            foreach (var idStr in key.GetSubKeyNames()) {
                using (var subkey = key.OpenSubKey(idStr, writable: false)) {
                    if (subkey == null) {
                        continue;
                    }

                    var codebase = subkey.GetValue(FactoryProviderCodeBaseSetting) as string;
                    if (!string.IsNullOrEmpty(codebase) && seen.Add(codebase)) {
                        catalog.Catalogs.Add(new AssemblyCatalog(codebase));
                    }
                }
            }

            var container = new CompositionContainer(catalog);
            return container.GetExportedValues<IPythonInterpreterFactoryProvider>();
        }

        public IEnumerable<IPythonInterpreterFactory> Interpreters {
            get {
                return _providers.SelectMany(provider => provider.GetInterpreterFactories())
                    .Where(fact => fact != null)
                    .OrderBy(fact => fact.GetInterpreterDisplay());
            }
        }

        public IPythonInterpreterFactory FindInterpreter(Guid id, Version version) {
            return Interpreters.FirstOrDefault(fact => AreEqual(fact, id, version));
        }

        public IPythonInterpreterFactory FindInterpreter(Guid id, string version) {
            Version parsedVersion;
            if (Version.TryParse(version, out parsedVersion)) {
                return FindInterpreter(id, parsedVersion);
            }
            return null;
        }

        public IPythonInterpreterFactory FindInterpreter(string id, string version) {
            Guid parsedId;
            if (Guid.TryParse(id, out parsedId)) {
                return FindInterpreter(parsedId, version);
            }
            return null;
        }

        public event EventHandler InterpretersChanged;

        private void DefaultInterpreterRegistry_Changed(object sender, RegistryChangedEventArgs e) {
            LoadDefaultInterpreter();
        }

        private static bool AreEqual(IPythonInterpreterFactory factory, Guid id, Version version) {
            return factory != null && factory.Id.Equals(id) && factory.Configuration.Version != null && factory.Configuration.Version.Equals(version);
        }

        private void LoadDefaultInterpreter(bool suppressChangeEvent = false) {
            string idStr, versionStr;
            using (var key = OpenDefaultInterpreterRegistryKey()) {
                if (key == null) {
                    return;
                }
                idStr = key.GetValue(DefaultInterpreterSetting) as string;
                versionStr = key.GetValue(DefaultInterpreterVersionSetting) as string;
            }

            Guid id;
            Version version;
            IPythonInterpreterFactory newDefault;
            if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out id) ||
                string.IsNullOrEmpty(versionStr) || !Version.TryParse(versionStr, out version)) {
                newDefault = null;
            } else {
                newDefault = FindInterpreter(id, version);
            }

            if (suppressChangeEvent) {
                _defaultInterpreter = newDefault;
            } else {
                DefaultInterpreter = newDefault;
            }
        }

        private void SaveDefaultInterpreter() {
            using (var key = OpenDefaultInterpreterRegistryKey(writable: true)) {
                if (_defaultInterpreter == null) {
                    key.SetValue(DefaultInterpreterSetting, Guid.Empty.ToString("B"));
                    key.SetValue(DefaultInterpreterVersionSetting, new Version(2, 6).ToString());
                } else {
                    Debug.Assert(_defaultInterpreter.Id != NoInterpretersFactoryGuid);

                    key.SetValue(DefaultInterpreterSetting, _defaultInterpreter.Id.ToString("B"));
                    key.SetValue(DefaultInterpreterVersionSetting, _defaultInterpreter.Configuration.Version.ToString());
                }
            }
        }

        public IPythonInterpreterFactory DefaultInterpreter {
            get {
                return _defaultInterpreter ?? NoInterpretersValue;
            }
            set {
                var newDefault = value;
                if (newDefault == NoInterpretersValue) {
                    newDefault = null;
                }
                if (newDefault != _defaultInterpreter) {
                    _defaultInterpreter = newDefault;
                    SaveDefaultInterpreter();

                    var evt = DefaultInterpreterChanged;
                    if (evt != null) {
                        evt(this, EventArgs.Empty);
                    }
                }
            }
        }

        public event EventHandler DefaultInterpreterChanged;


        public void BeginSuppressInterpretersChangedEvent() {
            lock (_suppressInterpretersChangedLock) {
                _suppressInterpretersChanged += 1;
            }
        }

        public void EndSuppressInterpretersChangedEvent() {
            bool shouldRaiseEvent = false;
            lock (_suppressInterpretersChangedLock) {
                _suppressInterpretersChanged -= 1;
                
                if (_suppressInterpretersChanged == 0 && _raiseInterpretersChanged) {
                    shouldRaiseEvent = true;
                    _raiseInterpretersChanged = false;
                }
            }

            if (shouldRaiseEvent) {
                var evt = InterpretersChanged;
                if (evt != null) {
                    evt(this, EventArgs.Empty);
                }
            }
        }

        public IEnumerable<IPythonInterpreterFactory> InterpretersOrDefault {
            get {
                bool anyYielded = false;
                foreach (var factory in _providers.SelectMany(provider => provider.GetInterpreterFactories())
                                                  .Where(fact => fact != null)
                                                  .OrderBy(fact => fact.GetInterpreterDisplay())) {
                    yield return factory;
                    anyYielded = true;
                }

                if (!anyYielded) {
                    yield return NoInterpretersValue;
                }
            }
        }

        public IPythonInterpreterFactory NoInterpretersValue {
            get {
                if (_noInterpretersValue == null) {
                    try {
                        _noInterpretersValue = PythonToolsPackage.ComponentModel.GetService<IDefaultInterpreterFactoryCreator>().CreateInterpreterFactory(
                            new Dictionary<InterpreterFactoryOptions, object>() {
                            { InterpreterFactoryOptions.Description, "Python 2.7 - No Interpreters Installed" },
                            { InterpreterFactoryOptions.Guid, NoInterpretersFactoryGuid }
                        }
                        );
                    } catch (Exception ex) {
                        Trace.TraceError("Failed to create NoInterpretersValue:\n{0}", ex);
                    }
                }
                return _noInterpretersValue;
            }
        }


        public IEnumerable<IPythonInterpreterFactoryProvider> KnownProviders {
            get {
                return _providers;
            }
        }
    }
}
