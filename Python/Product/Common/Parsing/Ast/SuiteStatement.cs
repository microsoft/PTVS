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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Core;
using Microsoft.PythonTools.Common.Core.Extensions;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public sealed class SuiteStatement : Statement {
        private Statement[] _statements;

        public SuiteStatement(Statement[] statements) {
            _statements = statements;
        }

        public IList<Statement> Statements => _statements;
        public override IEnumerable<Node> GetChildNodes() => _statements.WhereNotNull();

        public void FilterStatements(Func<Statement, bool> filter) 
            => _statements = _statements.Where(filter).ToArray();

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var s in _statements.MaybeEnumerate()) {
                    s.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                foreach (var s in _statements.MaybeEnumerate()) {
                    cancellationToken.ThrowIfCancellationRequested();
                    await s.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        public override string Documentation => _statements.Length > 0 ? _statements[0].Documentation : null;

        /// <summary>
        /// Returns a new SuiteStatement which is composed of a subset of the statements in this suite statement.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public SuiteStatement CloneSubset(PythonAst ast, int start, int end) {
            var statements = new Statement[end - start + 1];
            for (var i = start; i <= end; i++) {
                statements[i - start] = Statements[i];
            }

            var res = new SuiteStatement(statements);

            // propagate white space so we stay mostly the same...
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            var colonWhiteSpace = this.GetPreceedingWhiteSpaceDefaultNull(ast);

            if (itemWhiteSpace != null) {
                // semi-colon list of statements, must end in a new line, but the original new line
                // could be multiple lines.
                ast.SetAttribute(res, NodeAttributes.ListWhiteSpace, new string[0]);
            } else {
                ast.SetAttribute(res, NodeAttributes.IsAltFormValue, NodeAttributes.IsAltFormValue);
            }

            return res;
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            // SuiteStatement comes in 3 forms:
            //  1. The body of a if/else/while/for/etc... where there's an opening colon
            //  2. A set of semi-colon separated items
            //  3. A top-level group of statements in a top-level PythonAst node.
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            var colonWhiteSpace = this.GetPreceedingWhiteSpaceDefaultNull(ast);
            if (this.IsAltForm(ast)) {
                // suite statement in top-level PythonAst, we have no colons or other delimiters
                foreach (var statement in _statements) {
                    statement.AppendCodeString(res, ast, format);
                }
            } else if (itemWhiteSpace != null) {
                if (format.BreakMultipleStatementsPerLine) {
                    var leadingWhiteSpace = "";
                    for (var i = 0; i < _statements.Length; i++) {
                        if (i == 0) {
                            var tmp = new StringBuilder();
                            _statements[i].AppendCodeString(tmp, ast, format);
                            var stmt = tmp.ToString();
                            res.Append(stmt);

                            // figure out the whitespace needed for the next statement based upon the current statement
                            for (var curChar = 0; curChar < stmt.Length; curChar++) {
                                if (!char.IsWhiteSpace(stmt[curChar])) {
                                    leadingWhiteSpace = format.GetNextLineProceedingText(stmt.Substring(0, curChar));
                                    break;
                                }
                            }
                        } else {
                            _statements[i].AppendCodeString(res, ast, format, leadingWhiteSpace);
                        }
                    }
                } else {
                    // form 2, semi-colon seperated list.
                    for (var i = 0; i < _statements.Length; i++) {
                        if (i > 0) {
                            if (i - 1 < itemWhiteSpace.Length) {
                                res.Append(itemWhiteSpace[i - 1]);
                            }
                            res.Append(';');
                        }
                        _statements[i].AppendCodeString(res, ast, format);
                    }
                }

                if (itemWhiteSpace != null && itemWhiteSpace.Length == _statements.Length && _statements.Length != 0) {
                    // trailing semi-colon
                    if (!format.RemoveTrailingSemicolons) {
                        res.Append(itemWhiteSpace[itemWhiteSpace.Length - 1]);
                        res.Append(";");
                    }
                }
            } else {
                // 3rd form, suite statement as the body of a class/function, we include the colon.
                if (colonWhiteSpace != null) {
                    res.Append(colonWhiteSpace);
                }
                res.Append(':');

                foreach (var statement in _statements) {
                    statement.AppendCodeString(res, ast, format);
                }
            }
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            if (_statements.Length == 0) {
                return "";
            }
            return _statements[0].GetLeadingWhiteSpace(ast);
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            if (_statements.Length != 0) {
                _statements[0].SetLeadingWhiteSpace(ast, whiteSpace);
            }
        }
    }
}
