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

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast) {
            // SuiteStatement comes in 3 forms:
            //  1. The body of a if/else/while/for/etc... where there's an opening colon
            //  2. A set of semi-colon separated items
            //  3. A top-level group of statements in a top-level PythonAst node.
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            var colonWhiteSpace = this.GetProceedingWhiteSpaceDefaultNull(ast);
            if (itemWhiteSpace != null) {
                // form 2, semi-colon seperated list.
                for (int i = 0; i < _statements.Length; i++) {
                    if (i > 0) {
                        res.Append(itemWhiteSpace[i - 1]);
                        res.Append(';');
                    }
                    _statements[i].AppendCodeString(res, ast);
                }
                if (itemWhiteSpace != null && itemWhiteSpace.Length == _statements.Length && _statements.Length != 0) {
                    // trailing semi-colon
                    res.Append(itemWhiteSpace[itemWhiteSpace.Length - 1]);
                    res.Append(";");
                }
            } else if (colonWhiteSpace != null) {
                res.Append(colonWhiteSpace);
                res.Append(':');
                var secondWhiteSpace = this.GetSecondWhiteSpaceDefaultNull(ast);
                if (secondWhiteSpace != null) {
                    res.Append(secondWhiteSpace);
                }
                foreach (var statement in _statements) {
                    statement.AppendCodeString(res, ast);
                }
            } else {
                foreach (var statement in _statements) {
                    statement.AppendCodeString(res, ast);
                }
            }
        }
    }
}
