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

using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class ParameterInfo : LazyValueInfo {
        public ParameterInfo(FunctionInfo function, Node node, string name) : base(node) {
            Function = function;
            Name = name;
        }

        public override string Name { get; }
        public FunctionInfo Function { get; }

        public override IPythonProjectEntry DeclaringModule => Function.DeclaringModule;
        public override int DeclaringVersion => Function.DeclaringVersion;

        internal override IAnalysisSet Resolve(AnalysisUnit unit, ResolutionContext context) {
            if (Function == context.Caller && Push()) {
                try {
                    return Function.ResolveParameter(unit, Name, context.CallArgs);
                } finally {
                    Pop();
                }
            }

            if (context.ResolveFully) {
                if (Push()) {
                    try {
                        return Function.ResolveParameter(unit, Name);
                    } finally {
                        Pop();
                    }
                }
                return AnalysisSet.Empty;
            }

            return this;
        }

        internal override void AddReference(Node node, AnalysisUnit analysisUnit) {
            Function.AddParameterReference(node, analysisUnit, Name);
        }

        public override string ToString() => $"<arg {Name} in {Function.Name}>";

        public override bool Equals(object obj) {
            if (obj is ParameterInfo other) {
                return Name == other.Name && Function == other.Function;
            }
            return false;
        }

        public override int GetHashCode() {
            return new { Name, F = Function.Name }.GetHashCode();
        }
    }
}
