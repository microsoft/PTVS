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

using System.Collections.Generic;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class BoundBuiltinMethodInfo : BuiltinNamespace<IPythonType> {
        private readonly BuiltinMethodInfo _method;
        private OverloadResult[] _overloads;

        public BoundBuiltinMethodInfo(BuiltinMethodInfo method)
            : base(method.PythonType, method.ProjectState) {
            _method = method;
        }

        public BoundBuiltinMethodInfo(IPythonBoundFunction function, PythonAnalyzer projectState)
            : this(new BuiltinMethodInfo(function.Function, PythonMemberType.Method, projectState)) {
        }

        public override PythonMemberType MemberType => _method.MemberType;

        public override IPythonType PythonType => base._type;

        public BuiltinMethodInfo Method => _method;

        public override string Documentation => _method.Documentation;

        public override string Description {
            get {
                if (_method.Function.IsBuiltin) {
                    return "bound built-in method " + _method.Name;
                }
                return "bound method " + _method.Name;
            }
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            // Check if method returns self
            var returnType = _method.ReturnTypes.GetInstanceType();
            if (args.Length > 0 && returnType.Split(v => v is BuiltinInstanceInfo biif && biif.PythonType == _method.Function?.DeclaringType, out _, out _)) {
                return args[0]; //  Return actual self (i.e. derived class)
            }
            return returnType;
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                if (_overloads == null) {
                    var overloads = _method.Function.Overloads;
                    var result = new OverloadResult[overloads.Count];
                    for (int i = 0; i < result.Length; i++) {
                        result[i] = new BuiltinFunctionOverloadResult(_method.ProjectState, _method.Name, overloads[i], _method._fromFunction ? 1 : 0);
                    }
                    _overloads = result;
                }
                return _overloads;
            }
        }
    }
}
