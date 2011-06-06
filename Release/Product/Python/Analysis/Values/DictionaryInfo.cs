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

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class DictionaryInfo : BuiltinInstanceInfo {
        private readonly ISet<Namespace> _keyTypes;
        private readonly ISet<Namespace> _valueTypes;
        private ISet<Namespace> _getMethod;

        public DictionaryInfo(HashSet<Namespace> keyTypes, HashSet<Namespace> valueTypes, PythonAnalyzer projectState)
            : base(projectState._dictType) {
            _keyTypes = keyTypes;
            _valueTypes = valueTypes;
            _getMethod = null;
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

        public override ISet<Namespace> GetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index) {
            return _valueTypes;
        }

        public override void SetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index, ISet<Namespace> value) {
            _keyTypes.UnionWith(index);
            _valueTypes.UnionWith(value);
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            if (name == "get") {
                if (_getMethod == null) {
                    ISet<Namespace> getMeth;
                    if (TryGetMember("get", out getMeth)) {
                        _getMethod = new DictionaryGetBoundMethod((BuiltinMethodInfo)getMeth.First(), this).SelfSet;
                    }
                }
                return _getMethod;
            }

            return base.GetMember(node, unit, name);
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
    }
}
