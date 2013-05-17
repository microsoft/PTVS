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
    class SpecializedCallable : SpecializedNamespace {
        private readonly Func<CallExpression, AnalysisUnit, INamespaceSet[], NameExpression[], INamespaceSet> _call;

        public SpecializedCallable(Namespace original, Func<CallExpression, AnalysisUnit, INamespaceSet[], NameExpression[], INamespaceSet> call)
            : base(original) {
            _call = call;
        }

        public SpecializedCallable(Namespace original, Namespace inst, Func<CallExpression, AnalysisUnit, INamespaceSet[], NameExpression[], INamespaceSet> call)
            : base(original, inst) {
            _call = call;
        }

        internal static SpecializedNamespace MakeSpecializedCallable(Func<CallExpression, AnalysisUnit, INamespaceSet[], NameExpression[], INamespaceSet> dlg, bool analyze, Namespace v) {
            SpecializedNamespace special;
            if (analyze) {
                special = new SpecializedCallable(v, dlg);
            } else {
                special = new SpecializedCallableNoAnalyze(v, dlg);
            }
            return special;
        }

        public override INamespaceSet Call(Node node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgNames) {
            var realArgs = args;
            if (_inst != null) {
                realArgs = Utils.Concat(_inst.SelfSet, args);
            }

            var analyzed = _original.Call(node, unit, args, keywordArgNames);
            var res = _call((CallExpression)node, unit, realArgs, keywordArgNames);
            if (res == null) {
                return analyzed;
            } else if (analyzed.Count == 0) {
                return res;
            } else {
                return res.Union(analyzed);
            }
        }

        protected override SpecializedNamespace Clone(Namespace original, Namespace instance) {
            return new SpecializedCallable(original, instance, _call);
        }
    }
}
