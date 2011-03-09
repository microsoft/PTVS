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

namespace Microsoft.PythonTools.Parsing.Ast {
    public abstract class SequenceExpression : Expression {
        private readonly Expression[] _items;

        protected SequenceExpression(Expression[] items) {
            _items = items;
        }

        public IList<Expression> Items {
            get { return _items; }
        }

        internal override string CheckAssign() {
            return null;
        }

        internal override string CheckDelete() {
            return null;
        }

        internal override string CheckAugmentedAssign() {
            return "illegal expression for augmented assignment";
        }

        private static bool IsComplexAssignment(Expression expr) {
            return !(expr is NameExpression);
        }
    }
}
