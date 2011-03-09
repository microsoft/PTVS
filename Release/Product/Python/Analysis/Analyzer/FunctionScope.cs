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
        public FunctionScope(FunctionInfo functionInfo, FunctionDefinition node)
            : base(functionInfo, node) {
        }

        public FunctionInfo Function {
            get {
                return Namespace as FunctionInfo;
            }
        }

        public override string Name {
            get { return Function.FunctionDefinition.Name;  }
        }

        public VariableDef DefineVariable(Parameter node, AnalysisUnit unit) {
            return Variables[node.Name] = new LocatedVariableDef(unit.DeclaringModule.ProjectEntry, node);
        }

        public override IEnumerable<AnalysisVariable> GetVariablesForDef(string name, VariableDef def) {
            // if this is a parameter or a local indicate any values which we know are assigned to it.
            foreach (var type in def.Types) {
                if (type.Location != null) {
                    yield return new AnalysisVariable(VariableType.Value, type.Location);
                }
            }
        }
    }
}
