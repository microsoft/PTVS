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

using System;
using System.Diagnostics;
using System.Dynamic;

namespace Microsoft.PythonTools.Parsing.Ast {
    public class NameExpression : Expression {
        private readonly string _name;
#if NAME_BINDING
        private PythonReference _reference;
        private bool _assigned;                  // definitely assigned
#endif

        public NameExpression(string name) {
            _name = name;
        }

        public string Name {
            get { return _name; }
        }

#if NAME_BINDING
        internal PythonReference Reference {
            get { return _reference; }
            set { _reference = value; }
        }

        internal bool Assigned {
            get { return _assigned; }
            set { _assigned = value; }
        }
#endif

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
            }
            walker.PostWalk(this);
        }
    }
}
