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
    class ClassScopeNode : IScopeNode {
        private readonly ClassDefinition _klass;

        public ClassScopeNode(ClassDefinition klass) {
            _klass = klass;
        }

        public ClassDefinition Definition {
            get {
                return _klass;
            }
        }

        #region IScopeNode Members

        public LibraryNodeType NodeType {
            get {
                return LibraryNodeType.Classes;
            }
        }

        public string Name {
            get { return _klass.Name; }
        }

        public string Description {
            get { return _klass.Body.Documentation; }
        }

        public SourceLocation Start {
            get { return AstScopeNode.FromPythonSourceLocation(_klass.GetStart(_klass.GlobalParent)); }
        }

        public SourceLocation End {
            get { return AstScopeNode.FromPythonSourceLocation(_klass.GetEnd(_klass.GlobalParent)); }
        }

        public IEnumerable<IScopeNode> NestedScopes {
            get {
                return AstScopeNode.EnumerateBody(_klass.GlobalParent, _klass.Body);
            }
        }

        #endregion
    }
}
