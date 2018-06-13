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

using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents a list object with tracked type information.
    /// </summary>
    class ListInfo : SequenceInfo {
        private AnalysisValue _appendMethod, _popMethod, _insertMethod, _extendMethod;

        public ListInfo(VariableDef[] indexTypes, BuiltinClassInfo seqType, Node node, IPythonProjectEntry entry)
            : base(indexTypes, seqType, node, entry) {
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            // Must unconditionally call the base implementation of GetMember
            var res = base.GetMember(node, unit, name);

            switch (name) {
                case "append":
                    return _appendMethod = _appendMethod ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        ListAppend,
                        false
                    );
                case "pop":
                    return _popMethod = _popMethod ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        ListPop,
                        false
                    );
                case "insert":
                    return _insertMethod = _insertMethod ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        ListInsert,
                        false
                    );
                case "extend":
                    return _extendMethod = _extendMethod ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        ListExtend,
                        false
                    );
            }

            return res;
        }

        private IAnalysisSet ListAppend(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 1) {
                AppendItem(node, unit, args[0]);
            }

            return unit.State._noneInst;
        }

        private IAnalysisSet ListPop(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return UnionType;
        }

        private IAnalysisSet ListInsert(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 2) {
                AppendItem(node, unit, args[1]);
            }

            return unit.State._noneInst;
        }

        private IAnalysisSet ListExtend(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 1) {
                foreach (var type in args[0]) {
                    AppendItem(node, unit, type.GetEnumeratorTypes(node, unit));
                }
            }

            return unit.State._noneInst;
        }

        private void AppendItem(Node node, AnalysisUnit unit, IAnalysisSet set) {
            if (IndexTypes.Length == 0) {
                IndexTypes = new[] { new VariableDef() };
            }

            IndexTypes[0].MakeUnionStrongerIfMoreThan(ProjectState.Limits.IndexTypes, set);
            IndexTypes[0].AddTypes(unit, set, true, DeclaringModule);

            UnionType = null;
        }

        internal override IAnalysisSet Resolve(AnalysisUnit unit, ResolutionContext context) {
            var res = base.Resolve(unit, context);
            if (res is ProtocolInfo pi) {
                pi.RemoveProtocol<NameProtocol>(n => true);
                pi.AddProtocol(new NameProtocol(pi, ProjectState.Types[BuiltinTypeId.List]));
                pi.AddProtocol(new GetItemProtocol(pi, unit.State.ClassInfos[BuiltinTypeId.Int].Instance, pi.GetEnumeratorTypes(_node, unit)));
            }
            return res;
        }
    }
}
