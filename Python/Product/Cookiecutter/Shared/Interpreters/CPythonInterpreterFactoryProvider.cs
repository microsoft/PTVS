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

namespace Microsoft.CookiecutterTools.Interpreters
{
    class CPythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider
    {
        private readonly Dictionary<string, PythonInterpreterInformation> _factories = new Dictionary<string, PythonInterpreterInformation>();
        const string PythonPath = "Software\\Python";
        internal const string FactoryProviderName = "Global";
        private bool _initialized;

        public CPythonInterpreterFactoryProvider()
        {
        }

        private void EnsureInitialized()
        {
            lock (this)
            {
                if (!_initialized)
                {
                    _initialized = true;
                    DiscoverInterpreterFactories();
                }
            }
        }

        internal void DiscoverInterpreterFactories()
        {
            // Discover the available interpreters...
            bool anyChanged = false;

            var search = new PythonRegistrySearch();

            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
            using (var root = baseKey.OpenSubKey(PythonPath))
            {
                search.Search(
                    root,
                    Environment.Is64BitOperatingSystem ? InterpreterArchitecture.Unknown : InterpreterArchitecture.x86
                );
            }

            Dictionary<string, PythonInterpreterInformation> machineFactories = new Dictionary<string, PythonInterpreterInformation>();
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var root = baseKey.OpenSubKey(PythonPath))
            {
                search.Search(
                    root,
                    InterpreterArchitecture.x86
                );
            }

            if (Environment.Is64BitOperatingSystem)
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var root = baseKey.OpenSubKey(PythonPath))
                {
                    search.Search(
                        root,
                        InterpreterArchitecture.x64
                    );
                }
            }

            var found = search.Interpreters.ToList();
            var uniqueIds = new HashSet<string>(found.Select(i => i.Configuration.Id));

            // Then update our cached state with the lock held.
            lock (this)
            {
                foreach (var info in found)
                {
                    PythonInterpreterInformation existingInfo;
                    if (!_factories.TryGetValue(info.Configuration.Id, out existingInfo) ||
                        info.Configuration != existingInfo.Configuration)
                    {
                        _factories[info.Configuration.Id] = info;
                        anyChanged = true;
                    }
                }

                // Remove any factories we had before and no longer see...
                foreach (var unregistered in _factories.Keys.Except(uniqueIds).ToArray())
                {
                    _factories.Remove(unregistered);
                    anyChanged = true;
                }
            }

            if (anyChanged)
            {
                OnInterpreterFactoriesChanged();
            }
        }


        #region IPythonInterpreterProvider Members

        public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations()
        {
            EnsureInitialized();

            lock (_factories)
            {
                return _factories.Values.Select(x => x.Configuration).ToArray();
            }
        }

        public IPythonInterpreterFactory GetInterpreterFactory(string id)
        {
            EnsureInitialized();

            PythonInterpreterInformation info;
            lock (_factories)
            {
                _factories.TryGetValue(id, out info);
            }

            return null;
            //return info?.EnsureFactory();
        }

        private EventHandler _interpFactoriesChanged;
        public event EventHandler InterpreterFactoriesChanged
        {
            add
            {
                EnsureInitialized();
                _interpFactoriesChanged += value;
            }
            remove
            {
                _interpFactoriesChanged -= value;
            }
        }

        private void OnInterpreterFactoriesChanged()
        {
            _interpFactoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public object GetProperty(string id, string propName)
        {
            PythonInterpreterInformation info;

            switch (propName)
            {
                case PythonRegistrySearch.CompanyPropertyKey:
                    lock (_factories)
                    {
                        if (_factories.TryGetValue(id, out info))
                        {
                            return info.Vendor;
                        }
                    }
                    break;
                case PythonRegistrySearch.SupportUrlPropertyKey:
                    lock (_factories)
                    {
                        if (_factories.TryGetValue(id, out info))
                        {
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
    }
}
