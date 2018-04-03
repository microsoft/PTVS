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
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class DictionaryInfo : BuiltinInstanceInfo {
        private SequenceInfo _keyValueTuple;
        private readonly Node _node;
        private VariableDef _keysVariable, _valuesVariable, _keyValueTupleVariable;

        internal readonly DependentKeyValue _keysAndValues;
        private readonly ProjectEntry _declaringModule;
        private readonly int _declVersion;
        private AnalysisValue _getMethod, _itemsMethod, _keysMethod, _valuesMethod, _iterKeysMethod, _iterValuesMethod, _popMethod, _popItemMethod, _iterItemsMethod, _updateMethod;

        private ListInfo _keysList, _valuesList, _itemsList;
        private SingleIteratorValue _keysIter, _valuesIter, _itemsIter;

        private AnalysisUnit _unit;

        public DictionaryInfo(ProjectEntry declaringModule, Node node)
            : base(declaringModule.ProjectState.ClassInfos[BuiltinTypeId.Dict]) {
            _keysAndValues = new DependentKeyValue();
            _declaringModule = declaringModule;
            _declVersion = declaringModule.AnalysisVersion;
            _node = node;
        }

        private AnalysisUnit UpdateAnalysisUnit {
            get {
                return _unit = _unit ?? new UpdateItemsAnalysisUnit(this);
            }
        }

        public override IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems() {
            foreach (var keyValue in _keysAndValues.KeyValueTypes) {
                yield return new KeyValuePair<IAnalysisSet, IAnalysisSet>(
                    keyValue.Key,
                    keyValue.Value
                );
            }
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            _keysAndValues.AddDependency(unit);
            return _keysAndValues.GetValueType(index);
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            _keysAndValues.AddDependency(unit);

            return _keysAndValues.KeyTypes;
        }

        public override void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
            AddTypes(node, unit, index, value);
        }

        internal bool CopyFrom(DictionaryInfo other, bool enqueue = true) {
            return _keysAndValues.CopyFrom(other._keysAndValues, enqueue);
        }

        internal bool AddTypes(Node node, AnalysisUnit unit, IAnalysisSet key, IAnalysisSet value, bool enqueue = true) {
            if (_keysAndValues.AddTypes(unit, key, value, enqueue)) {
                if (_keysVariable != null) {
                    _keysVariable.MakeUnionStrongerIfMoreThan(ProjectState.Limits.DictKeyTypes, value);
                    if (_keysVariable.AddTypes(unit, key, enqueue, DeclaringModule)) {
                        if (_keysList != null) {
                            _keysList.UnionType = null;
                        }
                    }
                }
                if (_valuesVariable != null) {
                    _valuesVariable.MakeUnionStrongerIfMoreThan(ProjectState.Limits.DictValueTypes, value);
                    if (_valuesVariable.AddTypes(unit, value, enqueue, DeclaringModule)) {
                        if (_valuesList != null) {
                            _valuesList.UnionType = null;
                        }
                    }
                }
                if (_keyValueTuple != null) {
                    _keyValueTuple.IndexTypes[0].MakeUnionStrongerIfMoreThan(ProjectState.Limits.DictKeyTypes, key);
                    _keyValueTuple.IndexTypes[1].MakeUnionStrongerIfMoreThan(ProjectState.Limits.DictValueTypes, value);
                    _keyValueTuple.IndexTypes[0].AddTypes(unit, key, enqueue, DeclaringModule);
                    _keyValueTuple.IndexTypes[1].AddTypes(unit, value, enqueue, DeclaringModule);
                }
                return true;
            }
            return false;
        }

        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) {
            // Must unconditionally call the base implementation of GetMember
            var res = base.GetTypeMember(node, unit, name);

            switch (name) {
                case "get":
                    return _getMethod = _getMethod ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        DictionaryGet,
                        false
                    );
                case "items":
                    return _itemsMethod = _itemsMethod ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        unit.State.LanguageVersion.Is3x() ? (CallDelegate)DictionaryIterItems : DictionaryItems,
                        false
                    );
                case "keys":
                    return _keysMethod = _keysMethod ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        unit.State.LanguageVersion.Is3x() ? (CallDelegate)DictionaryIterKeys : DictionaryKeys,
                        false
                    );
                case "values":
                    return _valuesMethod = _valuesMethod ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        unit.State.LanguageVersion.Is3x() ? (CallDelegate)DictionaryIterValues : DictionaryValues,
                        false
                    );
                case "pop":
                    return _popMethod = _popMethod ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        DictionaryPop,
                        false
                    );
                case "popitem":
                    return _popItemMethod = _popItemMethod ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        DictionaryPopItem,
                        false
                    );
                case "iterkeys":
                    if (unit.State.LanguageVersion.Is2x()) {
                        return _iterKeysMethod = _iterKeysMethod ?? new SpecializedCallable(
                            res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                            DictionaryIterKeys,
                            false
                        );
                    }
                    break;
                case "itervalues":
                    if (unit.State.LanguageVersion.Is2x()) {
                        return _iterValuesMethod = _iterValuesMethod ?? new SpecializedCallable(
                            res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                            DictionaryIterValues,
                            false
                        );
                    }
                    break;
                case "iteritems":
                    if (unit.State.LanguageVersion.Is2x()) {
                        return _iterItemsMethod = _iterItemsMethod ?? new SpecializedCallable(
                            res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                            DictionaryIterItems,
                            false
                        );
                    }
                    break;
                case "update":
                    return _updateMethod = _updateMethod ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        DictionaryUpdate,
                        false
                    );
            }

            return res;
        }

        public override string ShortDescription {
            get {
                return "dict";
            }
        }

        public override string Description {
            get {
                // dict({k : v})
                AnalysisValue keyType = _keysAndValues.KeyTypes.GetUnionType();
                string keyName = keyType == null ? null : keyType.ShortDescription;
                AnalysisValue valueType = _keysAndValues.AllValueTypes.GetUnionType();
                string valueName = valueType == null ? null : valueType.ShortDescription;

                if (keyName != null || valueName != null) {
                    return "dict({" +
                        (keyName ?? "unknown") +
                        " : " +
                        (valueName ?? "unknown") +
                        "})";
                }

                return "dict";
            }
        }

        public override PythonMemberType MemberType {
            get {
                return PythonMemberType.Field;
            }
        }

        public override IPythonProjectEntry DeclaringModule {
            get {
                return _declaringModule;
            }
        }

        public override int DeclaringVersion {
            get {
                return _declVersion;
            }
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            if (strength < MergeStrength.IgnoreIterableNode) {
                return ClassInfo.Instance;
            }
            return base.UnionMergeTypes(ns, strength);
        }

        internal override int UnionHashCode(int strength) {
            if (strength < MergeStrength.IgnoreIterableNode) {
                return ClassInfo.Instance.UnionHashCode(strength);
            }
            return base.UnionHashCode(strength);
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            if (strength < MergeStrength.IgnoreIterableNode) {
                if (ns == ProjectState.ClassInfos[BuiltinTypeId.Dict].Instance) {
                    return true;
                }
                var ci = ns as ConstantInfo;
                if (ci != null && ci.ClassInfo == ProjectState.ClassInfos[BuiltinTypeId.Dict]) {
                    return true;
                }
                var di = ns as DictionaryInfo;
                if (di != null) {
                    return di._node.Equals(_node);
                }
                return false;
            }
            return base.UnionEquals(ns, strength);
        }

        /// <summary>
        /// This is a special AnalysisUnit which just propagates changes from our DictionaryInfo
        /// into our KeysAndValues tuple.  Once we've created the key/values tuple we make this
        /// analysis unit dependent upon the dictionary infos key/values.  Therefore whenever 
        /// those keys/values change we'll run this AnalysisUnit which will then copy the new
        /// values to our keys/values tuple which will then enqueue any items dependent upon
        /// them.
        /// </summary>
        class UpdateItemsAnalysisUnit : AnalysisUnit {
            private readonly DictionaryInfo _dictInfo;

            public UpdateItemsAnalysisUnit(DictionaryInfo dictInfo)
                : base(null, ((ProjectEntry)dictInfo.DeclaringModule).MyScope.Scope) {
                _dictInfo = dictInfo;
            }

            internal override void AnalyzeWorker(DDG ddg, CancellationToken cancel) {
                if (_dictInfo._keyValueTuple != null) {
                    if (_dictInfo._keysAndValues.CopyKeysTo(_dictInfo._keyValueTuple.IndexTypes[0]) |
                        _dictInfo._keysAndValues.CopyValuesTo(_dictInfo._keyValueTuple.IndexTypes[1])) {
                        _dictInfo._keyValueTuple.UnionType = null;
                    }
                }

                bool updatedKeys = false, updatedValues = false;
                if (_dictInfo._keysVariable != null) {
                    updatedKeys |= _dictInfo._keysAndValues.CopyKeysTo(_dictInfo._keysVariable);
                }
                if (_dictInfo._valuesVariable != null) {
                    updatedValues |= _dictInfo._keysAndValues.CopyValuesTo(_dictInfo._valuesVariable);
                }

                if (updatedKeys) {
                    if (_dictInfo._keysList != null) {
                        _dictInfo._keysList.UnionType = null;
                    }
                }

                if (updatedValues) {
                    if (_dictInfo._valuesList != null) {
                        _dictInfo._valuesList.UnionType = null;
                    }
                }
            }
        }

        /// <summary>
        /// Gets a type which represents the tuple of (keyTypes, valueTypes)
        /// </summary>
        private SequenceInfo KeyValueTuple {
            get {
                if (_keyValueTuple == null) {
                    var keysDef = new VariableDef();
                    var valuesDef = new VariableDef();

                    _keysAndValues.CopyKeysTo(keysDef);
                    _keysAndValues.CopyValuesTo(valuesDef);

                    _keyValueTuple = new SequenceInfo(
                        new[] { keysDef, valuesDef },
                        ProjectState.ClassInfos[BuiltinTypeId.Tuple],
                        _node,
                        _declaringModule
                    );
                    _keysAndValues.AddDependency(UpdateAnalysisUnit);
                }
                return _keyValueTuple;
            }
        }

        private VariableDef KeyValueTupleVariable {
            get {
                if (_keyValueTupleVariable == null) {
                    _keyValueTupleVariable = new VariableDef();
                    _keyValueTupleVariable.AddTypes(DeclaringModule, KeyValueTuple.SelfSet);
                }
                return _keyValueTupleVariable;
            }
        }

        private VariableDef KeysVariable {
            get {
                if (_keysVariable == null) {
                    _keysVariable = new VariableDef();
                    _keysAndValues.CopyKeysTo(_keysVariable);
                    _keysAndValues.AddDependency(UpdateAnalysisUnit);
                }
                return _keysVariable;
            }
        }

        private VariableDef ValuesVariable {
            get {
                if (_valuesVariable == null) {
                    _valuesVariable = new VariableDef();
                    _keysAndValues.CopyValuesTo(_valuesVariable);
                    _keysAndValues.AddDependency(UpdateAnalysisUnit);
                }
                return _valuesVariable;
            }
        }

        internal IAnalysisSet GetItemsView(AnalysisUnit unit) {
            return DictionaryIterItems(null, unit, null, null);
        }

        internal IAnalysisSet GetKeysView(AnalysisUnit unit) {
            return DictionaryIterKeys(null, unit, null, null);
        }

        internal IAnalysisSet GetValuesView(AnalysisUnit unit) {
            return DictionaryIterValues(null, unit, null, null);
        }


        #region Specialized functions

        private IAnalysisSet DictionaryUpdate(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length >= 1) {
                foreach (var otherDict in args[0].OfType<DictionaryInfo>()) {
                    if (!Object.ReferenceEquals(otherDict, this)) {
                        _keysAndValues.CopyFrom(otherDict._keysAndValues);
                    }
                }
            }
            // TODO: Process keyword args and add those values to our dictionary, plus a string key

            return AnalysisSet.Empty;
        }

        private IAnalysisSet DictionaryPopItem(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            _keysAndValues.AddDependency(unit);

            return KeyValueTuple;
        }

        private IAnalysisSet DictionaryPop(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            _keysAndValues.AddDependency(unit);

            return _keysAndValues.AllValueTypes;
        }

        private IAnalysisSet DictionaryItems(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            _keysAndValues.AddDependency(unit);

            if (_itemsList == null) {
                _itemsList = new ListInfo(
                    new[] { KeyValueTupleVariable },
                    unit.State.ClassInfos[BuiltinTypeId.List],
                    node,
                    unit.ProjectEntry
                );
            }

            return _itemsList;
        }

        private IAnalysisSet DictionaryIterItems(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            _keysAndValues.AddDependency(unit);

            if (_itemsIter == null) {
                _itemsIter = new SingleIteratorValue(
                    KeyValueTupleVariable,
                    unit.State.ClassInfos[BuiltinTypeId.DictItems],
                    DeclaringModule
                );
            }
            return _itemsIter;
        }

        private IAnalysisSet DictionaryKeys(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            _keysAndValues.AddDependency(unit);

            if (_keysList == null) {
                _keysList = new ListInfo(
                    new[] { KeysVariable },
                    unit.State.ClassInfos[BuiltinTypeId.List],
                    node,
                    unit.ProjectEntry
                );
            }
            return _keysList;
        }

        internal IAnalysisSet DictionaryIterKeys(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            _keysAndValues.AddDependency(unit);

            if (_keysIter == null) {
                _keysIter = new SingleIteratorValue(
                    KeysVariable,
                    unit.State.ClassInfos[BuiltinTypeId.DictKeys],
                    DeclaringModule
                );
            }
            return _keysIter;
        }

        private IAnalysisSet DictionaryValues(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            _keysAndValues.AddDependency(unit);

            if (_valuesList == null) {
                _valuesList = new ListInfo(
                    new[] { ValuesVariable },
                    unit.State.ClassInfos[BuiltinTypeId.List],
                    node,
                    unit.ProjectEntry
                );
            }
            return _valuesList;
        }

        internal IAnalysisSet DictionaryIterValues(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            _keysAndValues.AddDependency(unit);

            if (_valuesIter == null) {
                _valuesIter = new SingleIteratorValue(
                    ValuesVariable,
                    unit.State.ClassInfos[BuiltinTypeId.DictValues],
                    DeclaringModule
                );
            }
            return _valuesIter;
        }

        private IAnalysisSet DictionaryGet(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            _keysAndValues.AddDependency(unit);

            if (args.Length == 1) {
                return _keysAndValues.GetValueType(args[0]);
            } else if (args.Length >= 2) {
                return _keysAndValues.GetValueType(args[0]).Union(args[1]);
            }

            return AnalysisSet.Empty;
        }


        #endregion

        static bool _makingString = false;
        public override string ToString() {
            if (_makingString) {
                return "dict(...)";
            } else if (_keysAndValues.KeyTypes.Count == 0 && _keysAndValues.AllValueTypes.Count == 0) {
                return "dict()";
            }
            _makingString = true;
            try {
                StringBuilder sb = new StringBuilder();
                sb.Append("dict(keys=(");
                foreach (var type in _keysAndValues.KeyTypes) {
                    if (type.Push()) {
                        try {
                            sb.Append(type.ToString());
                            sb.Append(", ");
                        } finally {
                            type.Pop();
                        }
                    }
                }
                sb.Append("), values = (");
                foreach (var type in _keysAndValues.AllValueTypes) {
                    if (type.Push()) {
                        try {
                            sb.Append(type.ToString());
                            sb.Append(", ");
                        } finally {
                            type.Pop();
                        }
                    }
                }
                sb.Append(")");
                return sb.ToString();
            } finally {
                _makingString = false;
            }
        }
    }

    internal class StarArgsDictionaryInfo : DictionaryInfo {
        public StarArgsDictionaryInfo(ProjectEntry declaringModule, Node node)
            : base(declaringModule, node) { }


        internal int TypesCount {
            get {
                return _keysAndValues.AllValueTypes.Count;
            }
        }

        internal void MakeUnionStronger() {
            foreach (var dep in _keysAndValues._dependencies.Values) {
                dep.MakeUnionStronger();
            }
        }

        internal void MakeUnionStrongerIfMoreThan(int typeCount, IAnalysisSet extraTypes = null) {
            if (_keysAndValues.AllValueTypes.Count + (extraTypes ?? AnalysisSet.Empty).Count >= typeCount) {
                foreach (var dep in _keysAndValues._dependencies.Values) {
                    dep.MakeUnionStronger();
                }
            }
        }
    }

    /// <summary>
    /// Represents a **args parameter for a function definition.  Holds onto a DictionaryInfo
    /// which includes all of the types passed in via splatting or unused keyword arguments.
    /// </summary>
    sealed class DictParameterVariableDef : LocatedVariableDef {
        public readonly StarArgsDictionaryInfo Dict;
        public readonly string Name;

        public DictParameterVariableDef(AnalysisUnit unit, Node location, string name)
            : base(unit.DeclaringModule.ProjectEntry, new EncodedLocation(unit, location)) {
            Name = name;
            Dict = new StarArgsDictionaryInfo(unit.ProjectEntry, location);
            AddTypes(unit, Dict, false, Entry);
        }

        public DictParameterVariableDef(AnalysisUnit unit, Node location, VariableDef copy)
            : base(unit.DeclaringModule.ProjectEntry, new EncodedLocation(unit, location), copy) {
            Dict = new StarArgsDictionaryInfo(unit.ProjectEntry, location);
            AddTypes(unit, Dict, false, Entry);
        }
    }
}
