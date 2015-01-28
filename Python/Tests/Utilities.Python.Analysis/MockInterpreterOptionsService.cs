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
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;

namespace TestUtilities.Python {
    public class MockInterpreterOptionsService : IInterpreterOptionsService {
        readonly List<IPythonInterpreterFactoryProvider> _providers;
        readonly IPythonInterpreterFactory _noInterpretersValue;
        IPythonInterpreterFactory _defaultInterpreter;

        public MockInterpreterOptionsService() {
            _providers = new List<IPythonInterpreterFactoryProvider>();
            _noInterpretersValue = new MockPythonInterpreterFactory(Guid.NewGuid(), "No Interpreters", new InterpreterConfiguration(new Version(2, 7)));
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

        public IPythonInterpreterFactory FindInterpreter(Guid id, Version version) {
            return InterpretersOrDefault.FirstOrDefault(f => f.Id == id && f.Configuration.Version == version);
        }

        public IPythonInterpreterFactory FindInterpreter(Guid id, string version) {
            return FindInterpreter(id, Version.Parse(version));
        }

        public IPythonInterpreterFactory FindInterpreter(string id, string version) {
            return FindInterpreter(Guid.Parse(id), Version.Parse(version));
        }

        public IEnumerable<IPythonInterpreterFactoryProvider> KnownProviders {
            get { return _providers; }
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

        public event EventHandler DefaultInterpreterChanged;

        public bool IsInterpreterGeneratingDatabase(IPythonInterpreterFactory interpreter) {
            throw new NotImplementedException();
        }
    }
}
