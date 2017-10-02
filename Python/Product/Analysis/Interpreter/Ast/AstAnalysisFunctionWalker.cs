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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstAnalysisFunctionWalker : PythonWalker {
        private readonly FunctionDefinition _target;
        private readonly NameLookupContext _scope;
        private readonly List<IPythonType> _returnTypes;
        private readonly AstPythonFunctionOverload _overload;

        public AstAnalysisFunctionWalker(
            NameLookupContext scope,
            FunctionDefinition targetFunction
        ) {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _target = targetFunction ?? throw new ArgumentNullException(nameof(targetFunction));
            _returnTypes = new List<IPythonType>();
            _overload = new AstPythonFunctionOverload(
                AstPythonFunction.MakeParameters(_scope.Ast, _target),
                _scope.GetLocOfName(_target, _target.NameExpression),
                _returnTypes
            );
        }

        public IList<IPythonType> ReturnTypes => _returnTypes;
        public IPythonFunctionOverload Overload => _overload;

        private void GetMethodType(FunctionDefinition node, out bool classmethod, out bool staticmethod) {
            classmethod = false;
            staticmethod = false;

            if (node.IsLambda) {
                staticmethod = true;
                return;
            }

            var classmethodObj = _scope.Interpreter.GetBuiltinType(BuiltinTypeId.ClassMethod);
            var staticmethodObj = _scope.Interpreter.GetBuiltinType(BuiltinTypeId.StaticMethod);
            foreach (var d in (_target.Decorators?.Decorators).MaybeEnumerate()) {
                var m = _scope.GetValueFromExpression(d);
                if (m == classmethodObj) {
                    classmethod = true;
                } else if (m == staticmethodObj) {
                    staticmethod = true;
                }
            }
        }

        public void Walk() {
            IMember self = null;
            bool classmethod, staticmethod;
            GetMethodType(_target, out classmethod, out staticmethod);
            if (!staticmethod) {
                self = _scope.LookupNameInScopes("__class__", NameLookupContext.LookupOptions.Local);
                if (!classmethod) {
                    var cls = self as IPythonType;
                    if (cls == null) {
                        self = null;
                    } else {
                        self = new AstPythonConstant(cls, ((cls as ILocatedMember)?.Locations).MaybeEnumerate().ToArray());
                    }
                }
            }

            _scope.PushScope();
            if (self != null) {
                var p0 = _target.Parameters?.FirstOrDefault();
                if (p0 != null && !string.IsNullOrEmpty(p0.Name)) {
                    _scope.SetInScope(p0.Name, self);
                }
            }
            _target.Walk(this);
            _scope.PopScope();
        }

        public override bool Walk(FunctionDefinition node) {
            if (node != _target) {
                // Do not walk nested functions (yet)
                return false;
            }

            if (_overload.Documentation == null) {
                var docNode = (node.Body as SuiteStatement)?.Statements.FirstOrDefault();
                var ce = (docNode as ExpressionStatement)?.Expression as ConstantExpression;
                var doc = ce?.Value as string;
                if (doc != null) {
                    _overload.SetDocumentation(doc);
                }
            }

            return true;
        }

        public override bool Walk(ExpressionStatement node) {
            return base.Walk(node);
        }

        public override bool Walk(ReturnStatement node) {
            foreach (var type in _scope.GetTypesFromValue(_scope.GetValueFromExpression(node.Expression))) {
                _returnTypes.Add(type);
            }

            return false;
        }
    }
}
