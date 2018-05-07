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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstAnalysisFunctionWalker : PythonWalker {
        private readonly FunctionDefinition _target;
        private readonly NameLookupContext _scope;
        private readonly List<IPythonType> _returnTypes;
        private readonly AstPythonFunctionOverload _overload;
        private AstPythonType _selfType;

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
            foreach (var d in (_target.Decorators?.DecoratorsInternal).MaybeEnumerate()) {
                var m = _scope.GetValueFromExpression(d);
                if (m == classmethodObj) {
                    classmethod = true;
                } else if (m == staticmethodObj) {
                    staticmethod = true;
                }
            }
        }

        public void Walk() {
            var klass = GetClass();
            _selfType = _selfType ?? GetSelf(klass);

            if (_target.ReturnAnnotation != null) {
                var retAnn = new TypeAnnotation(_scope.Ast.LanguageVersion, _target.ReturnAnnotation);
                var m = retAnn.GetValue(new AstTypeAnnotationConverter(_scope));
                if (m is IPythonMultipleMembers mm) {
                    _returnTypes.AddRange(mm.Members.OfType<IPythonType>());
                } else if (m is IPythonType type) {
                    _returnTypes.Add(type);
                }
            }


            _scope.PushScope();
            if (klass != null) {
                var p0 = _target.ParametersInternal?.FirstOrDefault();
                if (p0 != null && !string.IsNullOrEmpty(p0.Name) && p0.Name != "self") {
                    _scope.SetInScope(p0.Name, klass);
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

        public override bool Walk(AssignmentStatement node) {
            var value = _scope.GetValueFromExpression(node.Right);
            var first = node.Left.FirstOrDefault();
            if (first is MemberExpression memberExp && memberExp.Target is NameExpression nameExp1) {
                if (_selfType != null && nameExp1.Name == "self") {
                    _selfType.AddMembers(new[] { new KeyValuePair<string, IMember>(memberExp.Name, value) }, true);
                }
            } else if (!(first is NameExpression nameExp2 && nameExp2.Name == "self")) {
                // Don't assign to 'self'
                var multiple = value as AstPythonMultipleMembers;
                value = multiple != null ? multiple.Members.First() : value;
                foreach (var ne in node.Left.OfType<NameExpression>()) {
                    _scope.SetInScope(ne.Name, value);
                }
            }
            return base.Walk(node);
        }

        public override bool Walk(ReturnStatement node) {
            if (node.Expression is NameExpression nex && nex.Name == "self") {
                // For self return the actual class without added private members
                var klass = _scope.LookupNameInScopes("__class__", NameLookupContext.LookupOptions.Local);
                var self = (klass as AstPythonConstant)?.Type as IPythonType;
                if (self != null) {
                    _returnTypes.Add(self);
                    return false;
                }
            }

            foreach (var type in _scope.GetTypesFromValue(_scope.GetValueFromExpression(node.Expression))) {
                _returnTypes.Add(type);
            }
            return false;
        }

        private IMember GetClass() {
            bool classmethod, staticmethod;
            GetMethodType(_target, out classmethod, out staticmethod);
            var klass = _scope.LookupNameInScopes("__class__", NameLookupContext.LookupOptions.Local);
            if (!staticmethod && !classmethod) {
                var cls = klass as IPythonType;
                if (cls == null) {
                    klass = null;
                } else {
                    klass = new AstPythonConstant(cls, ((cls as ILocatedMember)?.Locations).MaybeEnumerate().ToArray());
                }
            }
            return klass;
        }

        private AstPythonType GetSelf(IMember klass) {
            var cls = (klass as AstPythonConstant)?.Type as AstPythonType;
            if (cls != null) {
                var self = _scope.LookupNameInScopes("self", NameLookupContext.LookupOptions.Local);
                if (self == null) {
                    // Clone type since function analysis can add members that should not be
                    // visible to the user such as private variables backing public properties.
                    self = new AstPythonConstant(cls.Clone(), Array.Empty<LocationInfo>());
                    _scope.SetInScope("self", self, mergeWithExisting: false);
                }
                return (self as AstPythonConstant)?.Type as AstPythonType;
            }
            return null;
        }
    }
}
