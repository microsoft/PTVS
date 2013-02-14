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

using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Specialized built-in instance for sequences (lists, tuples)
    /// </summary>
    internal class IterableInfo : BuiltinInstanceInfo {
        internal readonly Node _node;
        private INamespaceSet _unionType;        // all types that have been seen
        private VariableDef[] _indexTypes;     // types for known indices
        private IterBoundBuiltinMethodInfo _iterMethod;

        public IterableInfo(VariableDef[] indexTypes, BuiltinClassInfo seqType, Node node)
            : base(seqType) {
            _indexTypes = indexTypes;
            _node = node;
        }

        public VariableDef[] IndexTypes {
            get { return _indexTypes; }
            set { _indexTypes = value; }
        }

        public INamespaceSet UnionType {
            get {
                EnsureUnionType();
                return _unionType;
            }
            set { _unionType = value; }
        }

        public override INamespaceSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            if (_indexTypes.Length == 0) {
                _indexTypes = new[] { new VariableDef() };
                _indexTypes[0].AddDependency(unit);
                return NamespaceSet.Empty;
            } else {
                _indexTypes[0].AddDependency(unit);
            }

            EnsureUnionType();
            return _unionType;
        }

        public override INamespaceSet GetMember(Node node, AnalysisUnit unit, string name) {
            if (name == "__iter__") {
                if (_iterMethod == null) {
                    var iterImpl = (base.GetMember(node, unit, name).FirstOrDefault() as BuiltinMethodInfo) ?? new IterBuiltinMethodInfo(PythonType, ProjectState);
                    _iterMethod = new IterBoundBuiltinMethodInfo(this, iterImpl);
                }
                return _iterMethod;
            }

            return base.GetMember(node, unit, name);
        }

        internal bool AddTypes(AnalysisUnit unit, INamespaceSet[] types) {
            if (_indexTypes.Length < types.Length) {
                VariableDef[] newTypes = new VariableDef[types.Length];
                for (int i = 0; i < _indexTypes.Length; i++) {
                    newTypes[i] = _indexTypes[i];
                }
                for (int i = _indexTypes.Length; i < types.Length; i++) {
                    newTypes[i] = new VariableDef();
                }
                _indexTypes = newTypes;
            }

            bool added = false;
            for (int i = 0; i < types.Length; i++) {
                added |= _indexTypes[i].AddTypes(unit, types[i]);
            }

            if (added) {
                _unionType = null;
            }

            return added;
        }

        protected string MakeDescription(string typeName) {
            EnsureUnionType();
            if (UnionType == null || UnionType.Count == 0) {
                return typeName;
            } else if (UnionType.Count == 1) {
                return typeName + " of " + UnionType.First().ShortDescription;
            } else if (UnionType.Count < 4) {
                return typeName + " of {" + string.Join(", ", UnionType.Select(ns => ns.ShortDescription)) + "}";
            } else {
                return typeName + " of multiple types";
            }
        }

        public override string Description {
            get {
                return MakeDescription("iterable");
            }
        }

        protected void EnsureUnionType() {
            if (_unionType == null) {
                INamespaceSet unionType = NamespaceSet.EmptyUnion;
                foreach (var set in _indexTypes) {
                    unionType = unionType.Union(set.TypesNoCopy);
                }
                _unionType = unionType;
            }
        }

        public override string ShortDescription {
            get {
                return _type.Name;
            }
        }

        private const int IGNORE_NODE_STRENGTH = 1;

        public override bool UnionEquals(Namespace ns, int strength) {
            var si = ns as IterableInfo;
            if (si != null && strength < IGNORE_NODE_STRENGTH) {
                return si.ClassInfo == ClassInfo && si._node.Equals(_node);
            }
            var bii = ns as BuiltinInstanceInfo;
            if (bii != null) {
                return bii.ClassInfo == ClassInfo;
            }
            return false;
        }

        public override int UnionHashCode(int strength) {
            return ClassInfo.GetHashCode();
        }

        internal override Namespace UnionMergeTypes(Namespace ns, int strength) {
            if (object.ReferenceEquals(this, ns)) {
                return this;
            }
            return ClassInfo.Instance;
        }
    }
}
