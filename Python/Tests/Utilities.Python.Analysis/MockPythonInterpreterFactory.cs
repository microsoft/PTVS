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
using Microsoft.PythonTools.Interpreter;

namespace TestUtilities.Python {
    public class MockPythonInterpreterFactory : IPythonInterpreterFactory, IDisposable {
        internal bool? _success;
        public Dictionary<string, object> _properties;

        public MockPythonInterpreterFactory(
            InterpreterConfiguration config,
            bool withStatusUpdater = false
        ) {
            Configuration = config;
        }

        public void Dispose() {
        }

        public InterpreterConfiguration Configuration { get; }

        public Dictionary<string, object> Properties {
            get {
                if (_properties == null) {
                    _properties = new Dictionary<string, object>();
                }
                return _properties;
            }
        }

        public object GetProperty(string propName) {
            object value = null;
            _properties?.TryGetValue(propName, out value);
            return value;
        }

        public void NotifyImportNamesChanged() { }
    }
}
