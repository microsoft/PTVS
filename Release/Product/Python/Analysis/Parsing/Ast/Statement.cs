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

using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public abstract class Statement : Node {
        internal Statement() {
        }

        public virtual string Documentation {
            get {
                return null;
            }
        }

        internal override sealed void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            AppendCodeStringStmt(res, ast, format);
        }

        internal abstract void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format);

        /// <summary>
        /// Returns the expression contained by the statement.
        /// 
        /// Returns null if it's not an expression statement or return statement.
        /// 
        /// New in 1.1.
        /// </summary>
        public static Expression GetExpression(Statement statement) {
            if (statement is ExpressionStatement) {
                return ((ExpressionStatement)statement).Expression;
            } else if (statement is ReturnStatement) {
                return ((ReturnStatement)statement).Expression;
            } else {
                return null;
            }
        }
    }
}
