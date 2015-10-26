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
    /// <summary>
    /// Provides a built-in function whose analysis we deeply understand.  Created with a custom delegate which
    /// allows custom behavior rather than the typical behavior of returning the return type of the function.
    /// 
    /// This is used for clr.AddReference* and calls to range() both of which we want to be customized in different
    /// ways.
    /// </summary>
    class SpecializedCallable : SpecializedNamespace {
        private readonly CallDelegate _callable;
        private readonly bool _mergeOriginalAnalysis;

        public SpecializedCallable(AnalysisValue original, CallDelegate callable, bool mergeOriginalAnalysis)
            : base(original) {
            _callable = callable;
            _mergeOriginalAnalysis = mergeOriginalAnalysis;
        }

        public SpecializedCallable(AnalysisValue original, AnalysisValue inst, CallDelegate callable, bool mergeNormalAnalysis)
            : base(original, inst) {
            _callable = callable;
            _mergeOriginalAnalysis = mergeNormalAnalysis;
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var realArgs = args;
            if (_inst != null) {
                realArgs = Utils.Concat(_inst.SelfSet, args);
            }

            var res = _callable(node, unit, args, keywordArgNames);
            if (_mergeOriginalAnalysis && _original != null) {
                return res.Union(_original.Call(node, unit, args, keywordArgNames));
            }

            return res;
        }

        protected override SpecializedNamespace Clone(AnalysisValue original, AnalysisValue instance) {
            return new SpecializedCallable(original, instance, _callable, _mergeOriginalAnalysis);
        }
    }
}
