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

namespace Microsoft.PythonTools.Parsing.Ast {
    public sealed class GeneratorExpression : Comprehension {
        private readonly ComprehensionIterator[] _iterators;
        private readonly Expression _item;

        public GeneratorExpression(Expression item, ComprehensionIterator[] iterators) {
            _item = item;
            _iterators = iterators;
        }

        public override IList<ComprehensionIterator> Iterators {
            get { return _iterators; }
        }

        public override string NodeName { get { return "generator"; } }

        public Expression Item {
            get {
                return _item;
            }
        }

        internal override string CheckAssign() {
            return "can't assign to generator expression";
        }

        internal override string CheckAugmentedAssign() {
            return CheckAssign();
        }

        internal override string CheckDelete() {
            return "can't delete generator expression";
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_item != null) {
                    _item.Walk(walker);
                }

                if (_iterators != null) {
                    foreach (ComprehensionIterator ci in _iterators) {
                        ci.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            if (this.IsAltForm(ast)) {
                this.AppendCodeString(res, ast, format, "", "", _item);
            } else {
                this.AppendCodeString(res, ast, format, "(", this.IsMissingCloseGrouping(ast) ? "" : ")", _item);
            }
        }
    }
}
