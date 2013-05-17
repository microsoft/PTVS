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
    public sealed class Arg : Node {
        private readonly Expression _name;
        private readonly Expression _expression;

        public Arg(Expression expression) : this(null, expression) { }

        public Arg(Expression name, Expression expression) {
            _name = name;
            _expression = expression;
        }

        public string Name {
            get {
                var nameExpr = _name as NameExpression;
                if (nameExpr != null) {
                    return nameExpr.Name;
                }
                return null;
            }
        }

        public Expression NameExpression {
            get {
                return _name;
            }
        }

        public Expression Expression {
            get { return _expression; }
        } 

        public override string ToString() {
            return base.ToString() + ":" + _name;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_expression != null) {
                    _expression.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format)
        {
            if (_name != null) {
                if (Name == "*" || Name == "**") {
                    _name.AppendCodeString(res, ast, format);
                    _expression.AppendCodeString(res, ast, format);
                } else {
                    // keyword arg
                    _name.AppendCodeString(res, ast, format);
                    res.Append(this.GetProceedingWhiteSpace(ast));
                    res.Append('=');
                    _expression.AppendCodeString(res, ast, format);
                }
            } else {
                _expression.AppendCodeString(res, ast, format);
            }
        }
    }
}
