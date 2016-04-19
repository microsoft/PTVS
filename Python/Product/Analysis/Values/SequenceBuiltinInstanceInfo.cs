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

using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class SequenceBuiltinInstanceInfo : BuiltinInstanceInfo {
        private readonly bool _supportsMod;
        private readonly IAnalysisSet _indexTypes;
        private AnalysisValue _iterMethod, _iterator;

        public SequenceBuiltinInstanceInfo(BuiltinClassInfo klass, bool sequenceOfSelf, bool supportsMod)
            : base(klass) {
            _supportsMod = supportsMod;

            var seqInfo = klass as SequenceBuiltinClassInfo;
            if (seqInfo != null) {
                _indexTypes = seqInfo.IndexTypes;
            } else if (sequenceOfSelf) {
                _indexTypes = SelfSet;
            } else {
                _indexTypes = AnalysisSet.Empty;
            }
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            return _indexTypes;
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            return _indexTypes;
        }

        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) {
            var res = base.GetTypeMember(node, unit, name);

            if (name == "__iter__") {
                return _iterMethod = _iterMethod ?? new SpecializedCallable(
                    res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                    SequenceIter,
                    false
                );
            }

            return res;
        }
        private IAnalysisSet SequenceIter(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (_iterator == null) {
                var types = new [] { new VariableDef() };
                types[0].AddTypes(unit, _indexTypes, false);
                _iterator = new IteratorInfo(types, IteratorInfo.GetIteratorTypeFromType(ClassInfo, unit), node);
            }
            return _iterator ?? AnalysisSet.Empty;
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, Parsing.PythonOperator operation, IAnalysisSet rhs) {
            var res = AnalysisSet.Empty;
            switch (operation) {
                case PythonOperator.Add:
                    foreach (var type in rhs) {
                        if (type.IsOfType(ClassInfo)) {
                            res = res.Union(ClassInfo.Instance);
                        } else {
                            res = res.Union(type.ReverseBinaryOperation(node, unit, operation, SelfSet));
                        }
                    }
                    break;
                case PythonOperator.Mod:
                    if (_supportsMod) {
                        res = SelfSet;
                    }
                    break;
                case PythonOperator.Multiply:
                    foreach (var type in rhs) {
                        if (type.IsOfType(ProjectState.ClassInfos[BuiltinTypeId.Int]) || type.IsOfType(ProjectState.ClassInfos[BuiltinTypeId.Long])) {
                            res = res.Union(ClassInfo.Instance);
                        } else {
                            var partialRes = ConstantInfo.NumericOp(node, this, unit, operation, rhs);
                            if (partialRes != null) {
                                res = res.Union(partialRes);
                            }
                        }
                    }
                    break;
            }
            return res ?? base.BinaryOperation(node, unit, operation, rhs);
        }

        public override string ToString() {
            return Description;
        }

        public override string Description {
            get {
                if (_indexTypes == this) {
                    return _type.Name;
                } else {
                    return IterableInfo.MakeDescription(this, _type.Name, _indexTypes);
                }
            }
        }

    }
}
