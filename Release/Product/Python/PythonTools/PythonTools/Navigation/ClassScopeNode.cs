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
            get { return _klass.Start; }
        }

        public SourceLocation End {
            get { return _klass.End; }
        }

        public IEnumerable<IScopeNode> NestedScopes {
            get {
                return AstScopeNode.EnumerateBody(_klass.Body);
            }
        }

        #endregion
    }
}
