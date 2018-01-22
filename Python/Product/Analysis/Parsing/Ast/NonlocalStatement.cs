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

using System.Collections.Generic;
using System.Text;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Parsing.Ast {
    public class NonlocalStatement : Statement {
        private readonly NameExpression[] _names;

        public NonlocalStatement(NameExpression[] names) {
            _names = names;
        }

        public IList<NameExpression> Names {
            get { return _names; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var n in _names.MaybeEnumerate()) {
                    n?.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var namesWhiteSpace = this.GetNamesWhiteSpace(ast);

            if (namesWhiteSpace != null) {
                ListExpression.AppendItems(res, ast, format, "nonlocal", "", this, Names.Count, (i, sb) => {
                    sb.Append(namesWhiteSpace[i]);
                    Names[i].AppendCodeString(res, ast, format);
                });
            } else {
                ListExpression.AppendItems(res, ast, format, "nonlocal", "", this, Names.Count, (i, sb) => Names[i].AppendCodeString(sb, ast, format));
            }
        }
    }
}
