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

using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Specialized built-in instance for sequences (lists, tuples)
    /// </summary>
    internal class IteratorInfo : IterableInfo {
        private IterBoundBuiltinMethodInfo _iter;
        private NextBoundMethod _next;

        internal static BuiltinClassInfo GetIteratorTypeFromType(BuiltinClassInfo klass, AnalysisUnit unit) {
            switch (klass.PythonType.TypeId) {
                case BuiltinTypeId.List:
                    return unit.ProjectState._listIteratorType;
                case BuiltinTypeId.Tuple:
                    return unit.ProjectState._tupleIteratorType;
                case BuiltinTypeId.Set:
                    return unit.ProjectState._setIteratorType;
                case BuiltinTypeId.Str:
                    return unit.ProjectState._strIteratorType;
                case BuiltinTypeId.Bytes:
                    return unit.ProjectState._bytesIteratorType;
                case BuiltinTypeId.Generator:
                case BuiltinTypeId.DictKeys:
                case BuiltinTypeId.DictValues:
                case BuiltinTypeId.DictItems:
                case BuiltinTypeId.ListIterator:
                case BuiltinTypeId.TupleIterator:
                case BuiltinTypeId.SetIterator:
                case BuiltinTypeId.StrIterator:
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

        public override INamespaceSet GetIterator(Node node, AnalysisUnit unit) {
            return SelfSet;
        }

        public override INamespaceSet GetMember(Node node, AnalysisUnit unit, string name) {
            if (unit.ProjectState.LanguageVersion.Is2x() && name == "next" ||
                unit.ProjectState.LanguageVersion.Is3x() && name == "__next__") {
                if (_next == null) {
                    var next = this._type.GetMember(unit.ProjectEntry.MyScope.InterpreterContext, name);
                    if (next != null) {
                        _next = new NextBoundMethod((BuiltinMethodInfo)unit.ProjectState.GetNamespaceFromObjects(next), this);
                    }
                }

                if (_next != null) {
                    return _next.SelfSet;
                }
            } else if (name == "__iter__") {
                if (_iter == null) {
                    var iter = this._type.GetMember(unit.ProjectEntry.MyScope.InterpreterContext, name);
                    if (iter != null) {
                        _iter = new IterBoundBuiltinMethodInfo((BuiltinMethodInfo)unit.ProjectState.GetNamespaceFromObjects(iter), this);
                    }
                }

                if (_iter != null) {
                    return _iter.SelfSet;
                }
            }
            return base.GetMember(node, unit, name);
        }

        class NextBoundMethod : BoundBuiltinMethodInfo {
            private readonly IteratorInfo _myIter;

            internal NextBoundMethod(BuiltinMethodInfo method, IteratorInfo myDict)
                : base(method) {
                _myIter = myDict;
            }

            public override INamespaceSet Call(Node node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgNames) {
                return _myIter.UnionType;
            }
        }

    }
}
