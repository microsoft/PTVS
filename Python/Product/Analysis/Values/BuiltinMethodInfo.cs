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
    internal class BuiltinMethodInfo : BuiltinNamespace<IPythonType>, IHasRichDescription {
        private readonly IPythonFunction _function;
        private readonly PythonMemberType _memberType;
        internal readonly bool _fromFunction;
        private string _doc;
        private readonly IAnalysisSet _returnTypes;
        private BoundBuiltinMethodInfo _boundMethod;

        public BuiltinMethodInfo(IPythonMethodDescriptor method, PythonAnalyzer projectState)
            : base(projectState.Types[BuiltinTypeId.BuiltinMethodDescriptor], projectState) {
            var function = method.Function;
            _memberType = method.MemberType;
            _function = function;
            _returnTypes = Utils.GetReturnTypes(function, projectState);
        }

        public BuiltinMethodInfo(IPythonFunction function, PythonMemberType memType, PythonAnalyzer projectState)
            : base(projectState.Types[BuiltinTypeId.BuiltinMethodDescriptor], projectState) {
            _memberType = memType;
            _function = function;
            _returnTypes = Utils.GetReturnTypes(function, projectState);
            _fromFunction = true;
        }

        public override IPythonType PythonType => _type;

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return _returnTypes.GetInstanceType();
        }

        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            if (instance == ProjectState._noneInst) {
                return base.GetDescriptor(node, instance, context, unit);
            }

            if (_boundMethod == null) {
                _boundMethod = new BoundBuiltinMethodInfo(this);
            }

            return _boundMethod.SelfSet;
        }

        public IEnumerable<KeyValuePair<string, string>> GetRichDescription()
            => BuiltinFunctionInfo.GetRichDescription(string.Empty, _function);

        public IAnalysisSet ReturnTypes => _returnTypes;
        public IPythonFunction Function => _function;

        public override IEnumerable<OverloadResult> Overloads {
            get {
                return Function.Overloads.Select(overload => 
                    new BuiltinFunctionOverloadResult(
                        ProjectState,
                        _function.Name,
                        overload,
                        0,
                        new ParameterResult("self")
                    )
                );
            }
        }

        public override string Documentation {
            get {
                if (_doc == null) {
                    var doc = new StringBuilder();
                    foreach (var overload in Function.Overloads) {
                        doc.Append(Utils.StripDocumentation(overload.Documentation));
                    }
                    _doc = doc.ToString();
                    if (string.IsNullOrWhiteSpace(_doc)) {
                        _doc = Utils.StripDocumentation(Function.Documentation);
                    }
                }
                return _doc;
            }
        }

        public override PythonMemberType MemberType => _memberType;
        public override string Name => _function.Name;
        public override ILocatedMember GetLocatedMember() => _function as ILocatedMember;

        public override int GetHashCode() => new { hc1 = base.GetHashCode(), hc2 = _function.GetHashCode() }.GetHashCode();
        public override bool Equals(object obj) => base.Equals(obj) && obj is BuiltinMethodInfo bmi && _function.Equals(bmi._function);
    }
}
