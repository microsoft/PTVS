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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter {
    public sealed class NoInterpretersInterpreter : IPythonInterpreter {
        private static readonly IBuiltinPythonModule _builtinModule = new FallbackBuiltinModule(PythonLanguageVersion.V37);

        public NoInterpretersInterpreter() { }

        public void Dispose() { }

        public event EventHandler ModuleNamesChanged { add { } remove { } }

        public void Initialize(PythonAnalyzer state) { }
        public IModuleContext CreateModuleContext() => null;
        public IList<string> GetModuleNames() => Array.Empty<string>();
        public IPythonModule ImportModule(string name) => null;

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            var res = _builtinModule.GetAnyMember(id.GetTypeName(PythonLanguageVersion.V37)) as IPythonType;
            if (res == null) {
                throw new KeyNotFoundException("{0} ({1})".FormatInvariant(id, (int)id));
            }
            return res;
        }
    }
}
