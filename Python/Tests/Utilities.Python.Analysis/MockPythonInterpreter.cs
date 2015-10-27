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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;

namespace TestUtilities.Python {
    public class MockPythonInterpreter : IPythonInterpreter {
        public readonly List<string> _modules;
        public bool IsDatabaseInvalid;

        public MockPythonInterpreter(IPythonInterpreterFactory factory) {
            _modules = new List<string>();
        }


        public void Initialize(PythonAnalyzer state) { }

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            throw new KeyNotFoundException();
        }

        public IList<string> GetModuleNames() {
            return _modules;
        }

        public event EventHandler ModuleNamesChanged { add { } remove { } }

        public IPythonModule ImportModule(string name) {
            if (_modules.Contains(name)) {
                return null;
            }
            return null;
        }

        public IModuleContext CreateModuleContext() {
            throw new NotImplementedException();
        }

        public Task AddReferenceAsync(ProjectReference reference, CancellationToken cancellationToken = default(CancellationToken)) {
            throw new NotImplementedException();
        }

        public void RemoveReference(ProjectReference reference) {
            throw new NotImplementedException();
        }
    }
}
