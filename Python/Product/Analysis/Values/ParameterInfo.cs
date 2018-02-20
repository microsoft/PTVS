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
        private readonly FunctionInfo _function;

        public ParameterInfo(FunctionInfo function, Node node, string name) : base(node) {
            _function = function;
            Name = name;
        }

        public override string Name { get; }

        public override IPythonProjectEntry DeclaringModule => _function.DeclaringModule;
        public override int DeclaringVersion => _function.DeclaringVersion;

        public override IAnalysisSet Resolve(AnalysisUnit unit, ResolutionContext context) {
            if (!context.AnyCaller && context.Caller == null) {
                return this;
            }
            if (_function == context.Caller) {
                return _function.ResolveParameter(unit, Name, context.CallArgs);
            }
            return _function.ResolveParameter(unit, Name);
        }

        public override string ToString() => $"<arg {Name} in {_function.Name}>";
    }

    class ClosureInfo : LazyValueInfo {
        private readonly PythonVariable _variable;

        public ClosureInfo(Node node, PythonVariable variable) : base(node) {
            _variable = variable;
        }

        public override IAnalysisSet Resolve(AnalysisUnit unit, ResolutionContext context) {
            if (context.Closure != null) {
                return context.Closure.TryGetValue(_variable.Name, out var res) ? res : AnalysisSet.Empty;
            }
            return this;
        }

        public override bool Equals(object obj) {
            if (obj is ClosureInfo ci) {
                return ci._variable.Name == _variable.Name;
            }
            return base.Equals(obj);
        }

        public override int GetHashCode() => _variable.Name.GetHashCode();
    }
}
