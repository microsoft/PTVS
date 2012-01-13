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
    public class StarredExpression : Expression {
        private readonly Expression _expr;

        public StarredExpression(Expression expr) {
            _expr = expr;
        }

        public Expression Expression {
            get {
                return _expr;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _expr.Walk(walker);
            }
        }

        internal override string CheckAssign() {
            return null;
        }

        internal override string CheckAugmentedAssign() {
            return "invalid syntax";
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast) {
            res.Append(this.GetProceedingWhiteSpace(ast));
            res.Append('*');
            _expr.AppendCodeString(res, ast);
        }
    }
}
