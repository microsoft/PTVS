/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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
