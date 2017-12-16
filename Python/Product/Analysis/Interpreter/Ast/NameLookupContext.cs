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
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class NameLookupContext {
        private readonly Stack<Dictionary<string, IMember>> _scopes;
        private readonly Lazy<IPythonModule> _builtinModule;
        private readonly AnalysisLogWriter _log;

        internal readonly IPythonType _unknownType;

        public NameLookupContext(
            IPythonInterpreter interpreter,
            IModuleContext context,
            PythonAst ast,
            IPythonModule self,
            string filePath,
            bool includeLocationInfo, 
            IPythonModule builtinModule = null,
            AnalysisLogWriter log = null
        ) {
            Interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
            Context = context;
            Ast = ast ?? throw new ArgumentNullException(nameof(ast));
            Module = self;
            FilePath = filePath;
            IncludeLocationInfo = includeLocationInfo;

            DefaultLookupOptions = LookupOptions.Normal;

            _unknownType = Interpreter.GetBuiltinType(BuiltinTypeId.Unknown) ??
                new FallbackBuiltinPythonType(new FallbackBuiltinModule(Ast.LanguageVersion), BuiltinTypeId.Unknown);

            _scopes = new Stack<Dictionary<string, IMember>>();
            _builtinModule = builtinModule == null ? new Lazy<IPythonModule>(ImportBuiltinModule) : new Lazy<IPythonModule>(() => builtinModule);
            _log = log;
        }

        public IPythonInterpreter Interpreter { get; }
        public IModuleContext Context { get; }
        public PythonAst Ast { get; }
        public IPythonModule Module { get; }
        public string FilePath { get; }
        public bool IncludeLocationInfo { get; }

        public LookupOptions DefaultLookupOptions { get; set; }
        public bool SuppressBuiltinLookup { get; set; }

        public NameLookupContext Clone(bool copyScopeContents = false) {
            var ctxt = new NameLookupContext(
                Interpreter,
                Context,
                Ast,
                Module,
                FilePath,
                IncludeLocationInfo,
                _builtinModule.IsValueCreated ? _builtinModule.Value : null,
                _log
            );

            ctxt.DefaultLookupOptions = DefaultLookupOptions;
            ctxt.SuppressBuiltinLookup = SuppressBuiltinLookup;

            foreach (var scope in _scopes.Reverse()) {
                if (copyScopeContents) {
                    ctxt._scopes.Push(new Dictionary<string, IMember>(scope));
                } else {
                    ctxt._scopes.Push(scope);
                }
            }

            return ctxt;
        }

        private IPythonModule ImportBuiltinModule() {
            var modname = BuiltinTypeId.Unknown.GetModuleName(Ast.LanguageVersion);
            var mod = Interpreter.ImportModule(modname);
            Debug.Assert(mod != null, "Failed to import " + modname);
            mod?.Imported(Context);
            return mod;
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

        internal LocationInfo GetLocOfName(Node node, NameExpression header) {
            var loc = GetLoc(node);
            if (loc == null || header == null) {
                return null;
            }

            var nameStart = header.GetStart(Ast);
            if (!nameStart.IsValid) {
                return loc;
            }

            if (nameStart.Line > loc.StartLine || (nameStart.Line == loc.StartLine && nameStart.Column > loc.StartColumn)) {
                return new LocationInfo(loc.FilePath, nameStart.Line, nameStart.Column, loc.EndLine, loc.EndColumn);
            }

            return loc;
        }

        private string GetNameFromExpressionWorker(Expression expr) {
            if (expr is NameExpression ne) {
                return ne.Name;
            }

            if (expr is MemberExpression me) {
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

        public IMember GetValueFromExpression(Expression expr) {
            return GetValueFromExpression(expr, DefaultLookupOptions);
        }

        public IMember GetValueFromExpression(Expression expr, LookupOptions options) {
            if (expr is ParenthesisExpression parExpr) {
                expr = parExpr.Expression;
            }

            if (expr == null) {
                return null;
            }

            var m = GetValueFromName(expr as NameExpression, options) ??
                    GetValueFromMember(expr as MemberExpression, options) ??
                    GetValueFromCallable(expr as CallExpression, options) ??
                    GetValueFromUnaryOp(expr as UnaryExpression, options) ??
                    GetValueFromBinaryOp(expr, options) ??
                    GetValueFromIndex(expr as IndexExpression, options) ??
                    GetValueFromConditional(expr as ConditionalExpression, options) ??
                    GetConstantFromLiteral(expr, options);
            if (m != null) {
                return m;
            }

            _log?.Log(TraceLevel.Verbose, "UnknownExpression", expr.ToCodeString(Ast).Trim());
            return null;
        }

        private IMember GetValueFromName(NameExpression expr, LookupOptions options) {
            if (expr == null || string.IsNullOrEmpty(expr.Name)) {
                return null;
            }

            IMember existing = LookupNameInScopes(expr.Name, options);
            if (existing != null) {
                return existing;
            }

            if (expr.Name == Module.Name) {
                return Module;
            }
            _log?.Log(TraceLevel.Verbose, "UnknownName", expr.Name);
            return new AstPythonConstant(_unknownType, GetLoc(expr));
        }

        private IMember GetValueFromMember(MemberExpression expr, LookupOptions options) {
            if (expr == null || expr.Target == null || string.IsNullOrEmpty(expr.Name)) {
                return null;
            }

            var e = GetValueFromExpression(expr.Target);
            if (e is IMemberContainer mc) {
                var m = mc.GetMember(Context, expr.Name);
                if (m != null) {
                    return m;
                }
                _log?.Log(TraceLevel.Verbose, "UnknownMember", expr.ToCodeString(Ast).Trim());
            } else {
                _log?.Log(TraceLevel.Verbose, "UnknownMemberContainer", expr.Target.ToCodeString(Ast).Trim());
            }
            return new AstPythonConstant(_unknownType, GetLoc(expr));
        }

        private IMember GetValueFromUnaryOp(UnaryExpression expr, LookupOptions options) {
            if (expr == null || expr.Expression == null) {
                return null;
            }

            // Assume that the type after the op is the same as before
            return GetValueFromExpression(expr.Expression);
        }

        private IMember GetValueFromBinaryOp(Expression expr, LookupOptions options) {
            if (expr is AndExpression || expr is OrExpression) {
                return new AstPythonConstant(Interpreter.GetBuiltinType(BuiltinTypeId.Bool), GetLoc(expr));
            }

            if (expr is BinaryExpression binop) {
                if (binop.Left == null) {
                    return null;
                }

                // TODO: Specific parsing
                switch (binop.Operator) {
                    case PythonOperator.Equal:
                    case PythonOperator.GreaterThan:
                    case PythonOperator.GreaterThanOrEqual:
                    case PythonOperator.In:
                    case PythonOperator.Is:
                    case PythonOperator.IsNot:
                    case PythonOperator.LessThan:
                    case PythonOperator.LessThanOrEqual:
                    case PythonOperator.Not:
                    case PythonOperator.NotEqual:
                    case PythonOperator.NotIn:
                        // Assume all of these return True/False
                        return new AstPythonConstant(Interpreter.GetBuiltinType(BuiltinTypeId.Bool), GetLoc(expr));
                }

                // When in doubt, assume that the type after the op is the same as the LHS
                return GetValueFromExpression(binop.Left);
            }

            return null;
        }

        private IMember GetValueFromIndex(IndexExpression expr, LookupOptions options) {
            if (expr == null || expr.Target == null) {
                return null;
            }

            if (expr.Index is SliceExpression || expr.Index is TupleExpression) {
                // When slicing, assume result is the same type
                return GetValueFromExpression(expr.Target);
            }

            var type = GetTypeFromLiteral(expr.Target);
            if (type != null) {
                switch (type.TypeId) {
                    case BuiltinTypeId.Bytes:
                        if (Ast.LanguageVersion.Is3x()) {
                            return new AstPythonConstant(Interpreter.GetBuiltinType(BuiltinTypeId.Int), GetLoc(expr));
                        } else {
                            return new AstPythonConstant(Interpreter.GetBuiltinType(BuiltinTypeId.Bytes), GetLoc(expr));
                        }
                    case BuiltinTypeId.Unicode:
                        return new AstPythonConstant(Interpreter.GetBuiltinType(BuiltinTypeId.Unicode), GetLoc(expr));
                }
            }

            _log?.Log(TraceLevel.Verbose, "UnknownIndex", expr.ToCodeString(Ast).Trim());
            return new AstPythonConstant(_unknownType, GetLoc(expr));
        }

        private IMember GetValueFromConditional(ConditionalExpression expr, LookupOptions options) {
            if (expr == null) {
                return null;
            }

            var left = GetValueFromExpression(expr.TrueExpression);
            var right = GetValueFromExpression(expr.FalseExpression);
            if (left?.MemberType == PythonMemberType.Unknown) {
                left = null;
            }
            if (right?.MemberType == PythonMemberType.Unknown) {
                right = null;
            }

            if (left != null && right != null && left != right) {
                return new AstPythonMultipleMembers(new[] { left, right });
            }
            return left ?? right;
        }

        private IMember GetValueFromCallable(CallExpression expr, LookupOptions options) {
            if (expr == null || expr.Target == null) {
                return null;
            }

            var m = GetValueFromExpression(expr.Target);
            if (m is IPythonType type) {
                if (type.TypeId == BuiltinTypeId.Type && type == Interpreter.GetBuiltinType(BuiltinTypeId.Type) && expr.Args.Count >= 1) {
                    var aType = GetTypeFromValue(GetValueFromExpression(expr.Args[0].Expression));
                    if (aType != null) {
                        return aType;
                    }
                }
                return new AstPythonConstant(type, GetLoc(expr));
            }

            if (m is IPythonFunction fn) {
                // TODO: Select correct overload and handle multiple return types
                if (fn.Overloads.Count > 0 && fn.Overloads[0].ReturnType.Count > 0) {
                    return new AstPythonConstant(fn.Overloads[0].ReturnType[0]);
                }
                _log?.Log(TraceLevel.Verbose, "NoReturn", expr.Target.ToCodeString(Ast).Trim());
                return new AstPythonConstant(Interpreter.GetBuiltinType(BuiltinTypeId.NoneType), GetLoc(expr));
            }

            _log?.Log(TraceLevel.Verbose, "UnknownCallable", expr.Target.ToCodeString(Ast).Trim());
            return new AstPythonConstant(_unknownType, GetLoc(expr));
        }

        public IPythonConstant GetConstantFromLiteral(Expression expr, LookupOptions options) {
            if (expr is ConstantExpression ce) {
                if (ce.Value is string s) {
                    return new AstPythonStringLiteral(s, Interpreter.GetBuiltinType(BuiltinTypeId.Unicode), GetLoc(expr));
                } else if (ce.Value is AsciiString b) {
                    return new AstPythonStringLiteral(b.String, Interpreter.GetBuiltinType(BuiltinTypeId.Bytes), GetLoc(expr));
                }
            }

            var type = GetTypeFromLiteral(expr);
            if (type != null) {
                return new AstPythonConstant(type, GetLoc(expr));
            }

            return null;
        }

        public IEnumerable<IPythonType> GetTypesFromValue(IMember value) {
            if (value is IPythonMultipleMembers mm) {
                return mm.Members.Select(GetTypeFromValue).Distinct();
            } else {
                var t = GetTypeFromValue(value);
                if (t != null) {
                    return Enumerable.Repeat(t, 1);
                }
            }
            return Enumerable.Empty<IPythonType>();
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

            if (value is IPythonBoundFunction) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Function);
            }

            if (value is IPythonModule) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Module);
            }

            if (value is IPythonMultipleMembers) {
                type = null;
                foreach (var t in GetTypesFromValue(value)) {
                    if (type == null) {
                        type = t;
                    } else if (t == null) {
                    } else if (t.MemberType == PythonMemberType.Module ||
                        (t.MemberType == PythonMemberType.Class && type.MemberType != PythonMemberType.Module) ||
                        (t.MemberType == PythonMemberType.Function && type.MemberType != PythonMemberType.Class)) {
                        type = t;
                    }
                }
                return type;
            }

            Debug.Fail("Unhandled type() value: " + value.GetType().FullName);
            return null;
        }

        public IPythonType GetTypeFromLiteral(Expression expr) {
            if (expr is ConstantExpression ce) {
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

        private static bool IsUnknown(IMember value) {
            return (value as IPythonType)?.TypeId == BuiltinTypeId.Unknown ||
                (value as IPythonConstant)?.Type?.TypeId == BuiltinTypeId.Unknown;
        }

        public void SetInScope(string name, IMember value, bool mergeWithExisting = true) {
            var s = _scopes.Peek();
            if (value == null) {
                s.Remove(name);
                return;
            }
            if (mergeWithExisting && s.TryGetValue(name, out IMember existing) && existing != null) {
                if (IsUnknown(existing)) {
                    s[name] = value;
                } else if (!IsUnknown(value)) {
                    s[name] = AstPythonMultipleMembers.Combine(existing, value);
                }
            } else {
                s[name] = value;
            }
        }

        [Flags]
        public enum LookupOptions {
            None = 0,
            Local,
            Nonlocal,
            Global,
            Builtins,
            Normal = Local | Nonlocal | Global | Builtins
        }

        public IMember LookupNameInScopes(string name) {
            return LookupNameInScopes(name, DefaultLookupOptions);
        }

        public IMember LookupNameInScopes(string name, LookupOptions options) {
            IMember value;

            var scopes = _scopes.ToList();
            if (scopes.Count == 1) {
                if (!options.HasFlag(LookupOptions.Local) && !options.HasFlag(LookupOptions.Global)) {
                    scopes = null;
                }
            } else if (scopes.Count >= 2) {
                if (!options.HasFlag(LookupOptions.Nonlocal)) {
                    while (scopes.Count > 2) {
                        scopes.RemoveAt(1);
                    }
                }
                if (!options.HasFlag(LookupOptions.Local)) {
                    scopes.RemoveAt(0);
                }
                if (!options.HasFlag(LookupOptions.Global)) {
                    scopes.RemoveAt(scopes.Count - 1);
                }
            }

            if (scopes != null) {
                foreach (var scope in scopes) {
                    if (scope.TryGetValue(name, out value) && value != null) {
                        if (value is ILazyMember lm) {
                            value = lm.Get();
                            scope[name] = value;
                        }
                        return value;
                    }
                }
            }

            if (!SuppressBuiltinLookup && options.HasFlag(LookupOptions.Builtins)) {
                return _builtinModule.Value.GetMember(Context, name);
            }

            return null;
        }
    }
}
