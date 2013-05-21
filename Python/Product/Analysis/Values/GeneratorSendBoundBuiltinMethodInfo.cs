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

using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class GeneratorSendBoundBuiltinMethodInfo : BoundBuiltinMethodInfo {
        private readonly GeneratorInfo _generator;

        public GeneratorSendBoundBuiltinMethodInfo(GeneratorInfo generator, BuiltinMethodInfo method)
            : base(method) {
            _generator = generator;
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 1) {
                _generator.AddSend(node, unit, args[0]);
            }

            _generator.Yields.AddDependency(unit);

            return _generator.Yields.Types;
        }
    }
}
