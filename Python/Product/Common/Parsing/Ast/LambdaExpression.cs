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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class LambdaExpression : Expression {
        public LambdaExpression(FunctionDefinition function) {
            Function = function;
        }

        public FunctionDefinition Function { get; }

        public override string NodeName => "lambda";

        public override IEnumerable<Node> GetChildNodes() {
            if (Function != null) yield return Function;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Function?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Function != null) {
                    await Function.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("lambda");
            var commaWhiteSpace = this.GetListWhiteSpace(ast);

            if (Function.Parameters.Length > 0) {
                var paramStr = new StringBuilder();
                Function.ParamsToString(paramStr, ast, commaWhiteSpace, format);
                if (paramStr.Length > 0 && !char.IsWhiteSpace(paramStr[0]) && !(Function.Parameters[0] is ErrorParameter)) {
                    res.Append(' ');
                }
                res.Append(paramStr.ToString());
            }
            var namedOnlyText = this.GetExtraVerbatimText(ast);
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
                if (Function.Body is ReturnStatement) {
                    ((ReturnStatement)Function.Body).Expression.AppendCodeString(res, ast, format, afterColon);
                } else {
                    Debug.Assert(Function.Body is ExpressionStatement);
                    ((ExpressionStatement)Function.Body).Expression.AppendCodeString(res, ast, format, afterColon);
                }
            }
        }
    }
}
