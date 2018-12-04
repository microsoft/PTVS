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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    // TODO: Move this class to Analysis and make it public

    class MethodExtractor {
        private readonly PythonAst _ast;
        private readonly string _code;

        public MethodExtractor(PythonAst ast, string code) {
            _code = code ?? throw new ArgumentNullException(nameof(code));
            _ast = ast ?? throw new ArgumentNullException(nameof(ast));
        }

        public AP.ExtractMethodResponse ExtractMethod(AP.ExtractMethodRequest input, int version) {
            // tighten up the selection so that we don't expand into any enclosing nodes because we overlap w/ their white space...
            var selectionStart = input.startIndex;
            while (selectionStart < _code.Length &&
                Char.IsWhiteSpace(_code[selectionStart])) {
                selectionStart++;
            }

            var selectionEnd = input.endIndex;
            if (selectionEnd == _code.Length) {
                selectionEnd -= 1;
            }
            while (selectionEnd >= 0 && char.IsWhiteSpace(_code[selectionEnd])) {
                selectionEnd -= 1;
            }

            var walker = new EnclosingNodeWalker(_ast, selectionStart, selectionEnd);
            _ast.Walk(walker);

            Debug.Assert(walker.Target != null);
            if (walker.Target == null) {
                return new AP.ExtractMethodResponse() {
                    cannotExtractReason = AP.CannotExtractReason.InvalidTargetSelected
                };
            }

            bool expanded = false;
            // expand the selection if we aren't currently covering a full expression/statement
            if (!walker.Target.IsValidSelection) {
                return new AP.ExtractMethodResponse() {
                    cannotExtractReason = AP.CannotExtractReason.InvalidExpressionSelected
                };
            }

            expanded = WasSelectionExpanded(walker.Target, selectionStart, selectionEnd);

            // check for things we cannot handle
            if (!IsValidExtraction(walker.Target, out var failureReason)) {
                return new AP.ExtractMethodResponse() {
                    // Note: this returns the unformatted error
                    cannotExtractReason = failureReason
                };
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
            var parent = walker.Target.Parents[walker.Target.Parents.Count - 1];
            HashSet<PythonVariable> readBeforeInit;
            FlowChecker extractedChecker = null;
            if (parent.ScopeVariables != null) {
                extractedChecker = new FlowChecker(parent);

                walker.Target.Walk(extractedChecker);
                readBeforeInit = extractedChecker.ReadBeforeInitializedVariables;
            } else {
                readBeforeInit = new HashSet<PythonVariable>();
            }

            // then on code which follows our extracted body
            var afterStmts = walker.Target.GetStatementsAfter();
            HashSet<PythonVariable> readByFollowingCodeBeforeInit = null;
            var parentNode = walker.Target.Parents[walker.Target.Parents.Count - 1];
            var outputVars = new HashSet<PythonVariable>();
            if (parentNode.ScopeVariables != null) {
                var checker = new FlowChecker(parentNode);

                foreach (var afterStmt in afterStmts) {
                    afterStmt.Walk(checker);
                }

                readByFollowingCodeBeforeInit = checker.ReadBeforeInitializedVariables;

                foreach (var variable in varCollector._allWrittenVariables) {
                    if (variable != null && variable.Scope is PythonAst) {
                        // global variable assigned to in outer scope, and left with
                        // a valid value (not deleted) from the extracted code.  We
                        // need to pass the value back out and assign to it in the
                        // global scope.
                        if (!checker.IsInitialized(variable) &&
                            extractedChecker.IsAssigned(variable)) {
                            outputVars.Add(variable);
                        }
                    }
                }
            }

            // collect any nested scopes, and see if they read any free variables
            var scopeCollector = new ScopeCollector();
            foreach (var afterStmt in afterStmts) {
                afterStmt.Walk(scopeCollector);
            }

            foreach (var scope in scopeCollector._scopes) {
                if (scope.FreeVariables != null) {
                    foreach (var freeVar in scope.FreeVariables) {
                        if (varCollector._allWrittenVariables.Contains(freeVar)) {
                            // we've assigned to a variable accessed from an inner
                            // scope, we need to get the value of the variable back out.
                            outputVars.Add(freeVar);
                        }
                    }
                }
            }


            // discover any variables which are consumed and need to be available as outputs...
            var outputCollector = new OuterVariableWalker(_ast, walker.Target, varCollector, readBeforeInit, readByFollowingCodeBeforeInit, outputVars);
            _ast.Walk(outputCollector);

            if (outputCollector._outputVars.Count > 0 &&
                walker.Target.ContainsReturn) {
                return new AP.ExtractMethodResponse {
                    cannotExtractReason = AP.CannotExtractReason.MethodAssignsVariablesAndReturns
                };
            }

            var targetScope = walker.Target.Parents[input.scope ?? 0];
            var creator = new OutOfProcExtractedMethodCreator(
                _ast,
                walker.Target.Parents,
                outputCollector._inputVars,
                outputCollector._outputVars,
                walker.Target,
                input.indentSize,
                !input.convertTabsToSpaces,
                input.newLine,
                input.name,
                input.parameters ?? new string[0],
                walker.Target.Parents[input.scope ?? 0]
            );

            // get the new method body...
            var newMethod = creator.GetExtractionResult();

            var changes = new List<DocumentChange>();

            var callChange = DocumentChange.Replace(walker.Target.StartIncludingIndentation, walker.Target.End, newMethod.Call);
            var methodChange = DocumentChange.Insert(newMethod.Method, walker.Target.InsertLocations[targetScope]);
            if (callChange.ReplacedSpan.Start < methodChange.ReplacedSpan.Start) {
                changes.Add(callChange);
                changes.Add(methodChange);
            } else {
                changes.Add(methodChange);
                changes.Add(callChange);
            }

            List<AP.ScopeInfo> scopes = new List<AP.ScopeInfo>();
            for(int i = 0; i<walker.Target.Parents.Count; i++) {
                var scope = walker.Target.Parents[i];
                var scopeInfo = new AP.ScopeInfo() {
                    name = scope.Name,
                    id = i,
                    type = GetScopeType(scope),
                    variables = GetScopeVariables(scope, outputCollector._inputVars)
                };
                scopes.Add(scopeInfo);
            }

            return new AP.ExtractMethodResponse() { 
                changes = changes.Select(AP.ChangeInfo.FromDocumentChange).ToArray(),
                methodBody = newMethod.Method,
                variables = outputCollector._inputVars.Select(x => x.Name).ToArray(),
                scopes = scopes.ToArray(),
                wasExpanded = expanded,
                startLine = walker.Target.StartIncludingIndentation.Line,
                startCol = walker.Target.StartIncludingIndentation.Column,
                endLine = walker.Target.End.Line,
                endCol = walker.Target.End.Column,
                version = version
            };
        }

        private string[] GetScopeVariables(ScopeStatement scope, HashSet<PythonVariable> inputVars) {
            List<string> res = new List<string>();
            foreach (var variable in inputVars) {
                var variableScope = variable.Scope;

                var parentScope = scope;
                // are these variables a child of the target scope so we can close over them?
                while (parentScope != null && parentScope != variableScope) {
                    parentScope = parentScope.Parent;
                }

                if (parentScope != null) {
                    res.Add(variable.Name);
                }
            }
            return res.ToArray();
        }

        private string GetScopeType(ScopeStatement scope) {
            if (scope is ClassDefinition) {
                return "class";
            } else if (scope is FunctionDefinition) {
                return "function";
            }

            return "global";
        }

        private bool WasSelectionExpanded(SelectionTarget target, int selectionStart, int selectionEnd) {
            int startIndex = target.Ast.LocationToIndex(target.StartIncludingIndentation);
            if (startIndex != selectionStart) {
                for (var curChar = selectionStart - 1; curChar >= startIndex; curChar -= 1) {
                    if (!Char.IsWhiteSpace(_code[curChar])) {
                        return true;
                    }
                }
            }
            int endIndex = target.Ast.LocationToIndex(target.End);
            if (endIndex != selectionEnd) {
                for (var curChar = selectionEnd + 1; curChar < endIndex; curChar += 1) {
                    if (!Char.IsWhiteSpace(_code[curChar])) {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsValidExtraction(SelectionTarget target, out AP.CannotExtractReason failureReason) {
            if (target.Parents[target.Parents.Count - 1] is ClassDefinition) {
                failureReason = AP.CannotExtractReason.StatementsFromClassDefinition;
                return false;
            }

            var breakContinueWalker = new ContinueBreakWalker();
            target.Walk(breakContinueWalker);
            if (breakContinueWalker.ContainsBreak) {
                failureReason = AP.CannotExtractReason.SelectionContainsBreakButNotEnclosingLoop;
                return false;
            } else if (breakContinueWalker.ContainsContinue) {
                failureReason = AP.CannotExtractReason.SelectionContainsContinueButNotEnclosingLoop;
                return false;
            }

            var yieldWalker = new YieldWalker();
            target.Walk(yieldWalker);
            if (yieldWalker.ContainsYield) {
                failureReason = AP.CannotExtractReason.ContainsYieldExpression;
                return false;
            }

            var importStarWalker = new ImportStarWalker();
            target.Walk(importStarWalker);
            if (importStarWalker.ContainsImportStar) {
                failureReason = AP.CannotExtractReason.ContainsFromImportStar;
                return false;
            }

            var returnWalker = new ReturnWalker();
            target.Walk(returnWalker);
            if (returnWalker.ContainsReturn && !returnWalker.Returns) {
                failureReason = AP.CannotExtractReason.SelectionContainsReturn;
                return false;
            }

            target.ContainsReturn = returnWalker.ContainsReturn;
            failureReason = AP.CannotExtractReason.None;
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
                for (int i = 0; i < node.TestsInternal.Length; i++) {
                    _raises = Returns = false;
                    node.TestsInternal[i].Body.Walk(this);

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
                    _raises = false;
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
            internal readonly HashSet<PythonVariable> _outputVars;
            internal readonly HashSet<PythonVariable> _inputVars = new HashSet<PythonVariable>();
            private readonly HashSet<PythonVariable> _readBeforeInitialized, _readByFollowingCodeBeforeInit;
            private bool _inLoop = false;

            public OuterVariableWalker(PythonAst root, SelectionTarget target, InnerVariableWalker inputCollector, HashSet<PythonVariable> readBeforeInitialized, HashSet<PythonVariable> readByFollowingCodeBeforeInit, HashSet<PythonVariable> outputVars) {
                _root = root;
                _target = target;
                _inputCollector = inputCollector;
                _readBeforeInitialized = readBeforeInitialized;
                _readByFollowingCodeBeforeInit = readByFollowingCodeBeforeInit;
                _outputVars = outputVars;
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
                if (reference != null && !_inputCollector._allReads.Contains(reference) && !_inputCollector._allWrites.Contains(reference)) {
                    // this variable is referenced outside of the refactored code
                    if (node.GetStart(_root) < _target.StartIncludingIndentation) {
                        // it's read before the extracted code, we don't care...
                    } else {
                        Debug.Assert(node.GetEnd(_root) > _target.End, "didn't reference variable in extracted range");

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
                            if (node.GetStart(_collector._root) < _collector._target.StartIncludingIndentation) {
                                // it's assigned before the extracted code
                                _collector._inputVars.Add(reference.Variable);
                            } else {
                                Debug.Assert(node.GetEnd(_collector._root) > _collector._target.End);
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

        class ScopeCollector : PythonWalker {
            internal readonly List<ScopeStatement> _scopes = new List<ScopeStatement>();

            public override void PostWalk(ClassDefinition node) {
                _scopes.Add(node);
                base.PostWalk(node);
            }

            public override void PostWalk(FunctionDefinition node) {
                _scopes.Add(node);
                base.PostWalk(node);
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

                if (reference != null) {
                    _allReads.Add(reference);
                    _allReadVariables.Add(reference.Variable);
                }

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
