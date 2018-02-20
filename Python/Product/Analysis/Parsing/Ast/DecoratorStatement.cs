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
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {

    public class DecoratorStatement : Statement {
        private readonly Expression[] _decorators;

        public DecoratorStatement(Expression[] decorators) {
            _decorators = decorators;
        }

        public IList<Expression> Decorators => _decorators;
        internal Expression[] DecoratorsInternal => _decorators;

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var decorator in _decorators) {
                    if (decorator != null) {
                        decorator.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var decorateWhiteSpace = this.GetNamesWhiteSpace(ast);
            if (DecoratorsInternal != null) {
                for (int i = 0, curWhiteSpace = 0; i < DecoratorsInternal.Length; i++) {
                    if (decorateWhiteSpace != null) {
                        format.ReflowComment(res, decorateWhiteSpace[curWhiteSpace++]);
                    }
                    res.Append('@');
                    if (DecoratorsInternal[i] != null) {
                        DecoratorsInternal[i].AppendCodeString(res, ast, format);
                        if (decorateWhiteSpace != null) {
                            format.ReflowComment(res, decorateWhiteSpace[curWhiteSpace++]);
                        } else {
                            res.Append(Environment.NewLine);
                        }
                    }
                }
            }
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            var decorateWhiteSpace = this.GetNamesWhiteSpace(ast);
            if (decorateWhiteSpace != null && decorateWhiteSpace.Length > 0) {
                return decorateWhiteSpace[0];
            }
            return "";
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            var decorateWhiteSpace = this.GetNamesWhiteSpace(ast);
            if (decorateWhiteSpace != null && decorateWhiteSpace.Length > 0) {
                decorateWhiteSpace[0] = whiteSpace;
            }
            
        }
    }
}
