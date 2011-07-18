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
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Navigation {
    class FunctionScopeNode : IScopeNode {
        private readonly FunctionDefinition _func;

        public FunctionScopeNode(FunctionDefinition func) {
            _func = func;
        }

        public FunctionDefinition Definition {
            get {
                return _func;
            }
        }

        #region IScopeNode Members

        public LibraryNodeType NodeType {
            get {
                return LibraryNodeType.Members;
            }
        }

        public string Name {
            get { return _func.Name; }
        }

        public string Description {
            get { return _func.Body.Documentation; }
        }

        public SourceLocation Start {
            get { return _func.GetStart(_func.GlobalParent); }
        }

        public SourceLocation End {
            get { return _func.GetEnd(_func.GlobalParent); }
        }

        public IEnumerable<IScopeNode> NestedScopes {
            get { return AstScopeNode.EnumerateBody(_func.GlobalParent, _func.Body, false); }
        }

        #endregion
    }
}
