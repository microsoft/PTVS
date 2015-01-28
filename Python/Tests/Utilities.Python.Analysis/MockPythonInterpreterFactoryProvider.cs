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
using Microsoft.PythonTools.Interpreter;

namespace TestUtilities.Python {
    public class MockPythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
        readonly string _name;
        readonly List<IPythonInterpreterFactory> _factories;

        public MockPythonInterpreterFactoryProvider(string name, params IPythonInterpreterFactory[] factories) {
            _name = name;
            _factories = factories.ToList();
        }

        public override string ToString() {
            return string.Format("{0}: {1}", GetType().Name, _name);
        }

        public void AddFactory(IPythonInterpreterFactory factory) {
            _factories.Add(factory);
            var evt = InterpreterFactoriesChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        public bool RemoveFactory(IPythonInterpreterFactory factory) {
            if (_factories.Remove(factory)) {
                var evt = InterpreterFactoriesChanged;
                if (evt != null) {
                    evt(this, EventArgs.Empty);
                }
                return true;
            }
            return false;
        }

        public void RemoveAllFactories() {
            if (_factories.Any()) {
                _factories.Clear();
                var evt = InterpreterFactoriesChanged;
                if (evt != null) {
                    evt(this, EventArgs.Empty);
                }
            }
        }

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            return _factories;
        }

        public event EventHandler InterpreterFactoriesChanged;
    }
}
