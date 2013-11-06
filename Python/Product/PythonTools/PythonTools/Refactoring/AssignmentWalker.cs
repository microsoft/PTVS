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

using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Refactoring {

    /// <summary>
    /// A walker which handles all nodes which can result in assignments and
    /// calls back on a special walker for defining the variable names. 
    /// 
    /// Note this class only handles things which show up as name expressions,
    /// other implicit assignments (such as class and function definitions,
    /// import/from import statements, need to be handled by the derived binder).
    /// </summary>
    abstract class AssignmentWalker : PythonWalker {
        public abstract AssignedNameWalker Define {
            get;
        }

        #region Assignment Walkers

        public override bool Walk(AssignmentStatement node) {
            foreach (var lhs in node.Left) {
                DefineExpr(lhs);
            }
            node.Right.Walk(this);
            return false;
        }

        private void DefineExpr(Expression lhs) {
            if (lhs is NameExpression) {
                lhs.Walk(Define);
            } else {
                // fob.oar = 42, fob[oar] = 42, we don't actually define any variables
                lhs.Walk(this);
            }
        }

        public override bool Walk(AugmentedAssignStatement node) {
            DefineExpr(node.Left);
            node.Right.Walk(this);
            return false;
        }

        public override bool Walk(DelStatement node) {
            foreach (var expr in node.Expressions) {
                DefineExpr(expr);
            }
            return false;
        }

        public override bool Walk(ComprehensionFor node) {
            if (node.Left != null) {
                node.Left.Walk(Define);
            }            
            if (node.List != null) {
                node.List.Walk(this);
            }
            return false;
        }

        private bool WalkIterators(Comprehension node) {
            if (node.Iterators != null) {
                foreach (ComprehensionIterator ci in node.Iterators) {
                    ci.Walk(this);
                }
            }

            return false;
        }

        public override bool Walk(ForStatement node) {
            if (node.Left != null) {
                node.Left.Walk(Define);
            }
            if (node.List != null) {
                node.List.Walk(this);
            }
            if (node.Body != null) {
                node.Body.Walk(this);
            }
            if (node.Else != null) {
                node.Else.Walk(this);
            }
            return false;
        }

        public override bool Walk(WithStatement node) {
            foreach (var item in node.Items) {
                if (item.Variable != null) {
                    item.Variable.Walk(Define);
                }
                if (item.ContextManager != null) {
                    item.ContextManager.Walk(this);
                }
            }
            if (node.Body != null) {
                node.Body.Walk(this);
            }
            return false;
        }

        #endregion
    }

    abstract class AssignedNameWalker : PythonWalkerNonRecursive {

        public override abstract bool Walk(NameExpression node);

        public override bool Walk(ParenthesisExpression node) {
            return true;
        }

        public override bool Walk(TupleExpression node) {
            return true;
        }

        public override bool Walk(ListExpression node) {
            return true;
        }
    }

}
