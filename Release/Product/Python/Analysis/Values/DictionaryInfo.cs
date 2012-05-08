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
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class DictionaryInfo : BuiltinInstanceInfo {
        private SequenceInfo _keyValueTuple;
        private VariableDef _keyValueTupleVariable;

        private readonly DependentKeyValue _keysAndValues;
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


        public DictionaryInfo(ProjectEntry declaringModule)
            : base(declaringModule.ProjectState._dictType) {
            _keysAndValues = new DependentKeyValue();
            _declaringModule = declaringModule;
            _declVersion = declaringModule.AnalysisVersion;
        }

        public override IEnumerable<KeyValuePair<IEnumerable<AnalysisValue>, IEnumerable<AnalysisValue>>> GetItems() {
            foreach (var keyValue in _keysAndValues.KeyValueTypes) {
                yield return new KeyValuePair<IEnumerable<AnalysisValue>, IEnumerable<AnalysisValue>>(
                    new [] { keyValue.Key },
                    keyValue.Value
                );
            }
        }

        public override ISet<Namespace> GetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index) {
            _keysAndValues.AddDependency(unit);
            return _keysAndValues.GetValueType(index);
        }

        public override ISet<Namespace> GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            _keysAndValues.AddDependency(unit);

            return _keysAndValues.KeyTypes;
        }

        public override void SetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index, ISet<Namespace> value) {
            AddTypes(node, unit, index, value);
        }

        internal bool AddTypes(Node node, AnalysisUnit unit, ISet<Namespace> key, ISet<Namespace> value) {
            if (_keysAndValues.AddTypes(unit, key, value)) {
                if (_iterValuesMethod != null) {
                    _iterValuesMethod.Update(unit, value);
                }
                if (_iterKeysMethod != null) {
                    _iterKeysMethod.Update(unit, key);
                }
                if (_keysMethod != null) {
                    _keysMethod.Update(unit, key);
                }
                if (_valuesMethod != null) {
                    _valuesMethod.Update(unit, value);
                }
                if (_keyValueTuple != null) {
                    _keyValueTuple.IndexTypes[0].AddTypes(
                        unit,
                        key
                    );
                    _keyValueTuple.IndexTypes[1].AddTypes(
                        unit,
                        value
                    );
                }
                return true;
            }
            return false;
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            ISet<Namespace> res = null;
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

        private ISet<Namespace> GetOrMakeSpecializedMethod(ref SpecializedDictionaryMethod method, string name, Func<DictionaryInfo, BuiltinMethodInfo, SpecializedDictionaryMethod> maker) {
            if (method == null) {
                ISet<Namespace> itemsMeth;
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
                Namespace keyType = _keysAndValues.KeyTypes.GetUnionType();
                string keyName = keyType == null ? null : keyType.ShortDescription;
                Namespace valueType = _keysAndValues.AllValueTypes.GetUnionType();
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

        public override bool UnionEquals(Namespace ns) {
            return ns is DictionaryInfo;
        }

        public override int UnionHashCode() {
            return 2;
        }

        public override PythonMemberType ResultType {
            get {
                return PythonMemberType.Field;
            }
        }

        public override ProjectEntry DeclaringModule {
            get {
                return _declaringModule;
            }
        }

        public override int DeclaringVersion {
            get {
                return _declVersion;
            }
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
                : base(null, new[] { dictInfo.DeclaringModule.MyScope.Scope }) {
                _dictInfo = dictInfo;
            }

            protected override void AnalyzeWorker(DDG ddg) {
                _dictInfo._keysAndValues.CopyKeysTo(_dictInfo._keyValueTuple.IndexTypes[0]);
                _dictInfo._keysAndValues.CopyValuesTo(_dictInfo._keyValueTuple.IndexTypes[0]);
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
                        ProjectState._tupleType
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

            public virtual void Update(AnalysisUnit unit, ISet<Namespace> newTypes) {
            }
        }

        #region Specialized functions

        class DictionaryItemsBoundMethod : SpecializedDictionaryMethod {
            private ListInfo _list;

            internal DictionaryItemsBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (_list == null) {
                    _list = new ListInfo(
                        new[] { _myDict.KeyValueTupleVariable },
                        unit.ProjectState._listType
                    );
                }

                return _list.SelfSet;
            }
        }

        class DictionaryGetBoundMethod : SpecializedDictionaryMethod {
            internal DictionaryGetBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (args.Length == 1) {
                    return _myDict._keysAndValues.GetValueType(args[0]);
                } else if (args.Length >= 2) {
                    return _myDict._keysAndValues.GetValueType(args[0]).Union(args[1]);
                }

                return EmptySet<Namespace>.Instance;
            }
        }

        class DictionaryKeysListBoundMethod : SpecializedDictionaryMethod {
            private ListInfo _list;

            internal DictionaryKeysListBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (_list == null) {
                    var listVar = new VariableDef();
                    _myDict._keysAndValues.CopyKeysTo(listVar);
                    _list = new ListInfo(new[] { listVar }, unit.ProjectState._listType);
                }
                return _list;
            }

            public override void Update(AnalysisUnit unit, ISet<Namespace> newTypes) {
                if (_list != null) {
                    _list.IndexTypes[0].AddTypes(unit, newTypes);
                    _list.UnionType = null;
                }
            }
        }

        class DictionaryValuesListBoundMethod : SpecializedDictionaryMethod {
            private ListInfo _list;
            
            internal DictionaryValuesListBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (_list == null) {
                    var valuesVar = new VariableDef();
                    _myDict._keysAndValues.CopyValuesTo(valuesVar);
                    _list = new ListInfo(new[] { valuesVar }, unit.ProjectState._listType);
                }
                return _list;
            }

            public override void Update(AnalysisUnit unit, ISet<Namespace> newTypes) {
                if (_list != null) {
                    _list.IndexTypes[0].AddTypes(unit, newTypes);
                    _list.UnionType = null;
                }
            }
        }

        class DictionaryKeysIterableBoundMethod : SpecializedDictionaryMethod {
            private IterableInfo _list;

            internal DictionaryKeysIterableBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (_list == null) {
                    var listVar = new VariableDef();
                    _myDict._keysAndValues.CopyKeysTo(listVar);
                    _list = new IteratorInfo(new[] { listVar }, unit.ProjectState._dictKeysType);
                }
                return _list;
            }

            public override void Update(AnalysisUnit unit, ISet<Namespace> newTypes) {
                if (_list != null) {
                    _list.IndexTypes[0].AddTypes(unit, newTypes);
                    _list.UnionType = null;
                }
            }
        }

        class DictionaryValuesIterableBoundMethod : SpecializedDictionaryMethod {
            private IterableInfo _list;

            internal DictionaryValuesIterableBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (_list == null) {
                    var valuesVar = new VariableDef();
                    _myDict._keysAndValues.CopyValuesTo(valuesVar);
                    _list = new IteratorInfo(new[] { valuesVar }, unit.ProjectState._dictValuesType);
                }
                return _list;
            }

            public override void Update(AnalysisUnit unit, ISet<Namespace> newTypes) {
                if (_list != null) {
                    _list.IndexTypes[0].AddTypes(unit, newTypes);
                    _list.UnionType = null;
                }
            }
        }

        class DictionaryItemsIterableBoundMethod : SpecializedDictionaryMethod {
            private IterableInfo _list;

            internal DictionaryItemsIterableBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                if (_list == null) {
                    _list = new IteratorInfo(new[] { _myDict.KeyValueTupleVariable }, unit.ProjectState._dictValuesType);
                }
                return _list;
            }
        }

        class DictionaryValuesBoundMethod : SpecializedDictionaryMethod {
            internal DictionaryValuesBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                return _myDict._keysAndValues.AllValueTypes;
            }
        }

        class DictionaryKeyValueTupleBoundMethod : SpecializedDictionaryMethod {
            internal DictionaryKeyValueTupleBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                _myDict._keysAndValues.AddDependency(unit);

                return _myDict.KeyValueTuple;
            }
        }

        class DictionaryUpdateBoundMethod : SpecializedDictionaryMethod {
            internal DictionaryUpdateBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                if (args.Length >= 1) {
                    foreach (var type in args[0]) {
                        DictionaryInfo otherDict = type as DictionaryInfo;
                        if (otherDict != null) {
                            _myDict._keysAndValues.CopyFrom(otherDict._keysAndValues);
                        }
                    }
                }
                // TODO: Process keyword args and add those values to our dictionary, plus a string key

                return EmptySet<Namespace>.Instance;
            }
        }

        #endregion

        public override string ToString() {
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
        }
    }
}
