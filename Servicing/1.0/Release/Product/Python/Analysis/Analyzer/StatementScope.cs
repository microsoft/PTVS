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

namespace Microsoft.PythonTools.Analysis.Interpreter {
    class StatementScope : InterpreterScope {
        public Statement _startNode, _endNode;

        public StatementScope(Statement startNode)
            : base(null) {
            _startNode = _endNode = startNode;
        }

        public override string Name {
            get { return "<statements>"; }
        }

        public override int GetStart(PythonAst ast) {
            return _startNode.GetStart(ast).Line;
        }

        public override int GetStop(PythonAst ast) {
            return _endNode.GetEnd(ast).Line;
        }

        public Statement StartNode {
            get {
                return _startNode;
            }
        }

        public Statement EndNode {
            get {
                return _endNode;
            }
            set {
                _endNode = value;
            }
        }
    }
}
