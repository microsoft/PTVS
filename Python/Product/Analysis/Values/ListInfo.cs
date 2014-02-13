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

using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents a list object with tracked type information.
    /// </summary>
    class ListInfo : SequenceInfo {
        private AnalysisValue _appendMethod, _popMethod, _insertMethod, _extendMethod;

        public ListInfo(VariableDef[] indexTypes, BuiltinClassInfo seqType, Node node, ProjectEntry entry)
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

            return unit.ProjectState._noneInst;
        }

        private IAnalysisSet ListPop(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return UnionType;
        }

        private IAnalysisSet ListInsert(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 2) {
                AppendItem(node, unit, args[1]);
            }

            return unit.ProjectState._noneInst;
        }

        private IAnalysisSet ListExtend(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 1) {
                foreach (var type in args[0]) {
                    AppendItem(node, unit, type.GetEnumeratorTypes(node, unit));
                }
            }

            return unit.ProjectState._noneInst;
        }

        private void AppendItem(Node node, AnalysisUnit unit, IAnalysisSet set) {
            if (IndexTypes.Length == 0) {
                IndexTypes = new[] { new VariableDef() };
            }

            IndexTypes[0].MakeUnionStrongerIfMoreThan(ProjectState.Limits.IndexTypes, set);
            IndexTypes[0].AddTypes(unit, set);

            UnionType = null;
        }
    }
}
