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
using System.Collections.Generic;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Debugger {
    /// <summary>
    /// Extracts a flat list of all the sections of code protected by exception
    /// handlers.
    /// </summary>
    class TryHandlerWalker : PythonWalker {
        private List<TryStatement> _statements;

        public TryHandlerWalker() {
            _statements = new List<TryStatement>();
        }

        public ICollection<TryStatement> Statements {
            get {
                return _statements;
            }
        }

        public override bool Walk(TryStatement node) {
            _statements.Add(node);
            return base.Walk(node);
        }
    }
}
