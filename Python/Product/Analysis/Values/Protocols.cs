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
using System.Linq;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    abstract class Protocol : AnalysisValue, IHasRichDescription {
        private Dictionary<string, IAnalysisSet> _members;

        public Protocol(ProtocolInfo self) {
            Self = self;
        }

        public ProtocolInfo Self { get; private set; }

        public virtual Protocol Clone(ProtocolInfo newSelf) {
            var p = ((Protocol)MemberwiseClone());
            p._members = null;
            p.Self = Self;
            return p;
        }

        protected void EnsureMembers() {
            if (_members == null) {
                var m = new Dictionary<string, IAnalysisSet>();
                EnsureMembers(m);
                _members = m;
            }
        }

        protected virtual void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
        }

        protected IAnalysisSet MakeMethod(string qualname, IAnalysisSet returnValue) {
            return MakeMethod(qualname, Array.Empty<IAnalysisSet>(), returnValue);
        }

        protected IAnalysisSet MakeMethod(string qualname, IReadOnlyList<IAnalysisSet> arguments, IAnalysisSet returnValue) {
            var v = new ProtocolInfo(Self.DeclaringModule, Self.State);
            v.AddProtocol(new CallableProtocol(v, qualname, arguments, returnValue, PythonMemberType.Method));
            return v;
        }

        public override PythonMemberType MemberType => PythonMemberType.Unknown;

        // Do not return any default values from protocols. We call these directly and handle null.
        public override IAnalysisSet Await(Node node, AnalysisUnit unit) => null;
        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) => null;
        public override IAnalysisSet GetAsyncEnumeratorTypes(Node node, AnalysisUnit unit) => null;
        public override IAnalysisSet GetAsyncIterator(Node node, AnalysisUnit unit) => null;
        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) => null;
        public override IAnalysisSet GetDescriptor(PythonAnalyzer projectState, AnalysisValue instance, AnalysisValue context) => null;
        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) => null;
        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) => null;
        public override IAnalysisSet GetInstanceType() => null;
        public override IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems() => null;
        public override IAnalysisSet GetIterator(Node node, AnalysisUnit unit) => null;
        public override IAnalysisSet GetReturnForYieldFrom(Node node, AnalysisUnit unit) => null;
        public override IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) => null;

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            EnsureMembers();
            return _members;
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            var res = base.GetMember(node, unit, name);
            EnsureMembers();
            if (_members.TryGetValue(name, out var m)) {
                return (m as Protocol)?.GetMember(node, unit, name) ?? m;
            }
            return res;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            base.SetMember(node, unit, name, value);
            EnsureMembers();
            if (_members.TryGetValue(name, out var m)) {
                (m as Protocol)?.SetMember(node, unit, name, value);
            }
        }

        public virtual IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, Name);
        }

        protected abstract bool Equals(Protocol other);

        public override bool Equals(object obj) {
            if (obj is Protocol other && GetType() == other.GetType()) {
                return Equals(other);
            }
            return false;
        }

        public override int GetHashCode() => GetType().GetHashCode();
    }

    class NameProtocol : Protocol {
        private readonly string _name, _doc;
        private readonly BuiltinTypeId _typeId;
        private List<KeyValuePair<string, string>> _richDescription;

        public NameProtocol(ProtocolInfo self, string name, string documentation = null, BuiltinTypeId typeId = BuiltinTypeId.Object) : base(self) {
            _name = name;
            _doc = documentation;
            _typeId = typeId;
            _richDescription = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, _name) };
        }

        public NameProtocol(ProtocolInfo self, IPythonType type) : base(self) {
            _name = type.Name;
            _doc = type.Documentation;
            _typeId = type.TypeId;
            _richDescription = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, _name) };
        }

        public void ExtendDescription(KeyValuePair<string, string> part) {
            _richDescription.Add(part);
        }

        public void ExtendDescription(IEnumerable<KeyValuePair<string, string>> parts) {
            _richDescription.AddRange(parts);
        }

        public override string Name => _name;
        public override string Documentation => _doc;
        internal override BuiltinTypeId TypeId => _typeId;
        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() => _richDescription;

        protected override bool Equals(Protocol other) => Name == other.Name;
        public override int GetHashCode() => new { Type = GetType(), Name }.GetHashCode();
    }

    class CallableProtocol : Protocol {
        private readonly Lazy<OverloadResult[]> _overloads;

        public CallableProtocol(ProtocolInfo self, string qualname, IReadOnlyList<IAnalysisSet> arguments, IAnalysisSet returnType, PythonMemberType memberType = PythonMemberType.Function)
            : base(self) {
            Name = qualname ?? "callable";
            Arguments = arguments;
            ReturnType = returnType;
            _overloads = new Lazy<OverloadResult[]>(GenerateOverloads);
            MemberType = memberType;
        }

        public override string Name { get; }

        internal override BuiltinTypeId TypeId => BuiltinTypeId.Function;
        public override PythonMemberType MemberType { get; }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            members["__call__"] = Self;
        }

        private OverloadResult[] GenerateOverloads() {
            return new[] {
                new OverloadResult(Arguments.Select(ToParameterResult).ToArray(), Name, null, ReturnType.GetShortDescriptions())
            };
        }

        private ParameterResult ToParameterResult(IAnalysisSet set, int i) {
            return new ParameterResult("${0}".FormatInvariant(i + 1), "Parameter {0}".FormatUI(i + 1), string.Join(", ", set.GetShortDescriptions()));
        }

        public IReadOnlyList<IAnalysisSet> Arguments { get; }
        public IAnalysisSet ReturnType { get; }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var def = base.Call(node, unit, args, keywordArgNames);
            return ReturnType ?? def;
        }

        public override IEnumerable<OverloadResult> Overloads => _overloads.Value;

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "(");
            int argNumber = 1;
            foreach (var a in Arguments) {
                if (argNumber > 1) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Parameter, $"${argNumber}");

                foreach (var kv in a.GetRichDescriptions(" : ")) {
                    yield return kv;
                }
            }
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, ")");

            foreach (var kv in ReturnType.GetRichDescriptions(" -> ")) {
                yield return kv;
            }
        }

        protected override bool Equals(Protocol other) =>
            Name == other.Name &&
            other is CallableProtocol cp &&
            Arguments.Zip(cp.Arguments, (x, y) => x.SetEquals(y)).All(b => b);
        public override int GetHashCode() => Name.GetHashCode();
    }

    class IterableProtocol : Protocol {
        protected readonly IAnalysisSet _iterator;
        protected readonly IAnalysisSet _yielded;

        public IterableProtocol(ProtocolInfo self, IAnalysisSet yielded) : base(self) {
            _yielded = yielded;

            var iterator = new ProtocolInfo(Self.DeclaringModule, Self.State);
            iterator.AddProtocol(new IteratorProtocol(iterator, _yielded));
            _iterator = iterator;
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            members["__iter__"] = MakeMethod("__iter__", _iterator);
        }

        public override IAnalysisSet GetIterator(Node node, AnalysisUnit unit) => _iterator;
        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) => _yielded;

        public override string Name => "iterable";

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_yielded.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                foreach (var kv in _yielded.GetRichDescriptions()) {
                    yield return kv;
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }

        protected override bool Equals(Protocol other) =>
            other is IterableProtocol ip &&
            ObjectComparer.Instance.Equals(_yielded, ip._yielded);

        public override int GetHashCode() => new {
            Type = GetType(),
            x = ObjectComparer.Instance.GetHashCode(_yielded)
        }.GetHashCode();
    }

    class IteratorProtocol : Protocol {
        protected readonly IAnalysisSet _yielded;

        public IteratorProtocol(ProtocolInfo self, IAnalysisSet yielded) : base(self) {
            _yielded = yielded;
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            if (Self.DeclaringModule?.Tree?.LanguageVersion.Is3x() ?? true) {
                members["__next__"] = MakeMethod("__next__", _yielded);
            } else {
                members["next"] = MakeMethod("next", _yielded);
            }
            members["__iter__"] = MakeMethod("__iter__", Self);
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            return _yielded;
        }

        public override string Name => "iterator";

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_yielded.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                foreach (var kv in _yielded.GetRichDescriptions()) {
                    yield return kv;
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }

        protected override bool Equals(Protocol other) =>
            other is IteratorProtocol ip &&
            ObjectComparer.Instance.Equals(_yielded, ip._yielded);

        public override int GetHashCode() => new {
            Type = GetType(),
            x = ObjectComparer.Instance.GetHashCode(_yielded)
        }.GetHashCode();
    }

    class GetItemProtocol : Protocol {
        private readonly IAnalysisSet _keyType, _valueType;

        public GetItemProtocol(ProtocolInfo self, IAnalysisSet keys, IAnalysisSet values) : base(self) {
            _keyType = keys ?? self.AnalysisUnit.State.ClassInfos[BuiltinTypeId.Int].Instance;
            _valueType = values;
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            members["__getitem__"] = MakeMethod("__getitem__", _valueType);
        }

        public override IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems() {
            yield return new KeyValuePair<IAnalysisSet, IAnalysisSet>(_keyType, _valueType);
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            if (index.IsObjectOrUnknown() || index.Intersect(_keyType, UnionComparer.Instances[1]).Any()) {
                return _valueType;
            }
            return base.GetIndex(node, unit, index);
        }

        public override string Name => "container";

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_valueType.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                if (_keyType.Any(k => k.TypeId != BuiltinTypeId.Int)) {
                    foreach (var kv in _keyType.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                        yield return kv;
                    }
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                }
                foreach (var kv in _valueType.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                    yield return kv;
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }

        protected override bool Equals(Protocol other) =>
            other is GetItemProtocol gip &&
            ObjectComparer.Instance.Equals(_keyType, gip._keyType) &&
            ObjectComparer.Instance.Equals(_valueType, gip._valueType);
        public override int GetHashCode() => new {
            x = ObjectComparer.Instance.GetHashCode(_keyType),
            y = ObjectComparer.Instance.GetHashCode(_valueType)
        }.GetHashCode();
    }

    class TupleProtocol : IterableProtocol {
        private readonly IAnalysisSet[] _values;

        public TupleProtocol(ProtocolInfo self, IEnumerable<IAnalysisSet> values) : base(self, AnalysisSet.UnionAll(values)) {
            _values = values.ToArray();
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            var intType = Self.State.ClassInfos[BuiltinTypeId.Int].GetInstanceType();
            members["__getitem__"] = MakeMethod("__getitem__", new[] { intType }, _yielded);
        }

        private IAnalysisSet GetItem(int index) {
            if (index < 0) {
                index += _values.Length;
            }
            if (index >= 0 && index < _values.Length) {
                return _values[index];
            }
            return AnalysisSet.Empty;
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            var constants = index.OfType<ConstantInfo>().Select(ci => ci.Value).OfType<int>().ToArray();
            if (constants.Length == 0) {
                return AnalysisSet.UnionAll(_values);
            }

            return AnalysisSet.UnionAll(constants.Select(GetItem));
        }

        public override string Name => "tuple";

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_values.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                bool needComma = false;
                foreach (var v in _values) {
                    if (needComma) {
                        yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    }
                    needComma = true;
                    foreach (var kv in v.GetRichDescriptions()) {
                        yield return kv;
                    }
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }

        protected override bool Equals(Protocol other) => 
            other is TupleProtocol tp &&
            _values.Zip(tp._values, (x, y) => ObjectComparer.Instance.Equals(x, y)).All(b => b);
        public override int GetHashCode() => _values.Aggregate(GetType().GetHashCode(), (h, s) => h + 37 * ObjectComparer.Instance.GetHashCode(s));
    }

    class MappingProtocol : IterableProtocol {
        private readonly IAnalysisSet _keyType, _valueType, _itemType;

        public MappingProtocol(ProtocolInfo self, IAnalysisSet keys, IAnalysisSet values, IAnalysisSet items) : base(self, keys) {
            _keyType = keys;
            _valueType = values;
            _itemType = items;
        }

        private IAnalysisSet MakeIterable(IAnalysisSet values) {
            var pi = new ProtocolInfo(DeclaringModule, Self.State);
            pi.AddProtocol(new IterableProtocol(pi, values));
            return pi;
        }

        private IAnalysisSet MakeView(IPythonType type, IAnalysisSet values) {
            var pi = new ProtocolInfo(DeclaringModule, Self.State);
            var np = new NameProtocol(pi, type);
            var ip = new IterableProtocol(pi, values);
            np.ExtendDescription(ip.GetRichDescription());
            pi.AddProtocol(np);
            pi.AddProtocol(ip);
            return pi;
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            var state = Self.State;
            var itemsIter = MakeIterable(_itemType);

            if (state.LanguageVersion.Is3x()) {
                members["keys"] = MakeMethod("keys", MakeView(state.Types[BuiltinTypeId.DictKeys], _keyType));
                members["values"] = MakeMethod("values", MakeView(state.Types[BuiltinTypeId.DictValues], _valueType));
                members["items"] = MakeMethod("items", MakeView(state.Types[BuiltinTypeId.DictItems], _itemType));
            } else {
                members["viewkeys"] = MakeMethod("viewkeys", MakeView(state.Types[BuiltinTypeId.DictKeys], _keyType));
                members["viewvalues"] = MakeMethod("viewvalues", MakeView(state.Types[BuiltinTypeId.DictValues], _valueType));
                members["viewitems"] = MakeMethod("viewitems", MakeView(state.Types[BuiltinTypeId.DictItems], _itemType));
                var keysIter = MakeIterable(_keyType);
                members["keys"] = MakeMethod("keys", keysIter);
                members["iterkeys"] = MakeMethod("iterkeys", keysIter);
                var valuesIter = MakeIterable(_valueType);
                members["values"] = MakeMethod("values", valuesIter);
                members["itervalues"] = MakeMethod("itervalues", valuesIter);
                members["items"] = MakeMethod("items", itemsIter);
                members["iteritems"] = MakeMethod("iteritems", itemsIter);
            }

            members["clear"] = MakeMethod("clear", AnalysisSet.Empty);
            members["get"] = MakeMethod("get", new[] { _keyType }, _valueType);
            members["pop"] = MakeMethod("pop", new[] { _keyType }, _valueType);
            members["popitem"] = MakeMethod("popitem", new[] { _keyType }, _itemType);
            members["setdefault"] = MakeMethod("setdefault", new[] { _keyType, _valueType }, _valueType);
            members["update"] = MakeMethod("update", new[] { AnalysisSet.UnionAll(new IAnalysisSet[] { this, itemsIter }) }, AnalysisSet.Empty);
        }

        public override IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems() {
            yield return new KeyValuePair<IAnalysisSet, IAnalysisSet>(_keyType, _valueType);
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            return _valueType;
        }

        public override string Name => "dict";

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_valueType.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                if (_keyType.Any(k => k.TypeId != BuiltinTypeId.Int)) {
                    foreach (var kv in _keyType.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                        yield return kv;
                    }
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                }
                foreach (var kv in _valueType.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                    yield return kv;
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }

        protected override bool Equals(Protocol other) =>
            other is MappingProtocol mp &&
            ObjectComparer.Instance.Equals(_keyType, mp._keyType) &&
            ObjectComparer.Instance.Equals(_valueType, mp._valueType);
        public override int GetHashCode() => new {
            Type = GetType(),
            x = ObjectComparer.Instance.GetHashCode(_keyType),
            y = ObjectComparer.Instance.GetHashCode(_valueType)
        }.GetHashCode();
    }

    class GeneratorProtocol : IteratorProtocol {
        public GeneratorProtocol(ProtocolInfo self, IAnalysisSet yields, IAnalysisSet sends, IAnalysisSet returns) : base(self, yields) {
            Sent = sends;
            Returned = returns;
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            base.EnsureMembers(members);

            members["send"] = MakeMethod("send", new[] { Sent }, _yielded);
            members["throw"] = MakeMethod("throw", new[] { AnalysisSet.Empty }, AnalysisSet.Empty);
        }

        public override string Name => "generator";

        public IAnalysisSet Yielded => _yielded;
        public IAnalysisSet Sent { get; }
        public IAnalysisSet Returned { get; }

        public override IAnalysisSet GetReturnForYieldFrom(Node node, AnalysisUnit unit) {
            return Returned;
        }

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_yielded.Any() || Sent.Any() || Returned.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                if (_yielded.Any()) {
                    foreach (var kv in _yielded.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                        yield return kv;
                    }
                } else {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[]");
                }

                if (Sent.Any()) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    foreach (var kv in Sent.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                        yield return kv;
                    }
                } else if (Returned.Any()) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[]");
                }

                if (Returned.Any()) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    foreach (var kv in Sent.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                        yield return kv;
                    }
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }
    }

    class NamespaceProtocol : Protocol {
        private readonly string _name;
        private readonly VariableDef _values;

        public NamespaceProtocol(ProtocolInfo self, string name) : base(self) {
            _name = name;
            _values = new VariableDef();
        }

        public override Protocol Clone(ProtocolInfo newSelf) {
            var np = new NamespaceProtocol(newSelf, _name);
            _values.CopyTo(np._values);
            return np;
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            members[_name] = this;
        }

        public override string Name => _name;

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            if (name == _name) {
                _values.AddDependency(unit);
                return _values.Types;
            }
            return AnalysisSet.Empty;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            _values.AddTypes(unit, value);
        }

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, Name);
            foreach (var kv in _values.Types.GetRichDescriptions(prefix: " : ", unionPrefix: "{", unionSuffix: "}")) {
                yield return kv;
            }
        }

        protected override bool Equals(Protocol other) => Name == other.Name;
        public override int GetHashCode() => new { Type = GetType(), Name }.GetHashCode();
    }
}
