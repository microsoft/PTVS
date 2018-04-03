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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {

    public class CallExpression : Expression {
        private readonly Arg[] _args;

        public CallExpression(Expression target, Arg[] args) {
            Target = target;
            _args = args;
        }

        public Expression Target { get; }
        public IList<Arg> Args => _args;

        public bool NeedsLocalsDictionary() {
            NameExpression nameExpr = Target as NameExpression;
            if (nameExpr == null) return false;

            if (_args.Length == 0) {
                if (nameExpr.Name == "locals") return true;
                if (nameExpr.Name == "vars") return true;
                if (nameExpr.Name == "dir") return true;
                return false;
            } else if (_args.Length == 1 && (nameExpr.Name == "dir" || nameExpr.Name == "vars")) {
                if (_args[0].Name == "*" || _args[0].Name == "**") {
                    // could be splatting empty list or dict resulting in 0-param call which needs context
                    return true;
                }
            } else if (_args.Length == 2 && (nameExpr.Name == "dir" || nameExpr.Name == "vars")) {
                if (_args[0].Name == "*" && _args[1].Name == "**") {
                    // could be splatting empty list and dict resulting in 0-param call which needs context
                    return true;
                }
            } else {
                if (nameExpr.Name == "eval") return true;
                if (nameExpr.Name == "execfile") return true;
            }
            return false;
        }

        internal override string CheckAssign() {
            return "can't assign to function call";
        }

        internal override string CheckDelete() {
            return "can't delete function call";
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (Target != null) {
                    Target.Walk(walker);
                }
                if (_args != null) {
                    foreach (Arg arg in _args) {
                        arg.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Target.AppendCodeString(res, ast, format);
            format.Append(
                res,
                format.SpaceBeforeCallParen,
                " ",
                "",
                this.GetPreceedingWhiteSpaceDefaultNull(ast)
            ); 

            res.Append('(');

            if (_args.Length == 0) {
                if (format.SpaceWithinEmptyCallArgumentList != null && format.SpaceWithinEmptyCallArgumentList.Value) {
                    res.Append(' ');
                }
            } else {
                var listWhiteSpace = format.SpaceBeforeComma == null ? this.GetListWhiteSpace(ast) : null;
                var spaceAfterComma = format.SpaceAfterComma.HasValue ? (format.SpaceAfterComma.Value ? " " : "") : (string)null;
                for (int i = 0; i < _args.Length; i++) {
                    if (i > 0) {
                        if (format.SpaceBeforeComma == true) {
                            res.Append(' ');
                        } else if (listWhiteSpace != null) {
                            res.Append(listWhiteSpace[i - 1]);
                        }
                        res.Append(',');
                    } else if (format.SpaceWithinCallParens != null) {
                        _args[i].AppendCodeString(res, ast, format, format.SpaceWithinCallParens.Value ? " " : "");
                        continue;
                    }

                    _args[i].AppendCodeString(res, ast, format, spaceAfterComma);
                }

                if (listWhiteSpace != null && listWhiteSpace.Length == _args.Length) {
                    // trailing comma
                    res.Append(listWhiteSpace[listWhiteSpace.Length - 1]);
                    res.Append(",");
                }
            }
            
            if (!this.IsMissingCloseGrouping(ast)) {
                if (Args.Count != 0 || 
                    format.SpaceWithinEmptyCallArgumentList == null ||
                    !String.IsNullOrWhiteSpace(this.GetSecondWhiteSpaceDefaultNull(ast))) {
                    format.Append(
                        res,
                        format.SpaceWithinCallParens,
                        " ",
                        "",
                        this.GetSecondWhiteSpaceDefaultNull(ast)
                    );
                }
                res.Append(')');
            }
        }

        /// <summary>
        /// Returns the index of the argument in Args at a given index.
        /// </summary>
        /// <returns>False if not within the call. -1 if it is in
        /// an argument not in Args.</returns>
        internal bool GetArgumentAtIndex(PythonAst ast, int index, out int argIndex) {
            argIndex = -1;
            if (index <= Target.EndIndex || index > EndIndex) {
                return false;
            }
            if (Args == null) {
                return true;
            }

            for (int i = 0; i < Args.Count; ++i) {
                var a = Args[i];
                if (index <= a.EndIndexIncludingWhitespace) {
                    argIndex = i;
                    return true;
                }
            }

            if (index < EndIndex) {
                argIndex = -1;
                return true;
            }

            return false;
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            return Target.GetLeadingWhiteSpace(ast);
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            Target.SetLeadingWhiteSpace(ast, whiteSpace);
        }
    }
}
