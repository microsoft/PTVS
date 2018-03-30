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
using System.Threading;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class LazyValueInfo : AnalysisValue {
        private readonly Node _node;
        private readonly LazyValueInfo _left, _right;
        private readonly IAnalysisSet _value;
        private readonly string _memberName;
        private readonly PythonOperator? _op;
        private readonly LazyOperation _lazyOp;
        private readonly IAnalysisSet[] _args;
        private readonly NameExpression[] _argNames;

        private enum LazyOperation {
            // We can infer many operations from available arguments
            Automatic = 0,
            GetIndex,
            GetIterator,
            GetEnumeratorTypes,
            Await,
            GetYieldFromReturn,
            GetInstance
        }

        protected LazyValueInfo(Node node) {
            _node = node;
        }

        public LazyValueInfo(Node node, IAnalysisSet value) {
            _node = node;
            _value = value;
        }

        private LazyValueInfo(Node node, LazyValueInfo target, string memberName) {
            _node = node;
            _left = target;
            _memberName = memberName;
        }

        private LazyValueInfo(Node node, PythonOperator op, LazyValueInfo right) {
            _node = node;
            _op = op;
            _right = right;
        }

        private LazyValueInfo(Node node, LazyValueInfo left, PythonOperator op, LazyValueInfo right) {
            _node = node;
            _left = left;
            _op = op;
            _right = right;
        }

        private LazyValueInfo(Node node, LazyValueInfo left, LazyValueInfo right, LazyOperation lazyOp) {
            _node = node;
            _left = left;
            _right = right;
            _lazyOp = lazyOp;
        }

        private LazyValueInfo(Node node, LazyValueInfo target, IAnalysisSet[] args, NameExpression[] argNames) {
            _node = node;
            _left = target;
            _args = args;
            _argNames = argNames;
        }

        public Node Node => _node;

        internal static IAnalysisSet GetInstance(Node node, LazyValueInfo value) {
            return new LazyValueInfo(node, value, null, LazyOperation.GetInstance);
        }

        internal override IAnalysisSet Resolve(AnalysisUnit unit, ResolutionContext context) {
            if (context.Cache.TryGetValue(this, out var res)) {
                return res;
            }

            if (!Push()) {
                return AnalysisSet.Empty;
            }

            try {
                if (!context.Push()) {
                    return AnalysisSet.Empty;
                }
                try {
                    res = ResolveOnce(unit, context, out var allowCache);
                    bool changed = !Equals(res);
                    while (changed && context.ResolveFully) {
                        res = res.Resolve(unit, context, out changed);
                    }
                    if (context.ResolveFully) {
                        res.Split<LazyValueInfo>(out _, out res);
                    }
                    if (allowCache) {
                        context.Cache[this] = res;
                    }
                    return res;
                } finally {
                    context.Pop();
                }
            } finally {
                Pop();
            }
        }

        private IAnalysisSet ResolveOnce(AnalysisUnit unit, ResolutionContext context, out bool cacheResult) {
            cacheResult = true;

            if (_value is ParameterInfo pi) {
                return pi.Resolve(unit, context);
            } else if (_value != null) {
                return _value;
            }

            var left = _left?.Resolve(unit, context);
            if (left != null && left.Equals(_left)) {
                // Failed to resolve the value, so we're not going to get any further here
                cacheResult = false;
                return this;
            }

            var right = new Lazy<IAnalysisSet>(() => _right.Resolve(unit, context), LazyThreadSafetyMode.None);

            switch (_lazyOp) {
                case LazyOperation.Automatic:
                    break;
                case LazyOperation.Await:
                    return left.Await(_node, unit);
                case LazyOperation.GetEnumeratorTypes:
                    return left.GetEnumeratorTypes(_node, unit);
                case LazyOperation.GetIndex:
                    // Only non-lazy indexes are supported
                    return left.GetIndex(_node, unit, _right?._value ?? AnalysisSet.Empty);
                case LazyOperation.GetIterator:
                    return left.GetIterator(_node, unit);
                case LazyOperation.GetYieldFromReturn:
                    return left.GetReturnForYieldFrom(_node, unit);
                case LazyOperation.GetInstance:
                    return left.GetInstanceType();
                default:
                    Debug.Fail($"Unhandled op {_lazyOp}");
                    return AnalysisSet.Empty;
            }

            if (_memberName != null) {
                return left.GetMember(_node, unit, _memberName);
            }

            if (_args != null) {
                return left.Call(_node, unit, ResolveArgs(_args, unit, context).ToArray(), _argNames);
            }

            if (_op.HasValue) {
                if (_left == null) {
                    return right.Value.UnaryOperation(_node, unit, _op.Value);
                }
                return left.BinaryOperation(_node, unit, _op.Value, right.Value);
            }

            return AnalysisSet.Empty;
        }

        private static IEnumerable<IAnalysisSet> ResolveArgs(IAnalysisSet[] args, AnalysisUnit unit, ResolutionContext context) {
            foreach (var a in args.MaybeEnumerate()) {
                yield return a.Resolve(unit, context, out _);
            }
        }

        // Lazy operations

        public override IAnalysisSet Await(Node node, AnalysisUnit unit) {
            return new LazyValueInfo(node, this, null, LazyOperation.Await);
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            // Eager call, but lazy result
            foreach (var ns in Resolve(unit, new ResolutionContext { ResolveDepth = 2, ResolveFully = true })) {
                Debug.Assert(!ReferenceEquals(this, ns));
                ns.GetMember(node, unit, name);
            }
            return new LazyValueInfo(node, this, name);
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            // Eager call, but lazy result
            foreach (var ns in Resolve(unit, new ResolutionContext { ResolveDepth = 2, ResolveFully = true })) {
                Debug.Assert(!ReferenceEquals(this, ns));
                ns.Call(node, unit, args, keywordArgNames);
            }
            return new LazyValueInfo(node, this, args, keywordArgNames);
        }

        private static IAnalysisSet QuickOp(IAnalysisSet lhs, PythonOperator op, IAnalysisSet rhs, PythonAnalyzer state) {
            // Concrete return for known (or conventional) operations
            switch (op) {
                case PythonOperator.Equal:
                case PythonOperator.In:
                case PythonOperator.Is:
                case PythonOperator.IsNot:
                case PythonOperator.Not:
                case PythonOperator.NotEqual:
                case PythonOperator.NotIn:
                    return state.ClassInfos[BuiltinTypeId.Bool].Instance;
                case PythonOperator.GreaterThan:
                case PythonOperator.GreaterThanOrEqual:
                case PythonOperator.LessThan:
                case PythonOperator.LessThanOrEqual:
                    return lhs.Union(rhs);
                case PythonOperator.Negate:
                case PythonOperator.Pos:
                    return rhs;
            }
            return null;
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            return QuickOp(SelfSet, operation, rhs, unit.State) ??
                new LazyValueInfo(node, this, operation, new LazyValueInfo(node, rhs));
        }

        public override IAnalysisSet ReverseBinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            return QuickOp(rhs, operation, SelfSet, unit.State) ??
                new LazyValueInfo(node, new LazyValueInfo(node, rhs), operation, this);
        }

        public override IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            return QuickOp(AnalysisSet.Empty, operation, SelfSet, unit.State) ??
                new LazyValueInfo(node, operation, this);
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            LazyValueInfo lazyIndex = null;
            // We only use the index if it is not lazy
            if (!index.OfType<LazyValueInfo>().Any()) {
                lazyIndex = new LazyValueInfo(node, index);
            }
            return new LazyValueInfo(node, this, lazyIndex, LazyOperation.GetIndex);
        }

        public override IAnalysisSet GetIterator(Node node, AnalysisUnit unit) {
            return new LazyValueInfo(node, this, null, LazyOperation.GetIterator);
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            return new LazyValueInfo(node, this, null, LazyOperation.GetEnumeratorTypes);
        }

        public override IAnalysisSet GetReturnForYieldFrom(Node node, AnalysisUnit unit) {
            return new LazyValueInfo(node, this, null, LazyOperation.GetYieldFromReturn);
        }

        // Eager operations

        internal override void AddReference(Node node, AnalysisUnit analysisUnit) {
            foreach (var ns in Resolve(analysisUnit, new ResolutionContext { ResolveFully = true, ResolveDepth = 3 })) {
                ns.AddReference(node, analysisUnit);
            }
        }

        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, IAnalysisSet value) {
            foreach (var ns in Resolve(unit)) {
                ns.AugmentAssign(node, unit, value);
            }
        }

        public override void DeleteMember(Node node, AnalysisUnit unit, string name) {
            foreach (var ns in Resolve(unit)) {
                ns.DeleteMember(node, unit, name);
            }
        }

        public override void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
            foreach (var ns in Resolve(unit)) {
                ns.SetIndex(node, unit, index, value);
            }
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            foreach (var ns in Resolve(unit)) {
                ns.SetMember(node, unit, name, value);
            }
        }

        // Invalid operations

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            Debug.Fail("Invalid operation on unresolved LazyValueInfo");
            return base.GetAllMembers(moduleContext, options);
        }

        // Equality

        public override bool Equals(object obj) {
            if (ReferenceEquals(this, obj)) {
                return true;
            }
            if (!(obj is LazyValueInfo other)) {
                return false;
            }
            if (!_node.Equals(other._node)) {
                return false;
            }
            return _lazyOp == other._lazyOp &&
                (_left == null) == (other._left == null) &&
                (_right == null) == (other._right == null) &&
                (_value == null) == (other._value == null);
        }

        public override int GetHashCode() {
            return GetType().GetHashCode() ^ _node.GetHashCode();
        }

        public override string ToString() {
            switch (_lazyOp) {
                case LazyOperation.Automatic:
                    break;
                case LazyOperation.GetIndex:
                    return "{0}[{1}]".FormatInvariant(_left, _right);
                case LazyOperation.GetIterator:
                    return "iter({0})".FormatInvariant(_left);
                case LazyOperation.GetEnumeratorTypes:
                    return "next({0})".FormatInvariant(_left);
                case LazyOperation.Await:
                    return "(await {0})".FormatInvariant(_left);
                case LazyOperation.GetYieldFromReturn:
                    return "(yield from {0})".FormatInvariant(_left);
                case LazyOperation.GetInstance:
                    return "new({0})".FormatInvariant(_left);
                default:
                    break;
            }

            if (_memberName != null) {
                return "{0}.{1}".FormatInvariant(_left, _memberName);
            }

            if (_args != null) {
                return "{0}({1})".FormatInvariant(_left, string.Join(", ", (object[])_args));
            }

            if (_op.HasValue) {
                if (_left == null) {
                    return "{0}{1}".FormatInvariant(_op.Value.ToCodeString(), _right);
                }
                return "{0}{1}{2}".FormatInvariant(_left, _op.Value.ToCodeString(), _right);
            }

            if (_value != null) {
                return "<{0}>".FormatInvariant(_value);
            }

            return "<{0}>".FormatInvariant(GetType().Name);
        }
    }

    class LazyIndexableInfo : LazyValueInfo {
        private readonly IAnalysisSet _indexTypes;
        private readonly Func<IAnalysisSet> _fallback;

        public LazyIndexableInfo(Node node, IAnalysisSet indexTypes, Func<IAnalysisSet> fallback) : base(node) {
            _indexTypes = indexTypes.AsUnion(1);
            _fallback = fallback;
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            return _indexTypes;
        }

        internal override IAnalysisSet Resolve(AnalysisUnit unit, ResolutionContext context) {
            if (_fallback().Split(out IReadOnlyList<LazyValueInfo> lvis, out IAnalysisSet rest)) {
                return rest.UnionAll(lvis.Select(lvi => lvi.Resolve(unit, context)));
            }
            return rest;
        }

        public override bool Equals(object obj) {
            if (obj is LazyIndexableInfo other) {
                return _indexTypes.SetEquals(other._indexTypes);
            }
            return false;
        }

        public override int GetHashCode() {
            return typeof(LazyIndexableInfo).GetHashCode() ^ _indexTypes.GetHashCode();
        }

        public override string ToString() {
            return "[{0}]".FormatInvariant(_indexTypes);
        }

        internal override bool UnionEquals(AnalysisValue av, int strength) {
            return av is LazyIndexableInfo;
        }

        internal override int UnionHashCode(int strength) {
            return GetType().GetHashCode();
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue av, int strength) {
            var combined = _indexTypes.Union(((av as LazyIndexableInfo)?._indexTypes).MaybeEnumerate().Where(v => !ReferenceEquals(v, this)));
            if (!combined.SetEquals(_indexTypes)) {
                return new LazyIndexableInfo(Node, combined, _fallback);
            }
            return this;
        }
    }

    static class LazyValueInfoExtensions {
        public static bool IsResolvable(this IAnalysisSet set) {
            if (set is AnalysisValue av) {
                return av.IsResolvable();
            }
            return set.Any(IsResolvable);
        }

        public static bool IsResolvable(this AnalysisValue av) {
            if (av is LazyValueInfo) {
                return true;
            }
            if (av is GeneratorInfo gi && gi.Push()) {
                try {
                    return gi.Yields.TypesNoCopy.IsResolvable() ||
                        gi.Returns.TypesNoCopy.IsResolvable() ||
                        gi.Sends.TypesNoCopy.IsResolvable();
                } finally {
                    gi.Pop();
                }
            }
            if (av is IterableValue iv && iv.Push()) {
                try {
                    return iv.IndexTypes.Any(v => v.TypesNoCopy.IsResolvable());
                } finally {
                    iv.Pop();
                }
            }
            return false;
        }
    }
}
