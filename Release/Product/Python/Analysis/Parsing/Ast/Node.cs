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

using System.Diagnostics;

namespace Microsoft.PythonTools.Parsing.Ast {
    public abstract class Node {
        private ScopeStatement _parent;
        private IndexSpan _span;

        protected Node() {
        }

        #region Public API

        public ScopeStatement Parent {
            get { return _parent; }
            set { _parent = value; }
        }

        internal void SetLoc(PythonAst globalParent, int start, int end) {
            _span = new IndexSpan(start, end > start ? end - start : start);
            _parent = globalParent;
        }

        internal void SetLoc(PythonAst globalParent, IndexSpan span) {
            _span = span;
            _parent = globalParent;
        }

        internal IndexSpan IndexSpan {
            get {
                return _span;
            }
            set {
                _span = value;
            }
        }

        public int EndIndex {
            get {
                return _span.End;
            }
            set {
                _span = new IndexSpan(_span.Start, value - _span.Start);
            }
        }

        public int StartIndex {
            get {
                return _span.Start;
            }
            set {
                _span = new IndexSpan(value, 0);
            }
        }

        public abstract void Walk(PythonWalker walker);

        public virtual string NodeName {
            get {
                return GetType().Name;
            }
        }

        #endregion

        #region Base Class Overrides

        public override string ToString() {
            return GetType().Name;
        }

        #endregion

        public SourceLocation Start {
            get {
                return GlobalParent.IndexToLocation(StartIndex);
            }
        }

        public SourceLocation End {
            get {
                return GlobalParent.IndexToLocation(EndIndex);
            }
        }

        public SourceSpan Span {
            get {
                return new SourceSpan(Start, End);
            }
        }

        #region Internal APIs

        internal PythonAst GlobalParent {
            get {
                Node cur = this;
                while (!(cur is PythonAst)) {
                    Debug.Assert(cur != null);
                    cur = cur.Parent;
                }
                return (PythonAst)cur;
            }
        }

        internal virtual string GetDocumentation(Statement/*!*/ stmt) {
            return stmt.Documentation;
        }

        #endregion
    }
}
