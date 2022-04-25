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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Core.Text;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public abstract class Node {

        #region Public API
        public int EndIndex {
            get => IndexSpan.End;
            set => IndexSpan = new IndexSpan(IndexSpan.Start, value - IndexSpan.Start);
        }

        public int StartIndex {
            get => IndexSpan.Start;
            set => IndexSpan = new IndexSpan(value, 0);
        }

        public abstract IEnumerable<Node> GetChildNodes();
        public abstract void Walk(PythonWalker walker);
        public abstract Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default);

        public virtual string NodeName => GetType().Name;

        public string ToCodeString(PythonAst ast) => ToCodeString(ast, CodeFormattingOptions.Default);

        public string ToCodeString(PythonAst ast, CodeFormattingOptions format) {
            var sb = new StringBuilder();
            AppendCodeString(sb, ast, format);
            var result = sb.ToString();
            return string.IsInterned(result) ?? result;
        }

        public SourceLocation GetStart(PythonAst ast) => ast.IndexToLocation(StartIndex);

        public SourceLocation GetEnd(PythonAst ast) => ast.IndexToLocation(EndIndex);

        public SourceSpan GetSpan(PythonAst ast) => new SourceSpan(GetStart(ast), GetEnd(ast));

        /// <summary>
        /// Returns the proceeding whitespace (newlines and comments) that
        /// shows up before this node.
        /// 
        /// New in 1.1.
        /// </summary>
        public virtual string GetLeadingWhiteSpace(PythonAst ast) => this.GetPreceedingWhiteSpaceDefaultNull(ast) ?? "";

        /// <summary>
        /// Sets the proceeding whitespace (newlines and comments) that shows up
        /// before this node.
        /// </summary>
        /// <param name="ast"></param>
        /// <param name="whiteSpace"></param>
        public virtual void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) => ast.SetAttribute(this, NodeAttributes.PreceedingWhiteSpace, whiteSpace);
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
            var tmp = new StringBuilder();
            AppendCodeString(tmp, ast, format);
            for (var curChar = 0; curChar < tmp.Length; curChar++) {
                if (!char.IsWhiteSpace(tmp[curChar])) {
                    res.Append(tmp.ToString(curChar, tmp.Length - curChar));
                    break;
                }
            }
        }

        public void SetLoc(int start, int end) => IndexSpan = new IndexSpan(start, end >= start ? end - start : start);
        public void SetLoc(IndexSpan span) => IndexSpan = span;

        public IndexSpan IndexSpan { get; set; }

        internal virtual string GetDocumentation(Statement/*!*/ stmt) => stmt.Documentation;

        internal static PythonReference GetVariableReference(Node node, PythonAst ast) {
            if (ast.TryGetAttribute(node, NodeAttributes.VariableReference, out var reference)) {
                return (PythonReference)reference;
            }
            return null;
        }

        internal static PythonReference[] GetVariableReferences(Node node, PythonAst ast) {
            if (ast.TryGetAttribute(node, NodeAttributes.VariableReference, out var reference)) {
                return (PythonReference[])reference;
            }
            return null;
        }

        #endregion
    }
}
