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
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudioTools.Navigation;
using Microsoft.VisualStudioTools.Parsing;

namespace Microsoft.PythonTools.Navigation {
    class AssignmentScopeNode : IScopeNode {
        private readonly AssignmentStatement _assign;
        private readonly NameExpression _name;
        private readonly PythonAst _ast;
        private static readonly IScopeNode[] EmptyScopeNodes = new IScopeNode[0];

        public AssignmentScopeNode(PythonAst ast, AssignmentStatement assign, NameExpression name) {
            _assign = assign;
            _name = name;
            _ast = ast;
        }

        #region IScopeNode Members

        public LibraryNodeType NodeType {
            get {
                return LibraryNodeType.Members;
            }
        }

        public string Name {
            get { return _name.Name; }
        }

        public string Description {
            get { return ""; }
        }

        public SourceLocation Start {
            get { return AstScopeNode.FromPythonSourceLocation(_name.GetStart(_ast)); }
        }

        public SourceLocation End {
            get { return AstScopeNode.FromPythonSourceLocation(_name.GetEnd(_ast)); }
        }

        public IEnumerable<IScopeNode> NestedScopes {
            get { return EmptyScopeNodes;  }
        }

        #endregion
    }
}
