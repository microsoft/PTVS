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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class DictionaryInfo : BuiltinInstanceInfo {
        private readonly ISet<Namespace> _keyTypes;
        private readonly ISet<Namespace> _valueTypes;
        private SequenceInfo _keyValueTuple;
        private ISet<Namespace> _getMethod, _itemsMethod, _keysMethod, _valuesMethod, _iterKeysMethod, _iterValuesMethod, _popMethod, _popItemMethod, _iterItemsMethod;

        public DictionaryInfo(HashSet<Namespace> keyTypes, HashSet<Namespace> valueTypes, PythonAnalyzer projectState)
            : base(projectState._dictType) {
            _keyTypes = keyTypes;
            _valueTypes = valueTypes;
            _getMethod = null;
        }
        
        public override ISet<Namespace> GetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index) {
            return _valueTypes;
        }

        public override void SetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index, ISet<Namespace> value) {
            _keyTypes.UnionWith(index);
            _valueTypes.UnionWith(value);
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            ISet<Namespace> res = null;
            switch (name) {
                case "get":
                    res = GetOrMakeSpecializedMethod(ref _getMethod, "get", method => new DictionaryGetBoundMethod(method, this).SelfSet);
                    break;
                case "items":
                    res = GetOrMakeSpecializedMethod(ref _itemsMethod, "items", method => new DictionaryItemsBoundMethod(method, this).SelfSet);
                    break;
                case "keys":
                    if (unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _keysMethod, "keys", method => new DictionaryKeysIterableBoundMethod(method, this).SelfSet);
                    } else {
                        res = GetOrMakeSpecializedMethod(ref _keysMethod, "keys", method => new DictionaryKeysListBoundMethod(method, this).SelfSet);
                    }
                    break;
                case "values":
                    if (unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _valuesMethod, "values", method => new DictionaryValuesIterableBoundMethod(method, this).SelfSet);
                    } else {
                        res = GetOrMakeSpecializedMethod(ref _valuesMethod, "values", method => new DictionaryValuesListBoundMethod(method, this).SelfSet);
                    }
                    break;
                case "pop":
                    res = GetOrMakeSpecializedMethod(ref _popMethod, "pop", method => new DictionaryValuesBoundMethod(method, this).SelfSet);
                    break;
                case "popitem":
                    res = GetOrMakeSpecializedMethod(ref _popItemMethod, "popitem", method => new DictionaryKeyValueTupleBoundMethod(method, this).SelfSet);
                    break;
                case "iterkeys":
                    if (!unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _iterKeysMethod, "iterkeys", method => new DictionaryKeysIterableBoundMethod(method, this).SelfSet);
                    }
                    break;
                case "itervalues":
                    if (!unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _iterValuesMethod, "itervalues", method => new DictionaryValuesIterableBoundMethod(method, this).SelfSet);
                    }
                    break;
                case "iteritems":
                    if (!unit.ProjectState.LanguageVersion.Is3x()) {
                        res = GetOrMakeSpecializedMethod(ref _iterItemsMethod, "iteritems", method => new DictionaryItemsIterableBoundMethod(method, this).SelfSet);
                    }
                    break;
            }

            return res ?? base.GetMember(node, unit, name);
        }

        private ISet<Namespace> GetOrMakeSpecializedMethod(ref ISet<Namespace> method, string name, Func<BuiltinMethodInfo, ISet<Namespace>> maker) {
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
                Namespace keyType = _keyTypes.GetUnionType();
                string keyName = keyType == null ? null : keyType.ShortDescription;
                Namespace valueType = _valueTypes.GetUnionType();
                string valueName = valueType == null ? null : valueType.ShortDescription;

                if (keyName != null || valueName != null) {
                    return "dict({" +
                        (keyName ?? "unknown") +
                        " : " +
                        (valueName ?? "unknown") +
                        "}";
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
        
        #region Specialized functions

        class DictionaryItemsBoundMethod : BoundBuiltinMethodInfo {
            private readonly DictionaryInfo _myDict;
            private SequenceInfo _tuple, _list;

            internal DictionaryItemsBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method) {
                _myDict = myDict;
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                if (_list == null) {
                    _list = new ListInfo(
                        new[] { _myDict.KeyValueTuple },
                        unit.ProjectState._listType
                    );
                }

                return _list.SelfSet;
            }
        }

        class DictionaryGetBoundMethod : BoundBuiltinMethodInfo {
            private readonly DictionaryInfo _myDict;

            internal DictionaryGetBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method) {
                _myDict = myDict;
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                if (args.Length <= 1) {
                    return _myDict._valueTypes;
                }

                return _myDict._valueTypes.Union(args[1]);
            }
        }

        class DictionaryKeysListBoundMethod : BoundBuiltinMethodInfo {
            private readonly DictionaryInfo _myDict;
            private ListInfo _list;

            internal DictionaryKeysListBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method) {
                _myDict = myDict;
                
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                if (_list == null) {
                    _list = new ListInfo(new[] { _myDict._keyTypes }, unit.ProjectState._listType);
                }
                return _list;
            }
        }

        class DictionaryValuesListBoundMethod : BoundBuiltinMethodInfo {
            private readonly DictionaryInfo _myDict;
            private ListInfo _list;
            
            internal DictionaryValuesListBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method) {
                _myDict = myDict;
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                if (_list == null) {
                    _list = new ListInfo(new[] { _myDict._valueTypes }, unit.ProjectState._listType);
                }
                return _list;
            }
        }

        class DictionaryKeysIterableBoundMethod : BoundBuiltinMethodInfo {
            private readonly DictionaryInfo _myDict;
            private IterableInfo _list;

            internal DictionaryKeysIterableBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method) {
                _myDict = myDict;

            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                if (_list == null) {
                    _list = new IteratorInfo(new[] { _myDict._keyTypes }, unit.ProjectState._dictKeysType);
                }
                return _list;
            }
        }

        class DictionaryValuesIterableBoundMethod : BoundBuiltinMethodInfo {
            private readonly DictionaryInfo _myDict;
            private IterableInfo _list;

            internal DictionaryValuesIterableBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method) {
                _myDict = myDict;
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                if (_list == null) {
                    _list = new IteratorInfo(new[] { _myDict._valueTypes }, unit.ProjectState._dictValuesType);
                }
                return _list;
            }
        }

        class DictionaryItemsIterableBoundMethod : BoundBuiltinMethodInfo {
            private readonly DictionaryInfo _myDict;
            private IterableInfo _list;

            internal DictionaryItemsIterableBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method) {
                _myDict = myDict;
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                if (_list == null) {
                    _list = new IteratorInfo(new[] { _myDict._keyValueTuple }, unit.ProjectState._dictValuesType);
                }
                return _list;
            }
        }

        class DictionaryValuesBoundMethod : BoundBuiltinMethodInfo {
            private readonly DictionaryInfo _myDict;

            internal DictionaryValuesBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method) {
                _myDict = myDict;
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                return _myDict._valueTypes;
            }
        }

        class DictionaryKeyValueTupleBoundMethod : BoundBuiltinMethodInfo {
            private readonly DictionaryInfo _myDict;

            internal DictionaryKeyValueTupleBoundMethod(BuiltinMethodInfo method, DictionaryInfo myDict)
                : base(method) {
                _myDict = myDict;
            }

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                return _myDict.KeyValueTuple;
            }
        }

        #endregion
    }
}
