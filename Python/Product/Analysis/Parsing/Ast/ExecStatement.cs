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

using System.Linq;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    
    public class ExecStatement : Statement {
        private readonly Expression _code, _locals, _globals;
        private readonly TupleExpression _codeTuple;

        public ExecStatement(Expression code, Expression locals, Expression globals, TupleExpression codeTuple) {
            _code = (code != codeTuple) ? code : null;
            _locals = locals;
            _globals = globals;
            _codeTuple = codeTuple;
        }

        public Expression Code {
            get { return _code ?? _codeTuple?.Items.ElementAtOrDefault(0); }
        }

        public Expression Locals {
            get { return _locals ?? _codeTuple?.Items.ElementAtOrDefault(2); }
        }

        public Expression Globals {
            get { return _globals ?? _codeTuple?.Items.ElementAtOrDefault(1); }
        }

        public bool NeedsLocalsDictionary() {
            return Globals == null && Locals == null;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_code != null) {
                    _code.Walk(walker);
                }
                if (_codeTuple != null) {
                    _codeTuple.Walk(walker);
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

            if (_codeTuple != null) {
                _codeTuple.AppendCodeString(res, ast, format);
            } else {
                _code.AppendCodeString(res, ast, format);
            }

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
