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
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public class ListExpression : SequenceExpression {
        public ListExpression(params Expression[] items)
            : base(items) {
        }

        public override string NodeName {
            get {
                return "list display";
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (Items != null) {
                    foreach (Expression e in Items) {
                        e.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            if (Items.Count == 0 && format.SpacesWithinEmptyListExpression != null) {
                res.Append(this.GetPreceedingWhiteSpace(ast));
                res.Append('[');
                if (String.IsNullOrWhiteSpace(this.GetSecondWhiteSpace(ast))) {
                    res.Append(format.SpacesWithinEmptyListExpression.Value ? " " : "");
                } else {
                    format.ReflowComment(res, this.GetSecondWhiteSpace(ast));
                }
                res.Append(']');
            } else {
                AppendItems(res, ast, format, "[", this.IsMissingCloseGrouping(ast) ? "" : "]", this, Items, format.SpacesWithinListExpression);
            }
        }

        internal static void AppendItems<T>(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string start, string end, Node node, IList<T> items, bool? delimiterWhiteSpace = null) where T : Expression {
            string initialWs = null, ws = null;
            if (delimiterWhiteSpace.HasValue) {
                initialWs = delimiterWhiteSpace.Value ? " " : "";
            }
            if (format.SpaceAfterComma.HasValue) {
                ws = format.SpaceAfterComma.Value ? " " : "";
            }
            AppendItems(res, ast, format, start, end, node, items.Count, (i, sb) => {
                if (i == 0) {
                    items[i].AppendCodeString(sb, ast, format, initialWs);
                } else {
                    items[i].AppendCodeString(sb, ast, format, ws);
                }
            }, delimiterWhiteSpace);
        }

        internal static void AppendItems(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string start, string end, Node node, int itemCount, Action<int, StringBuilder> appendItem, bool? trailingWhiteSpace = null) {
            if (!String.IsNullOrEmpty(start)) {
                format.ReflowComment(res, node.GetPreceedingWhiteSpace(ast));
                res.Append(start);
            }
            var listWhiteSpace = node.GetListWhiteSpace(ast);
            for (int i = 0; i < itemCount; i++) {
                if (i > 0) {
                    format.Append(res, format.SpaceBeforeComma, " ", "", listWhiteSpace?[i - 1]);
                    res.Append(",");
                }

                appendItem(i, res);
            }

            if (listWhiteSpace != null && listWhiteSpace.Length == itemCount && itemCount != 0) {
                // trailing comma
                format.Append(res, format.SpaceBeforeComma, " ", "", listWhiteSpace[listWhiteSpace.Length - 1]);
                res.Append(",");
            }

            if (!String.IsNullOrEmpty(end)) {
                format.Append(res, trailingWhiteSpace, " ", "", node.GetSecondWhiteSpaceDefaultNull(ast));
                res.Append(end);
            }
        }
    }
}
