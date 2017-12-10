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
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BoundMethodInfo : AnalysisValue, IHasRichDescription {
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

        public IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "method ");
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, _function.FunctionDefinition.Name);

            var ii = _instanceInfo as InstanceInfo;
            if (ii != null) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, " of ");
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, ii.ClassInfo.ClassDefinition.Name);
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, " objects ");
            }

            foreach (var kv in FunctionInfo.GetReturnTypeString(_function.GetReturnValue)) {
                yield return kv;
            }

            bool needsNl = true;
            var nlKind = WellKnownRichDescriptionKinds.EndOfDeclaration;

            foreach (var kv in FunctionInfo.GetDocumentationString(_function.Documentation)) {
                if (needsNl) {
                    yield return new KeyValuePair<string, string>(nlKind, "\r\n");
                    nlKind = WellKnownRichDescriptionKinds.Misc;
                    needsNl = false;
                }
                yield return kv;
            }
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                foreach (var p in _function.Overloads) {
                    yield return p.Parameters.Length > 0 ? p.WithNewParameters(p.Parameters.Skip(1).ToArray()) : p;
                }
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
