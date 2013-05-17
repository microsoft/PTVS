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

            _ast = _view.GetAnalyzer().ParseFile(snapshot);
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
            if (!walker.Target.IsValidSelection ||
                (WasSelectionExpanded(walker.Target, selectionStart, selectionEnd) && !input.ShouldExpandSelection())) {
                return false;
            }

            _view.Selection.Select(
                new SnapshotSpan(
                    _view.TextBuffer.CurrentSnapshot,
                    Span.FromBounds(
                        walker.Target.StartIncludingIndentation,
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

            // then on code which follows our extracted body
            var afterStmts = walker.Target.GetStatementsAfter(_ast);
            HashSet<PythonVariable> readByFollowingCodeBeforeInit = null;
            var parentNode = walker.Target.Parents[walker.Target.Parents.Length - 1];
            if (parentNode.ScopeVariables != null) {
                var checker = new FlowChecker(parentNode);

                foreach (var afterStmt in afterStmts) {
                    afterStmt.Walk(checker);
                }

                readByFollowingCodeBeforeInit = checker.ReadBeforeInitializedVariables;
            }

            // discover any variables which are consumed and need to be available as outputs...
            var outputCollector = new OuterVariableWalker(_ast, walker.Target, varCollector, readBeforeInit, readByFollowingCodeBeforeInit);
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
                !_view.Options.IsConvertTabsToSpacesEnabled(),
                _view.Options.GetNewLineCharacter()
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
            if (target.StartIncludingIndentation != selectionStart.Position) {
                for (var curChar = selectionStart - 1; curChar.Position >= target.StartIncludingIndentation; curChar -= 1) {
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

            public override bool Walk(YieldFromExpression node) {
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
            private readonly HashSet<PythonVariable> _readBeforeInitialized, _readByFollowingCodeBeforeInit;
            private bool _inLoop = false;

            public OuterVariableWalker(PythonAst root, SelectionTarget target, InnerVariableWalker inputCollector, HashSet<PythonVariable> readBeforeInitialized, HashSet<PythonVariable> readByFollowingCodeBeforeInit) {
                _root = root;
                _target = target;
                _inputCollector = inputCollector;
                _readBeforeInitialized = readBeforeInitialized;
                _readByFollowingCodeBeforeInit = readByFollowingCodeBeforeInit;
                _define = new DefineWalker(this);
            }

            public override AssignedNameWalker Define {
                get { return _define; }
            }

            public override bool Walk(FunctionDefinition node) {
                if (!node.IsLambda) {
                    _define.WalkName(node.NameExpression, node.GetVariableReference(_root));
                }

                bool oldInLoop = _inLoop;
                _inLoop = false;
                var res = base.Walk(node);
                _inLoop = oldInLoop;
                return res;
            }

            public override bool Walk(ClassDefinition node) {
                _define.WalkName(node.NameExpression, node.GetVariableReference(_root));

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
                    if (node.StartIndex < _target.StartIncludingIndentation) {
                        // it's read before the extracted code, we don't care...
                    } else {
                        Debug.Assert(node.EndIndex > _target.End, "didn't reference variable in extracted range");

                        // it's read after the extracted code, if its written to in the refactored 
                        // code we need to include it as an output
                        if (_inputCollector._allWrittenVariables.Contains(reference.Variable) &&
                            (_readByFollowingCodeBeforeInit == null || _readByFollowingCodeBeforeInit.Contains(reference.Variable))) {
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

                    return WalkName(node, reference);
                }

                internal bool WalkName(NameExpression node, PythonReference reference) {
                    if (_collector.ReadFromExtractedCode(reference.Variable)) {
                        if ((!_collector._inputCollector._allReads.Contains(reference) &&
                            !_collector._inputCollector._allWrites.Contains(reference))) {

                            // the variable is assigned outside the refactored code
                            if (node.StartIndex < _collector._target.StartIncludingIndentation) {
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
    }
}
