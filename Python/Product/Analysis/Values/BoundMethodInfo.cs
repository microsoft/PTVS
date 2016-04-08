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
using System.Text;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BoundMethodInfo : AnalysisValue {
        private readonly FunctionInfo _function;
        private readonly AnalysisValue _instanceInfo;

        public BoundMethodInfo(FunctionInfo function, AnalysisValue instance) {
            _function = function;
            _instanceInfo = instance;
        }

        public override AnalysisUnit AnalysisUnit {
            get {
                return _function.AnalysisUnit;
            }
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return _function.Call(node, unit, Utils.Concat(_instanceInfo.SelfSet, args), keywordArgNames);
        }

        public FunctionInfo Function {
            get {
                return _function;
            }
        }

        public AnalysisValue Instance {
            get {
                return _instanceInfo;
            }
        }

        public override IPythonProjectEntry DeclaringModule {
            get {
                return _function.DeclaringModule;
            }
        }

        public override int DeclaringVersion {
            get {
                return _function.DeclaringVersion;
            }
        }

        public override IEnumerable<LocationInfo> Locations {
            get {
                return _function.Locations;
            }
        }

        public override string Description {
            get {
                var result = new StringBuilder();
                result.Append("method ");
                result.Append(_function.FunctionDefinition.Name);
                
                if (_instanceInfo is InstanceInfo) {
                    result.Append(" of ");
                    result.Append(((InstanceInfo)_instanceInfo).ClassInfo.ClassDefinition.Name);
                    result.Append(" objects ");
                }

                FunctionInfo.AddReturnTypeString((text, type) => result.Append(text), _function.GetReturnValue);
                FunctionInfo.AddDocumentationString((text, type) => result.Append(text), _function.Documentation);

                return result.ToString();
            }
        }

        public override string ShortDescription {
            get {
                var result = new StringBuilder();
                result.Append("method ");
                result.Append(_function.FunctionDefinition.Name);

                if (_instanceInfo is InstanceInfo) {
                    result.Append(" of ");
                    result.Append(((InstanceInfo)_instanceInfo).ClassInfo.ClassDefinition.Name);
                    result.Append(" objects ");
                }

                return result.ToString();
            }
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                var p = _function.FunctionDefinition.Parameters;

                var pp = p.Count == 0 ? new ParameterResult[0] : new ParameterResult[p.Count - 1];
                for (int i = 1; i < p.Count; i++) {
                    pp[i - 1] = new ParameterResult(
                        FunctionInfo.MakeParameterName(p[i]),
                        string.Empty,
                        "object",
                        false,
                        null,
                        FunctionInfo.GetDefaultValue(_function.ProjectState, p[i], DeclaringModule.Tree)
                    );
                }
                string doc = _function.Documentation;

                yield return new SimpleOverloadResult(pp, _function.FunctionDefinition.Name, doc);
            }
        }

        public override string Documentation {
            get {
                return _function.Documentation;
            }
        }

        public override PythonMemberType MemberType {
            get {
                return PythonMemberType.Method;
            }
        }

        public override string ToString() {
            var name = _function.FunctionDefinition.Name;
            return "Method" /* + hex(id(self)) */ + " " + name;
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            var bmi = ns as BoundMethodInfo;
            if (bmi == null || (Function.Equals(bmi.Function) && _instanceInfo.Equals(bmi._instanceInfo))) {
                return this;
            } else {
                bool changed1, changed2;
                var cmp = UnionComparer.Instances[strength];
                var newFunc = cmp.MergeTypes(Function, bmi.Function, out changed1) as FunctionInfo;
                var newInst = cmp.MergeTypes(_instanceInfo, bmi._instanceInfo, out changed2);
                if (newFunc != null && newInst != null && (changed1 | changed2)) {
                    return new BoundMethodInfo(newFunc, newInst);
                }
            }
            return this;
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            var bmi = ns as BoundMethodInfo;
            return bmi != null && _instanceInfo.UnionEquals(bmi._instanceInfo, strength) && Function.UnionEquals(bmi.Function, strength);
        }

        internal override int UnionHashCode(int strength) {
            return _instanceInfo.UnionHashCode(strength) ^ Function.UnionHashCode(strength);
        }
    }
}
