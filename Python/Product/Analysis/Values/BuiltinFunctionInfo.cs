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
using System.Collections.ObjectModel;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BuiltinFunctionInfo : BuiltinNamespace<IPythonType> {
        private IPythonFunction _function;
        private string _doc;
        private ReadOnlyCollection<OverloadResult> _overloads;
        private readonly IAnalysisSet _returnTypes;
        private BuiltinMethodInfo _method;

        public BuiltinFunctionInfo(IPythonFunction function, PythonAnalyzer projectState)
            : base(projectState.Types[BuiltinTypeId.BuiltinFunction], projectState) {

            _function = function;
            _returnTypes = Utils.GetReturnTypes(function, projectState);
        }

        public override IPythonType PythonType {
            get { return _type; }
        }

        internal override bool IsOfType(IAnalysisSet klass) {
            return klass.Contains(ProjectState.ClassInfos[BuiltinTypeId.Function]) ||
                klass.Contains(ProjectState.ClassInfos[BuiltinTypeId.BuiltinFunction]);
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return _returnTypes.GetInstanceType();
        }

        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            if (_function.IsClassMethod) {
                instance = context;
            }

            if (_function.IsStatic || instance.IsOfType(ProjectState.ClassInfos[BuiltinTypeId.NoneType])) {
                return base.GetDescriptor(node, instance, context, unit);
            } else if (_method == null) {
                _method = new BuiltinMethodInfo(_function, PythonMemberType.Method, ProjectState);
            }

            return _method.GetDescriptor(node, instance, context, unit);
        }

        public IPythonFunction Function {
            get {
                return _function;
            }
        }

        public override string Name {
            get {
                return _function.Name;
            }
        }

        public override string Description {
            get {
                string res;
                if (_function.IsBuiltin) {
                    res = "built-in function " + _function.Name;
                } else {
                    res = "function " + _function.Name;
                }

                if (!string.IsNullOrWhiteSpace(Documentation)) {
                    res += System.Environment.NewLine + Documentation;
                }
                return res;
            }
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                if (_overloads == null) {
                    var overloads = _function.Overloads;
                    var result = new OverloadResult[overloads.Count];
                    for (int i = 0; i < result.Length; i++) {
                        result[i] = new BuiltinFunctionOverloadResult(ProjectState, _function.Name, overloads[i], 0, GetDoc);
                    }
                    _overloads = new ReadOnlyCollection<OverloadResult>(result);
                }
                return _overloads;
            }
        }

        // can't create delegate to property...
        private string GetDoc() {
            return Documentation;
        }

        public override string Documentation {
            get {
                if (_doc == null) {
                    _doc = Utils.StripDocumentation(_function.Documentation);
                }
                return _doc;
            }
        }

        public override PythonMemberType MemberType {
            get {
                return _function.MemberType;
            }
        }

        public override ILocatedMember GetLocatedMember() {
            return _function as ILocatedMember;
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ns is BuiltinFunctionInfo || ns is FunctionInfo || ns == ProjectState.ClassInfos[BuiltinTypeId.Function].Instance;
            }
            return base.UnionEquals(ns, strength);
        }

        internal override int UnionHashCode(int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ProjectState.ClassInfos[BuiltinTypeId.Function].Instance.UnionHashCode(strength);
            }
            return base.UnionHashCode(strength);
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ProjectState.ClassInfos[BuiltinTypeId.Function].Instance;
            }
            return base.UnionMergeTypes(ns, strength);
        }
    }
}
