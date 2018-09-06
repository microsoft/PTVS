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
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {

    /// <summary>
    /// Finds the outer most enclosing node(s) for the given selection as well as the set of parent nodes
    /// which lead to the selected nodes.  Provides this back as a SelectionTarget which encapsulates all
    /// of the data for performing the refactoring.
    /// </summary>
    class EnclosingNodeWalker : PythonWalker {
        private readonly PythonAst _root;
        private readonly int _start, _end;
        private SelectionTarget _targetNode;
        private List<ScopeStatement> _parents = new List<ScopeStatement>();
        private List<SuiteStatement> _suites = new List<SuiteStatement>();
        private Dictionary<ScopeStatement, SourceLocation> _insertLocations = new Dictionary<ScopeStatement, SourceLocation>();

        public EnclosingNodeWalker(PythonAst root, int start, int end) {
            _start = start;
            _end = end;
            _root = root;
        }

        /// <summary>
        /// Provides information about the target node(s) which end up being selected.
        /// </summary>
        public SelectionTarget Target {
            get {
                return _targetNode;
            }
        }

        private bool ShouldWalkWorker(Node node) {
            if (node is ScopeStatement) {
                _suites.Add(null);  // marker for a function/class boundary
                _parents.Add((ScopeStatement)node);
            }

            return node.StartIndex <= _end && node.EndIndex >= _start;
        }

        private bool ShouldWalkWorker(SuiteStatement node) {
            if (ShouldWalkWorker((Node)node)) {
                _suites.Add(node);
                foreach (var stmt in node.Statements) {
                    stmt.Walk(this);
                    if (_targetNode != null) {
                        // we have found our extracted code below this,
                        // we should insert before this statement.
                        _insertLocations[_parents[_parents.Count - 1]] = stmt.GetStartIncludingIndentation(_root);
                        break;
                    }
                }
                _suites.Pop();

            }
            return false;
        }

        private void PostWalkWorker(SuiteStatement node) {
            if (_targetNode == null && node.StartIndex <= _start && node.EndIndex >= _end) {
                // figure out the range of statements we cover...
                int startIndex = 0, endIndex = node.Statements.Count - 1;
                for (int i = 0; i < node.Statements.Count; i++) {
                    if (node.Statements[i].EndIndex >= _start) {
                        startIndex = i;
                        break;
                    }
                }
                for (int i = node.Statements.Count - 1; i >= 0; i--) {
                    if (node.Statements[i].StartIndex < _end) {
                        endIndex = i;
                        break;
                    }
                }
                List<SuiteStatement> followingSuites = new List<SuiteStatement>();
                for (int i = _suites.Count - 1; i >= 0; i--) {
                    if (_suites[i] == null) {
                        // we hit our marker, this is a function/class boundary
                        // We don't care about any suites which come before the marker
                        // because they live in a different scope.  We insert the marker in 
                        // ShouldWalkWorker(Node node) when we have a ScopeStatement.
                        break;
                    }

                    followingSuites.Add(_suites[i]);
                }
                _targetNode = new SuiteTarget(
                    _insertLocations, 
                    _parents.ToArray(), 
                    node, 
                    followingSuites.ToArray(), 
                    _end, 
                    startIndex, 
                    endIndex
                );
                _insertLocations[_parents[_parents.Count - 1]] = node.Statements.Count == 0 ?
                    node.GetStartIncludingIndentation(_root) :
                    node.Statements[startIndex].GetStartIncludingIndentation(_root);
            }
        }

        private void PostWalkWorker(IfStatementTest node) {
        }

        private void PostWalkWorker(Node node) {
            if (node is ScopeStatement) {
                _suites.Pop();
                _parents.Remove((ScopeStatement)node);
            }
            if (_targetNode == null &&
                node.StartIndex <= _start &&
                node.EndIndex >= _end &&
                node is Expression expression) {
                _targetNode = new NodeTarget(_insertLocations, _parents.ToArray(), expression);
            }
        }

        #region Walk/PostWalk Overrides

        // AndExpression
        public override bool Walk(AndExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(AndExpression node) { PostWalkWorker(node); }

        // BackQuoteExpression
        public override bool Walk(BackQuoteExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(BackQuoteExpression node) { PostWalkWorker(node); }

        // BinaryExpression
        public override bool Walk(BinaryExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(BinaryExpression node) { PostWalkWorker(node); }

        // CallExpression
        public override bool Walk(CallExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(CallExpression node) { PostWalkWorker(node); }

        // ConditionalExpression
        public override bool Walk(ConditionalExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ConditionalExpression node) { PostWalkWorker(node); }

        // ConstantExpression
        public override bool Walk(ConstantExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ConstantExpression node) { PostWalkWorker(node); }

        // DictionaryComprehension
        public override bool Walk(DictionaryComprehension node) { return ShouldWalkWorker(node); }
        public override void PostWalk(DictionaryComprehension node) { PostWalkWorker(node); }

        // DictionaryExpression
        public override bool Walk(DictionaryExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(DictionaryExpression node) { PostWalkWorker(node); }

        // ErrorExpression
        public override bool Walk(ErrorExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ErrorExpression node) { PostWalkWorker(node); }

        // GeneratorExpression
        public override bool Walk(GeneratorExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(GeneratorExpression node) { PostWalkWorker(node); }

        // IndexExpression
        public override bool Walk(IndexExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(IndexExpression node) { PostWalkWorker(node); }

        // LambdaExpression
        public override bool Walk(LambdaExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(LambdaExpression node) { PostWalkWorker(node); }

        // ListComprehension
        public override bool Walk(ListComprehension node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ListComprehension node) { PostWalkWorker(node); }

        // ListExpression
        public override bool Walk(ListExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ListExpression node) { PostWalkWorker(node); }

        // MemberExpression
        public override bool Walk(MemberExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(MemberExpression node) { PostWalkWorker(node); }

        // NameExpression
        public override bool Walk(NameExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(NameExpression node) { PostWalkWorker(node); }

        // OrExpression
        public override bool Walk(OrExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(OrExpression node) { PostWalkWorker(node); }

        // ParenthesisExpression
        public override bool Walk(ParenthesisExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ParenthesisExpression node) { PostWalkWorker(node); }

        // SetComprehension
        public override bool Walk(SetComprehension node) { return ShouldWalkWorker(node); }
        public override void PostWalk(SetComprehension node) { PostWalkWorker(node); }

        // SetExpression
        public override bool Walk(SetExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(SetExpression node) { PostWalkWorker(node); }

        // SliceExpression
        public override bool Walk(SliceExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(SliceExpression node) { PostWalkWorker(node); }

        // TupleExpression
        public override bool Walk(TupleExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(TupleExpression node) { PostWalkWorker(node); }

        // UnaryExpression
        public override bool Walk(UnaryExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(UnaryExpression node) { PostWalkWorker(node); }

        // YieldExpression
        public override bool Walk(YieldExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(YieldExpression node) { PostWalkWorker(node); }

        // YieldFromExpression
        public override bool Walk(YieldFromExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(YieldFromExpression node) { PostWalkWorker(node); }

        // StarredExpression
        public override bool Walk(StarredExpression node) { return ShouldWalkWorker(node); }
        public override void PostWalk(StarredExpression node) { PostWalkWorker(node); }

        // AssertStatement
        public override bool Walk(AssertStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(AssertStatement node) { PostWalkWorker(node); }

        // AssignmentStatement
        public override bool Walk(AssignmentStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(AssignmentStatement node) { PostWalkWorker(node); }

        // AugmentedAssignStatement
        public override bool Walk(AugmentedAssignStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(AugmentedAssignStatement node) { PostWalkWorker(node); }

        // BreakStatement
        public override bool Walk(BreakStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(BreakStatement node) { PostWalkWorker(node); }

        // ClassDefinition
        public override bool Walk(ClassDefinition node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ClassDefinition node) { PostWalkWorker(node); }

        // ContinueStatement
        public override bool Walk(ContinueStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ContinueStatement node) { PostWalkWorker(node); }

        // DelStatement
        public override bool Walk(DelStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(DelStatement node) { PostWalkWorker(node); }

        // EmptyStatement
        public override bool Walk(EmptyStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(EmptyStatement node) { PostWalkWorker(node); }

        // ExecStatement
        public override bool Walk(ExecStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ExecStatement node) { PostWalkWorker(node); }

        // ExpressionStatement
        public override bool Walk(ExpressionStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ExpressionStatement node) { PostWalkWorker(node); }

        // ForStatement
        public override bool Walk(ForStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ForStatement node) { PostWalkWorker(node); }

        // FromImportStatement
        public override bool Walk(FromImportStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(FromImportStatement node) { PostWalkWorker(node); }

        // FunctionDefinition
        public override bool Walk(FunctionDefinition node) { return ShouldWalkWorker(node); }
        public override void PostWalk(FunctionDefinition node) { PostWalkWorker(node); }

        // GlobalStatement
        public override bool Walk(GlobalStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(GlobalStatement node) { PostWalkWorker(node); }

        // NonlocalStatement
        public override bool Walk(NonlocalStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(NonlocalStatement node) { PostWalkWorker(node); }

        // IfStatement
        public override bool Walk(IfStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(IfStatement node) { PostWalkWorker(node); }

        // ImportStatement
        public override bool Walk(ImportStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ImportStatement node) { PostWalkWorker(node); }

        // PrintStatement
        public override bool Walk(PrintStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(PrintStatement node) { PostWalkWorker(node); }

        // PythonAst
        public override bool Walk(PythonAst node) { return ShouldWalkWorker(node); }
        public override void PostWalk(PythonAst node) { PostWalkWorker(node); }

        // RaiseStatement
        public override bool Walk(RaiseStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(RaiseStatement node) { PostWalkWorker(node); }

        // ReturnStatement
        public override bool Walk(ReturnStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ReturnStatement node) { PostWalkWorker(node); }

        // SuiteStatement
        public override bool Walk(SuiteStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(SuiteStatement node) { PostWalkWorker(node); }

        // TryStatement
        public override bool Walk(TryStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(TryStatement node) { PostWalkWorker(node); }

        // WhileStatement
        public override bool Walk(WhileStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(WhileStatement node) { PostWalkWorker(node); }

        // WithStatement
        public override bool Walk(WithStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(WithStatement node) { PostWalkWorker(node); }

        // Arg
        public override bool Walk(Arg node) { return ShouldWalkWorker(node); }
        public override void PostWalk(Arg node) { PostWalkWorker(node); }

        // ComprehensionFor
        public override bool Walk(ComprehensionFor node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ComprehensionFor node) { PostWalkWorker(node); }

        // ComprehensionIf
        public override bool Walk(ComprehensionIf node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ComprehensionIf node) { PostWalkWorker(node); }

        // DottedName
        public override bool Walk(DottedName node) { return ShouldWalkWorker(node); }
        public override void PostWalk(DottedName node) { PostWalkWorker(node); }

        // IfStatementTest
        public override bool Walk(IfStatementTest node) { return ShouldWalkWorker(node); }
        public override void PostWalk(IfStatementTest node) { PostWalkWorker(node); }

        // ModuleName
        public override bool Walk(ModuleName node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ModuleName node) { PostWalkWorker(node); }

        // Parameter
        public override bool Walk(Parameter node) { return ShouldWalkWorker(node); }
        public override void PostWalk(Parameter node) { PostWalkWorker(node); }

        // RelativeModuleName
        public override bool Walk(RelativeModuleName node) { return ShouldWalkWorker(node); }
        public override void PostWalk(RelativeModuleName node) { PostWalkWorker(node); }

        // SublistParameter
        public override bool Walk(SublistParameter node) { return ShouldWalkWorker(node); }
        public override void PostWalk(SublistParameter node) { PostWalkWorker(node); }

        // TryStatementHandler
        public override bool Walk(TryStatementHandler node) { return ShouldWalkWorker(node); }
        public override void PostWalk(TryStatementHandler node) { PostWalkWorker(node); }

        // ErrorStatement
        public override bool Walk(ErrorStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(ErrorStatement node) { PostWalkWorker(node); }

        // DecoratorStatement
        public override bool Walk(DecoratorStatement node) { return ShouldWalkWorker(node); }
        public override void PostWalk(DecoratorStatement node) { PostWalkWorker(node); }

        #endregion
    }
}
