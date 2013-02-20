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
            return ToCodeString(ast, CodeFormattingOptions.Default);
        }

        public string ToCodeString(PythonAst ast, CodeFormattingOptions format) {
            StringBuilder res = new StringBuilder();
            AppendCodeString(res, ast, format);
            return res.ToString();
        }

        public SourceLocation GetStart(PythonAst parent) {
            return parent.IndexToLocation(StartIndex);            
        }

        public SourceLocation GetEnd(PythonAst parent) {            
            return parent.IndexToLocation(EndIndex);
        }

        public SourceSpan GetSpan(PythonAst parent) {
            return new SourceSpan(GetStart(parent), GetEnd(parent));
        }

        public static void CopyLeadingWhiteSpace(PythonAst parentNode, Node fromNode, Node toNode) {
            parentNode.SetAttribute(toNode, NodeAttributes.PreceedingWhiteSpace, fromNode.GetLeadingWhiteSpace(parentNode));
        }

        public static void CopyTrailingNewLine(PythonAst parentNode, Node fromNode, Node toNode) {
            parentNode.SetAttribute(toNode, NodeAttributes.TrailingNewLine, fromNode.GetTrailingNewLine(parentNode));
        }

        /// <summary>
        /// Returns the proceeeding whitespace (newlines and comments) that
        /// shows up before this node.
        /// 
        /// New in 1.1.
        /// </summary>
        public virtual string GetLeadingWhiteSpace(PythonAst ast) {
            return this.GetProceedingWhiteSpaceDefaultNull(ast) ?? "";
        }

        /// <summary>
        /// Gets the indentation level for the current statement.  Requires verbose
        /// mode when parsing the trees.
        /// </summary>
        public string GetIndentationLevel(PythonAst parentNode) {
            var leading = GetLeadingWhiteSpace(parentNode);
            // we only want the trailing leading space for the current line...
            for (int i = leading.Length - 1; i >= 0; i--) {
                if (leading[i] == '\r' || leading[i] == '\n') {
                    leading = leading.Substring(i + 1);
                    break;
                }
            }
            return leading;
        }

        #endregion
        
        #region Internal APIs

        /// <summary>
        /// Appends the code representation of the node to the string builder.
        /// </summary>
        internal abstract void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format);

        /// <summary>
        /// Appends the code representation of the node to the string builder, replacing the initial whitespace.
        /// 
        /// If initialWhiteSpace is null then the default whitespace is used.
        /// </summary>
        internal virtual void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string leadingWhiteSpace) {
            if (leadingWhiteSpace == null) {
                AppendCodeString(res, ast, format);
                return;
            }
            res.Append(leadingWhiteSpace);
            StringBuilder tmp = new StringBuilder();
            AppendCodeString(tmp, ast, format);
            for (int curChar = 0; curChar < tmp.Length; curChar++) {
                if (!char.IsWhiteSpace(tmp[curChar])) {
                    res.Append(tmp.ToString(curChar, tmp.Length - curChar));
                    break;
                }
            }
        }

        internal void SetLoc(int start, int end) {
            _span = new IndexSpan(start, end >= start ? end - start : start);
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

        internal static PythonReference GetVariableReference(Node node, PythonAst ast) {
            object reference;
            if (ast.TryGetAttribute(node, NodeAttributes.VariableReference, out reference)) {
                return (PythonReference)reference;
            }
            return null;
        }

        internal static PythonReference[] GetVariableReferences(Node node, PythonAst ast) {
            object reference;
            if (ast.TryGetAttribute(node, NodeAttributes.VariableReference, out reference)) {
                return (PythonReference[])reference;
            }
            return null;
        }

        #endregion
    }
}
