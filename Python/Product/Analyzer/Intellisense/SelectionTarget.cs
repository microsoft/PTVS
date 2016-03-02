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

namespace Microsoft.PythonTools.Intellisense {
    abstract class SelectionTarget {
        private readonly ScopeStatement[] _parents;
        private readonly Dictionary<ScopeStatement, int> _insertLocations;
        public bool ContainsReturn;

        public SelectionTarget(Dictionary<ScopeStatement, int> insertLocations, ScopeStatement[] parents) {
            _parents = parents;
            _insertLocations = insertLocations;
        }

        public Dictionary<ScopeStatement, int> InsertLocations {
            get {
                return _insertLocations;
            }
        }

        public ScopeStatement[] Parents {
            get {
                return _parents;
            }
        }

        public abstract Statement GetBody(PythonAst root);
        public abstract Node GetNode(PythonAst root);

        public virtual IEnumerable<Statement> GetStatementsAfter(PythonAst root) {
            return new Statement[0];
        }

        /// <summary>
        /// Returns the start of the selection including any indentation on the current line, but
        /// excluding any previous lines of pure white space or comments.
        /// </summary>
        public abstract int StartIncludingIndentation {
            get;
        }

        public abstract int End {
            get;
        }

        /// <summary>
        /// True if we're in an expression context, false otherwise.
        /// </summary>
        public abstract bool IsExpression {
            get;
        }

        public virtual string InvalidExtractionMessage {
            get {
                return null;
            }
        }

        public abstract string IndentationLevel {
            get;
        }

        public virtual bool IsValidSelection {
            get {
                return true;
            }
        }

        public abstract void Walk(PythonWalker walker);
    }

    class NodeTarget : SelectionTarget {
        private readonly Node _node;

        public NodeTarget(Dictionary<ScopeStatement, int> insertLocations, ScopeStatement[] parents, Node node)
            : base(insertLocations, parents) {
            _node = node;
        }

        public override bool IsExpression {
            get { return _node is Expression; }
        }

        public override Node GetNode(PythonAst root) {
            return _node;
        }

        public override Statement GetBody(PythonAst root) {
            Statement body = _node as Statement;
            if (body == null) {
                var retStmt = new ReturnStatement((Expression)_node);
                retStmt.RoundTripRemoveValueWhiteSpace(root);
                body = retStmt;
            }

            return body;
        }

        public override string InvalidExtractionMessage {
            get {
                if (!(_node is Expression || _node is Statement)) {
                    return "Cannot extract " + _node.NodeName;
                }
                return null;
            }
        }

        public override int StartIncludingIndentation {
            get { return _node.GetStartIncludingIndentation(Parents[0] as PythonAst); }
        }

        public override int End {
            get { return _node.EndIndex; }
        }

        public override string IndentationLevel {
            get {
                return _node.GetIndentationLevel(Parents[0] as PythonAst);
            }
        }

        public override void Walk(PythonWalker walker) {
            _node.Walk(walker);
        }
    }

    class SuiteTarget : SelectionTarget {
        private readonly SuiteStatement _suite;
        private readonly SuiteStatement[] _followingSuites;
        private readonly int _start, _end;
        private readonly int _selectionEnd;

        public SuiteTarget(Dictionary<ScopeStatement, int> insertLocations, ScopeStatement[] parents, SuiteStatement suite, SuiteStatement[] followingSuites, int selectionEnd, int startIndex, int endIndex)
            : base(insertLocations, parents) {
            _suite = suite;
            _start = startIndex;
            _end = endIndex;
            _followingSuites = followingSuites;
            _selectionEnd = selectionEnd;
        }

        public override bool IsExpression {
            get { return false; }
        }

        public override Node GetNode(PythonAst root) {
            if (_suite.Statements.Count == 0) {
                return _suite;
            }
            return _suite.CloneSubset(Parents[0] as PythonAst, _start, _end);
        }

        public override Statement GetBody(PythonAst root) {
            if (_suite.Statements.Count == 0) {
                return _suite;
            }
            return _suite.CloneSubset(Parents[0] as PythonAst, _start, _end);
        }

        public override IEnumerable<Statement> GetStatementsAfter(PythonAst root) {
            yield return _suite.CloneSubset(Parents[0] as PythonAst, _end + 1, _suite.Statements.Count - 1);

            foreach (var suite in _followingSuites) {
                foreach (var stmt in suite.Statements) {
                    if (stmt.StartIndex > _selectionEnd) {
                        yield return stmt;
                    }
                }
            }
        }

        public override int StartIncludingIndentation {
            get {
                if (_suite.Statements.Count == 0) {
                    return _suite.GetStartIncludingIndentation(Parents[0] as PythonAst);
                }
                return _suite.Statements[_start].GetStartIncludingIndentation(Parents[0] as PythonAst); 
            }
        }

        public override int End {
            get {
                if (_suite.Statements.Count == 0) {
                    return _suite.EndIndex;
                }
                return _suite.Statements[_end].EndIndex; 
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
        internal static int GetStartIncludingIndentation(this Node self, PythonAst ast) {
            return self.StartIndex - (self.GetIndentationLevel(ast) ?? "").Length;
        }
    }
}
