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
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Specialized built-in instance for sequences (lists, tuples)
    /// </summary>
    internal class IterableInfo : BuiltinInstanceInfo {
        internal readonly Node _node;
        private IAnalysisSet _unionType;        // all types that have been seen
        private VariableDef[] _indexTypes;     // types for known indices
        private AnalysisValue _iterMethod;

        public IterableInfo(VariableDef[] indexTypes, BuiltinClassInfo seqType, Node node)
            : base(seqType) {
            _indexTypes = indexTypes;
            _node = node;
        }

        public VariableDef[] IndexTypes {
            get { return _indexTypes; }
            set { _indexTypes = value; }
        }

        public IAnalysisSet UnionType {
            get {
                EnsureUnionType();
                return _unionType;
            }
            set { _unionType = value; }
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            if (_indexTypes.Length == 0) {
                _indexTypes = new[] { new VariableDef() };
                _indexTypes[0].AddDependency(unit);
                return AnalysisSet.Empty;
            } else {
                _indexTypes[0].AddDependency(unit);
            }

            EnsureUnionType();
            return _unionType;
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            // Must unconditionally call the base implementation of GetMember
            var res = base.GetMember(node, unit, name);

            if (name == "__iter__") {
                return _iterMethod = _iterMethod ?? new SpecializedCallable(
                    res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                    IterableIter,
                    false
                );
            }

            return res;
        }

        private IAnalysisSet IterableIter(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 0) {
                return unit.Scope.GetOrMakeNodeValue(
                    node,
                    n => new IteratorInfo(
                        _indexTypes,
                        IteratorInfo.GetIteratorTypeFromType(ClassInfo, unit),
                        n
                    )
                );
            }
            return AnalysisSet.Empty;
        }

        internal bool AddTypes(AnalysisUnit unit, IAnalysisSet[] types) {
            if (_indexTypes.Length < types.Length) {
                _indexTypes = _indexTypes.Concat(VariableDef.Generator).Take(types.Length).ToArray();
            }

            bool added = false;
            for (int i = 0; i < types.Length; i++) {
                added |= _indexTypes[i].MakeUnionStrongerIfMoreThan(ProjectState.Limits.IndexTypes, types[i]);
                added |= _indexTypes[i].AddTypes(unit, types[i]);
            }

            if (added) {
                _unionType = null;
            }

            return added;
        }

        protected string MakeDescription(string typeName) {
            EnsureUnionType();
            return MakeDescription(this, typeName, UnionType);
        }

        internal static string MakeDescription(AnalysisValue type, string typeName, IAnalysisSet indexTypes) {
            if (type.Push()) {
                try {
                    if (indexTypes == null || indexTypes.Count == 0) {
                        return typeName;
                    } else if (indexTypes.Count == 1) {
                        return typeName + " of " + indexTypes.First().ShortDescription;
                    } else if (indexTypes.Count < 4) {
                        return typeName + " of {" + string.Join(", ", indexTypes.Select(ns => ns.ShortDescription)) + "}";
                    } else {
                        return typeName + " of multiple types";
                    }
                } finally {
                    type.Pop();
                }
            }
            return typeName;
        }

        public override string Description {
            get {
                return MakeDescription("iterable");
            }
        }

        protected void EnsureUnionType() {
            if (_unionType == null) {
                IAnalysisSet unionType = AnalysisSet.EmptyUnion;
                if (Push()) {
                    try {
                        foreach (var set in _indexTypes) {
                            unionType = unionType.Union(set.TypesNoCopy);
                        }
                    } finally {
                        Pop();
                    }
                }
                _unionType = unionType;
            }
        }

        public override string ShortDescription {
            get {
                return _type.Name;
            }
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            if (strength < MergeStrength.IgnoreIterableNode) {
                var si = ns as IterableInfo;
                if (si != null && !_node.Equals(_node)) {
                    // If nodes are not equal, iterables cannot be merged.
                    return false;
                }
            }

            return base.UnionEquals(ns, strength);
        }
    }
}
