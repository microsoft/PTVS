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
    internal class BoundMethodInfo : AnalysisValue, IHasRichDescription, IHasQualifiedName {
        public BoundMethodInfo(FunctionInfo function, AnalysisValue instance) {
            Function = function;
            Instance = instance;
        }

        public override AnalysisUnit AnalysisUnit {
            get {
                return Function.AnalysisUnit;
            }
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return Function.Call(node, unit, Utils.Concat(Instance.SelfSet, args), keywordArgNames);
        }

        public FunctionInfo Function { get; }
        public AnalysisValue Instance { get; }
        public override IPythonProjectEntry DeclaringModule => Function.DeclaringModule;
        public override int DeclaringVersion => Function.DeclaringVersion;
        public override IEnumerable<LocationInfo> Locations => Function.Locations;

        public IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (Push()) {
                try {
                    return GetRichDescriptionWorker(true).ToArray();
                } finally {
                    Pop();
                }
            }
            return GetRichDescriptionWorker(false);
        }

        private IEnumerable<KeyValuePair<string, string>> GetRichDescriptionWorker(bool includeTypes) {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "method ");
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, Function.FunctionDefinition.Name);

            if (Instance is InstanceInfo ii) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, " of ");
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, ii.ClassInfo.FullyQualifiedName);
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, " objects");
            }

            if (includeTypes) {
                foreach (var kv in FunctionInfo.GetReturnTypeString(Function.GetReturnValue)) {
                    yield return kv;
                }
            }
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.EndOfDeclaration, string.Empty);
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                foreach (var p in Function.Overloads) {
                    yield return p.Parameters.Length > 0 ? p.WithoutLeadingParameters(1) : p;
                }
            }
        }

        public override string Documentation => Function.Documentation;
        public override PythonMemberType MemberType => PythonMemberType.Method;
        public string FullyQualifiedName => Function.FullyQualifiedName;
        public KeyValuePair<string, string> FullyQualifiedNamePair => Function.FullyQualifiedNamePair;

        public override string ToString() {
            var name = Function.FunctionDefinition.Name;
            return "Method" /* + hex(id(self)) */ + " " + name;
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            var bmi = ns as BoundMethodInfo;
            if (bmi == null || (Function.Equals(bmi.Function) && Instance.Equals(bmi.Instance))) {
                return this;
            } else {
                bool changed1, changed2;
                var cmp = UnionComparer.Instances[strength];
                var newFunc = cmp.MergeTypes(Function, bmi.Function, out changed1) as FunctionInfo;
                var newInst = cmp.MergeTypes(Instance, bmi.Instance, out changed2);
                if (newFunc != null && newInst != null && (changed1 | changed2)) {
                    return new BoundMethodInfo(newFunc, newInst);
                }
            }
            return this;
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            var bmi = ns as BoundMethodInfo;
            return bmi != null && Instance.UnionEquals(bmi.Instance, strength) && Function.UnionEquals(bmi.Function, strength);
        }

        internal override int UnionHashCode(int strength) {
            return Instance.UnionHashCode(strength) ^ Function.UnionHashCode(strength);
        }
    }
}
