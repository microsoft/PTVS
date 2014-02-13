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
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Specialized built-in instance for sequences (lists, tuples)
    /// </summary>
    internal class IteratorInfo : IterableInfo {
        private AnalysisValue _iter;
        private AnalysisValue _next;

        internal static BuiltinClassInfo GetIteratorTypeFromType(BuiltinClassInfo klass, AnalysisUnit unit) {
            switch (klass.PythonType.TypeId) {
                case BuiltinTypeId.List:
                    return unit.ProjectState.ClassInfos[BuiltinTypeId.ListIterator];
                case BuiltinTypeId.Tuple:
                    return unit.ProjectState.ClassInfos[BuiltinTypeId.TupleIterator];
                case BuiltinTypeId.Set:
                    return unit.ProjectState.ClassInfos[BuiltinTypeId.SetIterator];
                case BuiltinTypeId.Str:
                    return unit.ProjectState.ClassInfos[BuiltinTypeId.StrIterator];
                case BuiltinTypeId.Unicode:
                    return unit.ProjectState.ClassInfos[BuiltinTypeId.UnicodeIterator];
                case BuiltinTypeId.Bytes:
                    return unit.ProjectState.ClassInfos[BuiltinTypeId.BytesIterator];
                case BuiltinTypeId.Generator:
                case BuiltinTypeId.DictKeys:
                case BuiltinTypeId.DictValues:
                case BuiltinTypeId.DictItems:
                case BuiltinTypeId.ListIterator:
                case BuiltinTypeId.TupleIterator:
                case BuiltinTypeId.SetIterator:
                case BuiltinTypeId.StrIterator:
                case BuiltinTypeId.UnicodeIterator:
                case BuiltinTypeId.BytesIterator:
                case BuiltinTypeId.CallableIterator:
                    return klass;
                default:
                    return null;
            }
        }

        public IteratorInfo(VariableDef[] indexTypes, BuiltinClassInfo iterType, Node node)
            : base(indexTypes, iterType, node) {
        }

        public override IAnalysisSet GetIterator(Node node, AnalysisUnit unit) {
            return SelfSet;
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            // Must unconditionally call the base implementation of GetMember
            var res = base.GetMember(node, unit, name);

            if (unit.ProjectState.LanguageVersion.Is2x() && name == "next" ||
                unit.ProjectState.LanguageVersion.Is3x() && name == "__next__") {
                return _next = _next ?? new SpecializedCallable(
                    res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                    IteratorNext,
                    false
                );
            } else if (name == "__iter__") {
                return _iter = _iter ?? new SpecializedCallable(
                    res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                    IteratorIter,
                    false
                );
            }
            return res;
        }

        private IAnalysisSet IteratorIter(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return this;
        }

        private IAnalysisSet IteratorNext(Node node, Analysis.AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return UnionType;
        }
    }
}
