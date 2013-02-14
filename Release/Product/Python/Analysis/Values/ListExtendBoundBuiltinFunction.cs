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
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents list.extend on a list with known type information.
    /// </summary>
    class ListExtendBoundBuiltinMethodInfo : BoundBuiltinMethodInfo {
        private readonly ListInfo _list;

        public ListExtendBoundBuiltinMethodInfo(ListInfo list, BuiltinMethodInfo method)
            : base(method) {
            _list = list;
        }

        public override INamespaceSet Call(Node node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 1) {
                foreach (var type in args[0]) {
                    _list.AppendItem(node, unit, type.GetEnumeratorTypes(node, unit));
                }
            }

            return ProjectState._noneInst.SelfSet;
        }
    }
}
