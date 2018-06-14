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

using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public sealed class MemberExpression : Expression {
        private readonly Expression _target;
        private readonly string _name;

        public MemberExpression(Expression target, string name) {
            _target = target;
            _name = name;
        }

        public void SetLoc(int start, int name, int end) {
            SetLoc(start, end);
            NameHeader = name;
        }

        /// <summary>
        /// Returns the index which is the start of the name.
        /// </summary>
        public int NameHeader { get; set; }

        /// <summary>
        /// The index where the dot appears.
        /// </summary>
        public int DotIndex { get; set; }

        public Expression Target {
            get { return _target; }
        }

        public string Name {
            get { return _name; }
        }

        public override string ToString() {
            return base.ToString() + ":" + _name;
        }

        internal override string CheckAssign() {
            return null;
        }

        internal override string CheckDelete() {
            return null;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_target != null) {
                    _target.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            _target.AppendCodeString(res, ast, format);
            format.Append(res, format.SpaceBeforeDot, " ", "", this.GetPreceedingWhiteSpaceDefaultNull(ast));
            res.Append('.');
            if (!this.IsIncompleteNode(ast)) {
                format.Append(res, format.SpaceAfterDot, " ", "", this.GetSecondWhiteSpaceDefaultNull(ast));
                if (format.UseVerbatimImage) {
                    res.Append(this.GetVerbatimImage(ast) ?? _name);
                } else {
                    res.Append(_name);
                }
            }
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            return _target.GetLeadingWhiteSpace(ast);
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            _target.SetLeadingWhiteSpace(ast, whiteSpace);
        }

        /// <summary>
        /// Returns the span of the name component of the expression
        /// </summary>
        public SourceSpan GetNameSpan(PythonAst parent) {
            return new SourceSpan(parent.IndexToLocation(NameHeader), GetEnd(parent));
        }
    }
}
