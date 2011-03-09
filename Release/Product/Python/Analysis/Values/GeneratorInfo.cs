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
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class GeneratorInfo : BuiltinInstanceInfo {
        private readonly FunctionInfo _functionInfo;
        private readonly GeneratorNextBoundBuiltinMethodInfo _nextMethod;
        private readonly GeneratorSendBoundBuiltinMethodInfo _sendMethod;        
        private ISet<Namespace> _yields = EmptySet<Namespace>.Instance;
        private VariableDef _sends;

        public GeneratorInfo(FunctionInfo functionInfo)
            : base(functionInfo.ProjectState._generatorType) {
            _functionInfo = functionInfo;
            ISet<Namespace> nextMeth;
            if (functionInfo._analysisUnit.ProjectState.LanguageVersion.Is3x()) {
                nextMeth = this["__next__"];
            } else {
                nextMeth = this["next"];
            }
            var sendMeth = this["send"];

            _nextMethod = new GeneratorNextBoundBuiltinMethodInfo(this, (BuiltinMethodInfo)nextMeth.First());
            _sendMethod = new GeneratorSendBoundBuiltinMethodInfo(this, (BuiltinMethodInfo)sendMeth.First());

            _sends = new VariableDef();
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            switch(name) {
                case "next": return _nextMethod.SelfSet;
                case "send": return _sendMethod.SelfSet;
            }
            
            return base.GetMember(node, unit, name);
        }

        public override ISet<Namespace> GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            return _yields;
        }

        public void AddYield(ISet<Namespace> yieldValue) {
            int count = _yields.Count;
            _yields = _yields.Union(yieldValue);
            if (_yields.Count != count) {
                _functionInfo.ReturnValue.EnqueueDependents();
            }
        }

        public void AddSend(Node node, AnalysisUnit unit, ISet<Namespace> sendValue) {
            if (_sends.AddTypes(node, unit, sendValue)) {
                _functionInfo._analysisUnit.Enqueue();
            }
        }

        public ISet<Namespace> Yields {
            get {
                return _yields;
            }
        }

        public VariableDef Sends {
            get {
                return _sends;
            }
        }
    }
}
