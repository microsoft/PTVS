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
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstAnalysisWalker : PythonWalker {
        private readonly IPythonInterpreter _interpreter;
        private readonly IModuleContext _context;
        private readonly PythonAst _ast;
        private readonly IPythonModule _module;
        private readonly string _filePath;
        private readonly Dictionary<string, IMember> _members;

        private readonly Stack<Dictionary<string, IMember>> _scope;

        private IMember _noneInst;

        public AstAnalysisWalker(
            IPythonInterpreter interpreter,
            PythonAst ast,
            IPythonModule module,
            string filePath,
            Dictionary<string, IMember> members
        ) {
            _interpreter = interpreter;
            _context = _interpreter.CreateModuleContext();
            _ast = ast;
            _module = module;
            _filePath = filePath;
            _members = members;
            _scope = new Stack<Dictionary<string, IMember>>();
            _noneInst = new AstPythonConstant(_interpreter.GetBuiltinType(BuiltinTypeId.NoneType));
        }

        public override bool Walk(PythonAst node) {
            if (_ast != node) {
                throw new InvalidOperationException("walking wrong AST");
            }
            _scope.Push(_members);
            return base.Walk(node);
        }

        public override void PostWalk(PythonAst node) {
            _scope.Pop();
            base.PostWalk(node);
        }

        internal LocationInfo GetLoc(ClassDefinition node) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return null;
            }

            var start = node.NameExpression?.GetStart(_ast) ?? node.GetStart(_ast);
            var end = node.GetEnd(_ast);
            return new LocationInfo(_filePath, start.Line, start.Column, end.Line, end.Column);
        }

        internal LocationInfo GetLoc(FunctionDefinition node) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return null;
            }

            var start = node.NameExpression?.GetStart(_ast) ?? node.GetStart(_ast);
            var end = node.GetEnd(_ast);
            return new LocationInfo(_filePath, start.Line, start.Column, end.Line, end.Column);
        }

        internal LocationInfo GetLoc(Node node) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return null;
            }

            var start = node.GetStart(_ast);
            var end = node.GetEnd(_ast);
            return new LocationInfo(_filePath, start.Line, start.Column, end.Line, end.Column);
        }

        private string GetNameFromExpressionWorker(Expression expr) {
            var ne = expr as NameExpression;
            if (ne != null) {
                return ne.Name;
            }

            var me = expr as MemberExpression;
            if (me != null) {
                return "{0}.{1}".FormatInvariant(GetNameFromExpressionWorker(me.Target), me.Name);
            }

            throw new FormatException();
        }

        private string GetNameFromExpression(Expression expr) {
            try {
                return GetNameFromExpressionWorker(expr);
            } catch (FormatException) {
                return null;
            }
        }

        private IMember GetValueFromExpression(Expression expr, Dictionary<string, IMember> scope) {
            var ne = expr as NameExpression;
            if (ne != null) {
                IMember existing = null;
                if (scope != null && scope.TryGetValue(ne.Name, out existing) && existing != null) {
                    return existing;
                }
                if (_members != null && _members.TryGetValue(ne.Name, out existing) && existing != null) {
                    return existing;
                }
            }

            IPythonType type;

            var me = expr as MemberExpression;
            if (me != null && me.Target != null && !string.IsNullOrEmpty(me.Name)) {
                var mc = GetValueFromExpression(me.Target, scope) as IMemberContainer;
                if (mc != null) {
                    return mc.GetMember(_context, me.Name);
                }
                return null;
            }

            if (expr is CallExpression) {
                var cae = (CallExpression)expr;
                var m = GetValueFromExpression(cae.Target, scope);
                type = m as IPythonType;
                if (type != null) {
                    return new AstPythonConstant(type, GetLoc(expr));
                }
                var fn = m as IPythonFunction;
                if (fn != null) {
                    return new AstPythonConstant(_interpreter.GetBuiltinType(BuiltinTypeId.NoneType), GetLoc(expr));
                }
            }

            type = GetTypeFromExpression(expr);
            if (type != null) {
                return new AstPythonConstant(type, GetLoc(expr));
            }

            return null;
        }

        private IPythonType GetTypeFromExpression(Expression expr) {
            var ce = expr as ConstantExpression;
            if (ce != null) {
                if (ce.Value == null) {
                    return _interpreter.GetBuiltinType(BuiltinTypeId.NoneType);
                }
                switch (Type.GetTypeCode(ce.Value.GetType())) {
                    case TypeCode.Boolean: return _interpreter.GetBuiltinType(BuiltinTypeId.Bool);
                    case TypeCode.Double: return _interpreter.GetBuiltinType(BuiltinTypeId.Float);
                    case TypeCode.Int32: return _interpreter.GetBuiltinType(BuiltinTypeId.Int);
                    case TypeCode.String: return _interpreter.GetBuiltinType(BuiltinTypeId.Unicode);
                    case TypeCode.Object:
                        if (ce.Value.GetType() == typeof(Complex)) {
                            return _interpreter.GetBuiltinType(BuiltinTypeId.Complex);
                        } else if (ce.Value.GetType() == typeof(AsciiString)) {
                            return _interpreter.GetBuiltinType(BuiltinTypeId.Bytes);
                        } else if (ce.Value.GetType() == typeof(BigInteger)) {
                            return _interpreter.GetBuiltinType(BuiltinTypeId.Long);
                        } else if (ce.Value.GetType() == typeof(Ellipsis)) {
                            return _interpreter.GetBuiltinType(BuiltinTypeId.Ellipsis);
                        }
                        break;
                }
                return null;
            }

            if (expr is ListExpression || expr is ListComprehension) {
                return _interpreter.GetBuiltinType(BuiltinTypeId.List);
            }
            if (expr is DictionaryExpression || expr is DictionaryComprehension) {
                return _interpreter.GetBuiltinType(BuiltinTypeId.Dict);
            }
            if (expr is TupleExpression) {
                return _interpreter.GetBuiltinType(BuiltinTypeId.Tuple);
            }
            if (expr is SetExpression || expr is SetComprehension) {
                return _interpreter.GetBuiltinType(BuiltinTypeId.Set);
            }
            if (expr is LambdaExpression) {
                return _interpreter.GetBuiltinType(BuiltinTypeId.Function);
            }

            return null;
        }

        private IMember GetInScope(string name) {
            foreach (var m in _scope) {
                if (m == null) {
                    continue;
                }
                IMember obj;
                if (m.TryGetValue("__class__", out obj)) {
                    return obj;
                }
            }
            return null;
        }

        private IPythonType CurrentClass {
            get {
                var m = _scope.Peek();
                if (m != null) {
                    IMember cls;
                    m.TryGetValue("__class__", out cls);
                    return cls as IPythonType;
                }
                return null;
            }
        }

        public override bool Walk(AssignmentStatement node) {
            var m = _scope.Peek();
            if (m != null) {
                var value = GetValueFromExpression(node.Right, m);
                foreach (var ne in node.Left.OfType<NameExpression>()) {
                    m[ne.Name] = value;
                }
            }
            return base.Walk(node);
        }

        public override bool Walk(ImportStatement node) {
            var m = _scope.Peek();
            if (m != null && node.Names != null) {
                try {
                    for (int i = 0; i < node.Names.Count; ++i) {
                        var n = node.AsNames?[i] ?? node.Names[i].Names[0];
                        if (n != null) {
                            
                            m[n.Name] = new AstNestedPythonModule(
                                _interpreter,
                                n.Name,
                                PythonAnalyzer.ResolvePotentialModuleNames(_module.Name, _filePath, n.Name, true).ToArray()
                            );
                        }
                    }
                } catch (IndexOutOfRangeException) {
                }
            }
            return false;
        }

        public override bool Walk(FromImportStatement node) {
            var m = _scope.Peek();
            var modName = node.Root.MakeString();
            if (modName == "__future__") {
                return false;
            }

            if (m != null && node.Names != null) {
                var mod = new AstNestedPythonModule(
                    _interpreter,
                    modName,
                    PythonAnalyzer.ResolvePotentialModuleNames(_module.Name, _filePath, modName, true).ToArray()
                );
                var ctxt = _interpreter.CreateModuleContext();
                mod.Imported(ctxt);

                try {
                    for (int i = 0; i < node.Names.Count; ++i) {
                        if (node.Names[i].Name == "*") {
                            foreach (var member in mod.GetMemberNames(ctxt)) {
                                m[member] = mod.GetMember(ctxt, member) ?? new AstPythonConstant(
                                    _interpreter.GetBuiltinType(BuiltinTypeId.Unknown),
                                    mod.Locations.ToArray()
                                );
                            }
                            continue;
                        }
                        var n = node.AsNames?[i] ?? node.Names[i];
                        if (n != null) {
                            m[n.Name] = mod.GetMember(ctxt, node.Names[i].Name) ?? new AstPythonConstant(
                                _interpreter.GetBuiltinType(BuiltinTypeId.Unknown),
                                GetLoc(n)
                            );
                        }
                    }
                } catch (IndexOutOfRangeException) {
                }
            }
            return false;
        }

        public override bool Walk(FunctionDefinition node) {
            if (node.IsLambda) {
                return false;
            }

            var existing = GetInScope(node.Name) as AstPythonFunction;
            if (existing == null) {
                var m = _scope.Peek();
                if (m != null) {
                    m[node.Name] = new AstPythonFunction(_ast, _module, CurrentClass, node, GetLoc(node), GetDoc(node.Body as SuiteStatement));
                }
            } else {
                existing.AddOverload(_ast, node);
            }
            // Do not recurse into functions
            return false;
        }

        private static string GetDoc(SuiteStatement node) {
            var docExpr = node?.Statements?.FirstOrDefault() as ExpressionStatement;
            var ce = docExpr?.Expression as ConstantExpression;
            return ce?.Value as string;
        }

        public override bool Walk(ClassDefinition node) {
            var m = _scope.Peek();
            if (m != null) {
                var n = new Dictionary<string, IMember>();
                var mro = node.Bases.Where(a => string.IsNullOrEmpty(a.Name))
                    .Select(a => GetNameFromExpression(a.Expression))
                    .Where(a => !string.IsNullOrEmpty(a))
                    .Select(a => new AstPythonType(a));

                var t = new AstPythonType(_ast, _module, node, GetDoc(node.Body as SuiteStatement), GetLoc(node), mro);
                m[node.Name] = n["__class__"] = t;
                _scope.Push(n);

                return true;
            }
            return false;
        }

        public override void PostWalk(ClassDefinition node) {
            var cls = CurrentClass as AstPythonType;
            var m = _scope.Pop();
            if (cls != null && m != null) {
                cls.AddMembers(m, true);
            }
            base.PostWalk(node);
        }
    }
}
