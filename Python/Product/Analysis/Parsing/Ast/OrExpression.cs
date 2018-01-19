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
    public class OrExpression : Expression {
        public OrExpression(Expression left, Expression right, int orIndex) {
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right;
            StartIndex = left.StartIndex;
            EndIndex = right.EndIndex;
            OrIndex = orIndex;
        }

        public Expression Left { get; }

        public Expression Right { get; }

        public int OrIndex { get; }

        public override string NodeName {
            get {
                return "or expression";
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Left.Walk(walker);
                Right?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            BinaryExpression.BinaryToCodeString(res, ast, format, this, Left, Right, "or");
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            return Left.GetLeadingWhiteSpace(ast);
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            Left.SetLeadingWhiteSpace(ast, whiteSpace);
        }
    }
}
