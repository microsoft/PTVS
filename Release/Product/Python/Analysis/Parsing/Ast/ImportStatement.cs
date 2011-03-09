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

    public class ImportStatement : Statement {
        private readonly ModuleName[] _names;
        private readonly string[] _asNames;
        private readonly bool _forceAbsolute;

        private PythonVariable[] _variables;

        public ImportStatement(ModuleName[] names, string[] asNames, bool forceAbsolute) {
            _names = names;
            _asNames = asNames;
            _forceAbsolute = forceAbsolute;
        }

        internal PythonVariable[] Variables {
            get { return _variables; }
            set { _variables = value; }
        }

        public IList<DottedName> Names {
            get { return _names; }
        }

        public IList<string> AsNames {
            get { return _asNames; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }
    }
}
