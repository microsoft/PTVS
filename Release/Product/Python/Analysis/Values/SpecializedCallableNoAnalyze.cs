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

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Provides a built-in function whose analysis we deeply understand.  Created with a custom delegate which
    /// allows custom behavior rather than the typical behavior of returning the return type of the function.
    /// 
    /// This is used for clr.AddReference* and calls to range() both of which we want to be customized in different
    /// ways.
    /// </summary>
    class SpecializedCallableNoAnalyze : SpecializedNamespace {
        private readonly Func<CallExpression, AnalysisUnit, ISet<Namespace>[], ISet<Namespace>> _call;

        public SpecializedCallableNoAnalyze(Namespace original, Func<CallExpression, AnalysisUnit, ISet<Namespace>[], ISet<Namespace>> call)
            : base(original) {
            _call = call;
        }

        public SpecializedCallableNoAnalyze(Namespace original, Namespace inst, Func<CallExpression, AnalysisUnit, ISet<Namespace>[], ISet<Namespace>> call)
            : base(original, inst) {
            _call = call;
        }

        public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
            var realArgs = args;
            if (_inst != null) {
                realArgs = Utils.Concat(_inst.SelfSet, args);
            }
            
            return _call((CallExpression)node, unit, realArgs) ?? EmptySet<Namespace>.Instance;
        }

        protected override SpecializedNamespace Clone(Namespace original, Namespace instance) {
            return new SpecializedCallableNoAnalyze(original, instance, _call);
        }
    }
}
