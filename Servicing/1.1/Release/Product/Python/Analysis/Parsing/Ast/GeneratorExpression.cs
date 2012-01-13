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

        internal override void AppendCodeString(StringBuilder res, PythonAst ast) {
            if (this.IsAltForm(ast)) {
                this.AppendCodeString(res, ast, "", "", _item);
            } else {
                this.AppendCodeString(res, ast, "(", this.IsMissingCloseGrouping(ast) ? "" : ")", _item);
            }
        }
    }
}
