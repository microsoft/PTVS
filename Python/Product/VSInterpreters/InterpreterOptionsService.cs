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
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Interpreter {
    [Export(typeof(IInterpreterOptionsService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    sealed class InterpreterOptionsService : IInterpreterOptionsService2, IDisposable {
        internal static Guid NoInterpretersFactoryGuid = new Guid("{15CEBB59-1008-4305-97A9-CF5E2CB04711}");

        // Two locations for specifying factory providers.
        // The first is within the VS 1x.0_Config hive, and is easiest to
        // specify in pkgdef files.
        internal const string FactoryProvidersCollection = @"PythonTools\InterpreterFactories";
        // The second is a static registry entry for the local machine and/or
        // the current user (HKCU takes precedence), intended for being set by
        // other installers.
        private const string FactoryProvidersRegKey = @"Software\Microsoft\PythonTools\" + AssemblyVersionInfo.VSVersion + @"\InterpreterFactories";
        internal const string FactoryProviderCodeBaseSetting = "CodeBase";

        // If this collection exists in the settings provider, no factories will
        // be loaded. This is meant for tests.
        private const string SuppressFactoryProvidersCollection = @"PythonTools\NoInterpreterFactories";

        private const string DefaultInterpreterOptionsCollection = @"PythonTools\Options\Interpreters";
        private const string DefaultInterpreterSetting = "DefaultInterpreter";
        private const string DefaultInterpreterVersionSetting = "DefaultInterpreterVersion";

        private readonly SettingsManager _settings;
        private readonly IVsActivityLog _activityLog;

        private IPythonInterpreterFactoryProvider[] _providers;

        private readonly object _suppressInterpretersChangedLock = new object();
        private int _suppressInterpretersChanged;
        private bool _raiseInterpretersChanged;

        IPythonInterpreterFactory _defaultInterpreter;
        IPythonInterpreterFactory _noInterpretersValue;

        sealed class LockInfo : IDisposable {
            public int _lockCount;
            public readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

            public void Dispose() {
                _lock.Dispose();
            }
        }
        private Dictionary<IPythonInterpreterFactory, Dictionary<object, LockInfo>> _locks;
        private readonly object _locksLock = new object();


        private readonly Thread _serviceThread;
#if !DEV12_OR_LATER
        private readonly SynchronizationContext _serviceContext;
#endif

        [ImportingConstructor]
        public InterpreterOptionsService([Import(typeof(SVsServiceProvider), AllowDefault = true)] IServiceProvider provider) {
            _serviceThread = Thread.CurrentThread;
#if !DEV12_OR_LATER
            _serviceContext = SynchronizationContext.Current;
#endif
            _settings = SettingsManagerCreator.GetSettingsManager(provider);
            if (provider != null) {
                _activityLog = provider.GetService(typeof(SVsActivityLog)) as IVsActivityLog;
            } else if (ServiceProvider.GlobalProvider != null) {
                _activityLog = ServiceProvider.GlobalProvider.GetService(typeof(SVsActivityLog)) as IVsActivityLog;
            }
            Initialize(provider);

            InitializeDefaultInterpreterWatcher(provider);
        }

        public void Dispose() {
            lock (_locksLock) {
                if (_locks != null) {
                    foreach (var dict in _locks.Values) {
                        foreach (var li in dict.Values) {
                            li.Dispose();
                        }
                    }
                    _locks = null;
                }
            }

            foreach (var provider in _providers.OfType<IDisposable>()) {
                provider.Dispose();
            }
        }

        private void InitializeDefaultInterpreterWatcher(IServiceProvider serviceProvider) {
            RegistryKey userSettingsKey;
            if (serviceProvider != null) {
                userSettingsKey = VSRegistry.RegistryRoot(serviceProvider, __VsLocalRegistryType.RegType_UserSettings, false);
            } else {
                userSettingsKey = VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_UserSettings, false);
            }
            using (userSettingsKey) {
                RegistryHive hive;
                RegistryView view;
                string keyName;
                if (RegistryWatcher.GetRegistryKeyLocation(userSettingsKey, out hive, out view, out keyName)) {
                    if (RegistryWatcher.Instance.TryAdd(
                        hive, view, keyName + "\\" + DefaultInterpreterOptionsCollection,
                        DefaultInterpreterRegistry_Changed,
                        recursive: false, notifyValueChange: true, notifyKeyChange: false
                    ) == null) {
                        // DefaultInterpreterOptions subkey does not exist yet, so
                        // create it and then start the watcher.
                        SaveDefaultInterpreter();

                        RegistryWatcher.Instance.Add(
                            hive, view, keyName + "\\" + DefaultInterpreterOptionsCollection,
                            DefaultInterpreterRegistry_Changed,
                            recursive: false, notifyValueChange: true, notifyKeyChange: false
                        );
                    }
                }
            }
        }

        private void Initialize(IServiceProvider serviceProvider) {
            BeginSuppressInterpretersChangedEvent();
            try {
                var store = _settings.GetReadOnlySettingsStore(SettingsScope.Configuration);
                _providers = LoadProviders(store, serviceProvider);

                foreach (var provider in _providers) {
                    provider.InterpreterFactoriesChanged += Provider_InterpreterFactoriesChanged;
                }

                LoadDefaultInterpreter(suppressChangeEvent: true);
            } finally {
                EndSuppressInterpretersChangedEvent();
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
                DefaultInterpreter = Interpreters.LastOrDefault(fact => fact.CanBeAutoDefault());
            }

            var evt = InterpretersChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        private static void LogException(
            IVsActivityLog log,
            string message,
            string path,
            Exception ex,
            IEnumerable<object> data = null
        ) {
            if (log == null) {
                return;
            }

            var fullMessage = string.Format("{1}:{0}{2}{0}{3}",
                Environment.NewLine,
                message,
                ex,
                data == null ? string.Empty : string.Join(Environment.NewLine, data)
            ).Trim();

            if (string.IsNullOrEmpty(path)) {
                log.LogEntry(
                    (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR,
                    "Python Tools",
                    fullMessage
                );
            } else {
                log.LogEntryPath(
                    (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR,
                    "Python Tools",
                    fullMessage,
                    path
                );
            }
        }

        private static void LoadOneProvider(
            string codebase,
            HashSet<string> seen,
            List<ComposablePartCatalog> catalog,
            IVsActivityLog log
        ) {
            if (string.IsNullOrEmpty(codebase)) {
                return;
            }
            
            if (!seen.Add(codebase)) {
                return;
            }

            if (log != null) {
                log.LogEntryPath(
                    (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION,
                    "Python Tools",
                    "Loading interpreter provider assembly",
                    codebase
                );
            }

            AssemblyCatalog assemblyCatalog = null;

            const string FailedToLoadAssemblyMessage = "Failed to load interpreter provider assembly";
            try {
                assemblyCatalog = new AssemblyCatalog(codebase);
            } catch (Exception ex) {
                LogException(log, FailedToLoadAssemblyMessage, codebase, ex);
            }

            if (assemblyCatalog == null) {
                return;
            }

            const string FailedToLoadMessage = "Failed to load interpreter provider";
            try {
                catalog.Add(assemblyCatalog);
            } catch (Exception ex) {
                LogException(log, FailedToLoadMessage, codebase, ex);
            }
        }

        private IPythonInterpreterFactoryProvider[] LoadProviders(
            SettingsStore store,
            IServiceProvider serviceProvider
        ) {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var catalog = new List<ComposablePartCatalog>();

            if (store.CollectionExists(SuppressFactoryProvidersCollection)) {
                return new IPythonInterpreterFactoryProvider[0];
            }

            if (store.CollectionExists(FactoryProvidersCollection)) {
                foreach (var idStr in store.GetSubCollectionNames(FactoryProvidersCollection)) {
                    var key = FactoryProvidersCollection + "\\" + idStr;
                    LoadOneProvider(
                        store.GetString(key, FactoryProviderCodeBaseSetting, ""),
                        seen,
                        catalog,
                        _activityLog
                    );
                }
            }

            foreach (var baseKey in new[] { Registry.CurrentUser, Registry.LocalMachine }) {
                using (var key = baseKey.OpenSubKey(FactoryProvidersRegKey)) {
                    if (key != null) {
                        foreach (var idStr in key.GetSubKeyNames()) {
                            using (var subkey = key.OpenSubKey(idStr)) {
                                if (subkey != null) {
                                    LoadOneProvider(
                                        subkey.GetValue(FactoryProviderCodeBaseSetting, "") as string,
                                        seen,
                                        catalog,
                                        _activityLog
                                    );
                                }
                            }
                        }
                    }
                }
            }

            if (!catalog.Any()) {
                LoadOneProvider(
                    typeof(CPythonInterpreterFactoryConstants).Assembly.Location,
                    seen,
                    catalog,
                    _activityLog
                );
            }

            const string FailedToImportMessage = "Failed to import factory providers";
            var providers = new List<IPythonInterpreterFactoryProvider>();
            var serviceProviderProvider = new MockExportProvider();
            if (serviceProvider != null) {
                serviceProviderProvider.SetExport(typeof(SVsServiceProvider), () => serviceProvider);
            }

            foreach (var part in catalog) {
                var container = new CompositionContainer(part, serviceProviderProvider);
                try {
                    foreach (var provider in container.GetExports<IPythonInterpreterFactoryProvider>()) {
                        if (provider.Value != null) {
                            providers.Add(provider.Value);
                        }
                    }
                } catch (CompositionException ex) {
                    LogException(_activityLog, FailedToImportMessage, null, ex, ex.Errors);
                } catch (ReflectionTypeLoadException ex) {
                    LogException(_activityLog, FailedToImportMessage, null, ex, ex.LoaderExceptions);
                } catch (Exception ex) {
                    LogException(_activityLog, FailedToImportMessage, null, ex);
                }
            }

            return providers.ToArray();
        }

        // Used for testing.
        internal IPythonInterpreterFactoryProvider[] SetProviders(IPythonInterpreterFactoryProvider[] providers) {
            var oldProviders = _providers;
            _providers = providers;
            Provider_InterpreterFactoriesChanged(this, EventArgs.Empty);
            return oldProviders;
        }

        public IEnumerable<IPythonInterpreterFactory> Interpreters {
            get {
                return _providers.SelectMany(provider => provider.GetInterpreterFactories())
                    .Where(fact => fact != null)
                    .OrderBy(fact => fact.Description)
                    .ThenBy(fact => fact.Configuration.Version);
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
#if !DEV12_OR_LATER
            // Pre-VS 2013 has trouble obtaining services from the original
            // thread, so attempt to get back to the thread we started on.
            if (_serviceContext != null && Thread.CurrentThread != _serviceThread) {
                _serviceContext.Post(_ => DefaultInterpreterRegistry_Changed(sender, e), null);
                return;
            }
#endif
            try {
                LoadDefaultInterpreter();
            } catch (InvalidComObjectException) {
                // Race between VS closing and accessing the settings store.
            } catch (Exception ex) {
                try {
                    ActivityLog.LogError(
                        "Python Tools for Visual Studio",
                        string.Format("Exception updating default interpreter: {0}", ex)
                    );
                } catch (InvalidOperationException) {
                    // Can't get the activity log service either. This probably
                    // means we're being used from outside of VS, but also
                    // occurs during some unit tests. We want to debug this if
                    // possible, but generally avoid crashing.
                    Debug.Fail(ex.ToString());
                }
            }
        }

        private static bool AreEqual(IPythonInterpreterFactory factory, Guid id, Version version) {
            return factory != null && factory.Id.Equals(id) && (factory.Configuration.Version == null || factory.Configuration.Version.Equals(version));
        }

        private void LoadDefaultInterpreter(bool suppressChangeEvent = false) {
            string id = null, version = null;
            var store = _settings.GetReadOnlySettingsStore(SettingsScope.UserSettings);
            if (store.CollectionExists(DefaultInterpreterOptionsCollection)) {
                id = store.GetString(DefaultInterpreterOptionsCollection, DefaultInterpreterSetting, string.Empty);
                version = store.GetString(DefaultInterpreterOptionsCollection, DefaultInterpreterVersionSetting, string.Empty);
            }

            var newDefault = FindInterpreter(id, version) ??
                Interpreters.LastOrDefault(fact => fact.CanBeAutoDefault());

            if (suppressChangeEvent) {
                _defaultInterpreter = newDefault;
            } else {
                DefaultInterpreter = newDefault;
            }
        }

        private void SaveDefaultInterpreter() {
            var store = _settings.GetWritableSettingsStore(SettingsScope.UserSettings);

            store.CreateCollection(DefaultInterpreterOptionsCollection);
            if (_defaultInterpreter == null) {
                store.SetString(DefaultInterpreterOptionsCollection, DefaultInterpreterSetting, Guid.Empty.ToString("B"));
                store.SetString(DefaultInterpreterOptionsCollection, DefaultInterpreterVersionSetting, new Version(2, 6).ToString());
            } else {
                Debug.Assert(_defaultInterpreter.Id != NoInterpretersFactoryGuid);

                store.SetString(DefaultInterpreterOptionsCollection, DefaultInterpreterSetting, _defaultInterpreter.Id.ToString("B"));
                store.SetString(DefaultInterpreterOptionsCollection, DefaultInterpreterVersionSetting, _defaultInterpreter.Configuration.Version.ToString());
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
                                                  .OrderBy(fact => fact.Description)
                                                  .ThenBy(fact => fact.Configuration.Version)) {
                    Debug.Assert(factory != NoInterpretersValue);
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
                        _noInterpretersValue = InterpreterFactoryCreator.CreateInterpreterFactory(
                            new InterpreterFactoryCreationOptions {
                                Id = NoInterpretersFactoryGuid,
                                Description = "No Interpreters",
                                LanguageVersion = new Version(2, 7)
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

        public async Task<object> LockInterpreterAsync(IPythonInterpreterFactory factory, object moniker, TimeSpan timeout) {
            LockInfo info;
            Dictionary<object, LockInfo> locks;

            lock (_locksLock) {
                if (_locks == null) {
                    _locks = new Dictionary<IPythonInterpreterFactory, Dictionary<object, LockInfo>>();
                }

                if (!_locks.TryGetValue(factory, out locks)) {
                    _locks[factory] = locks = new Dictionary<object, LockInfo>();
                }

                if (!locks.TryGetValue(moniker, out info)) {
                    locks[moniker] = info = new LockInfo();
                }
            }

            Interlocked.Increment(ref info._lockCount);
            bool result = false;
            try {
                result = await info._lock.WaitAsync(timeout.TotalDays > 1 ? Timeout.InfiniteTimeSpan : timeout);
                return result ? (object)info : null;
            } finally {
                if (!result) {
                    Interlocked.Decrement(ref info._lockCount);
                }
            }
        }

        public bool IsInterpreterLocked(IPythonInterpreterFactory factory, object moniker) {
            LockInfo info;
            Dictionary<object, LockInfo> locks;

            lock (_locksLock) {
                return _locks != null &&
                    _locks.TryGetValue(factory, out locks) &&
                    locks.TryGetValue(moniker, out info) &&
                    info._lockCount > 0;
            }
        }

        public bool UnlockInterpreter(object cookie) {
            var info = cookie as LockInfo;
            if (info == null) {
                throw new ArgumentException("cookie was not returned from a call to LockInterpreterAsync");
            }

            bool res = Interlocked.Decrement(ref info._lockCount) == 0;
            info._lock.Release();
            return res;
        }
    }
}
