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

using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BuiltinEventInfo : BuiltinNamespace<IPythonType> {
        private readonly IPythonEvent _value;
        private string _doc;

        public BuiltinEventInfo(IPythonEvent value, PythonAnalyzer projectState)
            : base(value.EventHandlerType, projectState) {
            _value = value;
            _doc = null;
        }

        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, IAnalysisSet value) {
            base.AugmentAssign(node, unit, value);
            var args = GetEventInvokeArgs(ProjectState);
            foreach (var r in value) {
                r.Call(node, unit, args, ExpressionEvaluator.EmptyNames);
            }
        }

        internal IAnalysisSet[] GetEventInvokeArgs(PythonAnalyzer state) {
            var p = _value.GetEventParameterTypes();

            var args = new IAnalysisSet[p.Count];
            for (int i = 0; i < p.Count; i++) {
                args[i] = state.GetInstance(p[i]).SelfSet;
            }
            return args;
        }

        public override string Description {
            get {
                return "event of type " + _value.EventHandlerType.Name;
            }
        }

        public override PythonMemberType MemberType {
            get {
                return _value.MemberType;
            }
        }

        public override string Documentation {
            get {
                if (_doc == null) {
                    _doc = Utils.StripDocumentation(_value.Documentation);
                }
                return _doc;
            }
        }

        public override ILocatedMember GetLocatedMember() {
            return _value as ILocatedMember;
        }
    }
}
