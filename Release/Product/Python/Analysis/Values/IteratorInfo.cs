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
using System.Text;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Specialized built-in instance for sequences (lists, tuples)
    /// </summary>
    internal class IteratorInfo : IterableInfo {
        private NextBoundMethod _next;

        public IteratorInfo(VariableDef[] indexTypes, BuiltinClassInfo seqType)
            : base(indexTypes, seqType) {
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            if (name == "next") {
                if (_next == null) {
                    var next = this._type.GetMember(unit.ProjectEntry.MyScope.InterpreterContext, "next");
                    if (next != null) {
                        _next = new NextBoundMethod((BuiltinMethodInfo)unit.ProjectState.GetNamespaceFromObjects(next), this);
                    }
                }

                if (_next != null) {
                    return _next.SelfSet;
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

            public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
                return _myIter.UnionType;
            }
        }

    }
}
