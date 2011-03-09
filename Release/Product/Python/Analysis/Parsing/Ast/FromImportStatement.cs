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
using System.Runtime.CompilerServices;

namespace Microsoft.PythonTools.Parsing.Ast {
    
    public class FromImportStatement : Statement {
        private static readonly string[] _star = new[] { "*" };
        private readonly ModuleName _root;
        private readonly string[] _names;
        private readonly string[] _asNames;
        private readonly bool _fromFuture;
        private readonly bool _forceAbsolute;

        private PythonVariable[] _variables;

        public static IList<string> Star {
            get { return FromImportStatement._star; }
        }

        public DottedName Root {
            get { return _root; }
        } 

        public bool IsFromFuture {
            get { return _fromFuture; }
        }

        public IList<string> Names {
            get { return _names; }
        }

        public IList<string> AsNames {
            get { return _asNames; }
        }

        internal PythonVariable[] Variables {
            get { return _variables; }
            set { _variables = value; }
        }

        public FromImportStatement(ModuleName root, string[] names, string[] asNames, bool fromFuture, bool forceAbsolute) {
            _root = root;
            _names = names;
            _asNames = asNames;
            _fromFuture = fromFuture;
            _forceAbsolute = forceAbsolute;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }
    }
}
