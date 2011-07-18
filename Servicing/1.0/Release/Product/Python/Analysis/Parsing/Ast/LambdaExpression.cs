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

using System.Diagnostics;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public class LambdaExpression : Expression {
        private readonly FunctionDefinition _function;

        public LambdaExpression(FunctionDefinition function) {
            _function = function;
        }

        public FunctionDefinition Function {
            get { return _function; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_function != null) {
                    _function.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast) {
            res.Append(this.GetProceedingWhiteSpace(ast));
            res.Append("lambda");
            var commaWhiteSpace = this.GetListWhiteSpace(ast);

            _function.ParamsToString(res, ast, commaWhiteSpace);
            string namedOnlyText = this.GetExtraVerbatimText(ast);
            if (namedOnlyText != null) {
                res.Append(namedOnlyText);
            }
            if (!this.IsIncompleteNode(ast)) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append(":");
                if (_function.Body is ReturnStatement) {
                    ((ReturnStatement)_function.Body).Expression.AppendCodeString(res, ast);
                } else {
                    Debug.Assert(_function.Body is ExpressionStatement);
                    ((ExpressionStatement)_function.Body).Expression.AppendCodeString(res, ast);
                }
            }
        }
    }
}
