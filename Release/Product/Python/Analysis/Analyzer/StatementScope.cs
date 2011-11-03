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
        public int _startIndex, _endIndex;

        public StatementScope(int index)
            : base(null) {
            _startIndex = _endIndex = index;
        }

        public override string Name {
            get { return "<statements>"; }
        }

        public override int GetStart(PythonAst ast) {
            return ast.IndexToLocation(_startIndex).Index;
        }

        public override int GetStop(PythonAst ast) {
            return ast.IndexToLocation(_endIndex).Index;
        }

        public int EndIndex {
            set {
                _endIndex = value;
            }
        }
    }
}
