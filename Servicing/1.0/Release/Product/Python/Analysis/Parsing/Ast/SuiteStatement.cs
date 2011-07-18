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

using System.Collections.Generic;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {

    public sealed class SuiteStatement : Statement {
        private readonly Statement[] _statements;

        public SuiteStatement(Statement[] statements) {
            _statements = statements;
        }

        public IList<Statement> Statements {
            get { return _statements; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_statements != null) {
                    foreach (Statement s in _statements) {
                        s.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        public override string Documentation {
            get {
                if (_statements.Length > 0) {
                    return _statements[0].Documentation;
                }
                return null;
            }
        }

        /// <summary>
        /// Returns a new SuiteStatement which is composed of a subset of the statements in this suite statement.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public SuiteStatement CloneSubset(PythonAst ast, int start, int end) {
            Statement[] statements = new Statement[end - start + 1];
            for (int i = start; i <= end; i++) {
                statements[i - start] = Statements[i];
            }


            var res = new SuiteStatement(statements);

            // propagate white space so we stay mostly the same...
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            var colonWhiteSpace = this.GetProceedingWhiteSpaceDefaultNull(ast);
            if (colonWhiteSpace != null) {
                ast.SetAttribute(res, NodeAttributes.PreceedingWhiteSpace, "");
            } else if (itemWhiteSpace != null) {
                ast.SetAttribute(res, NodeAttributes.ListWhiteSpace, new string[0]);

            }
            if (this.IsAltForm(ast)) {
                ast.SetAttribute(res, NodeAttributes.IsAltFormValue, NodeAttributes.IsAltFormValue);
            }
            return res;
        }

        /// <summary>
        /// True if this is a suite statement which is used as the top-level suite for
        /// a class / function definition
        /// </summary>
        public bool IsFunctionOrClassSuite(PythonAst ast) {
            return !this.IsAltForm(ast) && this.GetListWhiteSpace(ast) == null;
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast) {
            // SuiteStatement comes in 3 forms:
            //  1. The body of a if/else/while/for/etc... where there's an opening colon
            //  2. A set of semi-colon separated items
            //  3. A top-level group of statements in a top-level PythonAst node.
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            var colonWhiteSpace = this.GetProceedingWhiteSpaceDefaultNull(ast);
            if (this.IsAltForm(ast)) {
                // suite statement in top-level PythonAst, we have no colons or other delimiters
                foreach (var statement in _statements) {
                    statement.AppendCodeString(res, ast);
                }
            } else if (itemWhiteSpace != null) {
                // form 2, semi-colon seperated list.
                for (int i = 0; i < _statements.Length; i++) {
                    if (i > 0) {
                        if (i - 1 < itemWhiteSpace.Length) {
                            res.Append(itemWhiteSpace[i - 1]);
                        }
                        res.Append(';');
                    }
                    _statements[i].AppendCodeString(res, ast);
                }
                if (itemWhiteSpace != null && itemWhiteSpace.Length == _statements.Length && _statements.Length != 0) {
                    // trailing semi-colon
                    res.Append(itemWhiteSpace[itemWhiteSpace.Length - 1]);
                    res.Append(";");
                }
            } else {
                // 3rd form, suite statement as the body of a class/function, we include the colon.
                if (colonWhiteSpace != null) {
                    res.Append(colonWhiteSpace);
                }
                res.Append(':');

                var secondWhiteSpace = this.GetSecondWhiteSpaceDefaultNull(ast);
                if (secondWhiteSpace != null) {
                    res.Append(secondWhiteSpace);
                } else {
                    res.Append("\r\n");
                }

                foreach (var statement in _statements) {
                    statement.AppendCodeString(res, ast);
                }
            }
        }

        internal override string GetLeadingWhiteSpace(PythonAst ast) {
            return _statements[0].GetLeadingWhiteSpace(ast);
        }
    }
}
