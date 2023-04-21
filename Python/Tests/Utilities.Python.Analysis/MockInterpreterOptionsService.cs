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
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;

namespace TestUtilities.Python {
    public class MockInterpreterOptionsService : IInterpreterOptionsService, IInterpreterRegistryService {
        readonly List<IPythonInterpreterFactoryProvider> _providers;
        readonly IPythonInterpreterFactory _noInterpretersValue;
        IPythonInterpreterFactory _defaultInterpreter;

        readonly Dictionary<IPythonInterpreterFactory, IReadOnlyList<IPackageManager>> _packageManagers;

        public MockInterpreterOptionsService() {
            _providers = new List<IPythonInterpreterFactoryProvider>();
            _noInterpretersValue = new MockPythonInterpreterFactory(new VisualStudioInterpreterConfiguration("2.7", "No Interpreters", version: new Version(2, 7)));
            _packageManagers = new Dictionary<IPythonInterpreterFactory, IReadOnlyList<IPackageManager>>();
        }

        public void AddProvider(IPythonInterpreterFactoryProvider provider) {
            _providers.Add(provider);
            provider.InterpreterFactoriesChanged += provider_InterpreterFactoriesChanged;
            var evt = InterpretersChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        public void ClearProviders() {
            foreach (var p in _providers) {
                p.InterpreterFactoriesChanged -= provider_InterpreterFactoriesChanged;
            }
            _providers.Clear();
            var evt = InterpretersChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        void provider_InterpreterFactoriesChanged(object sender, EventArgs e) {
            var evt = InterpretersChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }
        
        
        public IEnumerable<IPythonInterpreterFactory> Interpreters {
            get { return _providers.Where(p => p != null).SelectMany(p => p.GetInterpreterFactories()); }
        }

        public IEnumerable<InterpreterConfiguration> Configurations {
            get { return _providers.Where(p => p != null).SelectMany(p => p.GetInterpreterFactories()).Select(x => x.Configuration); }
        }

        public IEnumerable<IPythonInterpreterFactory> InterpretersOrDefault {
            get {
                if (Interpreters.Any()) {
                    return Interpreters;
                }
                return Enumerable.Repeat(_noInterpretersValue, 1);
            }
        }

        public IPythonInterpreterFactory NoInterpretersValue {
            get { return _noInterpretersValue; }
        }

        public event EventHandler InterpretersChanged;

        public void BeginSuppressInterpretersChangedEvent() {
            throw new NotImplementedException();
        }

        public void EndSuppressInterpretersChangedEvent() {
            throw new NotImplementedException();
        }

        public IPythonInterpreterFactory DefaultInterpreter {
            get {
                return _defaultInterpreter ?? _noInterpretersValue;
            }
            set {
                if (value == _noInterpretersValue) {
                    value = null;
                }
                if (value != _defaultInterpreter) {
                    _defaultInterpreter = value;
                    var evt = DefaultInterpreterChanged;
                    if (evt != null) {
                        evt(this, EventArgs.Empty);
                    }
                }
            }
        }

        public string DefaultInterpreterId {
            get {
                return DefaultInterpreter?.Configuration?.Id;
            }

            set {
                DefaultInterpreter = FindInterpreter(value);
            }
        }

        public event EventHandler DefaultInterpreterChanged;
        public event EventHandler InterpreterDiscoveryCompleted {
            add { }
            remove { }
        }

        public bool IsInterpreterGeneratingDatabase(IPythonInterpreterFactory interpreter) {
            throw new NotImplementedException();
        }

        public void RemoveConfigurableInterpreter(string id) {
            throw new NotImplementedException();
        }

        public bool IsConfigurable(string id) {
            return true;
            //throw new NotImplementedException();
        }

        public IPythonInterpreterFactory FindInterpreter(string id) {
            foreach (var interp in _providers) {
                foreach (var config in interp.GetInterpreterConfigurations()) {
                    if (config.Id == id) {
                        return interp.GetInterpreterFactory(id);
                    }
                }
            }
            return null;
        }

        public Task<object> LockInterpreterAsync(IPythonInterpreterFactory factory, object moniker, TimeSpan timeout) {
            throw new NotImplementedException();
        }

        public bool IsInterpreterLocked(IPythonInterpreterFactory factory, object moniker) {
            throw new NotImplementedException();
        }

        public bool UnlockInterpreter(object cookie) {
            throw new NotImplementedException();
        }

        public InterpreterConfiguration FindConfiguration(string id) {
            foreach (var interp in _providers) {
                foreach (var config in interp.GetInterpreterConfigurations()) {
                    if (config.Id == id) {
                        return config;
                    }
                }
            }
            return null;
        }

        public string AddConfigurableInterpreter(string name, InterpreterConfiguration config) {
            throw new NotImplementedException();
        }

        public object GetProperty(string id, string propName) {
            foreach (var interp in _providers) {
                foreach (var config in interp.GetInterpreterConfigurations()) {
                    if (config.Id == id) {
                        return interp.GetProperty(id, propName);
                    }
                }
            }
            return null;
        }

        public void AddPackageManagers(IPythonInterpreterFactory factory, IReadOnlyList<IPackageManager> packageManagers) {
            _packageManagers[factory] = packageManagers;
        }

        public IEnumerable<IPackageManager> GetPackageManagers(IPythonInterpreterFactory factory) {
            try {
                return _packageManagers[factory];
            } catch (KeyNotFoundException) {
                return Enumerable.Empty<IPackageManager>();
            }
        }
    }
}
