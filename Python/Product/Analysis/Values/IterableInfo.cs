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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Base class for iterables.  Not tied to whether the iterable is fixed to
    /// certain known types (e.g. an iterable for a string) or a user defined
    /// iterable.
    /// 
    /// Implementors just need to provide the UnionType and the ability to make
    /// an iterator for the iterable.
    /// </summary>
    internal abstract class BaseIterableValue : BuiltinInstanceInfo, IHasRichDescription {
        protected IAnalysisSet _unionType;        // all types that have been seen
        private AnalysisValue _iterMethod;

        public BaseIterableValue(BuiltinClassInfo seqType)
            : base(seqType) {
        }

        public IAnalysisSet UnionType {
            get {
                EnsureUnionType();
                return _unionType;
            }
            set { _unionType = value; }
        }

        protected abstract void EnsureUnionType();
        protected virtual string TypeName => _type?.Name ?? "iterable";
        protected abstract IAnalysisSet MakeIteratorInfo(Node n, AnalysisUnit unit);

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            EnsureUnionType();
            return _unionType;
        }

        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) {
            var res = base.GetTypeMember(node, unit, name);

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
                    NodeValueKind.Iterator,
                    n => MakeIteratorInfo(n, unit)
                );
            }
            return AnalysisSet.Empty;
        }

        public override string Description => string.Join("", GetRichDescription().Select(kv => kv.Value));
        public override string ShortDescription => string.Join("", GetRichDescription().TakeWhile(kv => kv.Key != WellKnownRichDescriptionKinds.EndOfDeclaration).Select(kv => kv.Value));

        public virtual IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, TypeName);
            var indexTypes = UnionType;
            if (indexTypes.IsObjectOrUnknownOrNone()) {
                yield break;
            }

            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
            if (indexTypes.Count < 6) {
                foreach (var kv in indexTypes.GetRichDescriptions()) {
                    yield return kv;
                }
            } else {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "...");
            }
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
        }
    }

    /// <summary>
    /// Specialized built-in instance for sequences (lists, tuples) which are iterable.
    /// 
    /// Used for user defined sequence types where we'll track the individual values
    /// inside of the iterable.
    /// </summary>
    internal class IterableValue : BaseIterableValue {
        internal readonly Node _node;

        public IterableValue(VariableDef[] indexTypes, BuiltinClassInfo seqType, Node node)
            : base(seqType) {
            IndexTypes = indexTypes;
            _node = node;
        }

        public VariableDef[] IndexTypes { get; protected set; }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            if (IndexTypes.Length == 0) {
                if (unit.ForEval) {
                    return AnalysisSet.Empty;
                }
                IndexTypes = new[] { new VariableDef() };
                IndexTypes[0].AddDependency(unit);
                return AnalysisSet.Empty;
            } else if (!unit.ForEval) {
                foreach (var v in IndexTypes) {
                    v.AddDependency(unit);
                }
            }

            return base.GetEnumeratorTypes(node, unit);
        }

        internal bool AddTypes(AnalysisUnit unit, IAnalysisSet[] types) {
            if (IndexTypes.Length < types.Length) {
                IndexTypes = IndexTypes.Concat(VariableDef.Generator).Take(types.Length).ToArray();
            }

            bool added = false;
            for (int i = 0; i < types.Length; i++) {
                added |= IndexTypes[i].MakeUnionStrongerIfMoreThan(ProjectState.Limits.IndexTypes, types[i]);
                added |= IndexTypes[i].AddTypes(unit, types[i], true, DeclaringModule);
            }

            if (added) {
                _unionType = null;
            }

            return added;
        }

        protected override IAnalysisSet MakeIteratorInfo(Node n, AnalysisUnit unit) {
            var iterType = BaseIteratorValue.GetIteratorTypeFromType(ClassInfo, unit);
            if (iterType == null) {
                return AnalysisSet.Empty;
            }
            return new IteratorValue(this, iterType);
        }

        protected override void EnsureUnionType() {
            if (_unionType.IsObjectOrUnknown()) {
                IAnalysisSet unionType = AnalysisSet.EmptyUnion;
                if (Push()) {
                    try {
                        foreach (var set in IndexTypes) {
                            unionType = unionType.Union(set.TypesNoCopy);
                        }
                    } finally {
                        Pop();
                    }
                }
                unionType.Split(this.Equals, out _, out unionType);
                _unionType = unionType;
            }
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            if (strength < MergeStrength.IgnoreIterableNode) {
                var si = ns as IterableValue;
                if (si != null && !_node.Equals(si)) {
                    // If nodes are not equal, iterables cannot be merged.
                    return false;
                }
            }

            return base.UnionEquals(ns, strength);
        }

        protected virtual bool ResolveIndexTypes(AnalysisUnit unit, ResolutionContext context, VariableDef[] newTypes) {
            if (newTypes == null) {
                throw new ArgumentNullException(nameof(newTypes));
            } else if (newTypes.Any(v => v == null)) {
                throw new ArgumentException("Each element of newTypes must be initialized");
            }

            if (newTypes.Length == 0 || IndexTypes.Length == 0) {
                return false;
            }

            var resolvedTypes = new IAnalysisSet[IndexTypes.Length];
            bool anyChange = false;

            if (Push()) {
                try {
                    for (int i = 0; i < resolvedTypes.Length; ++i) {
                        resolvedTypes[i] = IndexTypes[i].TypesNoCopy.Resolve(unit, context, out bool changed);
                        anyChange |= changed;
                    }
                } finally {
                    Pop();
                }
            }

            if (!anyChange) {
                return false;
            }

            anyChange = false;
            if (resolvedTypes.Length <= newTypes.Length) {
                for (int i = 0; i < resolvedTypes.Length; ++i) {
                    anyChange |= newTypes[i].AddTypes(DeclaringModule, resolvedTypes[i]);
                }
            } else {
                // Add all types to the first element
                for (int i = 0; i < resolvedTypes.Length; ++i) {
                    anyChange |= newTypes[0].AddTypes(DeclaringModule, resolvedTypes[i]);
                }
            }
            return anyChange;
        }

        protected virtual IAnalysisSet CreateWithNewTypes(Node node, VariableDef[] types) {
            return new IterableValue(types, ClassInfo, node);
        }

        internal override IAnalysisSet Resolve(AnalysisUnit unit, ResolutionContext context) {
            VariableDef[] newTypes;
            if (context.CallSite == null) {
                // No ability to come back to this instance later, so resolve and return
                // imitation type
                var union = AnalysisSet.Empty;
                bool changed = false;
                if (Push()) {
                    try {
                        union = UnionType.Resolve(unit, context, out changed);
                    } finally {
                        Pop();
                    }
                }

                var pi = new ProtocolInfo(DeclaringModule, ProjectState);
                pi.AddProtocol(new IterableProtocol(pi, union));
                if (ClassInfo.TypeId == BuiltinTypeId.Tuple) {
                    newTypes = VariableDef.Generator.Take(IndexTypes.Length).ToArray();
                    changed |= ResolveIndexTypes(unit, context, newTypes);
                    if (newTypes.Length == 1) {
                        pi.AddProtocol(new GetItemProtocol(pi, unit.State.ClassInfos[BuiltinTypeId.Int], newTypes[0].TypesNoCopy));
                    } else if (newTypes.Length > 1) {
                        pi.AddProtocol(new TupleProtocol(pi, newTypes.Select(t => t.TypesNoCopy)));
                    }
                }

                return changed ? (AnalysisValue)pi : this;
            }

            if (unit.Scope.TryGetNodeValue(context.CallSite, NodeValueKind.Sequence, out var newSeq)) {
                newTypes = (newSeq as IterableValue)?.IndexTypes;
                if (newTypes != null) {
                    ResolveIndexTypes(unit, context, newTypes);
                }
                return newSeq;
            } else {
                newTypes = VariableDef.Generator.Take(Math.Max(1, IndexTypes.Length)).ToArray();
                if (ResolveIndexTypes(unit, context, newTypes)) {
                    return unit.Scope.GetOrMakeNodeValue(context.CallSite, NodeValueKind.Sequence, n => CreateWithNewTypes(n, newTypes));
                }
            }

            return this;
        }

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, TypeName);
            var indexTypes = IndexTypes;
            if (indexTypes == null || indexTypes.Length == 0) {
                yield break;
            }

            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
            if (indexTypes.Length < 6) {
                bool first = true;
                foreach (var i in indexTypes) {
                    if (first) {
                        first = false;
                    } else {
                        yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    }
                    foreach (var kv in i.TypesNoCopy.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                        yield return kv;
                    }
                }
            } else {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "...");
            }
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
        }

    }
}
