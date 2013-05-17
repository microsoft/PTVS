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
using Microsoft.PythonTools.Parsing;
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

        public override PythonMemberType MemberType {
            get {
                switch (_klass.MemberType) {
                    case PythonMemberType.Enum: return PythonMemberType.EnumInstance;
                    case PythonMemberType.Delegate: return PythonMemberType.DelegateInstance;
                    default:
                        return PythonMemberType.Instance;
                }
            }
        }

        public override INamespaceSet GetMember(Node node, AnalysisUnit unit, string name) {
            var res = base.GetMember(node, unit, name);
            if (res.Count > 0) {
                _klass.AddMemberReference(node, unit, name);
                return res.GetDescriptor(node, this, _klass, unit);
            }
            return res;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, INamespaceSet value) {
            var res = base.GetMember(node, unit, name);
            if (res.Count > 0) {
                _klass.AddMemberReference(node, unit, name);
            }
        }

        public override INamespaceSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, INamespaceSet rhs) {
            return ConstantInfo.NumericOp(node, this, unit, operation, rhs) ?? base.BinaryOperation(node, unit, operation, rhs) ?? NamespaceSet.Empty;
        }

        public override bool IsOfType(BuiltinClassInfo klass) {
            return this.ClassInfo == klass;
        }

        public override BuiltinTypeId TypeId {
            get {
                return ClassInfo.PythonType.TypeId;
            }
        }

        private const int MERGE_TO_OBJECT_STRENGTH = 3;

        public override bool UnionEquals(Namespace ns, int strength) {
            if (strength >= MERGE_TO_OBJECT_STRENGTH) {
                if (IsOfType(ProjectState.ClassInfos[BuiltinTypeId.Object]) && (ns is InstanceInfo || ns.IsOfType(ProjectState.ClassInfos[BuiltinTypeId.Object]))) {
                    return true;
                } else if (IsOfType(ProjectState.ClassInfos[BuiltinTypeId.Type]) && ns is ClassInfo) {
                    return true;
                } else if (IsOfType(ProjectState.ClassInfos[BuiltinTypeId.Function]) && ns is FunctionInfo) {
                    return true;
                }
            }
            var bi = ns as BuiltinInstanceInfo;
            return bi != null && ClassInfo.UnionEquals(bi.ClassInfo, strength);
        }

        public override int UnionHashCode(int strength) {
            return ClassInfo.UnionHashCode(strength);
        }

        internal override Namespace UnionMergeTypes(Namespace ns, int strength) {
            return ClassInfo.Instance;
        }

        #region IReferenceableContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            return _klass.GetDefinitions(name);
        }

        #endregion
    }
}
