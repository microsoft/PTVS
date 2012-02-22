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

using System;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public class ConstantExpression : Expression {
        private readonly object _value;

        public ConstantExpression(object value) {
            _value = value;
        }

        public object Value {
            get {
                return _value; 
            }
        }

        internal override string CheckAssign() {
            if (_value == null) {
                return "assignment to None";
            }

            return "can't assign to literal";
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        public override string NodeName {
            get {
                return "literal";
            }
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast) {
            res.Append(this.GetExtraVerbatimText(ast) ?? (this.GetProceedingWhiteSpaceDefaultNull(ast) + (_value == null ? "None" : _value.ToString())));
        }

        /// <summary>
        /// Gets the leading white space for the node.  Usually this is just the leading mark space marked for this node,
        /// but some nodes will have their leading white space captures in a child node and those nodes will extract
        /// the white space appropriately.
        /// </summary>
        internal override string GetLeadingWhiteSpace(PythonAst ast) {
            string verbatim = this.GetExtraVerbatimText(ast);
            if (verbatim != null) {
                for (int i = 0; i < verbatim.Length; i++) {
                    if (!Char.IsWhiteSpace(verbatim[i])) {
                        return verbatim.Substring(0, i);
                    }
                }
                return verbatim;
            }
            return base.GetLeadingWhiteSpace(ast);
        }
    }
}
