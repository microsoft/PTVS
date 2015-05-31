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
using System.Runtime.CompilerServices;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Refactoring {
    class ImportRemover {
        private readonly ITextView _view;
        private readonly PythonAst _ast;
        private readonly bool _allScopes;
        private readonly IServiceProvider _serviceProvider;

        public ImportRemover(IServiceProvider serviceProvider, ITextView textView, bool allScopes) {
            _view = textView;
            _serviceProvider = serviceProvider;
            var snapshot = _view.TextBuffer.CurrentSnapshot;

            _ast = _view.GetAnalyzer(_serviceProvider).ParseSnapshot(snapshot);
            _allScopes = allScopes;
        }

        internal void RemoveImports() {
            ScopeStatement targetStmt = null;
            if (!_allScopes) {
                var enclosingNodeWalker = new EnclosingNodeWalker(_ast, _view.Caret.Position.BufferPosition, _view.Caret.Position.BufferPosition);
                _ast.Walk(enclosingNodeWalker);
                targetStmt = enclosingNodeWalker.Target.Parents[enclosingNodeWalker.Target.Parents.Length - 1];
            }

            var walker = new ImportWalker(targetStmt);
            _ast.Walk(walker);

            using (var edit = _view.TextBuffer.CreateEdit()) {
                foreach (var removeInfo in walker.GetToRemove()) {
                    // see if we're removing some or all of the 
                    //var node = removeInfo.Node;
                    var removing = removeInfo.ToRemove;
                    var removed = removeInfo.Statement;
                    UpdatedStatement updatedStatement = removed.InitialStatement;

                    int removeCount = 0;
                    for (int i = 0, curRemoveIndex = 0; i < removed.NameCount; i++) {
                        if (removed.IsRemoved(i, removing)) {
                            removeCount++;
                            updatedStatement = updatedStatement.RemoveName(_ast, curRemoveIndex);
                        } else {
                            curRemoveIndex++;
                        }
                    }

                    var span = removed.Node.GetSpan(_ast);
                    if (removeCount == removed.NameCount) {
                        removeInfo.SiblingCount.Value--;
                        
                        DeleteStatement(edit, span, removeInfo.SiblingCount.Value == 0);
                    } else {
                        var newCode = updatedStatement.ToCodeString(_ast);

                        int proceedingLength = (removed.Node.GetLeadingWhiteSpace(_ast) ?? "").Length;
                        int start = span.Start.Index - proceedingLength;
                        int length = span.Length + proceedingLength;

                        edit.Delete(start, length);
                        edit.Insert(span.Start.Index, newCode);
                    }
                }

                edit.Apply();
            }
        }

        private static void DeleteStatement(VisualStudio.Text.ITextEdit edit, SourceSpan span, bool insertPass) {
            // remove the entire node, leave any trailing whitespace/comments, but include the
            // newline and any indentation.


            int start = span.Start.Index;
            int length = span.Length;
            int cur = start - 1;
            if (!insertPass) {
                // backup to remove any indentation
                while (start - 1 > 0) {
                    var curChar = edit.Snapshot.GetText(start - 1, 1);
                    if (curChar[0] == ' ' || curChar[0] == '\t' || curChar[0] == '\f') {
                        length++;
                        start--;
                    } else {
                        break;
                    }
                }
            }

            // extend past any trailing whitespace characters
            while (start + length < edit.Snapshot.Length) {
                var curChar = edit.Snapshot.GetText(start + length, 1);
                if (curChar[0] == ' ' || curChar[0] == '\t' || curChar[0] == '\f') {
                    length++;
                } else {
                    break;
                }
            }

            // remove the trailing newline as well.
            if (!insertPass) {
                if (start + length < edit.Snapshot.Length) {
                    var newlineText = edit.Snapshot.GetText(start + length, 1);
                    if (newlineText == "\r") {
                        if (start + length + 1 < edit.Snapshot.Length) {
                            newlineText = edit.Snapshot.GetText(start + length + 1, 1);
                            if (newlineText == "\n") {
                                length += 2;
                            } else {
                                length++;
                            }
                        } else {
                            length++;
                        }
                    } else if (newlineText == "\n") {
                        length++;
                    }
                }
            }
            edit.Delete(start, length);

            if (insertPass) {
                edit.Insert(start, "pass");
            }
        }

        /// <summary>
        /// Base class for a statement that we're removing names from.  This abstracts away the differences
        /// between removing names from a "from ... import" statement and an "import " statement so the code
        /// in RemoveImports can be shared.
        /// </summary>
        abstract class RemovedStatement {
            /// <summary>
            /// Returns the number of names this statement refers to.
            /// </summary>
            public abstract int NameCount {
                get;
            }

            /// <summary>
            /// Returns true if we're removing the name at the specified index.
            /// </summary>
            public abstract bool IsRemoved(int index, HashSet<string> removedNames);

            /// <summary>
            /// Gets the initial statement which we can update by removing names from.
            /// </summary>
            public abstract UpdatedStatement InitialStatement {
                get;
            }

            /// <summary>
            /// Gets the node which we're updating - used for getting the span and whitespace info.
            /// </summary>
            public abstract Statement Node {
                get;
            }
        }

        /// <summary>
        /// Represents a statement that can be edited by repeatedly removing names.
        /// </summary>
        abstract class UpdatedStatement {
            /// <summary>
            /// Turns the statement back into code.
            /// </summary>
            /// <param name="ast"></param>            
            public abstract string ToCodeString(PythonAst ast);

            /// <summary>
            /// Removes the name at the given index from the statement returning a new updated statemetn.
            /// </summary>
            public abstract UpdatedStatement RemoveName(PythonAst ast, int index);
        }

        /// <summary>
        /// An "import" statement which is having names removed.
        /// </summary>
        class RemovedImportStatement : RemovedStatement {
            private readonly ImportStatement _import;

            public RemovedImportStatement(ImportStatement removed) {
                _import = removed;
            }

            public override bool IsRemoved(int index, HashSet<string> removedNames) {
                string name;
                if (_import.AsNames != null && _import.AsNames[index] != null) {
                    name = _import.AsNames[index].Name;
                } else {
                    // only the first name becomes available
                    name = _import.Names[index].Names[0].Name;
                }

                return removedNames.Contains(name);
            }

            public override int NameCount {
                get { return _import.Names.Count; }
            }

            public override UpdatedStatement InitialStatement {
                get { return new UpdatedImportStatement(_import); }
            }

            public override Statement Node {
                get { return _import; }
            }

            class UpdatedImportStatement : UpdatedStatement {
                private readonly ImportStatement _import;

                public UpdatedImportStatement(ImportStatement import) {
                    _import = import;
                }

                public override string ToCodeString(PythonAst ast) {
                    return _import.ToCodeString(ast);
                }

                public override UpdatedStatement RemoveName(PythonAst ast, int index) {
                    return new UpdatedImportStatement(_import.RemoveImport(ast, index));
                }
            }
        }

        /// <summary>
        /// A "from ... import" statement which is having names removed.
        /// </summary>
        class RemovedFromImportStatement : RemovedStatement {
            private readonly FromImportStatement _fromImport;

            public RemovedFromImportStatement(FromImportStatement fromImport) {
                _fromImport = fromImport;
            }

            public override int NameCount {
                get { return _fromImport.Names.Count; }
            }

            public override bool IsRemoved(int index, HashSet<string> removedNames) {
                string name;
                if (_fromImport.AsNames != null && _fromImport.AsNames[index] != null) {
                    name = _fromImport.AsNames[index].Name;
                } else {
                    // only the first name becomes available
                    name = _fromImport.Names[index].Name;
                }

                return removedNames.Contains(name);
            }

            public override UpdatedStatement InitialStatement {
                get { return new UpdatedFromImportStatement(_fromImport); }
            }

            public override Statement Node {
                get { return _fromImport; }
            }

            class UpdatedFromImportStatement : UpdatedStatement {
                private readonly FromImportStatement _fromImport;

                public UpdatedFromImportStatement(FromImportStatement fromImport) {
                    _fromImport = fromImport;
                }

                public override string ToCodeString(PythonAst ast) {
                    return _fromImport.ToCodeString(ast);
                }

                public override UpdatedStatement RemoveName(PythonAst ast, int index) {
                    return new UpdatedFromImportStatement(_fromImport.RemoveImport(ast, index));
                }
            }
        }

        class ImportRemovalInfo {
            public readonly HashSet<string> ToRemove = new HashSet<string>();
            public readonly RemovedStatement Statement;
            public readonly StrongBox<int> SiblingCount;

            public ImportRemovalInfo(ImportStatementInfo statementInfo) {
                var node = statementInfo.Statement;
                SiblingCount = statementInfo.Siblings;

                if (node is FromImportStatement) {
                    Statement = new RemovedFromImportStatement((FromImportStatement)node);
                } else {
                    Statement = new RemovedImportStatement((ImportStatement)node);
                }
            }
        }

        /// <summary>
        /// Tracks a statement and the number of siblings that we share with our parent.
        /// 
        /// When we remove a complete statement we decrement the number of siblings we have.  If
        /// we remove all of our siblings then we need to insert a pass statement.
        /// </summary>
        class ImportStatementInfo {
            public readonly Statement Statement;
            public readonly StrongBox<int> Siblings;

            public ImportStatementInfo(Statement statement, StrongBox<int> siblings) {
                Statement = statement;
                Siblings = siblings;
            }
        }

        class ImportWalker : PythonWalker {
            private readonly List<ScopeStatement> _scopes = new List<ScopeStatement>();
            private readonly Dictionary<string, List<ImportStatementInfo>> _importedNames = new Dictionary<string, List<ImportStatementInfo>>();
            private readonly HashSet<string> _readNames = new HashSet<string>();
            private readonly ScopeStatement _targetStmt;
            private readonly Dictionary<ScopeStatement, StrongBox<int>> _statementCount = new Dictionary<ScopeStatement, StrongBox<int>>();

            public ImportWalker(ScopeStatement targetStmt) {
                _targetStmt = targetStmt;
            }

            public bool InTargetScope {
                get {
                    return _targetStmt == null || _scopes.Contains(_targetStmt);
                }
            }

            public ICollection<ImportRemovalInfo> GetToRemove() {
                Dictionary<Statement, ImportRemovalInfo> removeInfo = new Dictionary<Statement, ImportRemovalInfo>();

                foreach (var nameAndList in _importedNames) {
                    if (!_readNames.Contains(nameAndList.Key)) {
                        foreach (var node in nameAndList.Value) {
                            ImportRemovalInfo curInfo;
                            if (!removeInfo.TryGetValue(node.Statement, out curInfo)) {
                                removeInfo[node.Statement] = curInfo = new ImportRemovalInfo(node);
                            }

                            curInfo.ToRemove.Add(nameAndList.Key);
                        }
                    }
                }

                return removeInfo.Values;
            }

            public override bool Walk(ImportStatement node) {
                if (InTargetScope && !(_scopes[_scopes.Count - 1] is ClassDefinition)) {
                    for (int i = 0; i < node.Names.Count; i++) {
                        if (node.AsNames != null && node.AsNames[i] != null) {
                            var name = node.AsNames[i].Name;
                            TrackImport(node, name);
                        } else {
                            // only the first name becomes available
                            TrackImport(node, node.Names[i].Names[0].Name);
                        }
                    }
                }
                return base.Walk(node);
            }

            private void TrackImport(Statement node, string name) {
                var parent = _scopes[_scopes.Count - 1];
                StrongBox<int> statementCount;

                if (!_statementCount.TryGetValue(parent, out statementCount)) {
                    PythonAst outerParent = parent as PythonAst;
                    if (outerParent != null) {
                        // we don't care about the number of children at the top level
                        statementCount = new StrongBox<int>(-1);
                    } else {
                        FunctionDefinition funcDef = parent as FunctionDefinition;
                        if (funcDef != null) {
                            statementCount = GetNumberOfChildStatements(funcDef.Body);
                        } else {
                            var classDef = (ClassDefinition)parent;
                            statementCount = GetNumberOfChildStatements(classDef.Body);
                        }
                    }
                    _statementCount[parent] = statementCount;
                }

                List<ImportStatementInfo> imports;
                if (!_importedNames.TryGetValue(name, out imports)) {
                    _importedNames[name] = imports = new List<ImportStatementInfo>();
                }
                imports.Add(new ImportStatementInfo(node, statementCount));
            }

            private static StrongBox<int> GetNumberOfChildStatements(Statement body) {
                if (body is SuiteStatement) {
                    return new StrongBox<int>(((SuiteStatement)body).Statements.Count);
                } else {
                    return new StrongBox<int>(1);
                }
            }

            public override bool Walk(FromImportStatement node) {
                if (InTargetScope && !node.IsFromFuture && !(_scopes[_scopes.Count - 1] is ClassDefinition)) {
                    for (int i = 0; i < node.Names.Count; i++) {
                        if (node.Names[i].Name == "*") {
                            // ignore from .. import *
                            continue;
                        }

                        if (node.AsNames != null && node.AsNames[i] != null) {
                            TrackImport(node, node.AsNames[i].Name);
                        } else {
                            TrackImport(node, node.Names[i].Name);
                        }
                    }
                }
                return base.Walk(node);
            }

            public override bool Walk(NameExpression node) {
                if (InTargetScope) {
                    _readNames.Add(node.Name);
                }
                return base.Walk(node);
            }

            public override bool Walk(FunctionDefinition node) {
                _scopes.Add(node);
                return base.Walk(node);
            }

            public override bool Walk(ClassDefinition node) {
                _scopes.Add(node);
                return base.Walk(node);
            }

            public override bool Walk(PythonAst node) {
                _scopes.Add(node);
                return base.Walk(node);
            }

            public override void PostWalk(FunctionDefinition node) {
                _scopes.RemoveAt(_scopes.Count - 1);
                base.PostWalk(node);
            }

            public override void PostWalk(ClassDefinition node) {
                _scopes.RemoveAt(_scopes.Count - 1);
                base.PostWalk(node);
            }

            public override void PostWalk(PythonAst node) {
                _scopes.RemoveAt(_scopes.Count - 1);
                base.PostWalk(node);
            }
        }

    }
}
