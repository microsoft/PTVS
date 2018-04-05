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

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("lambda");
            var commaWhiteSpace = this.GetListWhiteSpace(ast);

            if (_function.ParametersInternal.Length > 0) {
                var paramStr = new StringBuilder();
                _function.ParamsToString(paramStr, ast, commaWhiteSpace, format);
                if (paramStr.Length > 0 && !char.IsWhiteSpace(paramStr[0]) && !(_function.ParametersInternal[0] is ErrorParameter)) {
                    res.Append(' ');
                }
                res.Append(paramStr.ToString());
            }
            string namedOnlyText = this.GetExtraVerbatimText(ast);
            if (namedOnlyText != null) {
                res.Append(namedOnlyText);
            }
            format.Append(res, format.SpaceBeforeLambdaColon, " ", "", this.GetSecondWhiteSpaceDefaultNull(ast) ?? "");
            if (!this.IsIncompleteNode(ast)) {
                res.Append(":");
                string afterColon = null;
                if (format.SpaceAfterLambdaColon == true) {
                    afterColon = " ";
                } else if (format.SpaceAfterLambdaColon == false) {
                    afterColon = "";
                }
                if (_function.Body is ReturnStatement) {
                    ((ReturnStatement)_function.Body).Expression.AppendCodeString(res, ast, format, afterColon);
                } else {
                    Debug.Assert(_function.Body is ExpressionStatement);
                    ((ExpressionStatement)_function.Body).Expression.AppendCodeString(res, ast, format, afterColon);
                }
            }
        }
    }
}
