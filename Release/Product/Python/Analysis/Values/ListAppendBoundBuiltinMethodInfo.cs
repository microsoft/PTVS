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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents list.append on a list with known type information.
    /// </summary>
    class ListAppendBoundBuiltinMethodInfo : BoundBuiltinMethodInfo {
        private readonly ListInfo _list;

        public ListAppendBoundBuiltinMethodInfo(ListInfo list, BuiltinMethodInfo method)
            : base(method) {
            _list = list;
        }

        public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, string[] keywordArgNames) {
            if (args.Length == 1) {
                _list.AppendItem(args[0]);
            }

            return ProjectState._noneInst.SelfSet;
        }
    }
}
