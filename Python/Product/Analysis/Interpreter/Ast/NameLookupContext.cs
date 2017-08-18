using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class NameLookupContext {
        private readonly Stack<Dictionary<string, IMember>> _scopes;

        public NameLookupContext(
            IPythonInterpreter interpreter,
            IModuleContext context,
            PythonAst ast,
            string filePath,
            bool includeLocationInfo
        ) {
            Interpreter = interpreter;
            Context = context;
            Ast = ast;
            FilePath = filePath;
            IncludeLocationInfo = includeLocationInfo;

            _scopes = new Stack<Dictionary<string, IMember>>();
        }

        public IPythonInterpreter Interpreter { get; }
        public IModuleContext Context { get; }
        public PythonAst Ast { get; }
        public string FilePath { get; }
        public bool IncludeLocationInfo { get; }

        public NameLookupContext Clone(bool copyScopeContents = false) {
            var ctxt = new NameLookupContext(
                Interpreter,
                Context,
                Ast,
                FilePath,
                IncludeLocationInfo
            );

            foreach (var scope in _scopes.Reverse()) {
                if (copyScopeContents) {
                    ctxt._scopes.Push(new Dictionary<string, IMember>(scope));
                } else {
                    ctxt._scopes.Push(scope);
                }
            }

            return ctxt;
        }

        public Dictionary<string, IMember> PushScope(Dictionary<string, IMember> scope = null) {
            scope = scope ?? new Dictionary<string, IMember>();
            _scopes.Push(scope);
            return scope;
        }

        public Dictionary<string, IMember> PopScope() {
            return _scopes.Pop();
        }

        internal LocationInfo GetLoc(Node node) {
            if (!IncludeLocationInfo) {
                return null;
            }
            if (node == null || node.StartIndex >= node.EndIndex) {
                return null;
            }

            var start = node.GetStart(Ast);
            var end = node.GetEnd(Ast);
            return new LocationInfo(FilePath, start.Line, start.Column, end.Line, end.Column);
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

        public string GetNameFromExpression(Expression expr) {
            try {
                return GetNameFromExpressionWorker(expr);
            } catch (FormatException) {
                return null;
            }
        }

        public IMember GetValueFromExpression(Expression expr, LookupOptions options = LookupOptions.Normal) {
            var ne = expr as NameExpression;
            if (ne != null) {
                IMember existing = LookupNameInScopes(ne.Name, options);
                if (existing != null) {
                    return existing;
                }
            }

            IPythonType type;

            var me = expr as MemberExpression;
            if (me != null && me.Target != null && !string.IsNullOrEmpty(me.Name)) {
                var mc = GetValueFromExpression(me.Target) as IMemberContainer;
                if (mc != null) {
                    return mc.GetMember(Context, me.Name);
                }
                return null;
            }

            if (expr is CallExpression) {
                var cae = (CallExpression)expr;
                var m = GetValueFromExpression(cae.Target);
                type = m as IPythonType;
                if (type != null) {
                    if (type == Interpreter.GetBuiltinType(BuiltinTypeId.Type) && cae.Args.Count >= 1) {
                        var aType = GetTypeFromValue(GetValueFromExpression(cae.Args[0].Expression));
                        if (aType != null) {
                            return aType;
                        }
                    }
                    return type;
                }

                var fn = m as IPythonFunction;
                if (fn != null) {
                    // TODO: Select correct overload and handle multiple return types
                    if (fn.Overloads.Count > 0 && fn.Overloads[0].ReturnType.Count > 0) {
                        return new AstPythonConstant(fn.Overloads[0].ReturnType[0]);
                    }
                    return new AstPythonConstant(Interpreter.GetBuiltinType(BuiltinTypeId.NoneType), GetLoc(expr));
                }
            }

            type = GetTypeFromLiteral(expr);
            if (type != null) {
                return new AstPythonConstant(type, GetLoc(expr));
            }

            return null;
        }

        public IPythonType GetTypeFromValue(IMember value) {
            if (value == null) {
                return null;
            }

            var type = (value as IPythonConstant)?.Type;
            if (type != null) {
                return type;
            }

            if (value is IPythonType) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Type);
            }

            if (value is IPythonFunction) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Function);
            }

            Debug.Fail("Unhandled type() value: " + value.GetType().FullName);
            return null;
        }

        public IPythonType GetTypeFromLiteral(Expression expr) {
            var ce = expr as ConstantExpression;
            if (ce != null) {
                if (ce.Value == null) {
                    return Interpreter.GetBuiltinType(BuiltinTypeId.NoneType);
                }
                switch (Type.GetTypeCode(ce.Value.GetType())) {
                    case TypeCode.Boolean: return Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
                    case TypeCode.Double: return Interpreter.GetBuiltinType(BuiltinTypeId.Float);
                    case TypeCode.Int32: return Interpreter.GetBuiltinType(BuiltinTypeId.Int);
                    case TypeCode.String: return Interpreter.GetBuiltinType(BuiltinTypeId.Unicode);
                    case TypeCode.Object:
                        if (ce.Value.GetType() == typeof(Complex)) {
                            return Interpreter.GetBuiltinType(BuiltinTypeId.Complex);
                        } else if (ce.Value.GetType() == typeof(AsciiString)) {
                            return Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);
                        } else if (ce.Value.GetType() == typeof(BigInteger)) {
                            return Interpreter.GetBuiltinType(BuiltinTypeId.Long);
                        } else if (ce.Value.GetType() == typeof(Ellipsis)) {
                            return Interpreter.GetBuiltinType(BuiltinTypeId.Ellipsis);
                        }
                        break;
                }
                return null;
            }

            if (expr is ListExpression || expr is ListComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.List);
            }
            if (expr is DictionaryExpression || expr is DictionaryComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Dict);
            }
            if (expr is TupleExpression) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Tuple);
            }
            if (expr is SetExpression || expr is SetComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Set);
            }
            if (expr is LambdaExpression) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Function);
            }

            return null;
        }

        public IMember GetInScope(string name) {
            if (_scopes.Count == 0) {
                return null;
            }

            IMember obj;
            if (_scopes.Peek().TryGetValue(name, out obj)) {
                return obj;
            }
            return null;
        }

        public void SetInScope(string name, IMember value) {
            _scopes.Peek()[name] = value;
        }

        public enum LookupOptions {
            Normal = 0,
            LocalOnly = 1,
            Nonlocal = 2,
            Global = 3
        }

        public IMember LookupNameInScopes(string name, LookupOptions options) {
            IMember value;
            if (options == LookupOptions.Global) {
                if (_scopes.Last().TryGetValue(name, out value) && value != null) {
                    return value;
                }
                return null;
            }

            foreach (var scope in _scopes) {
                if (options == LookupOptions.Nonlocal) {
                    options = LookupOptions.Normal;
                    continue;
                }

                if (scope.TryGetValue(name, out value) && value != null) {
                    return value;
                }

                if (options == LookupOptions.LocalOnly) {
                    return null;
                }
            }
            return null;
        }
    }
}
