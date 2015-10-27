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
            get { return AstScopeNode.FromPythonSourceLocation(_func.GetStart(_func.GlobalParent)); }
        }

        public SourceLocation End {
            get { return AstScopeNode.FromPythonSourceLocation(_func.GetEnd(_func.GlobalParent)); }
        }

        public IEnumerable<IScopeNode> NestedScopes {
            get { return AstScopeNode.EnumerateBody(_func.GlobalParent, _func.Body, false); }
        }

        #endregion
    }
}
