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
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Uap.Interpreter {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    class PythonUapInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
        private HashSet<IPythonInterpreterFactory> _factories = null;

        public event EventHandler InterpreterFactoriesChanged;

        public PythonUapInterpreterFactoryProvider() {
        }

        private void DiscoverFactories() {
            if (_factories == null) {
                _factories = new HashSet<IPythonInterpreterFactory>();

                _factories.Add(new PythonUapInterpreterFactory());

                if (InterpreterFactoriesChanged != null) {
                    InterpreterFactoriesChanged(this, new EventArgs());
                }
            }
        }

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            DiscoverFactories();
            return _factories;
        }
    }
}