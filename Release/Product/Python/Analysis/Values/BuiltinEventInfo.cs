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
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BuiltinEventInfo : BuiltinNamespace<IPythonType> {
        private readonly IPythonEvent _value;
        private string _doc;

        public BuiltinEventInfo(IPythonEvent value, PythonAnalyzer projectState)
            : base(value.EventHandlerType, projectState) {
            _value = value;
            _doc = null;
        }

        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, ISet<Namespace> value) {
            base.AugmentAssign(node, unit, value);
            var args = GetEventInvokeArgs(ProjectState);
            foreach (var r in value) {
                r.Call(node, unit, args, new string[0]);
            }
        }

        internal ISet<Namespace>[] GetEventInvokeArgs(PythonAnalyzer state) {
            var p = _value.GetEventParameterTypes();

            var args = new ISet<Namespace>[p.Count];
            for (int i = 0; i < p.Count; i++) {
                args[i] = state.GetInstance(p[i]).SelfSet;
            }
            return args;
        }

        public override string Description {
            get {
                return "event of type " + _value.EventHandlerType.Name;
            }
        }

        public override PythonMemberType ResultType {
            get {
                return _value.MemberType;
            }
        }

        public override string Documentation {
            get {
                if (_doc == null) {
                    _doc = Utils.StripDocumentation(_value.Documentation);
                }
                return _doc;
            }
        }
    }
}
