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
    public class NameExpressionWithAnnotation : NameExpression {
        private readonly Expression _annotation;

        public NameExpressionWithAnnotation(string name, Expression annotation) : base(name) {
            _annotation = annotation;
        }

        public override string ToString() {
            if (_annotation != null) {
                return base.ToString() + ":" + _annotation.ToString();
            }
            return base.ToString();
        }

        public Expression Annotation => _annotation;

        public override string NodeName => "annotated name";

        internal override string CheckAugmentedAssign() => "cannot assign to " + NodeName;
        internal override string CheckDelete() => "cannot delete " + NodeName;


        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            base.AppendCodeString(res, ast, format);
            if (_annotation != null) {
                // For now, use same formatting as around an assignment
                if (format.SpacesAroundAssignmentOperator == null) {
                    res.Append(this.GetSecondWhiteSpaceDefaultNull(ast) ?? "");
                } else if (format.SpacesAroundAssignmentOperator == true) {
                    res.Append(' ');
                }
                res.Append(':');
                _annotation.AppendCodeString(res, ast, format);
            }
        }
    }
}
