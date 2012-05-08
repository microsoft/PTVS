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
        private readonly VariableDef _keyTypes;
        private readonly VariableDef _valueTypes;
        private SequenceInfo _keyValueTuple;
        private VariableDef _keyValueTupleVariable;
        private readonly ProjectEntry _declaringModule;
        private readonly int _declVersion;
        private SpecializedDictionaryMethod _getMethod, _itemsMethod, _keysMethod, _valuesMethod, _iterKeysMethod, _iterValuesMethod, _popMethod, _popItemMethod, _iterItemsMethod, _updateMethod;

        public DictionaryInfo(ProjectEntry declaringModule)
            : base(declaringModule.ProjectState._dictType) {
            _keyTypes = new VariableDef();
            _valueTypes = new VariableDef();
            _declaringModule = declaringModule;
            _declVersion = declaringModule.AnalysisVersion;
        }

        public override IEnumerable<KeyValuePair<IEnumerable<AnalysisValue>, IEnumerable<AnalysisValue>>> GetItems() {
            yield return new KeyValuePair<IEnumerable<AnalysisValue>, IEnumerable<AnalysisValue>>(
                _keyTypes.Types,
                _valueTypes.Types
            );
        }

        public override ISet<Namespace> GetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index) {
            return _valueTypes.Types;
        }

        public override ISet<Namespace> GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            _keyTypes.AddDependency(unit);

            return _keyTypes.Types;
        }

        public override void SetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index, ISet<Namespace> value) {
            foreach (var indexVal in index) {
                AddKeyType(node, unit, indexVal);
            }
            foreach (var valueVal in value) {
                AddValueType(node, unit, valueVal);
            }
        }

        internal bool AddValueType(Node node, AnalysisUnit unit, Namespace valueVal) {
            if (_valueTypes.AddTypes(unit, valueVal)) {
                if (_iterValuesMethod != null) {
                    _iterValuesMethod.Update();
                }
                if (_valuesMethod != null) {
                    _valuesMethod.Update();
                }
                return true;
            }
            return false;
        }

        internal bool AddKeyType(Node node, AnalysisUnit unit, Namespace indexVal) {
            if (_keyTypes.AddTypes(unit, indexVal)) {
                if (_iterKeysMethod != null) {
                    _iterKeysMethod.Update();
                }
                if (_keysMethod != null) {
                    _keysMethod.Update();
                }
                return true;
            }
            return false;
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            ISet<Namespace> res = null;
            switch (name) {
                case "get":
                    res = GetOrMakeSpecializedMethod(ref _getMethod, "get", method => new DictionaryGetBoundMethod(method, this));
                    break;
                case "items":
                    res = GetOrMakeSpecializedMethod(ref _itemsMethod, "items", method => new DictionaryItemsBoundMethod(method, this));
                    break;
                case "keys":
                    if (unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _keysMethod, "keys", method => new DictionaryKeysIterableBoundMethod(method, this));
                    } else {
                        res = GetOrMakeSpecializedMethod(ref _keysMethod, "keys", method => new DictionaryKeysListBoundMethod(method, this));
                    }
                    break;
                case "values":
                    if (unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _valuesMethod, "values", method => new DictionaryValuesIterableBoundMethod(method, this));
                    } else {
                        res = GetOrMakeSpecializedMethod(ref _valuesMethod, "values", method => new DictionaryValuesListBoundMethod(method, this));
                    }
                    break;
                case "pop":
                    res = GetOrMakeSpecializedMethod(ref _popMethod, "pop", method => new DictionaryValuesBoundMethod(method, this));
                    break;
                case "popitem":
                    res = GetOrMakeSpecializedMethod(ref _popItemMethod, "popitem", method => new DictionaryKeyValueTupleBoundMethod(method, this));
                    break;
                case "iterkeys":
                    if (!unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _iterKeysMethod, "iterkeys", method => new DictionaryKeysIterableBoundMethod(method, this));
                    }
                    break;
                case "itervalues":
                    if (!unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _iterValuesMethod, "itervalues", method => new DictionaryValuesIterableBoundMethod(method, this));
                    }
                    break;
                case "iteritems":
                    if (!unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _iterItemsMethod, "iteritems", method => new DictionaryItemsIterableBoundMethod(method, this));
                    }
                    break;
                case "update":
                    res = GetOrMakeSpecializedMethod(ref _updateMethod, "update", method => new DictionaryUpdateBoundMethod(method, this));
                    break;
            }

            return res ?? base.GetMember(node, unit, name);
        }

        private ISet<Namespace> GetOrMakeSpecializedMethod(ref SpecializedDictionaryMethod method, string name, Func<BuiltinMethodInfo, SpecializedDictionaryMethod> maker) {
            if (method == null) {
                ISet<Namespace> itemsMeth;
                if (TryGetMember(name, out itemsMeth)) {
                    method = maker((BuiltinMethodInfo)itemsMeth.First());
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
                Namespace keyType = _keyTypes.Types.GetUnionType();
                string keyName = keyType == null ? null : keyType.ShortDescription;
                Namespace valueType = _valueTypes.Types.GetUnionType();
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
        /// Gets a type which represents the tuple of (keyTypes, valueTypes)
        /// </summary>
        private SequenceInfo KeyValueTuple {
            get {
                if (_keyValueTuple == null) {
                    _keyValueTuple = new SequenceInfo(
                        new[] { _keyTypes, _valueTypes },
                        ProjectState._tupleType
                    );
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

            public virtual void Update() {
            }
        }

        #region Specialized functions

        class DictionaryItemsBoundMethod : SpecializedDictionaryMethod {
            private ListInfo _list;

            internal DictionaryItemsBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                _myDict._valueTypes.AddDependency(unit);
                _myDict._keyTypes.AddDependency(unit);

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
                _myDict._valueTypes.AddDependency(unit);

                if (args.Length <= 1) {
                    return _myDict._valueTypes.Types;
                }

                return _myDict._valueTypes.Types.Union(args[1]);
            }
        }

        class DictionaryKeysListBoundMethod : SpecializedDictionaryMethod {
            private ListInfo _list;

            internal DictionaryKeysListBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                _myDict._keyTypes.AddDependency(unit);

                if (_list == null) {
                    _list = new ListInfo(new[] { _myDict._keyTypes }, unit.ProjectState._listType);
                }
                return _list;
            }

            public override void Update() {
                if (_list != null) {
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
                _myDict._valueTypes.AddDependency(unit);

                if (_list == null) {
                    _list = new ListInfo(new[] { _myDict._valueTypes }, unit.ProjectState._listType);
                }
                return _list;
            }
            
            public override void Update() {
                if (_list != null) {
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
                _myDict._keyTypes.AddDependency(unit);

                if (_list == null) {
                    _list = new IteratorInfo(new[] { _myDict._keyTypes }, unit.ProjectState._dictKeysType);
                }
                return _list;
            }

            public override void Update() {
                if (_list != null) {
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
                _myDict._valueTypes.AddDependency(unit);

                if (_list == null) {
                    _list = new IteratorInfo(new[] { _myDict._valueTypes }, unit.ProjectState._dictValuesType);
                }
                return _list;
            }

            public override void Update() {
                if (_list != null) {
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
                _myDict._valueTypes.AddDependency(unit);
                _myDict._keyTypes.AddDependency(unit);

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
                _myDict._valueTypes.AddDependency(unit);

                return _myDict._valueTypes.Types;
            }
        }

        class DictionaryKeyValueTupleBoundMethod : SpecializedDictionaryMethod {
            internal DictionaryKeyValueTupleBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method, myDict) {
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                _myDict._valueTypes.AddDependency(unit);
                _myDict._keyTypes.AddDependency(unit);

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
                            _myDict._valueTypes.AddTypes(unit, otherDict._valueTypes.Types);
                            _myDict._keyTypes.AddTypes(unit, otherDict._keyTypes.Types);
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
            foreach (var type in _keyTypes.Types) {
                if (type.Push()) {
                    sb.Append(type.ToString());
                    sb.Append(", ");
                    type.Pop();
                }
            }
            sb.Append("), values = (");
            foreach (var type in _valueTypes.Types) {
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
