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
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Interpreter {
    sealed class FunctionScope : InterpreterScope {
        internal HashSet<string> _assignedVars;

        public FunctionScope(FunctionInfo functionInfo, Node node)
            : base(functionInfo, node) {
        }

        public FunctionInfo Function {
            get {
                return Namespace as FunctionInfo;
            }
        }

        public override VariableDef CreateVariable(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            var res = base.CreateVariable(node, unit, name, addRef);
            if (_assignedVars == null) {
                _assignedVars = new HashSet<string>();
            }

            _assignedVars.Add(name);
            return res;
        }

        public override int GetBodyStart(PythonAst ast) {
            return ast.IndexToLocation(((FunctionDefinition)Node).HeaderIndex).Index;
        }

        public override string Name {
            get { return Function.FunctionDefinition.Name;  }
        }
    }
}
