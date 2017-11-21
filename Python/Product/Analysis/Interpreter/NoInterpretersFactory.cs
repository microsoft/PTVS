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
using System.Linq;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter {
    public sealed class NoInterpretersFactory : IPythonInterpreterFactory {
        public NoInterpretersFactory() {
            Configuration = new InterpreterConfiguration(
                InterpreterRegistryConstants.NoInterpretersFactoryId,
                Strings.NoInterpretersDescription,
                uiMode: InterpreterUIMode.CannotBeDefault | InterpreterUIMode.CannotBeConfigured
            );
        }

        public InterpreterConfiguration Configuration { get; }

        public IPackageManager PackageManager => null;

        public IPythonInterpreter CreateInterpreter() {
            return new NoInterpretersInterpreter(PythonTypeDatabase.CreateDefaultTypeDatabase());
        }
    }

    class NoInterpretersInterpreter : IPythonInterpreter {
        private readonly PythonTypeDatabase _database;

        public NoInterpretersInterpreter(PythonTypeDatabase database) {
            _database = database;
        }

        public void Dispose() { }

        public event EventHandler ModuleNamesChanged { add { } remove { } }

        public void Initialize(PythonAnalyzer state) { }
        public IModuleContext CreateModuleContext() => null;
        public IList<string> GetModuleNames() => _database.GetModuleNames().ToList();
        public IPythonModule ImportModule(string name) => _database.GetModule(name);

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            if (id == BuiltinTypeId.Unknown || _database.BuiltinModule == null) {
                return null;
            }
            var name = SharedDatabaseState.GetBuiltinTypeName(id, _database.LanguageVersion);
            var res = _database.BuiltinModule.GetAnyMember(name) as IPythonType;
            if (res == null) {
                throw new KeyNotFoundException(string.Format("{0} ({1})", id, (int)id));
            }
            return res;
        }
    }
}
