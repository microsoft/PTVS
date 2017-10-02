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

using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    
    public class ExecStatement : Statement {
        private readonly Expression _code, _locals, _globals;

        public ExecStatement(Expression code, Expression locals, Expression globals) {
            _code = code;
            _locals = locals;
            _globals = globals;
        }

        public Expression Code {
            get { return _code; }
        }

        public Expression Locals {
            get { return _locals; }
        }

        public Expression Globals {
            get { return _globals; }
        }

        /// <summary>
        /// Specifies the original tuple for round-tripping
        /// </summary>
        public TupleExpression CodeTuple { get; set; }

        public bool NeedsLocalsDictionary() {
            return _globals == null && _locals == null;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_code != null) {
                    _code.Walk(walker);
                }
                if (_locals != null) {
                    _locals.Walk(walker);
                }
                if (_globals != null) {
                    _globals.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("exec");
            if (CodeTuple != null) {
                CodeTuple.AppendCodeString(res, ast, format);
                return;
            }

            _code.AppendCodeString(res, ast, format);
            if (_globals != null) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append("in");
                _globals.AppendCodeString(res, ast, format);
                if (_locals != null) {
                    res.Append(this.GetThirdWhiteSpace(ast));
                    res.Append(',');
                    _locals.AppendCodeString(res, ast, format);
                }
            }
        }
    }
}
