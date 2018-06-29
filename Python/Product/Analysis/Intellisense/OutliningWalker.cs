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
                AddTagIfNecessary(node.ElseIndex, node.ElseStatement.EndIndex);
            }

            return base.Walk(node);
        }

        public override bool Walk(IfStatementTest node) {
            AddTagIfNecessary(node.HeaderIndex + 1, node.Body?.EndIndex);
            // Only walk body, not the condition.
            node.Body?.Walk(this);
            return false;
        }

        public override bool Walk(WhileStatement node) {
            // Walk while statements manually so we don't traverse the test.
            // This prevents the test from being collapsed ever.
            if (node.Body != null) {
                AddTagIfNecessary(
                    _ast.GetLineEndFromPosition(node.StartIndex),
                    node.Body.EndIndex
                );
                node.Body.Walk(this);
            }
            if (node.ElseStatement != null) {
                AddTagIfNecessary(node.ElseIndex, node.ElseStatement.EndIndex);
                node.ElseStatement.Walk(this);
            }
            return false;
        }

        public override bool Walk(ForStatement node) {
            // Walk for statements manually so we don't traverse the list.  
            // This prevents the list and/or left from being collapsed ever.
            node.List?.Walk(this);

            if (node.Body != null) {
                AddTagIfNecessary(node.HeaderIndex + 1, node.Body.EndIndex);
                node.Body.Walk(this);
            }
            if (node.Else != null) {
                AddTagIfNecessary(node.ElseIndex, node.Else.EndIndex);
                node.Else.Walk(this);
            }
            return false;
        }

        public override bool Walk(TryStatement node) {
            AddTagIfNecessary(node.HeaderIndex, node.Body?.EndIndex);
            if (node.Handlers != null) {
                foreach (var h in node.Handlers) {
                    AddTagIfNecessary(h.HeaderIndex, h.EndIndex);
                }
            }
            AddTagIfNecessary(node.FinallyIndex, node.Finally?.EndIndex);
            AddTagIfNecessary(node.ElseIndex, node.Else?.EndIndex);

            return base.Walk(node);
        }

        public override bool Walk(WithStatement node) {
            AddTagIfNecessary(node.HeaderIndex + 1, node.Body?.EndIndex);
            return base.Walk(node);
        }

        public override bool Walk(FunctionDefinition node) {
            // Walk manually so collapsing is not enabled for params.
            if (node.ParametersInternal != null) {
                AddTagIfNecessary(node.ParametersInternal.FirstOrDefault()?.StartIndex, node.ParametersInternal.LastOrDefault()?.EndIndex);
            }
            if (node.Body != null) {
                AddTagIfNecessary(node.HeaderIndex + 1, node.EndIndex);
                node.Body.Walk(this);
            }

            return false;
        }

        public override bool Walk(ClassDefinition node) {
            AddTagIfNecessary(node.HeaderIndex + 1, node.EndIndex);

            return base.Walk(node);
        }

        // Not-Compound Statements
        public override bool Walk(CallExpression node) {
            AddTagIfNecessary(node.Args?.FirstOrDefault()?.StartIndex, node.Args?.LastOrDefault()?.EndIndex);
            return base.Walk(node);
        }

        public override bool Walk(FromImportStatement node) {
            if (node.Names != null && node.Names.Any()) {
                int lastName = node.Names.Count - 1;
                int? nameEnd = node.Names[lastName]?.EndIndex;
                if (node.AsNames != null && node.AsNames.Count >= lastName && node.AsNames[lastName] != null) {
                    nameEnd = node.AsNames[lastName].EndIndex;
                }
                AddTagIfNecessary(node.Names[0].StartIndex, nameEnd);
            }
            return base.Walk(node);
        }

        public override bool Walk(ListExpression node) {
            AddTagIfNecessary(node.Items?.FirstOrDefault()?.StartIndex, node.Items?.LastOrDefault()?.EndIndex);
            return base.Walk(node);
        }

        public override bool Walk(TupleExpression node) {
            AddTagIfNecessary(node.Items?.FirstOrDefault()?.StartIndex, node.Items?.LastOrDefault()?.EndIndex);
            return base.Walk(node);
        }

        public override bool Walk(DictionaryExpression node) {
            AddTagIfNecessary(node.Items?.FirstOrDefault()?.StartIndex, node.Items?.LastOrDefault()?.EndIndex);
            return base.Walk(node);
        }

        public override bool Walk(SetExpression node) {
            AddTagIfNecessary(node.Items?.FirstOrDefault()?.StartIndex, node.Items?.LastOrDefault()?.EndIndex);
            return base.Walk(node);
        }

        public override bool Walk(ParenthesisExpression node) {
            AddTagIfNecessary(node.Expression?.StartIndex, node.Expression?.EndIndex);
            return base.Walk(node);
        }

        public override bool Walk(ConstantExpression node) {
            AddTagIfNecessary(_ast.GetLineEndFromPosition(node.StartIndex), node.EndIndex);
            return base.Walk(node);
        }

        private void AddTagIfNecessary(int? startIndex, int? endIndex, int minLinesToCollapse = 3) {
            if (!startIndex.HasValue || !endIndex.HasValue) {
                return;
            }

            if (startIndex < 0 || endIndex < 0) {
                return;
            }

            var start = _ast.IndexToLocation(startIndex.Value);
            var end = _ast.IndexToLocation(endIndex.Value);
            var lines = end.Line - start.Line + 1;

            // Collapse if more than 3 lines.
            if (lines < minLinesToCollapse) {
                return;
            }

            var tagSpan = new TaggedSpan(new SourceSpan(start, end), null);
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
