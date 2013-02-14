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
    public class ErrorExpression : Expression {
        private readonly Expression _preceeding;
        private readonly string _verbatimImage;

        public ErrorExpression(string verbatimImage, Expression preceeding) {
            _preceeding = preceeding;
            _verbatimImage = verbatimImage;
        }

        public string VerbatimImage {
            get {
                return _verbatimImage;
            }
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast) {
            if (_preceeding != null) {
                _preceeding.AppendCodeString(res, ast);
            }
            res.Append(_verbatimImage ?? "<error>");
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_preceeding != null) {
                    _preceeding.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
