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
