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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.PythonTools.Interpreter {
    [Export(typeof(IInterpreterRegistryService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    sealed class InterpreterRegistryService : IInterpreterRegistryService, IDisposable {
        private Lazy<IPythonInterpreterFactoryProvider, IDictionary<string, object>>[] _providers;
        private readonly object _suppressInterpretersChangedLock = new object();
        IPythonInterpreterFactory _noInterpretersValue;
        private int _suppressInterpretersChanged;
        private bool _raiseInterpretersChanged, _factoryChangesWatched;
        private EventHandler _interpretersChanged;

        private readonly Lazy<IInterpreterLog>[] _loggers;
        private const string InterpreterFactoryIdMetadata = "InterpreterFactoryId";

        [ImportingConstructor]
        public InterpreterRegistryService([ImportMany] Lazy<IPythonInterpreterFactoryProvider, IDictionary<string, object>>[] providers, [ImportMany] Lazy<IInterpreterLog>[] loggers) {
            _providers = providers;
            _loggers = loggers;
        }

        public IEnumerable<IPythonInterpreterFactory> Interpreters {
            get {
                return new InterpretersEnumerable(this);
            }
        }

        public IEnumerable<InterpreterConfiguration> Configurations {
            get {
                return GetConfigurations()
                    .Values
                    .OrderBy(config => config.Description)
                    .ThenBy(config => config.Version);
            }
        }

        public IPythonInterpreterFactory FindInterpreter(string id) {
            return GetFactoryProvider(id)?.GetInterpreterFactory(id);
        }

        public event EventHandler InterpretersChanged {
            add {
                EnsureFactoryChangesWatched();

                _interpretersChanged += value;
            }
            remove {
                _interpretersChanged -= value;
            }
        }

        private void EnsureFactoryChangesWatched() {
            if (!_factoryChangesWatched) {
                BeginSuppressInterpretersChangedEvent();
                try {
                    foreach (var provider in GetProviders()) {
                        provider.InterpreterFactoriesChanged += Provider_InterpreterFactoriesChanged;
                    }
                } finally {
                    EndSuppressInterpretersChangedEvent();
                }
                _factoryChangesWatched = true;
            }
        }

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
                OnInterpretersChanged();
            }
        }

        public IEnumerable<IPythonInterpreterFactory> InterpretersOrDefault {
            get {
                bool anyYielded = false;
                foreach (var factory in Interpreters) {
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
                    // Our default value is analysis-only for 3.6, since that is the default
                    // Python we would have shipped with VS.
                    var configuration = new VisualStudioInterpreterConfiguration("AnalysisOnly|3.6", Strings.NoInterpretersDescription, version: new Version(3, 6));
                    _noInterpretersValue = InterpreterFactoryCreator.CreateInterpreterFactory(configuration);
                }
                return _noInterpretersValue;
            }
        }

        private sealed class InterpretersEnumerator : IEnumerator<IPythonInterpreterFactory> {
            private readonly InterpreterRegistryService _owner;
            private readonly IEnumerator<IPythonInterpreterFactory> _e;

            public InterpretersEnumerator(InterpreterRegistryService owner, IEnumerator<IPythonInterpreterFactory> e) {
                _owner = owner;
                _owner.BeginSuppressInterpretersChangedEvent();
                _e = e;
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing) {
                if (disposing) {
                    _e.Dispose();
                }
                _owner.EndSuppressInterpretersChangedEvent();
            }

            ~InterpretersEnumerator() {
                Debug.Fail("Interpreter enumerator should always be disposed");
                Dispose(false);
            }

            public IPythonInterpreterFactory Current { get { return _e.Current; } }
            object IEnumerator.Current { get { return _e.Current; } }
            public bool MoveNext() { return _e.MoveNext(); }
            public void Reset() { _e.Reset(); }
        }

        private sealed class InterpretersEnumerable : IEnumerable<IPythonInterpreterFactory> {
            private readonly InterpreterRegistryService _owner;
            private readonly IEnumerable<IPythonInterpreterFactory> _e;

            private static IList<IPythonInterpreterFactory> GetFactories(IPythonInterpreterFactoryProvider provider) {
                if (provider == null) {
                    return Array.Empty<IPythonInterpreterFactory>();
                }

                while (true) {
                    try {
                        var res = new List<IPythonInterpreterFactory>();
                        foreach (var f in provider.GetInterpreterFactories()) {
                            res.Add(f);
                        }
                        return res;
                    } catch (InvalidOperationException ex) {
                        // Collection changed, so retry
                        Debug.WriteLine("Retrying GetInterpreterFactories because " + ex.Message);
                    }
                }
            }

            public InterpretersEnumerable(InterpreterRegistryService owner) {
                _owner = owner;
                _e = owner._providers
                    .Select(GetFactoryProvider)
                    .SelectMany(GetFactories)
                    .Where(fact => fact != null)
                    .OrderBy(fact => fact.Configuration.Description)
                    .ThenBy(fact => fact.Configuration.Architecture)
                    .ThenBy(fact => fact.Configuration.Version);
            }

            private IPythonInterpreterFactoryProvider GetFactoryProvider(Lazy<IPythonInterpreterFactoryProvider, IDictionary<string, object>> lazy) {
                try {
                    return lazy.Value;
                } catch (CompositionException ce) {
                    _owner.Log("Failed to get interpreter factory value: {0}", ce);
                    return null;
                }
            }

            public IEnumerator<IPythonInterpreterFactory> GetEnumerator() {
                return new InterpretersEnumerator(_owner, _e.GetEnumerator());
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return new InterpretersEnumerator(_owner, _e.GetEnumerator());
            }
        }

        private void OnInterpretersChanged() {
            try {
                BeginSuppressInterpretersChangedEvent();
                for (bool repeat = true; repeat; repeat = _raiseInterpretersChanged, _raiseInterpretersChanged = false) {
                    _interpretersChanged?.Invoke(this, EventArgs.Empty);
                }
            } finally {
                EndSuppressInterpretersChangedEvent();
            }
        }


        public void Dispose() {
            foreach (var provider in _providers.OfType<IDisposable>()) {
                provider.Dispose();
            }
        }

        // Used for testing.
        internal Lazy<IPythonInterpreterFactoryProvider, IDictionary<string, object>>[] SetProviders(Lazy<IPythonInterpreterFactoryProvider, IDictionary<string, object>>[] providers) {
            var oldProviders = _providers;
            _providers = providers;
            foreach (var p in oldProviders) {
                IPythonInterpreterFactoryProvider provider;
                try {
                    provider = p.Value;
                } catch (CompositionException) {
                    continue;
                }
                provider.InterpreterFactoriesChanged -= Provider_InterpreterFactoriesChanged;
            }
            foreach (var p in providers) {
                IPythonInterpreterFactoryProvider provider;
                try {
                    provider = p.Value;
                } catch (CompositionException) {
                    continue;
                }
                provider.InterpreterFactoriesChanged += Provider_InterpreterFactoriesChanged;
            }
            Provider_InterpreterFactoriesChanged(this, EventArgs.Empty);
            return oldProviders;
        }

        private void Provider_InterpreterFactoriesChanged(object sender, EventArgs e) {
            lock (_suppressInterpretersChangedLock) {
                if (_suppressInterpretersChanged > 0) {
                    _raiseInterpretersChanged = true;
                    return;
                }
            }

            OnInterpretersChanged();
        }

        public InterpreterConfiguration FindConfiguration(string id) {
            var factoryProvider = GetFactoryProvider(id);
            if (factoryProvider != null) {
                return factoryProvider
                    .GetInterpreterConfigurations()
                    .Where(x => x.Id == id)
                    .FirstOrDefault();
            }
            return null;
        }

        public object GetProperty(string id, string propName) {
            var factoryProvider = GetFactoryProvider(id);
            return factoryProvider?.GetProperty(id, propName);
        }

        private IPythonInterpreterFactoryProvider GetFactoryProvider(string id) {
            if (string.IsNullOrEmpty(id)) {
                return null;
            }
            var interpAndId = id.Split(new[] { '|' }, 2);
            if (interpAndId.Length == 2) {
                for (int i = 0; i < _providers.Length; i++) {
                    object value;
                    if (_providers[i].Metadata.TryGetValue(InterpreterFactoryIdMetadata, out value) &&
                        value is string &&
                        (string)value == interpAndId[0]) {
                        return LoadFactory(i);
                    }
                }
            }
            return null;
        }

        private void Log(string msg, params object[] args) {
            Log(string.Format(msg, args));
        }

        private Dictionary<string, InterpreterConfiguration> GetConfigurations() {
            Dictionary<string, InterpreterConfiguration> res = new Dictionary<string, InterpreterConfiguration>();
            foreach (var provider in GetProviders()) {
                foreach (var config in provider.GetInterpreterConfigurations()) {
                    res[config.Id] = config;
                }
            }

            return res;
        }

        private IEnumerable<IPythonInterpreterFactoryProvider> GetProviders() {
            foreach (var keyValue in GetProvidersAndMetadata()) {
                yield return keyValue.Key;
            }
        }

        private IEnumerable<KeyValuePair<IPythonInterpreterFactoryProvider, IDictionary<string, object>>> GetProvidersAndMetadata() {
            for (int i = 0; i < _providers.Length; i++) {
                IPythonInterpreterFactoryProvider value = LoadFactory(i);
                if (value != null) {
                    yield return new KeyValuePair<IPythonInterpreterFactoryProvider, IDictionary<string, object>>(value, _providers[i].Metadata);
                }
            }
        }

        /// <summary>
        /// Handles creating the factory value and logging any failures.
        /// </summary>
        private IPythonInterpreterFactoryProvider LoadFactory(int i) {
            IPythonInterpreterFactoryProvider value = null;
            try {
                var provider = _providers[i];
                if (provider != null) {
                    value = provider.Value;
                }
            } catch (CompositionException ce) {
                Log("Failed to get interpreter factory value: {0}", ce);
                _providers[i] = null;
            }

            return value;
        }

        private void Log(string msg) {
            foreach (var logger in _loggers) {
                IInterpreterLog loggerValue = null;
                try {
                    loggerValue = logger.Value;
                } catch (CompositionException) {
                }
                if (loggerValue != null) {
                    loggerValue.Log(msg);
                }
            }
        }

        public void GetSerializationInfo(IPythonInterpreterFactory factory, out string assembly, out string typeName, out Dictionary<string, object> properties) {
            if (factory is ICustomInterpreterSerialization serializer) {
                if (!serializer.GetSerializationInfo(out assembly, out typeName, out properties)) {
                    throw new InvalidOperationException($"Unable to serialize {factory.Configuration.Id}");
                }
                return;
            }

            var fallback = typeof(IPythonInterpreterFactory);
            assembly = fallback.Assembly.Location;
            typeName = "Microsoft.PythonTools.Interpreter.AnalysisOnlyInterpreterFactory";
            properties = new Dictionary<string, object>();
            if (factory.Configuration?.Version != null) {
                properties[nameof(Version)] = factory.Configuration.Version.ToString();
            }
        }
    }

}
