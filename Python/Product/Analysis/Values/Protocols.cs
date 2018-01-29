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
            var v = new ProtocolInfo(Self.DeclaringModule as ProjectEntry);
            v.AddProtocol(new CallableProtocol(v, qualname, arguments, returnValue, PythonMemberType.Method));
            return v;
        }

        public override PythonMemberType MemberType => PythonMemberType.Unknown;

        public override IAnalysisSet GetInstanceType() => null;

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
    }

    class NameProtocol : Protocol {
        private readonly string _name;

        public NameProtocol(ProtocolInfo self, string name) : base(self) {
            _name = name;
        }

        public override string Name => _name;
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
                new OverloadResult(Arguments.Select(ToParameterResult).ToArray(), Name)
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
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, Name);
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
    }

    class IterableProtocol : Protocol {
        private readonly IAnalysisSet _iterator;
        private readonly IAnalysisSet _yielded;

        public IterableProtocol(ProtocolInfo self, IAnalysisSet yielded) : base(self) {
            _yielded = yielded;

            var iterator = new ProtocolInfo(Self.DeclaringModule as ProjectEntry);
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
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, Name);
            if (_yielded.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                foreach (var kv in _yielded.GetRichDescriptions()) {
                    yield return kv;
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }
    }

    class IteratorProtocol : Protocol {
        private readonly IAnalysisSet _yielded;

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
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, Name);
            if (_yielded.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                foreach (var kv in _yielded.GetRichDescriptions()) {
                    yield return kv;
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
    }
}
