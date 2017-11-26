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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class CallableInfo : BuiltinClassInfo, ILocatedMember {
        private readonly Lazy<OverloadResult[]> _overloads;
        private readonly EncodedLocation _definition;

        public CallableInfo(
            IPythonType callableType,
            PythonAnalyzer projectState,
            IReadOnlyList<IAnalysisSet> arguments,
            IAnalysisSet returnType,
            EncodedLocation definition
        ) : base(callableType, projectState) {
            Arguments = arguments;
            ReturnType = returnType;
            this["__call__"] = this;
            _overloads = new Lazy<OverloadResult[]>(GenerateOverloads);
            _definition = definition;
        }

        public override string Name => "Callable";

        public override ILocatedMember GetLocatedMember() => this;

        public override IEnumerable<LocationInfo> Locations {
            get {
                var loc = _definition.GetLocationInfo();
                if (loc != null) {
                    yield return loc;
                }
            }
        }

        private OverloadResult[] GenerateOverloads() {
            return new[] {
                new OverloadResult(Arguments.Select(ToParameterResult).ToArray(), Name)
            };
        }

        private ParameterResult ToParameterResult(IAnalysisSet set, int i) {
            return new ParameterResult($"${i}", $"Parameter {i}", set.ToString());
        }

        public IReadOnlyList<IAnalysisSet> Arguments { get; }
        public IAnalysisSet ReturnType { get; }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var def = base.Call(node, unit, args, keywordArgNames);
            return ReturnType ?? def;
        }

        protected override BuiltinInstanceInfo MakeInstance() {
            return new CallableInstanceInfo(this);
        }

        public override IEnumerable<OverloadResult> Overloads => _overloads.Value;
    }

    class CallableInstanceInfo : BuiltinInstanceInfo {
        public CallableInstanceInfo(BuiltinClassInfo klass) : base(klass) {
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return ClassInfo.Call(node, unit, args, keywordArgNames);
        }

        public override IEnumerable<OverloadResult> Overloads => ClassInfo.Overloads;
    }
}
