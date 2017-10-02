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
    public class RaiseStatement : Statement {
        private readonly Expression _type, _value, _traceback, _cause;

        public RaiseStatement(Expression exceptionType, Expression exceptionValue, Expression traceBack, Expression cause) {
            _type = exceptionType;
            _value = exceptionValue;
            _traceback = traceBack;
            _cause = cause;
        }

        public Expression ExceptType {
            get {
                return _type;
            }
        }

        public Expression Value {
            get { return _value; }
        }

        public Expression Traceback {
            get { return _traceback; }
        }

        public Expression Cause {
            get {
                return _cause;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_type != null) {
                    _type.Walk(walker);
                }
                if (_value != null) {
                    _value.Walk(walker);
                }
                if (_traceback != null) {
                    _traceback.Walk(walker);
                }
            }
            walker.PostWalk(this);
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
                if (_value != null) {
                    res.Append(this.GetSecondWhiteSpace(ast));
                    res.Append(',');
                    _value.AppendCodeString(res, ast, format);
                    if (_traceback != null) {
                        res.Append(this.GetThirdWhiteSpace(ast));
                        res.Append(',');
                        _traceback.AppendCodeString(res, ast, format);
                    }
                }
            }
        }
    }
}
