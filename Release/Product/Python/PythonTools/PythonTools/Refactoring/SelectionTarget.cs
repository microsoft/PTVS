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

        public virtual SuiteStatement GetStatementsAfter(PythonAst root) {
            return null;
        }

        public abstract int Start {
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

        public override Statement GetBody(PythonAst root) {
            Statement body = _node as Statement;
            if (body == null) {
                var retStmt = new ReturnStatement((Expression)_node);
                retStmt.RoundTripRemoveValueWhiteSpace(root);
                body = retStmt;
            }

            return new SuiteStatement(new[] { body });
        }

        public override string InvalidExtractionMessage {
            get {
                if (!(_node is Expression || _node is Statement)) {
                    return "Cannot extract " + _node.NodeName;
                }
                return null;
            }
        }

        public override int Start {
            get { return _node.StartIndex; }
        }

        public override int End {
            get { return _node.EndIndex; }
        }

        public override void Walk(PythonWalker walker) {
            _node.Walk(walker);
        }
    }

    class SuiteTarget : SelectionTarget {
        private readonly SuiteStatement _suite;
        private readonly int _start, _end;

        public SuiteTarget(Dictionary<ScopeStatement, int> insertLocations, ScopeStatement[] parents, SuiteStatement suite, int startIndex, int endIndex)
            : base(insertLocations, parents) {
            _suite = suite;
            _start = startIndex;
            _end = endIndex;
        }

        public override bool IsExpression {
            get { return false; }
        }

        public override Statement GetBody(PythonAst root) {
            var ast = _suite.CloneSubset(Parents[0] as PythonAst, _start, _end);
            if (!_suite.IsFunctionOrClassSuite(root)) {
                ast = new SuiteStatement(new[] { ast });
            }
            return ast;
        }

        public override SuiteStatement GetStatementsAfter(PythonAst root) {
            var ast = _suite.CloneSubset(Parents[0] as PythonAst, _end + 1, _suite.Statements.Count - 1);
            if (!_suite.IsFunctionOrClassSuite(root)) {
                ast = new SuiteStatement(new[] { ast });
            }
            return ast;
        }

        public override int Start {
            get { return _suite.Statements[_start].StartIndex; }
        }

        public override int End {
            get { return _suite.Statements[_end].EndIndex; }
        }

        public override void Walk(PythonWalker walker) {
            for (int i = _start; i <= _end; i++) {
                _suite.Statements[i].Walk(walker);
            }
        }
    }
}
