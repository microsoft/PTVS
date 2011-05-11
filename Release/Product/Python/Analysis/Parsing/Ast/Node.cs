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
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public abstract class Node {
        private IndexSpan _span;

        internal Node() {
        }

        #region Public API

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

        public string ToCodeString(PythonAst ast) {
            StringBuilder res = new StringBuilder();
            AppendCodeString(res, ast);
            return res.ToString();
        }

        public SourceLocation GetStart(PythonAst parent) {
            return parent.IndexToLocation(StartIndex);            
        }

        public SourceLocation GetEnd(PythonAst parent)  {            
            return parent.IndexToLocation(EndIndex);
        }

        public SourceSpan GetSpan(PythonAst parent) {
            return new SourceSpan(GetStart(parent), GetEnd(parent));
        }

        #endregion
        
        #region Internal APIs

        /// <summary>
        /// Appends the code representation of the node to the string builder.
        /// </summary>
        internal abstract void AppendCodeString(StringBuilder res, PythonAst ast);

        internal void SetLoc(int start, int end) {
            _span = new IndexSpan(start, end > start ? end - start : start);
        }

        internal void SetLoc(IndexSpan span) {
            _span = span;
        }

        internal IndexSpan IndexSpan {
            get {
                return _span;
            }
            set {
                _span = value;
            }
        }

        internal virtual string GetDocumentation(Statement/*!*/ stmt) {
            return stmt.Documentation;
        }

        #endregion
    }
}
