// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

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
