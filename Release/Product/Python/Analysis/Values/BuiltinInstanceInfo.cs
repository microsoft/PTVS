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
    class BuiltinInstanceInfo : BuiltinNamespace<IPythonType>, IReferenceableContainer {
        private readonly BuiltinClassInfo _klass;

        public BuiltinInstanceInfo(BuiltinClassInfo klass)
            : base(klass._type, klass.ProjectState) {
            _klass = klass;
        }

        public BuiltinClassInfo ClassInfo {
            get {
                return _klass;
            }
        }

        public override IPythonType PythonType {
            get { return _type; }
        }

        public override ICollection<OverloadResult> Overloads {
            get {
                // TODO: look for __call__ and return overloads
                return base.Overloads;
            }
        }

        public override string Description {
            get {
                return _klass._type.Name;
            }
        }

        public override string Documentation {
            get {
                return _klass.Documentation;
            }
        }

        public override PythonMemberType ResultType {
            get {
                switch (_klass.ResultType) {
                    case PythonMemberType.Enum: return PythonMemberType.EnumInstance;
                    case PythonMemberType.Delegate: return PythonMemberType.DelegateInstance;
                    default:
                        return PythonMemberType.Instance;
                }
            }
        }

        public override ISet<Namespace> GetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index) {
            // TODO: look for __getitem__, index, get result
            return base.GetIndex(node, unit, index);
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            var res = base.GetMember(node, unit, name);
            if (res.Count > 0) {
                _klass.AddMemberReference(node, unit, name);
                return res.GetDescriptor(node, this, _klass, unit);
            }
            return res;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, ISet<Namespace> value) {
            var res = base.GetMember(node, unit, name);
            if (res.Count > 0) {
                _klass.AddMemberReference(node, unit, name);
            }
        }

        #region IReferenceableContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            return _klass.GetDefinitions(name);
        }

        #endregion
    }
}
