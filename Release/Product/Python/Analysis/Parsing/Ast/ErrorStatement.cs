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

using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public class ErrorStatement : Statement {
        private readonly Statement[] _preceeding;

        public ErrorStatement(Statement[] preceeding) {
            _preceeding = preceeding;
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast) {
            foreach(var preceeding in _preceeding) {
                preceeding.AppendCodeString(res, ast);
            }
            res.Append(this.GetVerbatimImage(ast) ?? "<error stmt>");
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var preceeding in _preceeding) {
                    preceeding.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
