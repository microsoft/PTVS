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
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public sealed class Arg : Node {
        private int? _endIndexIncludingWhitespace;

        public Arg(Expression expression) : this(null, expression) { }

        public Arg(Expression name, Expression expression) {
            NameExpression = name;
            Expression = expression;
        }

        public string Name => (NameExpression as NameExpression)?.Name;
        public Expression NameExpression { get; }

        public Expression Expression { get; }
        public int EndIndexIncludingWhitespace {
            get => _endIndexIncludingWhitespace ?? throw new InvalidOperationException("EndIndexIncludingWhitespace has not been initialized");
            set => _endIndexIncludingWhitespace = value;
        }

        public override string ToString() {
            return base.ToString() + ":" + NameExpression;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (Expression != null) {
                    Expression.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        internal string GetPreceedingWhiteSpaceDefaultNull(PythonAst ast) {
            if (NameExpression != null) {
                return NameExpression.GetPreceedingWhiteSpaceDefaultNull(ast);
            }
            return Expression?.GetPreceedingWhiteSpaceDefaultNull(ast);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format)
        {
            if (NameExpression != null) {
                if (Name == "*" || Name == "**") {
                    NameExpression.AppendCodeString(res, ast, format);
                    Expression.AppendCodeString(res, ast, format);
                } else {
                    // keyword arg
                    NameExpression.AppendCodeString(res, ast, format);
                    res.Append(this.GetPreceedingWhiteSpace(ast));
                    res.Append('=');
                    Expression.AppendCodeString(res, ast, format);
                }
            } else {
                Expression.AppendCodeString(res, ast, format);
            }
        }
    }
}
