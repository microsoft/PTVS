/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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
                res.Append(this.GetProceedingWhiteSpace(ast));
                res.Append('[');
                if (String.IsNullOrWhiteSpace(this.GetSecondWhiteSpace(ast))) {
                    res.Append(format.SpacesWithinEmptyListExpression.Value ? " " : "");
                } else {
                    format.ReflowComment(res, this.GetSecondWhiteSpace(ast));
                }
                res.Append(']');
            } else {
                string delimWs =
                 format.SpacesWithinListExpression != null ?
                 format.SpacesWithinListExpression.Value ? " " : "" : null;

                AppendItems(res, ast, format, "[", this.IsMissingCloseGrouping(ast) ? "" : "]", this, Items, delimWs);
            }
        }

        internal static void AppendItems<T>(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string start, string end, Node node, IList<T> items, string delimiterWhiteSpace = null) where T : Expression {
            AppendItems(res, ast, format, start, end, node, items.Count, (i, sb) => {
                if (i == 0) {
                    items[i].AppendCodeString(sb, ast, format, delimiterWhiteSpace);
                } else {
                    items[i].AppendCodeString(sb, ast, format);
                }
            }, delimiterWhiteSpace);
        }

        internal static void AppendItems(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string start, string end, Node node, int itemCount, Action<int, StringBuilder> appendItem, string trailingWhiteSpace = null) {
            if (!String.IsNullOrEmpty(start)) {
                format.ReflowComment(res, node.GetProceedingWhiteSpace(ast));
                res.Append(start);
            }
            var listWhiteSpace = node.GetListWhiteSpace(ast);
            for (int i = 0; i < itemCount; i++) {
                if (i > 0) {
                    if (listWhiteSpace != null) {
                        res.Append(listWhiteSpace[i - 1]);
                    }
                    res.Append(",");
                }
                
                appendItem(i, res);
            }

            if (listWhiteSpace != null && listWhiteSpace.Length == itemCount && itemCount != 0) {
                // trailing comma
                res.Append(listWhiteSpace[listWhiteSpace.Length - 1]);
                res.Append(",");
            }

            if (!String.IsNullOrEmpty(end)) {
                res.Append(
                    String.IsNullOrWhiteSpace(node.GetSecondWhiteSpace(ast)) ?
                        trailingWhiteSpace ?? node.GetSecondWhiteSpace(ast) :
                        node.GetSecondWhiteSpace(ast)
                );
                res.Append(end);
            }
        }
    }
}
