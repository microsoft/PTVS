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
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter {
    [InterpreterFactoryId(FactoryProviderName)]
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [Export(typeof(CPythonInterpreterFactoryProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class CPythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider, IDisposable {
        private readonly IServiceProvider _site;
        private readonly Dictionary<string, PythonInterpreterInformation> _factories = new Dictionary<string, PythonInterpreterInformation>();
        const string PythonPath = "Software\\Python";
        internal const string FactoryProviderName = CPythonInterpreterFactoryConstants.FactoryProviderName;
        private readonly bool _watchRegistry;
        private readonly HashSet<object> _registryTags;
        private int _ignoreNotifications;
        private bool _initialized;

        private static readonly Version[] ExcludedVersions = new[] {
            new Version(2, 5),
            new Version(3, 0)
        };

        [ImportingConstructor]
        public CPythonInterpreterFactoryProvider(
            [Import(typeof(SVsServiceProvider), AllowDefault = true)] IServiceProvider site = null,
            [Import("Microsoft.VisualStudioTools.MockVsTests.IsMockVs", AllowDefault = true)] object isMockVs = null
        ) : this(site, isMockVs == null) {
        }

        public CPythonInterpreterFactoryProvider(IServiceProvider site, bool watchRegistry) {
            _site = site;
            _watchRegistry = watchRegistry;
            _registryTags = _watchRegistry ? new HashSet<object>() : null;
        }

        protected void Dispose(bool disposing) {
            foreach (var tag in _registryTags.MaybeEnumerate()) {
                RegistryWatcher.Instance.Remove(tag);
            }
            _registryTags?.Clear();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CPythonInterpreterFactoryProvider() {
            Dispose(false);
        }

        private void EnsureInitialized() {
            lock (this) {
                if (!_initialized) {
                    _initialized = true;
                    DiscoverInterpreterFactories();

                    if (_watchRegistry) {
                        _registryTags.Add(StartWatching(RegistryHive.CurrentUser, RegistryView.Default));
                        _registryTags.Add(StartWatching(RegistryHive.LocalMachine, RegistryView.Registry32));
                        if (Environment.Is64BitOperatingSystem) {
                            _registryTags.Add(StartWatching(RegistryHive.LocalMachine, RegistryView.Registry64));
                        }
                    }
                }
            }
        }

        private object StartWatching(RegistryHive hive, RegistryView view, int retries = 5) {
            var tag = RegistryWatcher.Instance.TryAdd(
                hive, view, PythonPath, Registry_PythonPath_Changed,
                recursive: true, notifyValueChange: true, notifyKeyChange: true
            ) ?? RegistryWatcher.Instance.TryAdd(
                hive, view, "Software", Registry_Software_Changed,
                recursive: false, notifyValueChange: false, notifyKeyChange: true
            );

            if (tag == null && retries > 0) {
                Trace.TraceWarning("Failed to watch registry. Retrying {0} more times", retries);
                Thread.Sleep(100);
                StartWatching(hive, view, retries - 1);
            } else if (tag == null) {
                Trace.TraceError("Failed to watch registry");
            }
            return tag;
        }

        #region Registry Watching

        private static bool Exists(RegistryChangedEventArgs e) {
            using (var root = RegistryKey.OpenBaseKey(e.Hive, e.View))
            using (var key = root.OpenSubKey(e.Key)) {
                return key != null;
            }
        }

        private void Registry_PythonPath_Changed(object sender, RegistryChangedEventArgs e) {
            if (!Exists(e)) {
                // Python key no longer exists, so go back to watching
                // Software.
                e.CancelWatcher = true;
                _registryTags.Remove(e.Tag);
                _registryTags.Add(StartWatching(e.Hive, e.View));
            } else {
                DiscoverInterpreterFactories();
            }
        }

        private void Registry_Software_Changed(object sender, RegistryChangedEventArgs e) {
            Registry_PythonPath_Changed(sender, e);
            if (e.CancelWatcher) {
                // Python key no longer exists, we're still watching Software
                return;
            }

            if (RegistryWatcher.Instance.TryAdd(
                e.Hive, e.View, PythonPath, Registry_PythonPath_Changed,
                recursive: true, notifyValueChange: true, notifyKeyChange: true
            ) != null) {
                // Python exists, we no longer need to watch Software
                e.CancelWatcher = true;
            }
        }

        #endregion

        public static InterpreterArchitecture ArchitectureFromExe(string path) {
            try {
                switch (NativeMethods.GetBinaryType(path)) {
                    case ProcessorArchitecture.X86:
                        return InterpreterArchitecture.x86;
                    case ProcessorArchitecture.Amd64:
                        return InterpreterArchitecture.x64;
                }
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(InterpreterArchitecture)));
            }
            return InterpreterArchitecture.Unknown;
        }

        public static Version VersionFromSysVersionInfo(string interpreterPath) {
            Version version = null;
            using (var output = ProcessOutput.RunHiddenAndCapture(
                interpreterPath, "-c", "import sys; print('%s.%s' % (sys.version_info[0], sys.version_info[1]))"
            )) {
                output.Wait();
                if (output.ExitCode == 0) {
                    var versionName = output.StandardOutputLines.FirstOrDefault() ?? "";
                    if (!Version.TryParse(versionName, out version)) {
                        version = null;
                    }
                }
            }

            return version;
        }

        internal void DiscoverInterpreterFactories() {
            if (Volatile.Read(ref _ignoreNotifications) > 0) {
                return;
            }

            // Discover the available interpreters...
            bool anyChanged = false;

            PythonRegistrySearch search = null;
            Dictionary<string, PythonInterpreterInformation> machineFactories = null;

            try {
                search = new PythonRegistrySearch();

                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
                using (var root = baseKey.OpenSubKey(PythonPath)) {
                    search.Search(
                        root,
                        Environment.Is64BitOperatingSystem ? InterpreterArchitecture.Unknown : InterpreterArchitecture.x86
                    );
                }

                machineFactories = new Dictionary<string, PythonInterpreterInformation>();
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var root = baseKey.OpenSubKey(PythonPath)) {
                    search.Search(
                        root,
                        InterpreterArchitecture.x86
                    );
                }

                if (Environment.Is64BitOperatingSystem) {
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (var root = baseKey.OpenSubKey(PythonPath)) {
                        search.Search(
                            root,
                            InterpreterArchitecture.x64
                        );
                    }
                }
            } catch (ObjectDisposedException) {
                // We are aborting, so silently return with no results.
                return;
            }

            var found = search.Interpreters
                .Where(i => !ExcludedVersions.Contains(i.Configuration.Version))
                .ToList();
            var uniqueIds = new HashSet<string>(found.Select(i => i.Configuration.Id));

            // Then update our cached state with the lock held.
            lock (this) {
                foreach (var info in found) {
                    PythonInterpreterInformation existingInfo;
                    if (!_factories.TryGetValue(info.Configuration.Id, out existingInfo) ||
                        info.Configuration != existingInfo.Configuration) {

                        _factories[info.Configuration.Id] = info;
                        anyChanged = true;
                    }
                }

                // Remove any factories we had before and no longer see...
                foreach (var unregistered in _factories.Keys.Except(uniqueIds).ToArray()) {
                    _factories.Remove(unregistered);
                    anyChanged = true;
                }
            }

            if (anyChanged) {
                OnInterpreterFactoriesChanged();
            }
        }


        #region IPythonInterpreterProvider Members

        public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations() {
            EnsureInitialized();

            lock (_factories) {
                return _factories.Values.Select(x => x.Configuration).ToArray();
            }
        }

        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
            EnsureInitialized();

            PythonInterpreterInformation info;
            lock (_factories) {
                _factories.TryGetValue(id, out info);
            }

            return info?.GetOrCreateFactory(CreateFactory);
        }

        private IPythonInterpreterFactory CreateFactory(PythonInterpreterInformation info) {
            return InterpreterFactoryCreator.CreateInterpreterFactory(
                info.Configuration,
                new InterpreterFactoryCreationOptions {
                    WatchFileSystem = true,
                }
            );
        }

        private EventHandler _interpFactoriesChanged;
        public event EventHandler InterpreterFactoriesChanged {
            add {
                EnsureInitialized();
                _interpFactoriesChanged += value;
            }
            remove {
                _interpFactoriesChanged -= value;
            }
        }

        private void OnInterpreterFactoriesChanged() {
            _interpFactoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public object GetProperty(string id, string propName) {
            PythonInterpreterInformation info;

            switch (propName) {
                case PythonRegistrySearch.CompanyPropertyKey:
                    lock (_factories) {
                        if (_factories.TryGetValue(id, out info)) {
                            return info.Vendor;
                        }
                    }
                    break;
                case PythonRegistrySearch.SupportUrlPropertyKey:
                    lock (_factories) {
                        if (_factories.TryGetValue(id, out info)) {
                            return info.SupportUrl;
                        }
                    }
                    break;
                case "PersistInteractive":
                    return true;
            }

            return null;
        }

        #endregion

        private sealed class DiscoverOnDispose : IDisposable {
            private readonly CPythonInterpreterFactoryProvider _provider;

            public DiscoverOnDispose(CPythonInterpreterFactoryProvider provider) {
                _provider = provider;
                Interlocked.Increment(ref _provider._ignoreNotifications);
            }

            public void Dispose() {
                if (Interlocked.Decrement(ref _provider._ignoreNotifications) == 0) {
                    _provider.DiscoverInterpreterFactories();
                }
            }
        }

        internal IDisposable SuppressDiscoverFactories() {
            return new DiscoverOnDispose(this);
        }
    }
}
