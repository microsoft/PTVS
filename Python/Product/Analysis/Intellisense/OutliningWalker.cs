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
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
    class OutliningWalker : PythonWalker {
        private readonly PythonAst _ast;
        private readonly List<TaggedSpan> _tagSpans;

        public OutliningWalker(PythonAst ast) {
            _ast = ast;
            _tagSpans = new List<TaggedSpan>();
        }

        // Compound Statements: if, while, for, try, with, func, class, decorated
        public override bool Walk(IfStatement node) {
            if (node.ElseStatement != null) {
                AddTagIfNecessary(node.ElseStatement, node.ElseIndex);
            }

            return base.Walk(node);
        }

        public override bool Walk(IfStatementTest node) {
            if (node.Test != null && node.Body != null) {
                AddTagIfNecessary(node.Test.StartIndex, node.Body.EndIndex);
                // Don't walk test condition.
                node.Body.Walk(this);
            }
            return false;
        }

        public override bool Walk(WhileStatement node) {
            // Walk while statements manually so we don't traverse the test.
            // This prevents the test from being collapsed ever.
            if (node.Body != null) {
                AddTagIfNecessary(
                    node.StartIndex,
                    node.Body.EndIndex,
                    _ast.GetLineEndFromPosition(node.StartIndex)
                );
                node.Body.Walk(this);
            }
            if (node.ElseStatement != null) {
                AddTagIfNecessary(node.ElseStatement, node.ElseIndex);
                node.ElseStatement.Walk(this);
            }
            return false;
        }

        public override bool Walk(ForStatement node) {
            // Walk for statements manually so we don't traverse the list.  
            // This prevents the list and/or left from being collapsed ever.
            
            if (node.Body != null) {
                AddTagIfNecessary(
                    node.StartIndex,
                    node.Body.EndIndex,
                    _ast.GetLineEndFromPosition(node.StartIndex)
                );
                node.Body.Walk(this);
            }
            if (node.Else != null) {
                AddTagIfNecessary(node.Else, node.ElseIndex);
                node.Else.Walk(this);
            }
            return false;
        }

        public override bool Walk(TryStatement node) {
            if (node.Body != null) {
                AddTagIfNecessary(node.StartIndex, node.Body.EndIndex, node.HeaderIndex);
            }
            if (node.Handlers != null) {
                foreach (var h in node.Handlers) {
                    AddTagIfNecessary(h, h.HeaderIndex);
                }
            }
            if (node.Finally != null) {
                AddTagIfNecessary(node.FinallyIndex, node.Finally.EndIndex, node.FinallyIndex);
            }
            if (node.Else != null) {
                AddTagIfNecessary(node.ElseIndex, node.Else.EndIndex, node.ElseIndex);
            }

            return base.Walk(node);
        }

        public override bool Walk(WithStatement node) {
            AddTagIfNecessary(node);
            return base.Walk(node);
        }

        public override bool Walk(FunctionDefinition node) {
            // Walk manually so collapsing is not enabled for params.
            if (node.Body != null) {
                AddTagIfNecessary(node, node.HeaderIndex + 1, node.Decorators);
                node.Body.Walk(this);
            }

            return false;
        }

        public override bool Walk(ClassDefinition node) {
            AddTagIfNecessary(node, node.HeaderIndex + 1, node.Decorators);

            return base.Walk(node);
        }

        // Not-Compound Statements
        public override bool Walk(CallExpression node) {
            AddTagIfNecessary(node);
            return base.Walk(node);
        }

        public override bool Walk(FromImportStatement node) {
            AddTagIfNecessary(node);
            return base.Walk(node);
        }

        public override bool Walk(ListExpression node) {
            AddTagIfNecessary(node);
            return base.Walk(node);
        }

        public override bool Walk(TupleExpression node) {
            AddTagIfNecessary(node);
            return base.Walk(node);
        }

        public override bool Walk(DictionaryExpression node) {
            AddTagIfNecessary(node);
            return base.Walk(node);
        }

        public override bool Walk(SetExpression node) {
            AddTagIfNecessary(node);
            return base.Walk(node);
        }

        public override bool Walk(ParenthesisExpression node) {
            AddTagIfNecessary(node);
            return base.Walk(node);
        }

        public override bool Walk(ConstantExpression node) {
            AddTagIfNecessary(node);
            return base.Walk(node);
        }

        private void AddTagIfNecessary(Node node, int headerIndex = -1, DecoratorStatement decorator = null) {
            AddTagIfNecessary(node.StartIndex, node.EndIndex, headerIndex, decorator);
        }

        private void AddTagIfNecessary(int startIndex, int endIndex, int headerIndex = -1, DecoratorStatement decorator = null, int minLinesToCollapse = 3) {
            var start = _ast.IndexToLocation(startIndex);
            var end = _ast.IndexToLocation(endIndex);
            var lines = end.Line - start.Line + 1;

            // Collapse if more than 3 lines.
            if (lines < minLinesToCollapse) {
                return;
            }

            if (decorator != null) {
                // we don't want to collapse the decorators, we like them visible, so
                // we base our starting position on where the decorators end.
                startIndex = decorator.EndIndex + 1;
            }

            var tagSpan = new TaggedSpan(new SourceSpan(start, end), null, headerIndex);
            _tagSpans.Add(tagSpan);
        }

        internal IEnumerable<TaggedSpan> GetTags() {
            return _tagSpans
                .GroupBy(s => s.Span.Start.Line)
                .Select(ss => ss.OrderByDescending(s => s.Span.End.Line).First())
                .ToArray();
        }
    }
}
