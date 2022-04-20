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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Common.Parsing.Ast {    
    public class RaiseStatement : Statement {
        public RaiseStatement(Expression exceptionType, Expression exceptionValue, Expression traceBack, Expression cause) {
            ExceptType = exceptionType;
            Value = exceptionValue;
            Traceback = traceBack;
            Cause = cause;
        }

        public Expression ExceptType { get; }
        public Expression Value { get; }
        public Expression Traceback { get; }
        public Expression Cause { get; }
        public int ValueFieldStartIndex { get; set; }
        public int TracebackFieldStartIndex { get; set; }
        public int CauseFieldStartIndex { get; set; }

        public override int KeywordLength => 5;

        public override IEnumerable<Node> GetChildNodes() {
            if (ExceptType != null) yield return ExceptType;
            if (Value != null) yield return Value;
            if (Traceback != null) yield return Traceback;
            if (Cause != null) yield return Cause;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                ExceptType?.Walk(walker);
                Value?.Walk(walker);
                Traceback?.Walk(walker);
                Cause?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (ExceptType != null) {
                    await ExceptType.WalkAsync(walker, cancellationToken);
                }
                if (Value != null) {
                    await Value.WalkAsync(walker, cancellationToken);
                }
                if (Traceback != null) {
                    await Traceback.WalkAsync(walker, cancellationToken);
                }
                if (Cause != null) {
                    await Cause.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("raise");
            if (ExceptType != null) {
                ExceptType.AppendCodeString(res, ast, format);
            }
            if (this.IsAltForm(ast)) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append("from");
                Cause.AppendCodeString(res, ast, format);
            } else {
                if (Value != null) {
                    res.Append(this.GetSecondWhiteSpace(ast));
                    res.Append(',');
                    Value.AppendCodeString(res, ast, format);
                    if (Traceback != null) {
                        res.Append(this.GetThirdWhiteSpace(ast));
                        res.Append(',');
                        Traceback.AppendCodeString(res, ast, format);
                    }
                }
            }
        }
    }
}
