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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class DictionaryInfo : BuiltinInstanceInfo {
        private SequenceInfo _keyValueTuple;
        private readonly Node _node;
        private VariableDef _keyValueTupleVariable;

        internal readonly DependentKeyValue _keysAndValues;
        private readonly ProjectEntry _declaringModule;
        private readonly int _declVersion;
        private SpecializedDictionaryMethod _getMethod, _itemsMethod, _keysMethod, _valuesMethod, _iterKeysMethod, _iterValuesMethod, _popMethod, _popItemMethod, _iterItemsMethod, _updateMethod;

        // cached delegates for GetMember
        private static readonly Func<DictionaryInfo, BuiltinMethodInfo, SpecializedDictionaryMethod> GetMaker = (self, method) => new DictionaryGetBoundMethod(method, self);
        private static readonly Func<DictionaryInfo, BuiltinMethodInfo, SpecializedDictionaryMethod> ItemsMaker = (self, method) => new DictionaryItemsBoundMethod(method, self);
        private static readonly Func<DictionaryInfo, BuiltinMethodInfo, SpecializedDictionaryMethod> KeysIterableMaker = (self, method) => new DictionaryKeysIterableBoundMethod(method, self);
        private static readonly Func<DictionaryInfo, BuiltinMethodInfo, SpecializedDictionaryMethod> KeysListMaker = (self, method) => new DictionaryKeysListBoundMethod(method, self);
        private static readonly Func<DictionaryInfo, BuiltinMethodInfo, SpecializedDictionaryMethod> ValuesIterableMaker = (self, method) => new DictionaryValuesIterableBoundMethod(method, self);
        private static readonly Func<DictionaryInfo, BuiltinMethodInfo, SpecializedDictionaryMethod> ValuesListMaker = (self, method) => new DictionaryValuesListBoundMethod(method, self);
        private static readonly Func<DictionaryInfo, BuiltinMethodInfo, SpecializedDictionaryMethod> ValuesMaker = (self, method) => new DictionaryValuesBoundMethod(method, self);
        private static readonly Func<DictionaryInfo, BuiltinMethodInfo, SpecializedDictionaryMethod> KeyValueMaker = (self, method) => new DictionaryKeyValueTupleBoundMethod(method, self);
        private static readonly Func<DictionaryInfo, BuiltinMethodInfo, SpecializedDictionaryMethod> IterableItemsMaker = (self, method) => new DictionaryItemsIterableBoundMethod(method, self);
        private static readonly Func<DictionaryInfo, BuiltinMethodInfo, SpecializedDictionaryMethod> UpdateMaker = (self, method) => new DictionaryUpdateBoundMethod(method, self);


        public DictionaryInfo(ProjectEntry declaringModule, Node node)
            : base(declaringModule.ProjectState.ClassInfos[BuiltinTypeId.Dict]) {
            _keysAndValues = new DependentKeyValue();
            _declaringModule = declaringModule;
            _declVersion = declaringModule.AnalysisVersion;
            _node = node;
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
                if (_iterValuesMethod != null) {
                    _iterValuesMethod.Update(unit, value, enqueue);
                }
                if (_iterKeysMethod != null) {
                    _iterKeysMethod.Update(unit, key, enqueue);
                }
                if (_keysMethod != null) {
                    _keysMethod.Update(unit, key, enqueue);
                }
                if (_valuesMethod != null) {
                    _valuesMethod.Update(unit, value, enqueue);
                }
                if (_keyValueTuple != null) {
                    _keyValueTuple.IndexTypes[0].MakeUnionStrongerIfMoreThan(ProjectState.Limits.DictKeyTypes, key);
                    _keyValueTuple.IndexTypes[1].MakeUnionStrongerIfMoreThan(ProjectState.Limits.DictValueTypes, value);
                    _keyValueTuple.IndexTypes[0].AddTypes(unit, key, enqueue);
                    _keyValueTuple.IndexTypes[1].AddTypes(unit, value, enqueue);
                }
                return true;
            }
            return false;
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            var res = AnalysisSet.Empty;
            switch (name) {
                case "get":
                    res = GetOrMakeSpecializedMethod(ref _getMethod, "get", GetMaker);
                    break;
                case "items":
                    res = GetOrMakeSpecializedMethod(ref _itemsMethod, "items", ItemsMaker);
                    break;
                case "keys":
                    if (unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _keysMethod, "keys", KeysIterableMaker);
                    } else {
                        res = GetOrMakeSpecializedMethod(ref _keysMethod, "keys", KeysListMaker);
                    }
                    break;
                case "values":
                    if (unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _valuesMethod, "values", ValuesIterableMaker);
                    } else {
                        res = GetOrMakeSpecializedMethod(ref _valuesMethod, "values", ValuesListMaker);
                    }
                    break;
                case "pop":
                    res = GetOrMakeSpecializedMethod(ref _popMethod, "pop", ValuesMaker);
                    break;
                case "popitem":
                    res = GetOrMakeSpecializedMethod(ref _popItemMethod, "popitem", KeyValueMaker);
                    break;
                case "iterkeys":
                    if (!unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _iterKeysMethod, "iterkeys", KeysIterableMaker);
                    }
                    break;
                case "itervalues":
                    if (!unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _iterValuesMethod, "itervalues", ValuesIterableMaker);
                    }
                    break;
                case "iteritems":
                    if (!unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _iterItemsMethod, "iteritems", IterableItemsMaker);
                    }
                    break;
                case "update":
                    res = GetOrMakeSpecializedMethod(ref _updateMethod, "update", UpdateMaker);
                    break;
            }

            return res ?? base.GetMember(node, unit, name);
        }

        private IAnalysisSet GetOrMakeSpecializedMethod(ref SpecializedDictionaryMethod method, string name, Func<DictionaryInfo, BuiltinMethodInfo, SpecializedDictionaryMethod> maker) {
            if (method == null) {
                IAnalysisSet itemsMeth;
                if (TryGetMember(name, out itemsMeth)) {
                    method = maker(this, (BuiltinMethodInfo)itemsMeth.First());
                }
            }
            return method;
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
                _dictInfo._keysAndValues.CopyKeysTo(_dictInfo._keyValueTuple.IndexTypes[0]);
                _dictInfo._keysAndValues.CopyValuesTo(_dictInfo._keyValueTuple.IndexTypes[1]);
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
                        _node
                    );
                    _keysAndValues.AddDependency(new UpdateItemsAnalysisUnit(this));
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

        class SpecializedDictionaryMethod : BoundBuiltinMethodInfo {
            internal readonly DictionaryInfo _myDict;

            internal SpecializedDictionaryMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method) {
                _myDict = myDict;
            }

            public virtual void Update(AnalysisUnit unit, IAnalysisSet newTypes, bool enqueue) {
            }
        }

        #region Specialized functions

        class DictionaryItemsBoundMethod : SpecializedDictionaryMethod {
            private ListInfo _list;

            internal DictionaryItemsBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (_list == null) {
                    _list = new ListInfo(
                        new[] { _myDict.KeyValueTupleVariable },
                        unit.ProjectState.ClassInfos[BuiltinTypeId.List],
                        node
                    );
                }

                return _list.SelfSet;
            }
        }

        class DictionaryGetBoundMethod : SpecializedDictionaryMethod {
            internal DictionaryGetBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (args.Length == 1) {
                    return _myDict._keysAndValues.GetValueType(args[0]);
                } else if (args.Length >= 2) {
                    return _myDict._keysAndValues.GetValueType(args[0]).Union(args[1]);
                }

                return AnalysisSet.Empty;
            }
        }

        class DictionaryKeysListBoundMethod : SpecializedDictionaryMethod {
            private ListInfo _list;

            internal DictionaryKeysListBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (_list == null) {
                    var listVar = new VariableDef();
                    _myDict._keysAndValues.CopyKeysTo(listVar);
                    _list = new ListInfo(new[] { listVar }, unit.ProjectState.ClassInfos[BuiltinTypeId.List], node);
                }
                return _list;
            }

            public override void Update(AnalysisUnit unit, IAnalysisSet newTypes, bool enqueue) {
                if (_list != null) {
                    _list.IndexTypes[0].AddTypes(unit, newTypes, enqueue);
                    _list.UnionType = null;
                }
            }
        }

        class DictionaryValuesListBoundMethod : SpecializedDictionaryMethod {
            private ListInfo _list;
            
            internal DictionaryValuesListBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (_list == null) {
                    var valuesVar = new VariableDef();
                    _myDict._keysAndValues.CopyValuesTo(valuesVar);
                    _list = new ListInfo(new[] { valuesVar }, unit.ProjectState.ClassInfos[BuiltinTypeId.List], node);
                }
                return _list;
            }

            public override void Update(AnalysisUnit unit, IAnalysisSet newTypes, bool enqueue) {
                if (_list != null) {
                    _list.IndexTypes[0].AddTypes(unit, newTypes, enqueue);
                    _list.UnionType = null;
                }
            }
        }

        class DictionaryKeysIterableBoundMethod : SpecializedDictionaryMethod {
            private IterableInfo _list;

            internal DictionaryKeysIterableBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (_list == null) {
                    var listVar = new VariableDef();
                    _myDict._keysAndValues.CopyKeysTo(listVar);
                    _list = new IteratorInfo(new[] { listVar }, unit.ProjectState.ClassInfos[BuiltinTypeId.DictKeys], node);
                }
                return _list;
            }

            public override void Update(AnalysisUnit unit, IAnalysisSet newTypes, bool enqueue) {
                if (_list != null) {
                    _list.IndexTypes[0].AddTypes(unit, newTypes, enqueue);
                    _list.UnionType = null;
                }
            }
        }

        class DictionaryValuesIterableBoundMethod : SpecializedDictionaryMethod {
            private IterableInfo _list;

            internal DictionaryValuesIterableBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (_list == null) {
                    var valuesVar = new VariableDef();
                    _myDict._keysAndValues.CopyValuesTo(valuesVar);
                    _list = new IteratorInfo(new[] { valuesVar }, unit.ProjectState.ClassInfos[BuiltinTypeId.DictValues], node);
                }
                return _list;
            }

            public override void Update(AnalysisUnit unit, IAnalysisSet newTypes, bool enqueue) {
                if (_list != null) {
                    _list.IndexTypes[0].AddTypes(unit, newTypes, enqueue);
                    _list.UnionType = null;
                }
            }
        }

        class DictionaryItemsIterableBoundMethod : SpecializedDictionaryMethod {
            private IterableInfo _list;

            internal DictionaryItemsIterableBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (_list == null) {
                    _list = new IteratorInfo(new[] { _myDict.KeyValueTupleVariable }, unit.ProjectState.ClassInfos[BuiltinTypeId.DictItems], node);
                }
                return _list;
            }
        }

        class DictionaryValuesBoundMethod : SpecializedDictionaryMethod {
            internal DictionaryValuesBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                return _myDict._keysAndValues.AllValueTypes;
            }
        }

        class DictionaryKeyValueTupleBoundMethod : SpecializedDictionaryMethod {
            internal DictionaryKeyValueTupleBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                return _myDict.KeyValueTuple;
            }
        }

        class DictionaryUpdateBoundMethod : SpecializedDictionaryMethod {
            internal DictionaryUpdateBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
                if (args.Length >= 1) {
                    foreach (var type in args[0]) {
                        DictionaryInfo otherDict = type as DictionaryInfo;
                        if (otherDict != null && !Object.ReferenceEquals(otherDict, _myDict)) {
                            _myDict._keysAndValues.CopyFrom(otherDict._keysAndValues);
                        }
                    }
                }
                // TODO: Process keyword args and add those values to our dictionary, plus a string key

                return AnalysisSet.Empty;
            }
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
                        sb.Append(type.ToString());
                        sb.Append(", ");
                        type.Pop();
                    }
                }
                sb.Append("), values = (");
                foreach (var type in _keysAndValues.AllValueTypes) {
                    if (type.Push()) {
                        sb.Append(type.ToString());
                        sb.Append(", ");
                        type.Pop();
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
}
