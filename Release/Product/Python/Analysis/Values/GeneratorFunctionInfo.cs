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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents a function which is a generator (it contains yield expressions)
    /// </summary>
    class GeneratorFunctionInfo : FunctionInfo {
        private readonly GeneratorInfo _generator;

        internal GeneratorFunctionInfo(AnalysisUnit unit)
            : base(unit) {
            _generator = new GeneratorInfo(unit);
        }

        public GeneratorInfo Generator {
            get {
                return _generator;
            }
        }

        public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
            _generator.Callers.AddDependency(unit);

            base.Call(node, unit, args, keywordArgNames);
            
            return _generator.SelfSet;
        }
    }
}
