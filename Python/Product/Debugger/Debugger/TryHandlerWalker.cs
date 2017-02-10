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

namespace Microsoft.PythonTools.Debugger {
    /// <summary>
    /// Extracts a flat list of all the sections of code protected by exception
    /// handlers.
    /// </summary>
    class TryHandlerWalker : PythonWalker {
        private readonly PythonAst _ast;
        private List<Tuple<int, int, IList<string>>> _statements;

        public TryHandlerWalker(PythonAst ast) {
            _ast = ast;
            _statements = new List<Tuple<int, int, IList<string>>>();
        }

        public IList<Tuple<int, int, IList<string>>> Statements => _statements;

        private string ToDottedNameString(Expression expr) {
            NameExpression name;
            MemberExpression member;
            ParenthesisExpression paren;
            if ((name = expr as NameExpression) != null) {
                return name.Name;
            } else if ((member = expr as MemberExpression) != null) {
                while (member.Target is MemberExpression) {
                    member = (MemberExpression)member.Target;
                }
                if (member.Target is NameExpression) {
                    return expr.ToCodeString(_ast);
                }
            } else if ((paren = expr as ParenthesisExpression) != null) {
                return ToDottedNameString(paren.Expression);
            }
            return null;
        }

        private void Add(Statement block, Statement statement, IEnumerable<string> exceptions) {
            int start = block.GetStart(_ast).Line;
            int end = statement.GetEnd(_ast).Line + 1;
            var exc = exceptions.ToList();
            if (exc.Any()) {
                _statements.Add(new Tuple<int, int, IList<string>>(start, end, exc));
            }
        }

        public override bool Walk(TryStatement node) {
            var expressions = new List<string>();
            foreach (var handler in node.Handlers.MaybeEnumerate()) {
                Expression expr = handler.Test;
                TupleExpression tuple;
                if (expr == null) {
                    expressions.Clear();
                    expressions.Add("*");
                    break;
                } else if ((tuple = handler.Test as TupleExpression) != null) {
                    foreach (var e in tuple.Items) {
                        var text = ToDottedNameString(e);
                        if (text != null) {
                            expressions.Add(text);
                        }
                    }

                } else {
                    var text = ToDottedNameString(expr);
                    if (text != null) {
                        expressions.Add(text);
                    }
                }
            }

            if (node.Handlers == null && node.Finally == null) {
                // If Handlers and Finally are null, there was probably
                // a parser error. We assume all exceptions are handled
                // by default, to avoid bothering the user too much, so
                // handle everything here since we can't be more
                // accurate.
                expressions.Clear();
                expressions.Add("*");
            }
            Add(node, node.Body, expressions);

            return base.Walk(node);
        }

        private static bool IsFullOrPartialName(Expression expr, string modName, string name) {
            var ne = expr as NameExpression;
            if (ne != null) {
                return ne.Name == name;
            }
            var me = expr as MemberExpression;
            ne = me?.Target as NameExpression;
            if (me != null && ne != null) {
                return me.Name == name && ne.Name == modName;
            }

            return false;
        }

        public override bool Walk(WithStatement node) {
            foreach (var item in node.Items.MaybeEnumerate()) {
                var cm = item.ContextManager as CallExpression;
                if (!IsFullOrPartialName(cm?.Target, "contextlib", "ignored")) {
                    continue;
                }

                Add(node, node.Body, cm.Args.Select(a => ToDottedNameString(a.Expression)).Where(n => !string.IsNullOrEmpty(n)));
            }
            return base.Walk(node);
        }
    }
}
