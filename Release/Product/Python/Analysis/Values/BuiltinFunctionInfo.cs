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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BuiltinFunctionInfo : BuiltinNamespace<IPythonType> {
        private IPythonFunction _function;
        private string _doc;
        private ReadOnlyCollection<OverloadResult> _overloads;
        private readonly ISet<Namespace> _returnTypes;
        private BuiltinMethodInfo _method;

        public BuiltinFunctionInfo(IPythonFunction function, PythonAnalyzer projectState)
            : base(projectState.Types.BuiltinFunction, projectState) {
            
            _function = function;
            _returnTypes = Utils.GetReturnTypes(function, projectState);                        
        }

        public override IPythonType PythonType {
            get { return _type; }
        }

        public override ISet<Namespace> Call(Node node, Interpreter.AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
            return _returnTypes;
        }

        public override ISet<Namespace> GetDescriptor(Node node, Namespace instance, Namespace context, Interpreter.AnalysisUnit unit) {
            if (_function.IsStatic || instance == unit.ProjectState._noneInst) {
                return base.GetDescriptor(node, instance, context, unit);
            } else if (_method == null) {
                _method = new BuiltinMethodInfo(_function, PythonMemberType.Method, ProjectState);
            }

            return _method.GetDescriptor(node, instance, context, unit);
        }

        public IPythonFunction Function {
            get {
                return _function;
            }
        }

        public override string Description {
            get {
                if (_function.IsBuiltin) {
                    return "built-in function " + _function.Name;
                }
                return "function " + _function.Name;
            }
        }

        public override ICollection<OverloadResult> Overloads {
            get {
                if (_overloads == null) {
                    var overloads = _function.Overloads;
                    var result = new OverloadResult[overloads.Count];
                    for (int i = 0; i < result.Length; i++) {
                        result[i] = new BuiltinFunctionOverloadResult(ProjectState, _function.Name, overloads[i], 0);
                    }
                    _overloads = new ReadOnlyCollection<OverloadResult>(result);
                }
                return _overloads;
            }
        }

        public override string Documentation {
            get {
                if (_doc == null) {
                    _doc = Utils.StripDocumentation(_function.Documentation);
                }
                return _doc;
            }
        }

        public override PythonMemberType ResultType {
            get {
                return _function.MemberType;
            }
        }
    }
}
