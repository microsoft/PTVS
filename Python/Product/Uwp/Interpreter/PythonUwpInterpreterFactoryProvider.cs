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
using System.Reflection;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Uwp.Interpreter {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    class PythonUwpInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
        private HashSet<IPythonInterpreterFactory> _factories = null;

        public const string DefaultVersion = "3.5";

        public event EventHandler InterpreterFactoriesChanged;

        public PythonUwpInterpreterFactoryProvider() {
            PythonVersion = DefaultVersion;
        }

        public string PythonVersion {
            get; set;
        }

        private void DiscoverFactories() {
            if (_factories == null) {
                var defaultConfig = new InterpreterConfiguration(
                    null,
                    null,
                    null,
                    null,
                    null,
                    ProcessorArchitecture.None,
                    new Version(PythonVersion),
                    InterpreterUIMode.CannotBeDefault
                );

                _factories = new HashSet<IPythonInterpreterFactory>();

                _factories.Add(new PythonUwpInterpreterFactory(defaultConfig));

                var factoriesChanged = InterpreterFactoriesChanged;

                if (factoriesChanged != null) {
                    factoriesChanged(this, new EventArgs());
                }
            }
        }

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            DiscoverFactories();
            return _factories;
        }
    }
}