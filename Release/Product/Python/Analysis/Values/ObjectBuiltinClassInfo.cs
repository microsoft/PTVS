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

using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class ObjectBuiltinClassInfo : BuiltinClassInfo {
        NewFunction _new;

        public ObjectBuiltinClassInfo(IPythonType classObj, PythonAnalyzer projectState)
            : base(classObj, projectState) {
        }

        public override INamespaceSet GetMember(Parsing.Ast.Node node, Interpreter.AnalysisUnit unit, string name) {
            if (name == "__new__") {
                if (_new == null) {
                    var func = this._type.GetMember(unit.ProjectEntry.MyScope.InterpreterContext, name);
                    if (func != null) {
                        _new = new NewFunction((BuiltinFunctionInfo)unit.ProjectState.GetNamespaceFromObjects(func), ProjectState);
                    }
                }
                if (_new != null) {
                    return _new.SelfSet;
                }
            }
            return base.GetMember(node, unit, name);
        }

        class NewFunction : BuiltinFunctionInfo {
            internal NewFunction(BuiltinFunctionInfo function, PythonAnalyzer projectState)
                : base(function.Function, projectState) {
            }

            public override INamespaceSet Call(Node node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgNames) {
                if (args.Length >= 1) {
                    var instance = NamespaceSet.Empty;
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
                return ProjectState._objectType.Instance;
            }
        }
    }
}
