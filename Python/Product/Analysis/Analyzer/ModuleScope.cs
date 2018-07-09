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

using Microsoft.PythonTools.Analysis.Values;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    sealed class ModuleScope : InterpreterScope {

        public ModuleScope(ModuleInfo moduleInfo)
            : base(moduleInfo, null) {
        }

        private ModuleScope(ModuleScope scope)
            : base(scope.AnalysisValue, scope, true) {
        }

        internal void SetModuleVariable(string name, IAnalysisSet value) {
            CreateVariable(null, null, name, addRef: false).AddTypes(Module.ProjectEntry, value);
        }

        public ModuleInfo Module { get { return AnalysisValue as ModuleInfo; } }

        public override string Name {
            get { return Module.Name; }
        }

        public override bool AssignVariable(string name, Parsing.Ast.Node location, AnalysisUnit unit, IAnalysisSet values) {
            if (base.AssignVariable(name, location, unit, values)) {
                Module.ModuleDefinition.EnqueueDependents();
                return true;
            }

            return false;
        }

        public ModuleScope CloneForPublish() {
            return new ModuleScope(this);
        }
    }
}
