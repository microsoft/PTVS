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

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
    abstract class SelectionTarget {
        public bool ContainsReturn;

        public SelectionTarget(Dictionary<ScopeStatement, SourceLocation> insertLocations, ScopeStatement[] parents, PythonAst ast) {
            Parents = parents;
            InsertLocations = insertLocations;
            Ast = ast ?? throw new ArgumentNullException(nameof(ast));
        }

        public Dictionary<ScopeStatement, SourceLocation> InsertLocations { get; }
        public IReadOnlyList<ScopeStatement> Parents { get; }
        public PythonAst Ast { get; }

        public abstract Statement GetBody();
        public abstract Node GetNode();

        public virtual IEnumerable<Statement> GetStatementsAfter() {
            return new Statement[0];
        }

        public abstract SourceLocation StartIncludingLeadingWhiteSpace { get; }

        /// <summary>
        /// Returns the start of the selection including any indentation on the current line, but
        /// excluding any previous lines of pure white space or comments.
        /// </summary>
        public abstract SourceLocation StartIncludingIndentation { get; }

        public abstract SourceLocation End { get; }

        /// <summary>
        /// True if we're in an expression context, false otherwise.
        /// </summary>
        public abstract bool IsExpression { get; }

        public abstract string IndentationLevel { get; }

        public virtual bool IsValidSelection => true;

        public abstract void Walk(PythonWalker walker);
    }

    class NodeTarget : SelectionTarget {
        private readonly Expression _node;

        public NodeTarget(Dictionary<ScopeStatement, SourceLocation> insertLocations, ScopeStatement[] parents, Expression node)
            : base(insertLocations, parents, parents[0] as PythonAst) {
            _node = node;
        }

        public override bool IsExpression => true;

        public override Node GetNode() => _node;

        public override Statement GetBody() {
            var retStmt = new ReturnStatement(_node);
            retStmt.RoundTripRemoveValueWhiteSpace(Ast);
            return retStmt;
        }
        
        public override SourceLocation StartIncludingLeadingWhiteSpace => _node.GetStartIncludingLeadingWhiteSpace(Ast);

        public override SourceLocation StartIncludingIndentation => _node.GetStartIncludingIndentation(Ast);

        public override SourceLocation End => _node.GetEnd(Ast);

        public override string IndentationLevel => _node.GetIndentationLevel(Parents[0] as PythonAst);

        public override void Walk(PythonWalker walker) {
            _node.Walk(walker);
        }
    }

    class SuiteTarget : SelectionTarget {
        private readonly SuiteStatement _suite;
        private readonly SuiteStatement[] _followingSuites;
        private readonly int _start, _end;
        private readonly int _selectionEnd;

        public SuiteTarget(
            Dictionary<ScopeStatement, SourceLocation> insertLocations,
            ScopeStatement[] parents,
            SuiteStatement suite,
            SuiteStatement[] followingSuites,
            int selectionEnd,
            int startIndex,
            int endIndex
        )
            : base(insertLocations, parents, parents[0] as PythonAst) {
            _suite = suite;
            _start = startIndex;
            _end = endIndex;
            _followingSuites = followingSuites;
            _selectionEnd = selectionEnd;
        }

        public override bool IsExpression => false;

        public override Node GetNode() {
            if (_suite.Statements.Count == 0) {
                return _suite;
            }
            return _suite.CloneSubset(Ast, _start, _end);
        }

        public override Statement GetBody() {
            if (_suite.Statements.Count == 0) {
                return _suite;
            }
            return _suite.CloneSubset(Ast, _start, _end);
        }

        public override IEnumerable<Statement> GetStatementsAfter() {
            yield return _suite.CloneSubset(Ast, _end + 1, _suite.Statements.Count - 1);

            foreach (var suite in _followingSuites) {
                foreach (var stmt in suite.Statements) {
                    if (stmt.StartIndex > _selectionEnd) {
                        yield return stmt;
                    }
                }
            }
        }

        public override SourceLocation StartIncludingLeadingWhiteSpace {
            get {
                if (_suite.Statements.Count == 0) {
                    return _suite.GetStartIncludingLeadingWhiteSpace(Ast);
                }
                return _suite.Statements[_start].GetStartIncludingLeadingWhiteSpace(Ast);
            }
        }

        public override SourceLocation StartIncludingIndentation {
            get {
                if (_suite.Statements.Count == 0) {
                    return _suite.GetStartIncludingIndentation(Ast);
                }
                return _suite.Statements[_start].GetStartIncludingIndentation(Ast); 
            }
        }

        public override SourceLocation End {
            get {
                if (_suite.Statements.Count == 0) {
                    return _suite.GetEnd(Ast);
                }
                return _suite.Statements[_end].GetEnd(Ast); 
            }
        }

        public override string IndentationLevel {
            get {
                if (_suite.Statements.Count == 0) {
                    return "";
                }
                return _suite.Statements[_start].GetIndentationLevel(Parents[0] as PythonAst);
            }
        }

        public override bool IsValidSelection {
            get {
                return _start <= _end;
            }
        }

        public override void Walk(PythonWalker walker) {
            for (int i = _start; i <= _end; i++) {
                _suite.Statements[i].Walk(walker);
            }
        }
    }

    static class NodeExtensions {
        internal static SourceLocation GetStartIncludingIndentation(this Node self, PythonAst ast) {
            return ast.IndexToLocation(self.StartIndex - (self.GetIndentationLevel(ast) ?? "").Length);
        }
        internal static SourceLocation GetStartIncludingLeadingWhiteSpace(this Node self, PythonAst ast) {
            return ast.IndexToLocation(self.StartIndex - (self.GetLeadingWhiteSpace(ast) ?? "").Length);
        }
    }
}
