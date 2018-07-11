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
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    sealed class AnalysisOnlyInterpreterFactory : IPythonInterpreterFactory, ICustomInterpreterSerialization {
        private readonly static InterpreterFactoryCreationOptions DefaultCreationOptions = new InterpreterFactoryCreationOptions {
            WatchFileSystem = false
        };

        public InterpreterConfiguration Configuration { get; }


        public AnalysisOnlyInterpreterFactory(InterpreterConfiguration config) {
            Configuration = config;
        }

        public AnalysisOnlyInterpreterFactory(Version version, string description = null)
            : this(GetConfiguration(version, description ?? "Analysis Interpreter {0}".FormatUI(version))) {
        }


        internal AnalysisOnlyInterpreterFactory(Dictionary<string, object> properties)
            : this(InterpreterConfiguration.FromDictionary(properties)) {
        }

        private static InterpreterConfiguration GetConfiguration(Version version, string description) {
            return new InterpreterConfiguration(
                "AnalysisOnly|{0}|{1}".FormatInvariant(version, description),
                description,
                arch: InterpreterArchitecture.Unknown,
                version: version,
                uiMode: InterpreterUIMode.CannotBeDefault | InterpreterUIMode.CannotBeConfigured
            );
        }

        public bool GetSerializationInfo(out string assembly, out string typeName, out Dictionary<string, object> properties) {
            assembly = GetType().Assembly.Location;
            typeName = GetType().FullName;
            properties = new Dictionary<string, object> {
                { nameof(Version), Configuration.Version.ToString() }
            };
            return true;
        }

        public IPythonInterpreter CreateInterpreter() {
            return new AnalysisOnlyInterpreter(this);
        }

        public void NotifyImportNamesChanged() { }
    }

    sealed class AnalysisOnlyInterpreter : IPythonInterpreter2 {
        private IBuiltinPythonModule _builtins;

        public AnalysisOnlyInterpreter(IPythonInterpreterFactory factory) {
            Factory = factory;
        }

        public IPythonInterpreterFactory Factory { get; }

        public event EventHandler ModuleNamesChanged { add { } remove { } }

        public IModuleContext CreateModuleContext() => null;

        public void Dispose() { }

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            return _builtins?.GetAnyMember(id.GetTypeName(Factory.Configuration.Version)) as IPythonType;
        }

        public IList<string> GetModuleNames() {
            if (_builtins != null) {
                return new[] { _builtins.Name };
            }
            return Array.Empty<string>();
        }

        public Task<IPythonModule> ImportModuleAsync(string name, CancellationToken token) => Task.FromResult(ImportModule(name));

        public IPythonModule ImportModule(string name) {
            if (_builtins != null && _builtins.Name == name) {
                return _builtins;
            }
            return null;
        }

        public void Initialize(PythonAnalyzer state) {
            _builtins = state.BuiltinModule.InterpreterModule as IBuiltinPythonModule;
        }
    }
}
