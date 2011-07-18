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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

namespace Microsoft.PythonTools.Refactoring {
    class MethodExtractor {
        private readonly ITextView _view;
        private readonly PythonAst _ast;

        public MethodExtractor(ITextView textView) {
            _view = textView;
            var snapshot = _view.TextBuffer.CurrentSnapshot;

            _ast = ParseFile(snapshot);
        }

        public bool ExtractMethod(IExtractMethodInput input) {
            // tighten up the selection so that we don't expand into any enclosing nodes because we overlap w/ their white space...
            var selectionStart = _view.Selection.Start.Position;
            while (selectionStart.Position < _view.TextBuffer.CurrentSnapshot.Length && Char.IsWhiteSpace(selectionStart.GetChar())) {
                selectionStart += 1;
            }

            var selectionEnd = _view.Selection.End.Position;
            if (selectionEnd.Position == _view.TextBuffer.CurrentSnapshot.Length) {
                selectionEnd -= 1;
            }
            while (selectionEnd.Position >= 0 && Char.IsWhiteSpace(selectionEnd.GetChar())) {
                selectionEnd -= 1;
            }

            var walker = new EnclosingNodeWalker(_ast, selectionStart, selectionEnd);
            _ast.Walk(walker);

            Debug.Assert(walker.Target != null);
            // expand the selection if we aren't currently covering a full expression/statement
            if (WasSelectionExpanded(walker.Target, selectionStart, selectionEnd) && !input.ShouldExpandSelection()) {
                return false;
            }

            _view.Selection.Select(
                new SnapshotSpan(
                    _view.TextBuffer.CurrentSnapshot,
                    Span.FromBounds(
                        walker.Target.Start,
                        walker.Target.End
                    )
                ),
                false
            );

            // check for things we cannot handle
            if (!IsValidExtraction(input, walker.Target)) {
                return false;
            }

            // get the variables which are read by the selected statement(s)
            var varCollector = new InnerVariableWalker(_ast);
            walker.Target.Walk(varCollector);

            // Walk the target to understand flow control and definite assignment to further understand
            // what we need to flow in.  For example if a variable is assigned to both in the un-extracted
            // and extracted code but it's definitely assigned in the extracted code then we don't need
            // to flow the variable in.

            // Run flow checker, first on any nested scopes...
            foreach (ScopeStatement scope in varCollector._scopes) {
                FlowChecker.Check(scope);
            }

            // then on our extracted code
            var parent = walker.Target.Parents[walker.Target.Parents.Length - 1];
            HashSet<PythonVariable> readBeforeInit;
            if (parent.ScopeVariables != null) {
                var flowChecker = new FlowChecker(parent);

                walker.Target.Walk(flowChecker);
                readBeforeInit = flowChecker.ReadBeforeInitializedVariables;
            } else {
                readBeforeInit = new HashSet<PythonVariable>();
            }

            // discover any variables which are consumed and need to be available as outputs...
            var outputCollector = new OuterVariableWalker(_ast, walker.Target, varCollector, readBeforeInit);
            _ast.Walk(outputCollector);

            if (outputCollector._outputVars.Count > 0 &&
                walker.Target.ContainsReturn) {
                input.CannotExtract("Cannot extract method that assigns to variables and returns");
                return false;
            }

            var creator = new ExtractedMethodCreator(
                _ast,
                walker.Target.Parents,
                outputCollector._inputVars,
                outputCollector._outputVars,
                walker.Target,
                _view.Options.GetIndentSize(),
                !_view.Options.IsConvertTabsToSpacesEnabled()
            );

            var info = input.GetExtractionInfo(creator);
            if (info == null) {
                // user cancelled extract method
                return false;
            }

            // get the new method body...
            var newMethod = creator.GetExtractionResult(info);

            // generate the call site.
            using (var edit = _view.TextBuffer.CreateEdit()) {
                // delete the selected code
                int start = _view.Selection.Start.Position;
                edit.Delete(Span.FromBounds(_view.Selection.Start.Position, _view.Selection.End.Position));

                // insert the newly generated method
                edit.Insert(walker.Target.InsertLocations[info.TargetScope], newMethod.Method);

                // insert the call to the new method
                edit.Insert(start, newMethod.Call);

                edit.Apply();
            }

            return true;
        }

        private static bool WasSelectionExpanded(SelectionTarget target, SnapshotPoint selectionStart, SnapshotPoint selectionEnd) {
            if (target.Start != selectionStart.Position) {
                for (var curChar = selectionStart; curChar.Position >= target.Start; curChar -= 1) {
                    if (!Char.IsWhiteSpace(curChar.GetChar())) {
                        return true;
                    }
                }
            }
            if (target.End != selectionEnd.Position) {
                for (var curChar = selectionEnd + 1; curChar.Position < target.End; curChar += 1) {
                    if (!Char.IsWhiteSpace(curChar.GetChar())) {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsValidExtraction(IExtractMethodInput input, SelectionTarget target) {
            if (target.Parents[target.Parents.Length - 1] is ClassDefinition) {
                input.CannotExtract("Cannot extract statements from a class definition");
                return false;
            }

            string invalidExtractMsg = target.InvalidExtractionMessage;
            if (invalidExtractMsg != null) {
                input.CannotExtract(invalidExtractMsg);
                return false;
            }

            var breakContinueWalker = new ContinueBreakWalker();
            target.Walk(breakContinueWalker);
            if (breakContinueWalker.ContainsBreak) {
                input.CannotExtract("The selection contains a \"break\" statement, but not the enclosing loop");
                return false;
            } else if (breakContinueWalker.ContainsContinue) {
                input.CannotExtract("The selection contains a \"continue\" statement, but not the enclosing loop");
                return false;
            }

            var yieldWalker = new YieldWalker();
            target.Walk(yieldWalker);
            if (yieldWalker.ContainsYield) {
                input.CannotExtract("Cannot extract code containing \"yield\" expression");
                return false;
            }

            var importStarWalker = new ImportStarWalker();
            target.Walk(importStarWalker);
            if (importStarWalker.ContainsImportStar) {
                input.CannotExtract("Cannot extract method containing from ... import * statement");
                return false;
            }

            var returnWalker = new ReturnWalker();
            target.Walk(returnWalker);
            if (returnWalker.ContainsReturn && !returnWalker.Returns) {
                input.CannotExtract("When the selection contains a return statement, all code paths must be terminated by a return statement too.");
                return false;
            }
            target.ContainsReturn = returnWalker.ContainsReturn;

            return true;
        }

        private PythonAst ParseFile(ITextSnapshot snapshot) {
            var parser = Parser.CreateParser(
                new SnapshotSpanSourceCodeReader(
                    new SnapshotSpan(snapshot, 0, snapshot.Length)
                ),
                _view.GetAnalyzer().Project.LanguageVersion,
                new ParserOptions() { Verbatim = true, BindReferences = true }
            );

            var ast = parser.ParseFile();
            return ast;
        }

        class ReturnWalker : PythonWalker {
            public bool ContainsReturn, Returns;
            private bool _raises;

            public override bool Walk(ReturnStatement node) {
                Returns = true;
                ContainsReturn = true;
                return base.Walk(node);
            }

            public override bool Walk(RaiseStatement node) {
                _raises = true;
                return base.Walk(node);
            }

            public override bool Walk(IfStatement node) {
                bool allReturn = true;
                for (int i = 0; i < node.Tests.Count; i++) {
                    _raises = Returns = false;
                    node.Tests[i].Body.Walk(this);

                    allReturn &= Returns || _raises;
                }

                _raises = Returns = false;
                if (node.ElseStatement != null) {
                    node.ElseStatement.Walk(this);

                    allReturn &= Returns || _raises;
                } else {
                    allReturn = false;
                }


                Returns = allReturn;
                return false;
            }

            public override bool Walk(ForStatement node) {
                WalkLoop(node.Body, node.Else);
                return false;
            }

            public override bool Walk(WhileStatement node) {
                WalkLoop(node.Body, node.ElseStatement);
                return false;
            }

            private void WalkLoop(Statement body, Statement elseStmt) {
                bool allReturn = true;

                _raises = Returns = false;
                body.Walk(this);

                allReturn &= Returns || _raises;

                if (elseStmt != null) {
                    _raises = _raises = false;
                    elseStmt.Walk(this);
                    allReturn &= Returns || _raises;
                }

                Returns = allReturn;
            }

            public override bool Walk(SuiteStatement node) {
                foreach (var statement in node.Statements) {
                    if (statement is BreakStatement || statement is ContinueStatement) {
                        // code after here is unreachable
                        break;
                    }

                    Returns = false;
                    statement.Walk(this);
                    if (Returns) {
                        // rest of the code is unreachable...
                        break;
                    }
                }
                return false;
            }

            public override bool Walk(TryStatement node) {
                node.Body.Walk(this);

                if (node.Handlers != null && node.Handlers.Count > 0) {
                    // treat any exceptions from the body as handled, any exceptions
                    // from the handlers/else are not handled.
                    _raises = false;

                    foreach (var handler in node.Handlers) {
                        handler.Walk(this);
                    }
                }

                if (node.Finally != null) {
                    node.Finally.Walk(this);
                }

                if (node.Else != null) {
                    node.Else.Walk(this);
                }

                return false;
            }

            public override bool Walk(FunctionDefinition node) {
                return false;
            }

            public override bool Walk(ClassDefinition node) {
                return false;
            }
        }

        class ContinueBreakWalker : PythonWalker {
            public bool ContainsBreak, ContainsContinue;

            public override bool Walk(ForStatement node) {
                return false;
            }

            public override bool Walk(WhileStatement node) {
                return false;
            }

            public override bool Walk(FunctionDefinition node) {
                return false;
            }

            public override bool Walk(ContinueStatement node) {
                if (!ContainsBreak) {
                    ContainsContinue = true;
                }
                return base.Walk(node);
            }

            public override bool Walk(BreakStatement node) {
                if (!ContainsContinue) {
                    ContainsBreak = true;
                }
                return base.Walk(node);
            }
        }

        class YieldWalker : PythonWalker {
            public bool ContainsYield;

            public override bool Walk(FunctionDefinition node) {
                return false;
            }

            public override bool Walk(YieldExpression node) {
                ContainsYield = true;
                return base.Walk(node);
            }
        }

        class ImportStarWalker : PythonWalker {
            public bool ContainsImportStar;

            public override bool Walk(FunctionDefinition node) {
                return false;
            }

            public override bool Walk(ClassDefinition node) {
                return false;
            }

            public override bool Walk(FromImportStatement node) {
                if (node.Names.Count == 1 && node.Names[0].Name == "*") {
                    ContainsImportStar = true;
                }
                return base.Walk(node);
            }
        }

        /// <summary>
        /// Inspects the variables used in the surrounding code to figure out which ones need to be flowed in
        /// and which ones need to be returned based upon the variables we collected which are read/assigned
        /// from the code being extracted.
        /// </summary>
        class OuterVariableWalker : AssignmentWalker {
            private readonly PythonAst _root;
            private readonly InnerVariableWalker _inputCollector;
            private readonly DefineWalker _define;
            private readonly SelectionTarget _target;
            internal readonly HashSet<PythonVariable> _outputVars = new HashSet<PythonVariable>();
            internal readonly HashSet<PythonVariable> _inputVars = new HashSet<PythonVariable>();
            private readonly HashSet<PythonVariable> _readBeforeInitialized;
            private bool _inLoop = false;

            public OuterVariableWalker(PythonAst root, SelectionTarget target, InnerVariableWalker inputCollector, HashSet<PythonVariable> readBeforeInitialized) {
                _root = root;
                _target = target;
                _inputCollector = inputCollector;
                _readBeforeInitialized = readBeforeInitialized;
                _define = new DefineWalker(this);
            }

            public override AssignedNameWalker Define {
                get { return _define; }
            }

            public override bool Walk(FunctionDefinition node) {
                bool oldInLoop = _inLoop;
                _inLoop = false;
                var res = base.Walk(node);
                _inLoop = oldInLoop;
                return res;
            }

            public override bool Walk(ClassDefinition node) {
                bool oldInLoop = _inLoop;
                _inLoop = false;
                var res = base.Walk(node);
                _inLoop = oldInLoop;
                return res;
            }

            public override bool Walk(WhileStatement node) {
                if (node.Test != null) {
                    node.Test.Walk(this);
                }
                if (node.Body != null) {
                    bool oldInLoop = _inLoop;
                    _inLoop = true;
                    node.Body.Walk(this);
                    _inLoop = oldInLoop;
                }
                if (node.ElseStatement != null) {
                    node.ElseStatement.Walk(this);
                }
                return false;
            }

            public override bool Walk(ForStatement node) {
                if (node.Left != null) {
                    node.Left.Walk(Define);
                }

                if (node.List != null) {
                    node.List.Walk(this);
                }
                if (node.Body != null) {
                    bool oldInLoop = _inLoop;
                    _inLoop = true;
                    node.Body.Walk(this);
                    _inLoop = oldInLoop;
                }
                if (node.Else != null) {
                    node.Else.Walk(this);
                }
                return false;
            }

            public override bool Walk(NameExpression node) {
                var reference = node.GetVariableReference(_root);
                if (!_inputCollector._allReads.Contains(reference) && !_inputCollector._allWrites.Contains(reference)) {
                    // this variable is referenced outside of the refactored code
                    if (node.StartIndex < _target.Start) {
                        // it's read before the extracted code, we don't care...
                    } else {
                        Debug.Assert(node.EndIndex > _target.End, "didn't reference variable in extracted range");

                        // it's read after the extracted code, if its written to in the refactored 
                        // code we need to include it as an output
                        if (_inputCollector._allWrittenVariables.Contains(reference.Variable)) {
                            // the variable is written to by the refactored code
                            _outputVars.Add(reference.Variable);
                        }
                    }
                }

                return true;
            }

            public override bool Walk(Parameter node) {
                var variable = node.GetVariable(_root);
                if (ReadFromExtractedCode(variable)) {
                    _inputVars.Add(variable);
                }

                return base.Walk(node);
            }

            private bool ReadFromExtractedCode(PythonVariable variable) {
                return _readBeforeInitialized.Contains(variable) &&
                    _inputCollector._allReadVariables.Contains(variable);
            }

            class DefineWalker : AssignedNameWalker {
                private readonly OuterVariableWalker _collector;

                public DefineWalker(OuterVariableWalker collector) {
                    _collector = collector;
                }

                public override bool Walk(NameExpression node) {
                    var reference = node.GetVariableReference(_collector._root);

                    if (_collector.ReadFromExtractedCode(reference.Variable)) {
                        if ((!_collector._inputCollector._allReads.Contains(reference) &&
                            !_collector._inputCollector._allWrites.Contains(reference))) {

                            // the variable is assigned outside the refactored code
                            if (node.StartIndex < _collector._target.Start) {
                                // it's assigned before the extracted code
                                _collector._inputVars.Add(reference.Variable);
                            } else {
                                Debug.Assert(node.EndIndex > _collector._target.End);
                                // it's assigned afterwards, we don't care...
                            }
                        } else if (_collector._readBeforeInitialized.Contains(reference.Variable) &&
                            _collector._inputCollector._allWrites.Contains(reference) &&
                            _collector._inLoop) {
                            // we read an un-initialized value, so it needs to be passed in.  If we
                            // write to it as well then we need to pass it back out for future calls.
                            _collector._outputVars.Add(reference.Variable);
                        }
                    }

                    return false;
                }
            }
        }

        /// <summary>
        /// Walks the code which is being extracted and collects all the variables which are read from and written to.
        /// </summary>
        class InnerVariableWalker : AssignmentWalker {
            private readonly PythonAst _root;
            internal readonly HashSet<PythonReference> _allReads = new HashSet<PythonReference>();
            internal readonly HashSet<PythonReference> _allWrites = new HashSet<PythonReference>();
            internal readonly HashSet<PythonVariable> _allWrittenVariables = new HashSet<PythonVariable>();
            internal readonly HashSet<PythonVariable> _allReadVariables = new HashSet<PythonVariable>();
            internal readonly List<ScopeStatement> _scopes = new List<ScopeStatement>();

            private readonly DefineWalker _define;

            public InnerVariableWalker(PythonAst root) {
                _root = root;
                _define = new DefineWalker(this);
            }

            public override AssignedNameWalker Define {
                get { return _define; }
            }

            public override bool Walk(NameExpression node) {
                var reference = node.GetVariableReference(_root);

                _allReads.Add(reference);
                _allReadVariables.Add(reference.Variable);
                return true;
            }

            public override void PostWalk(ClassDefinition node) {
                _scopes.Add(node);
                _allWrites.Add(node.GetVariableReference(_root));
                _allWrittenVariables.Add(node.Variable);
                base.PostWalk(node);
            }

            public override void PostWalk(FunctionDefinition node) {
                _scopes.Add(node);
                _allWrites.Add(node.GetVariableReference(_root));
                _allWrittenVariables.Add(node.Variable);
                base.PostWalk(node);
            }

            public override bool Walk(FromImportStatement node) {
                var vars = node.Variables;
                var refs = node.GetReferences(_root);
                if (refs != null) { // from .. import * will have null refs
                    for (int i = 0; i < vars.Length; i++) {
                        if (vars[i] != null) {
                            _allWrites.Add(refs[i]);
                            _allWrittenVariables.Add(vars[i]);
                        }
                    }
                }
                return base.Walk(node);
            }

            public override bool Walk(ImportStatement node) {
                var vars = node.Variables;
                var refs = node.GetReferences(_root);
                for (int i = 0; i < vars.Length; i++) {
                    if (vars[i] != null) {
                        _allWrites.Add(refs[i]);
                        _allWrittenVariables.Add(vars[i]);
                    }
                }
                return base.Walk(node);
            }

            class DefineWalker : AssignedNameWalker {
                private readonly InnerVariableWalker _collector;

                public DefineWalker(InnerVariableWalker collector) {
                    _collector = collector;
                }

                public override bool Walk(NameExpression node) {
                    var reference = node.GetVariableReference(_collector._root);

                    _collector._allWrites.Add(reference);
                    _collector._allWrittenVariables.Add(reference.Variable);
                    return false;
                }
            }
        }

        /// <summary>
        /// Finds the outer most enclosing node(s) for the given selection as well as the set of parent nodes
        /// which lead to the selected nodes.  Provides this back as a SelectionTarget which encapsulates all
        /// of the data for performing the refactoring.
        /// </summary>
        class EnclosingNodeWalker : PythonWalker {
            private readonly PythonAst _root;
            private readonly Span _selectedSpan;
            private SelectionTarget _targetNode;
            private List<ScopeStatement> _parents = new List<ScopeStatement>();
            private Dictionary<ScopeStatement, int> _insertLocations = new Dictionary<ScopeStatement, int>();

            public EnclosingNodeWalker(PythonAst root, SnapshotPoint start, SnapshotPoint end) {
                _selectedSpan = Span.FromBounds(start.Position, end.Position);
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
                    _parents.Add((ScopeStatement)node);
                }
                return _selectedSpan.IntersectsWith(Span.FromBounds(GetStartIndex(node), node.EndIndex));
            }

            private static int GetStartIndex(Node node) {
                FunctionDefinition funcDef;
                ClassDefinition classDef;
                if ((funcDef = node as FunctionDefinition) != null) {
                    if (funcDef.Decorators != null) {
                        return funcDef.Decorators.StartIndex;
                    }
                } else if ((classDef = node as ClassDefinition) != null) {
                    if (classDef.Decorators != null) {
                        return classDef.Decorators.StartIndex;
                    }
                }
                return node.StartIndex;
            }

            private bool ShouldWalkWorker(SuiteStatement node) {
                if (ShouldWalkWorker((Node)node)) {
                    foreach (var stmt in node.Statements) {
                        stmt.Walk(this);
                        if (_targetNode != null) {
                            // we have found our extracted code below this,
                            // we should insert before this statement.
                            _insertLocations[_parents[_parents.Count - 1]] = GetStartIndex(stmt);
                            break;
                        }
                    }

                }
                return false;
            }

            private void PostWalkWorker(SuiteStatement node) {
                if (_targetNode == null && node.StartIndex <= _selectedSpan.Start && node.EndIndex >= _selectedSpan.End) {
                    // figure out the range of statements we cover...
                    int startIndex = 0, endIndex = node.Statements.Count - 1;
                    for (int i = 0; i < node.Statements.Count; i++) {
                        if (node.Statements[i].EndIndex >= _selectedSpan.Start) {
                            startIndex = i;
                            break;
                        }
                    }
                    for (int i = node.Statements.Count - 1; i >= 0; i--) {
                        if (node.Statements[i].StartIndex < _selectedSpan.End) {
                            endIndex = i;
                            break;
                        }
                    }
                    _targetNode = new SuiteTarget(_insertLocations, _parents.ToArray(), node, startIndex, endIndex);
                    _insertLocations[_parents[_parents.Count - 1]] = GetStartIndex(node.Statements[startIndex]);
                }
            }

            private void PostWalkWorker(IfStatementTest node) {
            }

            private void PostWalkWorker(Node node) {
                if (node is ScopeStatement) {
                    _parents.Remove((ScopeStatement)node);
                }
                if (_targetNode == null &&
                    node.StartIndex <= _selectedSpan.Start &&
                    node.EndIndex >= _selectedSpan.End) {
                    _targetNode = new NodeTarget(_insertLocations, _parents.ToArray(), node);
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
}
