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
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BuiltinPropertyInfo : BuiltinNamespace<IPythonType> {
        private readonly IBuiltinProperty _value;
        private string _doc;

        public BuiltinPropertyInfo(IBuiltinProperty value, PythonAnalyzer projectState)
            : base(value.Type, projectState) {
            _value = value;
            _doc = null;
        }

        public override IPythonType PythonType {
            get { return _type; }
        }

        public override ISet<Namespace> GetDescriptor(Node node, Namespace instance, Namespace context, Interpreter.AnalysisUnit unit) {
            if (instance == unit.ProjectState._noneInst) {
                return base.GetDescriptor(node, instance, context, unit);
            }

            return ((BuiltinClassInfo)ProjectState.GetNamespaceFromObjects(_value.Type)).Instance.SelfSet;
        }

        public override ISet<Namespace> GetStaticDescriptor(Interpreter.AnalysisUnit unit) {
            if (_value.IsStatic) {
                BuiltinClassInfo klass = (BuiltinClassInfo)ProjectState.GetNamespaceFromObjects(_value.Type);
                return klass.Instance.SelfSet;
            }

            return base.GetStaticDescriptor(unit);
        }

        public override string Description {
            get {
                return _value.Description;
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
                    _doc = _value.Documentation;
                }
                return _doc;
            }
        }

        public override ILocatedMember GetLocatedMember() {
            return _value as ILocatedMember;
        }
    }
}
