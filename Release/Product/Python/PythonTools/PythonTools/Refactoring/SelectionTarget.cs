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
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Refactoring {
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
        private readonly Span _selectedSpan;

        public SuiteTarget(Dictionary<ScopeStatement, int> insertLocations, ScopeStatement[] parents, SuiteStatement suite, SuiteStatement[] followingSuites, Span selectedSpan, int startIndex, int endIndex)
            : base(insertLocations, parents) {
            _suite = suite;
            _start = startIndex;
            _end = endIndex;
            _followingSuites = followingSuites;
            _selectedSpan = selectedSpan;
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
                    if (stmt.StartIndex > _selectedSpan.End) {
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
}
