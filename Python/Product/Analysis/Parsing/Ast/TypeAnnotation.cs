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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Parsing.Ast {
    class TypeAnnotation {
        public TypeAnnotation(PythonLanguageVersion version, Expression expr) {
            LanguageVersion = version;
            Expression = expr ?? throw new ArgumentNullException(nameof(expr));
        }

        public static TypeAnnotation FromType<T>(TypeAnnotationConverter<T> converter, T type) where T : class {
            throw new NotImplementedException();
        }

        public PythonLanguageVersion LanguageVersion { get; }
        public Expression Expression { get; }

        private Expression ParseSubExpression(string expr) {
            var parser = Parser.CreateParser(new StringReader(expr), LanguageVersion);
            return Statement.GetExpression(parser.ParseTopExpression()?.Body);
        }

        /// <summary>
        /// Converts this type annotation 
        /// </summary>
        public T GetValue<T>(TypeAnnotationConverter<T> converter) where T : class {
            var walker = new Walker(ParseSubExpression);
            Expression.Walk(walker);
            return walker.GetResult(converter);
        }

        internal IEnumerable<string> GetTransformSteps() {
            var walker = new Walker(ParseSubExpression);
            Expression.Walk(walker);
            return walker._ops.Select(o => o.ToString());
        }

        private class Walker : PythonWalker {
            private readonly Func<string, Expression> _parse;
            internal readonly List<Op> _ops;

            public Walker(Func<string, Expression> parse) {
                _parse = parse;
                _ops = new List<Op>();
            }

            public T GetResult<T>(TypeAnnotationConverter<T> converter) where T : class {
                var stack = new Stack<KeyValuePair<string, T>>();
                try {
                    foreach (var op in _ops) {
                        if (!op.Apply(converter, stack)) {
                            return default(T);
                        }
                    }
                } catch (InvalidOperationException) {
                    return default(T);
                }

                if (stack.Count == 1) {
                    return converter.Finalize(stack.Pop().Value);
                }
                return default(T);
            }

            public override bool Walk(ConstantExpression node) {
                if (node.Value is string s) {
                    _parse(s)?.Walk(this);
                } else if (node.Value is AsciiString a) {
                    _parse(a.String)?.Walk(this);
                } else if (node.Value == null) {
                    _ops.Add(new NameOp { Name = "None" });
                }
                return false;
            }

            public override bool Walk(NameExpression node) {
                _ops.Add(new NameOp { Name = node.Name });
                return false;
            }

            public override bool Walk(MemberExpression node) {
                if (base.Walk(node)) {
                    node.Target?.Walk(this);
                    _ops.Add(new MemberOp { Member = node.Name });
                }
                return false;
            }

            public override bool Walk(TupleExpression node) {
                _ops.Add(new StartListOp());
                return base.Walk(node);
            }

            public override void PostWalk(TupleExpression node) {
                _ops.Add(new EndListOp());
                base.PostWalk(node);
            }

            public override bool Walk(ListExpression node) {
                _ops.Add(new StartListOp());
                return base.Walk(node);
            }

            public override void PostWalk(ListExpression node) {
                _ops.Add(new EndListOp());
                base.PostWalk(node);
            }

            public override void PostWalk(IndexExpression node) {
                if (_ops.LastOrDefault() is EndListOp) {
                    _ops[_ops.Count - 1] = new MakeGenericOp(true);
                } else {
                    _ops.Add(new MakeGenericOp(false));
                }
                base.PostWalk(node);
            }

            public override bool Walk(CallExpression node) {
                if (!base.Walk(node)) {
                    return false;
                }

                node.Target?.Walk(this);
                _ops.Add(new StartListOp());
                foreach (var a in node.Args.MaybeEnumerate()) {
                    a.Walk(this);
                }
                _ops.Add(new MakeGenericOp(true));
                return false;
            }

            internal abstract class Op {
                public abstract bool Apply<T>(TypeAnnotationConverter<T> converter, Stack<KeyValuePair<string, T>> stack) where T : class;
                public override string ToString() => GetType().Name;
            }

            class NameOp : Op {
                public string Name { get; set; }

                public override bool Apply<T>(TypeAnnotationConverter<T> converter, Stack<KeyValuePair<string, T>> stack) {
                    var t = converter.LookupName(Name) ?? converter.MakeNameType(Name);
                    if (t == null) {
                        return false;
                    }
                    stack.Push(new KeyValuePair<string, T>(Name, t));
                    return true;
                }

                public override string ToString() => $"{GetType().Name}:{Name}";
            }

            class MemberOp : Op {
                public string Member { get; set; }

                public override bool Apply<T>(TypeAnnotationConverter<T> converter, Stack<KeyValuePair<string, T>> stack) {
                    if (!stack.Any()) {
                        return false;
                    }
                    var t = stack.Pop();
                    t = new KeyValuePair<string, T>(t.Key == null ? null : $"{t.Key}.{Member}", converter.GetTypeMember(t.Value, Member));
                    if (t.Value == null) {
                        return false;
                    }
                    stack.Push(t);
                    return true;
                }

                public override string ToString() => $"{GetType().Name}:{Member}";
            }

            class OptionalOp : Op {
                public override bool Apply<T>(TypeAnnotationConverter<T> converter, Stack<KeyValuePair<string, T>> stack) {
                    if (!stack.Any()) {
                        return false;
                    }
                    var t = stack.Pop();
                    t = new KeyValuePair<string, T>(null, converter.MakeOptional(t.Value));
                    if (t.Value == null) {
                        return false;
                    }
                    stack.Push(t);
                    return true;
                }
            }

            class StartListOp : Op {
                public override bool Apply<T>(TypeAnnotationConverter<T> converter, Stack<KeyValuePair<string, T>> stack) {
                    stack.Push(new KeyValuePair<string, T>(nameof(StartListOp), null));
                    return true;
                }
            }

            class EndListOp : Op {
                public override bool Apply<T>(TypeAnnotationConverter<T> converter, Stack<KeyValuePair<string, T>> stack) {
                    var items = new List<T>();
                    if (!stack.Any()) {
                        return false;
                    }
                    var t = stack.Pop();
                    while (t.Key != nameof(StartListOp)) {
                        items.Add(t.Value);
                        if (!stack.Any()) {
                            return false;
                        }
                        t = stack.Pop();
                    }
                    items.Reverse();
                    t = new KeyValuePair<string, T>(null, converter.MakeList(items));
                    if (t.Value == null) {
                        return false;
                    }
                    stack.Push(t);
                    return true;
                }
            }

            class MakeGenericOp : Op {
                private readonly bool _multipleArgs;

                public MakeGenericOp(bool multipleArgs) {
                    _multipleArgs = multipleArgs;
                }

                public override bool Apply<T>(TypeAnnotationConverter<T> converter, Stack<KeyValuePair<string, T>> stack) {
                    var items = new List<T>();
                    if (!stack.Any()) {
                        return false;
                    }
                    var t = stack.Pop();
                    if (t.Value == null) {
                        return false;
                    }
                    if (_multipleArgs) {
                        while (t.Key != nameof(StartListOp)) {
                            items.Add(t.Value);
                            if (!stack.Any()) {
                                return false;
                            }
                            t = stack.Pop();
                        }
                        items.Reverse();
                    } else if (t.Key != nameof(StartListOp)) {
                        items.Add(t.Value);
                    }
                    var baseType = stack.Pop();
                    t = new KeyValuePair<string, T>(null, converter.MakeGeneric(baseType.Value, items));
                    if (t.Value == null) {
                        return false;
                    }
                    stack.Push(t);
                    return true;
                }
            }
        }
    }

    public abstract class TypeAnnotationConverter<T> where T : class {
        #region Convert Type Hint to Type

        /// <summary>
        /// Returns the type or module object for the specified name.
        /// </summary>
        public virtual T LookupName(string name) => default(T);
        /// <summary>
        /// Returns a member of the preceding module object.
        /// </summary>
        public virtual T GetTypeMember(T baseType, string member) => default(T);

        /// <summary>
        /// Returns the specialized type object for the base
        /// type and generic types provided.
        /// </summary>
        public virtual T MakeGeneric(T baseType, IReadOnlyList<T> args) => default(T);

        /// <summary>
        /// Returns the type as an optional type.
        /// </summary>
        public virtual T MakeOptional(T type) => default(T);

        /// <summary>
        /// Returns the types as a single union type.
        /// </summary>
        public virtual T MakeUnion(IReadOnlyList<T> types) => default(T);

        /// <summary>
        /// Returns the types as a single list type.
        /// </summary>
        public virtual T MakeList(IReadOnlyList<T> types) => default(T);

        /// <summary>
        /// Returns a value containing an unresolved name.
        /// </summary>
        public virtual T MakeNameType(string name) => default(T);

        /// <summary>
        /// Ensure the final result is a suitable type. Return null
        /// if not.
        /// </summary>
        public virtual T Finalize(T type) => type;

        #endregion


        #region Convert Type to Type Hint

        /// <summary>
        /// Returns the name of the provided type. This should always
        /// be the name of the base type, omitting any generic arguments.
        /// It may include dots to fully qualify the name.
        /// </summary>
        public virtual string GetName(T type) => null;

        /// <summary>
        /// Gets the base type from a generic type. If it is already a
        /// base type, return null.
        /// </summary>
        public virtual T GetBaseType(T genericType) => default(T);
        /// <summary>
        /// Gets the generic types from a generic type. Return null if
        /// there are no generic types.
        /// </summary>
        public virtual IReadOnlyList<T> GetGenericArguments(T genericType) => null;

        /// <summary>
        /// Gets the non-optional type from an optional type. If it is
        /// already a non-optional type, return null.
        /// </summary>
        public virtual T GetNonOptionalType(T optionalType) => default(T);

        /// <summary>
        /// Gets the original types from a type union. If it is not a
        /// union type, return null.
        /// </summary>
        public virtual IReadOnlyList<T> GetUnionTypes(T unionType) => null;
        

        /// <summary>
        /// Returns True if the provided type is not fully defined and
        /// should use a string literal rather than its actual name.
        /// </summary>
        public virtual bool IsForwardReference(T type) => false;

        #endregion
    }
}
