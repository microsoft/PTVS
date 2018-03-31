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
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents a value that implements one or more protocols.
    /// </summary>
    class ProtocolInfo : AnalysisValue, IHasRichDescription {
        private IAnalysisSet _protocols;
        private readonly ReferenceDict _references;

        private IAnalysisSet _instance;
        private Dictionary<string, IAnalysisSet> _members;
        private BuiltinTypeId? _typeId;
        private PythonMemberType? _memberType;

        public ProtocolInfo(IPythonProjectEntry declaringModule, PythonAnalyzer state) {
            _protocols = AnalysisSet.CreateUnion(1);
            DeclaringModule = declaringModule;
            DeclaringVersion = declaringModule?.AnalysisVersion ?? -1;
            _references = new ReferenceDict();
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        internal PythonAnalyzer State { get; }

        public void AddProtocol(Protocol p) {
            _protocols = _protocols.Add(p);
            _instance = null;
            _members = null;
            _typeId = null;
            _memberType = null;
        }

        public IEnumerable<T> GetProtocols<T>() {
            return _protocols.OfType<T>();
        }

        public bool RemoveProtocol<T>(Func<T, bool> pred) where T : Protocol {
            if (_protocols.Split(v => v is T p && pred(p), out _protocols, out _)) {
                _instance = null;
                _members = null;
                _typeId = null;
                _members = null;
                return true;
            }
            return false;
        }

        public override string Name => _protocols.OfType<NameProtocol>().FirstOrDefault()?.Name ?? string.Join(", ", _protocols.Select(p => p.Name).Ordered());
        public override string Documentation => _protocols.OfType<NameProtocol>().FirstOrDefault()?.Documentation ?? string.Join(", ", _protocols.OrderBy(p => p.Name).Select(p => p.Documentation).Where(d => !string.IsNullOrEmpty(d)));
        public override IEnumerable<OverloadResult> Overloads => _protocols.SelectMany(p => p.Overloads);
        public override IPythonProjectEntry DeclaringModule { get; }
        public override int DeclaringVersion { get; }

        public override IEnumerable<IAnalysisSet> Mro => base.Mro;

        public override AnalysisUnit AnalysisUnit => base.AnalysisUnit;

        internal override BuiltinTypeId TypeId {
            get {
                if (_typeId == null) {
                    _typeId = _protocols.OfType<NameProtocol>().FirstOrDefault()?.TypeId;
                    if (_typeId == null) {
                        foreach (var p in _protocols) {
                            if (p.TypeId == BuiltinTypeId.Unknown) {
                                continue;
                            }

                            if (_typeId == null) {
                                _typeId = p.TypeId;
                            } else if (_typeId != p.TypeId) {
                                _typeId = BuiltinTypeId.Unknown;
                                break;
                            }
                        }
                    }
                    if (_typeId == null) {
                        _typeId = BuiltinTypeId.Unknown;
                    }
                }
                return _typeId.GetValueOrDefault();
            }
        }

        public override PythonMemberType MemberType {
            get {
                if (_memberType == null) {
                    foreach (var p in _protocols) {
                        if (p.MemberType == PythonMemberType.Unknown) {
                            continue;
                        }

                        if (_memberType == null) {
                            _memberType = p.MemberType;
                        } else if (_memberType != p.MemberType) {
                            _memberType = PythonMemberType.Multiple;
                            break;
                        }
                    }
                    if (_memberType == null) {
                        _memberType = PythonMemberType.Unknown;
                    }
                }
                return _memberType.GetValueOrDefault();
            }
        }


        internal override void AddReference(Node node, AnalysisUnit analysisUnit) {
            _references.GetReferences(analysisUnit.ProjectEntry).AddReference(new EncodedLocation(analysisUnit, node));
        }

        public override IEnumerable<LocationInfo> Locations {
            get {
                ReferenceList defns;
                if (!_references.TryGetValue(DeclaringModule, out defns)) {
                    return Enumerable.Empty<LocationInfo>();
                }
                return defns.Definitions.Select(l => l.GetLocationInfo()).Where(l => l != null);
            }
        }

        internal override IEnumerable<LocationInfo> References => _references.AllReferences;

        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, IAnalysisSet value) {
            foreach (var p in _protocols) {
                p.AugmentAssign(node, unit, value);
            }
        }

        public override IAnalysisSet Await(Node node, AnalysisUnit unit) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.Await(node, unit)));
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.BinaryOperation(node, unit, operation, rhs)));
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.Call(node, unit, args, keywordArgNames)));
        }

        public override void DeleteMember(Node node, AnalysisUnit unit, string name) {
            foreach (var p in _protocols) {
                p.DeleteMember(node, unit, name);
            }
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            if (_members != null) {
                return _members;
            }

            var members = new Dictionary<string, IAnalysisSet>();
            foreach (var p in _protocols) {
                foreach (var kv in p.GetAllMembers(moduleContext, options).MaybeEnumerate()) {
                    if (members.TryGetValue(kv.Key, out var existing)) {
                        members[kv.Key] = existing.Union(kv.Value);
                    } else {
                        members[kv.Key] = kv.Value;
                    }
                }
            }
            _members = _members ?? members;
            return _members;
        }

        public override IAnalysisSet GetAsyncEnumeratorTypes(Node node, AnalysisUnit unit) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetAsyncEnumeratorTypes(node, unit)).WhereNotNull());
        }

        public override IAnalysisSet GetAsyncIterator(Node node, AnalysisUnit unit) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetAsyncIterator(node, unit)).WhereNotNull());
        }

        public override object GetConstantValue() {
            return _protocols.Select(p => p.GetConstantValue()).FirstOrDefault(p => p != null) ?? base.GetConstantValue();
        }

        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetDescriptor(node, instance, context, unit)).WhereNotNull());
        }

        public override IAnalysisSet GetDescriptor(PythonAnalyzer projectState, AnalysisValue instance, AnalysisValue context) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetDescriptor(projectState, instance, context)).WhereNotNull());
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetEnumeratorTypes(node, unit)).WhereNotNull());
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetIndex(node, unit, index)).WhereNotNull());
        }

        public override IAnalysisSet GetInstanceType() {
            _instance = _instance ?? AnalysisSet.UnionAll(_protocols.Select(p => p.GetInstanceType()).WhereNotNull().DefaultIfEmpty(this));
            return _instance;
        }

        public override IAnalysisSet GetIterator(Node node, AnalysisUnit unit) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetIterator(node, unit)).WhereNotNull());
        }

        public override int? GetLength() {
            return _protocols.Select(p => p.GetLength()).FirstOrDefault(v => v.HasValue) ?? base.GetLength();
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetMember(node, unit, name)).WhereNotNull());
        }

        public override IAnalysisSet GetReturnForYieldFrom(Node node, AnalysisUnit unit) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetReturnForYieldFrom(node, unit)).WhereNotNull());
        }

        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetTypeMember(node, unit, name)).WhereNotNull());
        }

        public override IAnalysisSet ReverseBinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.ReverseBinaryOperation(node, unit, operation, rhs)).WhereNotNull());
        }

        public override void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
            foreach (var p in _protocols) {
                p.SetIndex(node, unit, index, value);
            }
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            foreach (var p in _protocols) {
                p.SetMember(node, unit, name, value);
            }
        }

        public override IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.UnaryOperation(node, unit, operation)).WhereNotNull());
        }

        public override bool Equals(object obj) {
            if (obj is ProtocolInfo other) {
                if (GetHashCode() != other.GetHashCode()) {
                    return false;
                }
                return ObjectComparer.Instance.Equals(_protocols, other._protocols);
            }
            return false;
        }

        public override int GetHashCode() => ObjectComparer.Instance.GetHashCode(_protocols);

        internal override bool UnionEquals(AnalysisValue av, int strength) {
            if (av is ProtocolInfo pi) {
                if (strength > 0) {
                    return Name == pi.Name;
                }
                return ObjectComparer.Instance.Equals(_protocols, pi._protocols);
            }
            return false;
        }

        internal override int UnionHashCode(int strength) {
            if (strength > 0) {
                return Name.GetHashCode();
            }
            return GetHashCode();
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue av, int strength) {
            if (strength > 0 && av is ProtocolInfo pi && pi.Push()) {
                try {
                    var name = _protocols.OfType<NameProtocol>().FirstOrDefault();
                    if (_protocols.Count == 1 && name != null) {
                        return this;
                    }

                    var protocols = _protocols.Union(pi._protocols, out bool changed);
                    if (!changed) {
                        return this;
                    }

                    if (name != null) {
                        protocols.Split<NameProtocol>(out _, out protocols);
                        protocols = protocols.Add(name);
                    }

                    return new ProtocolInfo(DeclaringModule, State) {
                        _protocols = protocols
                    };
                } finally {
                    pi.Pop();
                }
            }

            return this;
        }

        public virtual IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            var names = _protocols.OfType<NameProtocol>().ToArray();
            Debug.Assert(names.Length <= 1, "Multiple names are not supported");
            var name = names.FirstOrDefault();
            if (name != null) {
                return name.GetRichDescription();
            }

            var res = new List<KeyValuePair<string, string>>();
            var namespaces = _protocols.OfType<NamespaceProtocol>().ToArray();
            var other = _protocols.OfType<Protocol>().Except(names).Except(namespaces).ToArray();

            var fallbackName = other.Select(p => p.Name).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "<unknown>";
            if (!string.IsNullOrEmpty(fallbackName)) {
                res.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, fallbackName));
            }

            if (namespaces.Any()) {
                bool addComma = false;
                res.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "("));
                foreach (var p in namespaces) {
                    if (addComma) {
                        res.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", "));
                    }
                    addComma = true;
                    res.AddRange(p.GetRichDescription());
                }
                res.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, ")"));
            }

            foreach (var p in other) {
                res.AddRange(p.GetRichDescription().ToArray());
            }

            res.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.EndOfDeclaration, ""));

            return res;
        }
    }
}
