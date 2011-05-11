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

        internal override void AppendCodeString(StringBuilder res, PythonAst ast) {
            AppendItems(res, ast, "[", "]", this, Items);
        }

        internal static void AppendItems<T>(StringBuilder res, PythonAst ast, string start, string end, Node node, IList<T> items) where T : Expression {
            AppendItems(res, ast, start, end, node, items.Count, (i, sb) => items[i].AppendCodeString(sb, ast));
        }

        internal static void AppendItems(StringBuilder res, PythonAst ast, string start, string end, Node node, int itemCount, Action<int, StringBuilder> appendItem) {
            if (!String.IsNullOrEmpty(start)) {
                res.Append(node.GetProceedingWhiteSpace(ast));
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
                res.Append(node.GetSecondWhiteSpace(ast));
                res.Append(end);
            }
        }
    }
}
