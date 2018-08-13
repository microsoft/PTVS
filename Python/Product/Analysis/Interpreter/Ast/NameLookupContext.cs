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
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    sealed class NameLookupContext {
        private readonly Stack<Dictionary<string, IMember>> _scopes = new Stack<Dictionary<string, IMember>>();
        private readonly Lazy<IPythonModule> _builtinModule;
        private readonly AnalysisLogWriter _log;

        internal readonly IPythonType _unknownType;

        public NameLookupContext(
            IPythonInterpreter interpreter,
            IModuleContext context,
            PythonAst ast,
            IPythonModule self,
            string filePath,
            Uri documentUri,
            bool includeLocationInfo,
            IPythonModule builtinModule = null,
            AnalysisLogWriter log = null
        ) {
            Interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
            Context = context;
            Ast = ast ?? throw new ArgumentNullException(nameof(ast));
            Module = self;
            FilePath = filePath;
            DocumentUri = documentUri;
            IncludeLocationInfo = includeLocationInfo;

            DefaultLookupOptions = LookupOptions.Normal;

            _unknownType = Interpreter.GetBuiltinType(BuiltinTypeId.Unknown) ??
                new FallbackBuiltinPythonType(new FallbackBuiltinModule(Ast.LanguageVersion), BuiltinTypeId.Unknown);

            _builtinModule = builtinModule == null ? new Lazy<IPythonModule>(ImportBuiltinModule) : new Lazy<IPythonModule>(() => builtinModule);
            _log = log;
        }

        public IPythonInterpreter Interpreter { get; }
        public IModuleContext Context { get; }
        public PythonAst Ast { get; }
        public IPythonModule Module { get; }
        public string FilePath { get; }
        public Uri DocumentUri { get; }
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
                DocumentUri,
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
            return new LocationInfo(FilePath, DocumentUri, start.Line, start.Column, end.Line, end.Column);
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
                return new LocationInfo(loc.FilePath, loc.DocumentUri, nameStart.Line, nameStart.Column, loc.EndLine, loc.EndColumn);
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

        public IEnumerable<IPythonType> GetTypesFromAnnotation(Expression expr) {
            if (expr == null) {
                return Enumerable.Empty<IPythonType>();
            }

            var ann = new TypeAnnotation(Ast.LanguageVersion, expr);
            var m = ann.GetValue(new AstTypeAnnotationConverter(this));
            if (m is IPythonMultipleMembers mm) {
                return mm.Members.OfType<IPythonType>();
            } else if (m is IPythonType type) {
                return Enumerable.Repeat(type, 1);
            }

            return Enumerable.Empty<IPythonType>();
        }

        public IMember GetValueFromExpression(Expression expr) => GetValueFromExpression(expr, DefaultLookupOptions);

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

                // Try LHS, then, if unknown, try RHS. Example: y = 1 when y is not declared by the walker yet.
                var value = GetValueFromExpression(binop.Left);
                return IsUnknown(value) ? GetValueFromExpression(binop.Right) : value;
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

            var type = GetTypeFromValue(GetValueFromExpression(expr.Target));
            if (type != null && type != _unknownType) {
                if (AstTypingModule.IsTypingType(type)) {
                    return type;
                }

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

                if (type.MemberType == PythonMemberType.Class) {
                    // When indexing into a type, assume result is the type
                    // TODO: Proper generic handling
                    return type;
                }

                _log?.Log(TraceLevel.Verbose, "UnknownIndex", type.TypeId, expr.ToCodeString(Ast, CodeFormattingOptions.Traditional).Trim());
            } else {
                _log?.Log(TraceLevel.Verbose, "UnknownIndex", expr.ToCodeString(Ast, CodeFormattingOptions.Traditional).Trim());
            }
            return new AstPythonConstant(_unknownType, GetLoc(expr));
        }

        private IMember GetValueFromConditional(ConditionalExpression expr, LookupOptions options) {
            if (expr == null) {
                return null;
            }

            return AstPythonMultipleMembers.Combine(
                GetValueFromExpression(expr.TrueExpression),
                GetValueFromExpression(expr.FalseExpression)
            );
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

            switch (value.MemberType) {
                case PythonMemberType.Class:
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Type);
                case PythonMemberType.Delegate:
                case PythonMemberType.DelegateInstance:
                case PythonMemberType.Function:
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Function);
                case PythonMemberType.Method:
                    return Interpreter.GetBuiltinType(BuiltinTypeId.BuiltinMethodDescriptor);
                case PythonMemberType.Enum:
                case PythonMemberType.EnumInstance:
                    break;
                case PythonMemberType.Module:
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Module);
                case PythonMemberType.Namespace:
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Object);
                case PythonMemberType.Event:
                    break;
            }

            IPythonFunction f;
            if ((f = value as IPythonFunction ?? (value as IPythonBoundFunction)?.Function) != null) {
                if (f.IsStatic) {
                    return Interpreter.GetBuiltinType(BuiltinTypeId.StaticMethod);
                } else if (f.IsClassMethod) {
                    return Interpreter.GetBuiltinType(BuiltinTypeId.ClassMethod);
                }
                return Interpreter.GetBuiltinType(BuiltinTypeId.Function);
            }

            if (value is IBuiltinProperty prop) {
                return prop.Type ?? Interpreter.GetBuiltinType(BuiltinTypeId.Property);
            } else if (value.MemberType == PythonMemberType.Property) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Property);
            }

            if (value is IPythonMethodDescriptor) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.BuiltinMethodDescriptor);
            }

            if (value is IPythonMultipleMembers mm) {
                return AstPythonMultipleMembers.CreateAs<IPythonType>(mm.Members);
            }

            if (value is IPythonType) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Type);
            }

#if DEBUG
            var implements = string.Join(", ", new[] { value.GetType().FullName }.Concat(value.GetType().GetInterfaces().Select(i => i.Name)));
            Debug.Fail("Unhandled type() value: " + implements);
#endif
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
            if (expr is TupleExpression tex) {
                var types = tex.Items
                    .Select(x => {
                        IPythonType t = null;
                        if (x is NameExpression ne) {
                            t = (GetInScope(ne.Name) as AstPythonConstant)?.Type;
                        }
                        return t ?? Interpreter.GetBuiltinType(BuiltinTypeId.Unknown);
                    }).ToArray();
                var res = Interpreter.GetBuiltinType(BuiltinTypeId.Tuple);
                if (types.Length > 0) {
                    var iterRes = Interpreter.GetBuiltinType(BuiltinTypeId.TupleIterator);
                    res = new AstPythonSequence(res, Module, types, iterRes);
                }
                return res;
            }
            if (expr is SetExpression || expr is SetComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Set);
            }
            if (expr is LambdaExpression) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Function);
            }

            return null;
        }

        public IMember GetInScope(string name, Dictionary<string, IMember> scope = null) {
            if (scope == null && _scopes.Count == 0) {
                return null;
            }

            IMember obj;
            if ((scope ?? _scopes.Peek()).TryGetValue(name, out obj)) {
                return obj;
            }
            return null;
        }

        private static bool IsUnknown(IMember value) {
            return (value as IPythonType)?.TypeId == BuiltinTypeId.Unknown ||
                (value as IPythonConstant)?.Type?.TypeId == BuiltinTypeId.Unknown;
        }

        public void SetInScope(string name, IMember value, bool mergeWithExisting = true, Dictionary<string, IMember> scope = null) {
            Debug.Assert(_scopes.Count > 0);
            if (value == null && _scopes.Count == 0) {
                return;
            }
            var s = scope ?? _scopes.Peek();
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
