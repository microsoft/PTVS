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

    public class NonlocalStatement : Statement {
        private readonly string[] _names;

        public NonlocalStatement(string[] names) {
            _names = names;
        }

        public IList<string> Names {
            get { return _names; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast) {
            var namesWhiteSpace = this.GetNamesWhiteSpace(ast);
            var verbatimNames = this.GetVerbatimNames(ast);

            if (namesWhiteSpace != null) {
                ListExpression.AppendItems(res, ast, "nonlocal", "", this, Names.Count, (i, sb) => {
                    sb.Append(namesWhiteSpace[i]);
                    sb.Append(verbatimNames != null ? (verbatimNames[i] ?? Names[i]) : Names[i]); 
                });
            } else {
                ListExpression.AppendItems(res, ast, "nonlocal", "", this, Names.Count, (i, sb) => sb.Append(verbatimNames != null ? (verbatimNames[i] ?? Names[i]) : Names[i]));
            }

        }
    }
}
