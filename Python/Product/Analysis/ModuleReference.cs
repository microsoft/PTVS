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
using Microsoft.PythonTools.Analysis.Values;

namespace Microsoft.PythonTools.Analysis {
    class ModuleReference {
        public IModule Module;

        private readonly Lazy<HashSet<ModuleInfo>> _references = new Lazy<HashSet<ModuleInfo>>();
        private string _name;

        public ModuleReference(IModule module = null, string name = null) {
            Module = module;
            _name = name;
        }

        public string Name => (_name ?? AnalysisModule?.Name) ?? string.Empty;

        public AnalysisValue AnalysisModule {
            get {
                return Module as AnalysisValue;
            }
        }

        public bool AddReference(ModuleInfo module) {
            return _references.Value.Add(module);
        }

        public bool RemoveReference(ModuleInfo module) => _references.IsValueCreated && _references.Value.Remove(module);

        public bool HasReferences => _references.IsValueCreated && _references.Value.Any();

        public IEnumerable<ModuleInfo> References {
            get {
                return _references.IsValueCreated ? _references.Value : Enumerable.Empty<ModuleInfo>();
            }
        }
    }
}
