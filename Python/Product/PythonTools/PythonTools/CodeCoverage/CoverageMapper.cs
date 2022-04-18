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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;

namespace Microsoft.PythonTools.CodeCoverage {
    /// <summary>
    /// Maps line hit information into exact source code locations for 
    /// highlighting in the editor.
    /// </summary>

    class CoverageMapper : PythonWalker {
        /// <summary>
        /// Tracks coverage information for the gloal scope
        /// </summary>
        public readonly CoverageScope GlobalScope;
        public readonly List<CoverageScope> Classes = new List<CoverageScope>();
        public List<CoverageScope> CurScopes = new List<CoverageScope>();
        private readonly PythonAst _ast;
        private readonly string _filename;
        public int _blockCount;
        private readonly HashSet<int> _hits;
        private bool? _blockCovered;

        public CoverageMapper(PythonAst ast, string filename, HashSet<int> hits) {
            _ast = ast;
            _filename = filename;
            GlobalScope = new CoverageScope(ast);
            CurScopes.Add(GlobalScope);
            _blockCount++;
            _hits = hits;
        }

        public bool IsCovered(int line) {
            return _hits.Contains(line);
        }

        public CoverageScope CurScope {
            get {
                return CurScopes[CurScopes.Count - 1];
            }
        }

        public string ModuleName {
            get {
                return ModulePath.FromFullPath(_filename).ModuleName;
            }
        }

        private bool UpdateLineInfo(Node node, bool inheritCoverage = false) {
            if (node == null) {
                return false;
            }

            var start = node.GetStart(_ast);
            var end = node.GetEnd(_ast);
            bool covered;
            bool multiline = false;
            if (start.Line != end.Line) {
                multiline = true;
                // multi line statement, just figure out if we hit anywhere in it,
                // and if so update our coverage state.  We'll continue to descend
                // and mark the individual items that make up the multiple lines.
                bool isCovered = false;
                for (int i = start.Line; i <= end.Line; i++) {
                    isCovered |= IsCovered(i);
                }

                covered = isCovered;
            } else {
                // single line statement
                covered = MarkCoverage(inheritCoverage, start, end, IsCovered(start.Line));
            }

            if (_blockCovered == null) {
                _blockCovered = covered;
                if (covered) {
                    ReportCovered();
                } else {
                    ReportNotCovered();
                }
            }

            return multiline;
        }

        private bool MarkCoverage(bool inheritCoverage, SourceLocation start, SourceLocation end, bool isCovered) {
            bool covered;
            int line = start.Line;
            CoverageLineInfo info;
            if (!CurScope.Lines.TryGetValue(line, out info)) {
                CurScope.Lines[line] = info = new CoverageLineInfo();
            }

            info.Covered = covered = inheritCoverage && (_blockCovered ?? false) || isCovered;
            info.ColumnStart = Math.Min(info.ColumnStart, start.Column);
            info.ColumnEnd = Math.Max(info.ColumnEnd, end.Column);
            return covered;
        }

        private void ReportCovered() {
            CurScope.BlocksCovered++;
        }

        private void ReportNotCovered() {
            CurScope.BlocksNotCovered++;
        }

        #region Scope statements

        private CoverageScope WalkScope(ScopeStatement node) {
            var prevScope = CurScope;
            var newScope = new CoverageScope(node);
            CurScopes.Add(newScope);
            var wasCovered = _blockCovered;
            _blockCovered = null;
            if (node.Body != null) {
                node.Body.Walk(this);
            }

            CurScopes.RemoveAt(CurScopes.Count - 1);
            _blockCovered = wasCovered;
            return newScope;
        }

        public override bool Walk(FunctionDefinition node) {
            var newScope = WalkScope(node);
            CurScope.Children.Add(newScope);

            return false;
        }

        public override bool Walk(ClassDefinition node) {
            Classes.Add(WalkScope(node));
            return false;
        }

        public void AddBlock() {
            _blockCovered = null;
        }

        #endregion

        #region Flow Control Statements

        public override bool Walk(IfStatement node) {
            foreach (var test in node.Tests) {
                UpdateLineInfo(test.Test);
                AddBlock();

                if (test.Body != null) {
                    test.Body.Walk(this);
                } else {
                    ReportNotCovered();
                }
            }

            if (node.ElseStatement != null) {
                AddBlock();
                node.ElseStatement.Walk(this);
            }

            AddBlock();

            return false;
        }

        public override bool Walk(WhileStatement node) {
            UpdateLineInfo(node.Test);
            AddBlock();

            if (node.Body != null) {
                node.Body.Walk(this);
            }

            if (node.ElseStatement != null) {
                AddBlock();
                node.ElseStatement.Walk(this);
            }

            AddBlock();

            return false;
        }

        public override bool Walk(ForStatement node) {
            UpdateLineInfo(node.List);
            AddBlock();

            if (node.Body != null) {
                node.Body.Walk(this);
            }

            if (node.Else != null) {
                AddBlock();
                node.Else.Walk(this);
            }

            AddBlock();

            return false;
        }

        public override bool Walk(RaiseStatement node) {
            UpdateLineInfo(node);
            AddBlock();

            return false;
        }

        public override bool Walk(ReturnStatement node) {
            UpdateLineInfo(node);
            AddBlock();

            return false;
        }

        public override bool Walk(BreakStatement node) {
            UpdateLineInfo(node);
            AddBlock();

            return false;
        }

        public override bool Walk(ContinueStatement node) {
            UpdateLineInfo(node);
            AddBlock();

            return false;
        }

        #endregion

        #region Simple statements 

        public override bool Walk(DelStatement node) {
            UpdateLineInfo(node);

            return base.Walk(node);
        }


        public override bool Walk(AssertStatement node) {
            UpdateLineInfo(node);

            return base.Walk(node);
        }

        public override bool Walk(AssignmentStatement node) {
            UpdateLineInfo(node);

            return base.Walk(node);
        }

        public override bool Walk(AugmentedAssignStatement node) {
            UpdateLineInfo(node);

            return base.Walk(node);
        }
        public override bool Walk(EmptyStatement node) {
            UpdateLineInfo(node, true);

            return base.Walk(node);
        }

        public override bool Walk(ExecStatement node) {
            UpdateLineInfo(node);

            return base.Walk(node);
        }

        public override bool Walk(ExpressionStatement node) {
            UpdateLineInfo(node);

            return base.Walk(node);
        }

        public override bool Walk(FromImportStatement node) {
            UpdateLineInfo(node);

            return base.Walk(node);
        }

        public override bool Walk(GlobalStatement node) {
            UpdateLineInfo(node, true);

            return base.Walk(node);
        }


        public override bool Walk(ImportStatement node) {
            UpdateLineInfo(node);

            return base.Walk(node);
        }

        public override bool Walk(NonlocalStatement node) {
            UpdateLineInfo(node);

            return base.Walk(node);
        }

        public override bool Walk(PrintStatement node) {
            UpdateLineInfo(node);

            return base.Walk(node);
        }

        public override bool Walk(TryStatement node) {
            UpdateLineInfo(node);

            return base.Walk(node);
        }

        public override bool Walk(WithStatement node) {
            UpdateLineInfo(node);

            return base.Walk(node);
        }

        #endregion

        #region Expressions

        public override bool Walk(NameExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(ConstantExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(BinaryExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(DictionaryExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(ListExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(ParenthesisExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(CallExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(AndExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(AwaitExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(BackQuoteExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(ComprehensionFor node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(ComprehensionIf node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(ConditionalExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(DictionaryComprehension node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(GeneratorExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(IndexExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(MemberExpression node) {
            if (UpdateLineInfo(node, true)) {
                // make sure we get the name marked as well if we have a multiline
                // name expression...
                var nameSpan = node.GetNameSpan(_ast);
                MarkCoverage(
                    true, 
                    nameSpan.Start, 
                    nameSpan.End, 
                    IsCovered(node.GetStart(_ast).Line)
                );
                return true;
            }
            return false;
        }

        public override bool Walk(OrExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(TupleExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(SliceExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(SetExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(UnaryExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(SetComprehension node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(YieldExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(YieldFromExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(LambdaExpression node) {
            return UpdateLineInfo(node, true);
        }

        public override bool Walk(ListComprehension node) {
            return UpdateLineInfo(node, true);
        }

        #endregion
    }
}
