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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    sealed class NotFoundInterpreter : IPythonInterpreter {
        public void Dispose() { }
        public void Initialize(PythonAnalyzer state) { }
        public IPythonType GetBuiltinType(BuiltinTypeId id) { throw new KeyNotFoundException(); }
        public IList<string> GetModuleNames() { return new string[0]; }
        public event EventHandler ModuleNamesChanged { add { } remove { } }
        public IPythonModule ImportModule(string name) { return null; }
        public IModuleContext CreateModuleContext() { return null; }
    }

    sealed public class NotFoundInterpreterFactory : IPythonInterpreterFactory {
        public NotFoundInterpreterFactory(
            string id,
            Version version,
            string description = null,
            string prefixPath = null,
            InterpreterArchitecture architecture = default(InterpreterArchitecture),
            string descriptionSuffix = null) {
            Configuration = new VisualStudioInterpreterConfiguration(
                id,
                description ?? "Unknown Python {0}{1: ()} (unavailable)".FormatUI(version, architecture),
                prefixPath,
                null,
                null,
                null,
                architecture,
                version,
                InterpreterUIMode.CannotBeDefault | InterpreterUIMode.CannotBeConfigured
            );
        }

        public string Description { get; private set; }
        public InterpreterConfiguration Configuration { get; private set; }
        public Guid Id { get; private set; }
        public void NotifyImportNamesChanged() { }

        public IPythonInterpreter CreateInterpreter() {
            return new NotFoundInterpreter();
        }
    }
}
