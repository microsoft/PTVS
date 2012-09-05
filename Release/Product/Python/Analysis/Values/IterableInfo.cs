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
using System.Text;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Specialized built-in instance for sequences (lists, tuples)
    /// </summary>
    internal class IterableInfo : BuiltinInstanceInfo {
        private ISet<Namespace> _unionType;        // all types that have been seen
        private VariableDef[] _indexTypes;     // types for known indices
        private IterBoundBuiltinMethodInfo _iterMethod;

        public IterableInfo(VariableDef[] indexTypes, BuiltinClassInfo seqType)
            : base(seqType) {
            _indexTypes = indexTypes;
        }

        public VariableDef[] IndexTypes {
            get { return _indexTypes; }
            set { _indexTypes = value; }
        }

        public ISet<Namespace> UnionType {
            get {
                EnsureUnionType();
                return _unionType; 
            }
            set { _unionType = value; }
        }

        public override ISet<Namespace> GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            if (_indexTypes.Length == 0) {
                _indexTypes = new[] { new VariableDef() };
                _indexTypes[0].AddDependency(unit);
                return EmptySet<Namespace>.Instance;
            } else {
                _indexTypes[0].AddDependency(unit);
            }

            EnsureUnionType();
            return _unionType;
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            if (name == "__iter__") {
                if (_iterMethod == null) {
                    var iterImpl = (base.GetMember(node, unit, name).FirstOrDefault() as BuiltinMethodInfo) ?? new IterBuiltinMethodInfo(PythonType, ProjectState);
                    _iterMethod = new IterBoundBuiltinMethodInfo(this, iterImpl);
                }
                return _iterMethod;
            }
            
            return base.GetMember(node, unit, name);
        }

        internal bool AddTypes(AnalysisUnit unit, ISet<Namespace>[] types) {
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
                added = _indexTypes[i].AddTypes(unit, types[i]) || added;
            }
            
            if (added) {
                _unionType = null;
            }

            return added;
        }

        public override string Description {
            get {
                EnsureUnionType();
                StringBuilder result = new StringBuilder("iterable");
                var unionType = _unionType.GetUnionType();
                if (unionType != null) {
                    result.Append(" of " + unionType.ShortDescription);
                } else {
                    result.Append("()");
                }

                return result.ToString();
            }
        }

        protected void EnsureUnionType() {
            if (_unionType == null) {
                ISet<Namespace> unionType = EmptySet<Namespace>.Instance;
                bool setMade = false;
                foreach (var set in _indexTypes) {
                    unionType = unionType.Union(set.Types, ref setMade);
                }
                _unionType = unionType;
            }
        }

        public override string ShortDescription {
            get {
                return _type.Name;
            }
        }

        public override bool UnionEquals(Namespace ns) {
            IterableInfo si = ns as IterableInfo;
            if (si == null) {
                return false;
            }

            return si._indexTypes.Length == _indexTypes.Length;
        }

        public override int UnionHashCode() {
            return _indexTypes.Length;
        }
    }
}
