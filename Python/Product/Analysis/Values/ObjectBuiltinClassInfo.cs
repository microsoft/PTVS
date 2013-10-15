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

using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class ObjectBuiltinClassInfo : BuiltinClassInfo {
        private AnalysisValue _new;

        public ObjectBuiltinClassInfo(IPythonType classObj, PythonAnalyzer projectState)
            : base(classObj, projectState) {
        }

        public override IAnalysisSet GetMember(Parsing.Ast.Node node, AnalysisUnit unit, string name) {
            if (name == "__new__") {
                return _new = _new ?? new SpecializedCallable(
                    base.GetMember(node, unit, name).OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                    ObjectNew,
                    false
                );
            }
            return base.GetMember(node, unit, name);
        }

        private IAnalysisSet ObjectNew(Node node, Analysis.AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length >= 1) {
                var instance = AnalysisSet.Empty;
                foreach (var n in args[0]) {
                    var bci = n as BuiltinClassInfo;
                    var ci = n as ClassInfo;
                    if (bci != null) {
                        instance = instance.Union(bci.Instance);
                    } else if (ci != null) {
                        instance = instance.Union(ci.Instance);
                    }
                }
                return instance;
            }
            return ProjectState.ClassInfos[BuiltinTypeId.Object].Instance;
        }
    }
}
