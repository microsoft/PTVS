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
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class TypingTypeInfo : AnalysisValue, IHasRichDescription {
        private readonly AnalysisValue _innerValue;
        private readonly string _baseName;
        private readonly IReadOnlyList<IAnalysisSet> _args;

        public TypingTypeInfo(string baseName, AnalysisValue innerValue) : this(baseName, innerValue, Array.Empty<IAnalysisSet>()) { }

        private TypingTypeInfo(string baseName, AnalysisValue innerValue, IReadOnlyList<IAnalysisSet> args) {
            _baseName = baseName;
            _innerValue = innerValue;
            _args = args;
        }

        public TypingTypeInfo MakeGeneric(IReadOnlyList<IAnalysisSet> args) {
            if (_args != null) {
                return new TypingTypeInfo(_baseName, _innerValue, args);
            }
            return this;
        }

        public override IAnalysisSet GetInstanceType() => this;

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if ((_args == null || !_args.Any()) && node is CallExpression ce) {
                return unit.Scope.GetOrMakeNodeValue(node, NodeValueKind.TypeAnnotation, n => {
                    // Use annotation converter and reparse the arguments
                    var newArgs = new List<IAnalysisSet>();
                    var eval = new ExpressionEvaluatorAnnotationConverter(
                        new ExpressionEvaluator(unit),
                        node,
                        unit,
                        returnInternalTypes: true
                    );
                    foreach (var type in ce.Args.MaybeEnumerate().Where(e => e?.Expression != null).Select(e => new TypeAnnotation(unit.State.LanguageVersion, e.Expression))) {
                        newArgs.Add(type.GetValue(eval) ?? AnalysisSet.Empty);
                    }
                    return new TypingTypeInfo(_baseName, _innerValue, newArgs);
                });
            }
            return this;
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            if (node is IndexExpression ie) {
                return unit.Scope.GetOrMakeNodeValue(node, NodeValueKind.TypeAnnotation, n => {
                    // Use annotation converter and reparse the index
                    var exprs = new List<Expression>();
                    if (ie.Index is SequenceExpression te) {
                        exprs.AddRange(te.Items.MaybeEnumerate());
                    } else {
                        exprs.Add(ie.Index);
                    }
                    var newArgs = new List<IAnalysisSet>();
                    var eval = new ExpressionEvaluatorAnnotationConverter(
                        new ExpressionEvaluator(unit),
                        node,
                        unit,
                        returnInternalTypes: true
                    );
                    foreach (var type in exprs.Select(e => new TypeAnnotation(unit.State.LanguageVersion, e))) {
                        newArgs.Add(type.GetValue(eval) ?? AnalysisSet.Empty);
                    }
                    return new TypingTypeInfo(_baseName, _innerValue, newArgs);
                });
            }
            return this;
        }

        public IAnalysisSet Finalize(ExpressionEvaluator eval, Node node, AnalysisUnit unit) {
            if (Push()) {
                try {
                    var finalizer = new TypingTypeInfoFinalizer(eval, node, unit);
                    return finalizer.Finalize(_baseName, _args)
                        ?? finalizer.Finalize(_baseName)
                        ?? AnalysisSet.Empty;
                } finally {
                    Pop();
                }
            }
            return AnalysisSet.Empty;
        }

        public override string ToString() {
            if (_args != null) {
                return $"<Typing:{_baseName}[{string.Join(", ", _args)}]>";
            }
            return $"<Typing:{_baseName}>";
        }

        public IReadOnlyList<IAnalysisSet> ToTypeList() {
            if (_baseName == " List") {
                return _args;
            }
            return null;
        }

        public static IReadOnlyList<IAnalysisSet> ToTypeList(IAnalysisSet set) {
            if (set.Split(out IReadOnlyList<TypingTypeInfo> tti, out _)) {
                return tti.Select(t => t.ToTypeList()).FirstOrDefault(t => t != null);
            }
            return null;
        }

        public IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_baseName != " List") {
                if (_innerValue == null) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, _baseName);
                } else {
                    foreach (var kv in _innerValue.GetRichDescriptions()) {
                        yield return kv;
                    }
                }
            }
            if (_args != null && _args.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                bool addComma = false;
                foreach (var arg in _args) {
                    if (addComma) {
                        yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    }
                    addComma = true;
                    foreach (var kv in arg.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                        yield return kv;
                    }
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }

        public override string Description => base.Description ?? _innerValue?.Description; 
        public override string Documentation => base.Documentation ?? _innerValue?.Documentation;

        public override bool Equals(object obj) {
            if (obj is TypingTypeInfo other) {
                if (_baseName != other._baseName) {
                    return false;
                }
                if ((_args == null) != (other._args == null)) {
                    return false;
                }
                if (_args == null || other._args == null) {
                    return true;
                }
                return _args.Zip(other._args, (x, y) => ObjectComparer.Instance.Equals(x, y)).All(b => b);
            }
            return false;
        }

        public override int GetHashCode() {
            if (_args != null) {
                return _args.Aggregate(_baseName.GetHashCode(), (h, s) => h + 37 * ObjectComparer.Instance.GetHashCode(s));
            }
            return _baseName.GetHashCode();
        }

        internal override bool UnionEquals(AnalysisValue av, int strength) {
            if (strength == 0) {
                return Equals(av);
            } else {
                return _baseName == (av as TypingTypeInfo)?._baseName;
            }
        }

        internal override int UnionHashCode(int strength) {
            if (strength == 0) {
                return GetHashCode();
            } else {
                return _baseName.GetHashCode();
            }
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue av, int strength) {
            if (strength == 0 || (_args != null && _args.Count == 0)) {
                return this;
            } else {
                return new TypingTypeInfo(_baseName, _innerValue, Array.Empty<IAnalysisSet>());
            }
        }
    }

    sealed class TypingTypeInfoFinalizer {
        private readonly ExpressionEvaluator _eval;
        private readonly Node _node;
        private readonly AnalysisUnit _unit;

        public TypingTypeInfoFinalizer(ExpressionEvaluator eval, Node node, AnalysisUnit unit) {
            _eval = eval;
            _node = node;
            _unit = unit;
        }

        private InterpreterScope Scope => _eval.Scope;
        private PythonAnalyzer State => _unit.State;
        private IKnownPythonTypes Types => State.Types;
        private IKnownClasses ClassInfo => State.ClassInfos;
        private AnalysisValue NoneType => ClassInfo[BuiltinTypeId.NoneType];
        private AnalysisValue None => State._noneInst;
        private ProjectEntry Entry => _unit.ProjectEntry;


        private static IReadOnlyList<IAnalysisSet> GetTypeList(IAnalysisSet item) {
            return item.OfType<TypingTypeInfo>().FirstOrDefault()?.ToTypeList() ?? new[] { item };
        }

        private IAnalysisSet MakeTuple(params IAnalysisSet[] types) {
            var p = new ProtocolInfo(Entry, State);
            var np = new NameProtocol(p, Types[BuiltinTypeId.Tuple]);
            var tp = new TupleProtocol(p, types);
            np.ExtendDescription(tp.GetRichDescription());
            p.AddProtocol(np);
            p.AddProtocol(tp);
            return p;
        }

        private bool GetSequenceTypes(string name, IReadOnlyList<IAnalysisSet> args, out IPythonType realType, out IAnalysisSet keyTypes, out IAnalysisSet valueTypes) {
            switch (name) {
                case "List":
                case "Container":
                case "Sequence":
                case "MutableSequence":
                    realType = Types[BuiltinTypeId.List];
                    keyTypes = ClassInfo[BuiltinTypeId.Int].Instance;
                    valueTypes = AnalysisSet.UnionAll(args.Select(ToInstance));
                    return true;
                case "Tuple":
                    realType = Types[BuiltinTypeId.Tuple];
                    keyTypes = ClassInfo[BuiltinTypeId.Int].Instance;
                    valueTypes = AnalysisSet.UnionAll(args.Select(ToInstance));
                    return true;
                case "MutableSet":
                case "Set":
                    realType = Types[BuiltinTypeId.Set];
                    keyTypes = null;
                    valueTypes = AnalysisSet.UnionAll(args.Select(ToInstance));
                    return true;
                case "FrozenSet":
                    realType = Types[BuiltinTypeId.FrozenSet];
                    keyTypes = null;
                    valueTypes = AnalysisSet.UnionAll(args.Select(ToInstance));
                    return true;

                case "KeysView":
                    if (args.Count >= 1) {
                        realType = Types[BuiltinTypeId.DictKeys];
                        keyTypes = null;
                        valueTypes = AnalysisSet.UnionAll(GetTypeList(args[0]).Select(ToInstance));
                        return true;
                    }
                    break;
                case "ValuesView":
                    if (args.Count >= 1) {
                        realType = Types[BuiltinTypeId.DictValues];
                        keyTypes = null;
                        valueTypes = AnalysisSet.UnionAll(GetTypeList(args[0]).Select(ToInstance));
                        return true;
                    }
                    break;

                case "ItemsView":
                    if (args.Count >= 2) {
                        realType = Types[BuiltinTypeId.DictItems];
                        keyTypes = null;
                        valueTypes = MakeTuple(
                            AnalysisSet.UnionAll(GetTypeList(args[0]).Select(ToInstance)),
                            AnalysisSet.UnionAll(GetTypeList(args[1]).Select(ToInstance))
                        );
                        return true;
                    }
                    break;

                case "Dict":
                case "Mapping":
                    if (args.Count >= 2) {
                        realType = Types[BuiltinTypeId.Dict];
                        keyTypes = AnalysisSet.UnionAll(GetTypeList(args[0]).Select(ToInstance));
                        valueTypes = AnalysisSet.UnionAll(GetTypeList(args[1]).Select(ToInstance));
                        return true;
                    }
                    break;
            }
            realType = null;
            keyTypes = null;
            valueTypes = null;
            return false;
        }


        public IAnalysisSet Finalize(string name, IReadOnlyList<IAnalysisSet> args) {
            if (string.IsNullOrEmpty(name) || args == null || args.Count == 0) {
                return null;
            }

            NameProtocol np;
            IPythonType realType;
            IAnalysisSet keyTypes, valueTypes;

            switch (name) {
                case "Union":
                    return AnalysisSet.UnionAll(args.Select(a => Finalize(a)));
                case "Optional":
                    return Finalize(args[0]).Add(NoneType);

                case "Tuple":
                    if (!args.SelectMany(a => a).Any(a => a.TypeId == BuiltinTypeId.Ellipsis)) {
                        return MakeTuple(args.Select(ToInstance).ToArray());
                    }
                    goto case "List";

                case "List":
                case "Container":
                case "MutableSequence":
                case "Sequence":
                case "MutableSet":
                case "Set":
                case "FrozenSet":
                case "KeysView":
                case "ValuesView":
                case "ItemsView":
                    if (GetSequenceTypes(name, args, out realType, out keyTypes, out valueTypes)) {
                        var p = new ProtocolInfo(Entry, State);
                        p.AddReference(_node, _unit);
                        np = realType == null ? new NameProtocol(p, name) : new NameProtocol(p, realType);
                        np.ExtendDescription(valueTypes.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]", alwaysUsePrefixSuffix: true));
                        p.AddProtocol(np);
                        p.AddProtocol(new IterableProtocol(p, valueTypes));
                        if (keyTypes != null) {
                            p.AddProtocol(new GetItemProtocol(p, keyTypes, valueTypes));
                        }
                        return p;
                    }
                    break;

                case "Mapping":
                case "MappingView":
                case "MutableMapping":
                case "Dict":
                    if (GetSequenceTypes(name, args, out realType, out keyTypes, out valueTypes)) {
                        var p = new ProtocolInfo(Entry, State);
                        p.AddReference(_node, _unit);
                        np = realType == null ? new NameProtocol(p, name) : new NameProtocol(p, realType);
                        np.ExtendDescription(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "["));
                        np.ExtendDescription(keyTypes.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]", defaultIfEmpty: "Any"));
                        np.ExtendDescription(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", "));
                        np.ExtendDescription(valueTypes.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]", defaultIfEmpty: "Any"));
                        np.ExtendDescription(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]"));
                        p.AddProtocol(np);
                        p.AddProtocol(new MappingProtocol(p, keyTypes, valueTypes, MakeTuple(keyTypes, valueTypes)));
                        return p;
                    }
                    break;

                case "Callable":
                    if (args.Count > 0) {
                        var p = new ProtocolInfo(Entry, State);
                        p.AddReference(_node, _unit);
                        np = new NameProtocol(p, Types[BuiltinTypeId.Function]);
                        var call = new CallableProtocol(
                            p,
                            null,
                            GetTypeList(args[0]).Select(ToInstance).ToArray(),
                            ToInstance(args.ElementAtOrDefault(1) ?? AnalysisSet.Empty)
                        );
                        np.ExtendDescription(call.GetRichDescription());
                        p.AddProtocol(np);
                        p.AddProtocol(call);
                        return p;
                    }
                    break;

                case "Iterable":
                    if (args.Count > 0) {
                        var p = new ProtocolInfo(Entry, State);
                        p.AddReference(_node, _unit);
                        np = new NameProtocol(p, "iterable", memberType: PythonMemberType.Class);
                        var ip = new IterableProtocol(p, AnalysisSet.UnionAll(args.Select(ToInstance)));
                        np.ExtendDescription(ip.GetRichDescription());
                        p.AddProtocol(np);
                        p.AddProtocol(ip);
                        return p;
                    }
                    break;

                case "Iterator": 
                    if (args.Count > 0) {
                        var p = new ProtocolInfo(Entry, State);
                        p.AddReference(_node, _unit);
                        np = new NameProtocol(p, "iterator", memberType: PythonMemberType.Class);
                        var ip = new IteratorProtocol(p, AnalysisSet.UnionAll(args.Select(ToInstance)));
                        np.ExtendDescription(ip.GetRichDescription());
                        p.AddProtocol(np);
                        p.AddProtocol(ip);
                        return p;
                    }
                    break;

                case "Generator":
                    if (args.Count > 0) {
                        var p = new ProtocolInfo(Entry, State);
                        p.AddReference(_node, _unit);
                        np = new NameProtocol(p, Types[BuiltinTypeId.Generator]);
                        var yielded = ToInstance(args[0]);
                        var sent = args.Count > 1 ? ToInstance(args[1]) : AnalysisSet.Empty;
                        var returned = args.Count > 2 ? ToInstance(args[2]) : AnalysisSet.Empty;
                        var gp = new GeneratorProtocol(p, yielded, sent, returned);
                        np.ExtendDescription(gp.GetRichDescription());
                        p.AddProtocol(np);
                        p.AddProtocol(gp);
                        return p;
                    }
                    break;

                case "NamedTuple":
                    return CreateNamedTuple(_node, _unit, args.ElementAtOrDefault(0), args.ElementAtOrDefault(1));
                case " List":
                    return AnalysisSet.UnionAll(args.Select(ToInstance));
            }

            return null;
        }

        private IAnalysisSet CreateNamedTuple(Node node, AnalysisUnit unit, IAnalysisSet namedTupleName, IAnalysisSet namedTupleArgs) {
            var args = namedTupleArgs == null ? null : TypingTypeInfo.ToTypeList(namedTupleArgs);

            var res = new ProtocolInfo(unit.ProjectEntry, unit.State);

            string name;
            var description = new List<KeyValuePair<string, string>>();

            if (args != null && args.Any()) {
                var tupleItem = new List<IAnalysisSet>();
                foreach (var a in args) {
                    // each arg is going to be either a union containing a string literal and type,
                    // or a list with string literal and type.
                    IAnalysisSet nameSet = a, valueSet = a;
                    if (a is TypingTypeInfo tti) {
                        var list = tti.ToTypeList();
                        if (list != null && list.Count >= 2) {
                            nameSet = list[0];
                            valueSet = AnalysisSet.UnionAll(list.Skip(1));
                        }
                    }

                    if (!nameSet.Split(out IReadOnlyList<ConstantInfo> names, out var rest)) {
                        names = a.OfType<ConstantInfo>().ToArray();
                    }
                    name = names.Select(n => n.GetConstantValueAsString()).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "unnamed";

                    var p = new NamespaceProtocol(res, name);
                    var value = ToInstance(valueSet);
                    p.SetMember(node, unit, name, value);
                    tupleItem.Add(value);
                    res.AddProtocol(p);

                    if (description.Any()) {
                        description.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", "));
                    }
                    description.AddRange(p.GetRichDescription());
                }

                res.AddProtocol(new TupleProtocol(res, tupleItem));
            }

            name = namedTupleName?.GetConstantValueAsString().FirstOrDefault() ?? "tuple";
            var np = new NameProtocol(res, name);
            if (description.Any()) {
                np.ExtendDescription(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "("));
                np.ExtendDescription(description);
                np.ExtendDescription(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, ")"));
            }
            res.AddProtocol(np);

            return res;
        }

        public IAnalysisSet Finalize(string name) {
            if (string.IsNullOrEmpty(name)) {
                return null;
            }

            switch (name) {
                case "Callable": return ClassInfo[BuiltinTypeId.Function];
                case "Tuple": return ClassInfo[BuiltinTypeId.Tuple];
                case "Container": return ClassInfo[BuiltinTypeId.List];
                case "ItemsView": return ClassInfo[BuiltinTypeId.DictItems];
                case "Iterable":
                case "Iterator": {
                        var p = new ProtocolInfo(Entry, State);
                        p.AddReference(_node, _unit);
                        p.AddProtocol(new NameProtocol(p, name.ToLowerInvariant(), memberType: PythonMemberType.Class));
                        p.AddProtocol(name == "Iterable" ? (Protocol)new IterableProtocol(p, AnalysisSet.Empty) : new IteratorProtocol(p, AnalysisSet.Empty));
                        return p;
                    }
                case "KeysView": return ClassInfo[BuiltinTypeId.DictKeys];
                case "Mapping": return ClassInfo[BuiltinTypeId.Dict];
                case "MappingView": return ClassInfo[BuiltinTypeId.Dict];
                case "MutableMapping": return ClassInfo[BuiltinTypeId.Dict];
                case "MutableSequence": return ClassInfo[BuiltinTypeId.List];
                case "MutableSet": return ClassInfo[BuiltinTypeId.Set];
                case "Sequence": return ClassInfo[BuiltinTypeId.List];
                case "ValuesView": return ClassInfo[BuiltinTypeId.DictValues];
                case "Dict": return ClassInfo[BuiltinTypeId.Dict];
                case "List": return ClassInfo[BuiltinTypeId.List];
                case "Set": return ClassInfo[BuiltinTypeId.Set];
                case "FrozenSet": return ClassInfo[BuiltinTypeId.FrozenSet];
                case "NamedTuple": return ClassInfo[BuiltinTypeId.Tuple];
                case "Generator": return ClassInfo[BuiltinTypeId.Generator];
                case "NoReturn": return AnalysisSet.Empty;
                case " List": return null;
            }

            return null;
        }

        private IAnalysisSet Finalize(IAnalysisSet set) {
            if (set.Split(out IReadOnlyList<ConstantInfo> constants, out var rest)) {
                var names = constants.Select(c => c.GetConstantValueAsString()).Where(n => !string.IsNullOrEmpty(n)).ToArray();
                rest = rest.Union(constants.Where(c => string.IsNullOrEmpty(c.GetConstantValueAsString())));
                rest = rest.UnionAll(
                    names.Select(n => _eval.LookupAnalysisSetByName(_node, n, addDependency: true))
                );
            }
            if (rest.Split(out IReadOnlyList<TypingTypeInfo> typeInfo, out rest)) {
                rest = rest.UnionAll(
                    typeInfo.Select(t => t.Finalize(_eval, _node, _unit))
                );
            }
            return rest;
        }

        private VariableDef ToVariableDef(IAnalysisSet set) {
            var v = new VariableDef();
            v.AddTypes(_unit, ToInstance(set), enqueue: false, declaringScope: Entry);
            return v;
        }

        private IAnalysisSet ToInstance(IAnalysisSet set) {
            return Finalize(set).GetInstanceType();
        }

    }
}
