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
using System.Linq;
using System.Windows.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.PythonTools.Intellisense {
    sealed class UnresolvedImportSquiggleProvider {
        // Allows test cases to skip checking user options
        internal static bool _alwaysCreateSquiggle;
        private readonly IServiceProvider _serviceProvider;
        private readonly Lazy<TaskProvider> _taskProvider;

        public UnresolvedImportSquiggleProvider(IServiceProvider serviceProvider, Lazy<TaskProvider> taskProvider) {
            _serviceProvider = serviceProvider;
            _taskProvider = taskProvider;
        }

        public void ListenForNextNewAnalysis(IPythonProjectEntry entry) {
            if (entry != null && !string.IsNullOrEmpty(entry.FilePath)) {
                entry.OnNewAnalysis += OnNewAnalysis;
            }
        }

        public void StopListening(IPythonProjectEntry entry) {
            if (entry != null) {
                entry.OnNewAnalysis -= OnNewAnalysis;
            }
        }

        private void OnNewAnalysis(object sender, EventArgs e) {
            if (!_alwaysCreateSquiggle &&
                !_serviceProvider.GetPythonToolsService().GeneralOptions.UnresolvedImportWarning
            ) {
                return;
            }

            var entry = sender as IPythonProjectEntry;
            if (entry == null ||
                string.IsNullOrEmpty(entry.ModuleName) ||
                string.IsNullOrEmpty(entry.FilePath)
            ) {
                return;
            }

            var analysis = entry.Analysis;
            var analyzer = analysis != null ? analysis.ProjectState : null;
            if (analyzer == null) {
                return;
            }

            PythonAst ast;
            IAnalysisCookie cookie;
            entry.GetTreeAndCookie(out ast, out cookie);
            var snapshotCookie = cookie as SnapshotCookie;
            var snapshot = snapshotCookie != null ? snapshotCookie.Snapshot : null;
            if (ast == null || snapshot == null) {
                return;
            }

            var walker = new ImportStatementWalker(entry, analyzer);
            ast.Walk(walker);

            if (walker.Imports.Any()) {
                var f = new TaskProviderItemFactory(snapshot);

                _taskProvider.Value.ReplaceItems(
                    entry,
                    VsProjectAnalyzer.UnresolvedImportMoniker,
                    walker.Imports.Select(t => f.FromUnresolvedImport(
                        _serviceProvider,
                        analyzer.InterpreterFactory as IPythonInterpreterFactoryWithDatabase,
                        t.Item1,
                        t.Item2.GetSpan(ast)
                    )).ToList()
                );
            } else {
                _taskProvider.Value.Clear(entry, VsProjectAnalyzer.UnresolvedImportMoniker);
            }
        }

        class ImportStatementWalker : PythonWalker {
            public readonly List<Tuple<string, DottedName>> Imports = new List<Tuple<string, DottedName>>();

            readonly IPythonProjectEntry _entry;
            readonly PythonAnalyzer _analyzer;

            public ImportStatementWalker(IPythonProjectEntry entry, PythonAnalyzer analyzer) {
                _entry = entry;
                _analyzer = analyzer;
            }

            public override bool Walk(FromImportStatement node) {
                var name = node.Root.MakeString();
                if (!_analyzer.IsModuleResolved(_entry, name, node.ForceAbsolute)) {
                    Imports.Add(Tuple.Create(name, node.Root));
                }
                return base.Walk(node);
            }

            public override bool Walk(ImportStatement node) {
                foreach (var nameNode in node.Names) {
                    var name = nameNode.MakeString();
                    if (!_analyzer.IsModuleResolved(_entry, name, node.ForceAbsolute)) {
                        Imports.Add(Tuple.Create(name, nameNode));
                    }
                }
                return base.Walk(node);
            }

            private static bool IsImportError(Expression expr) {
                var name = expr as NameExpression;
                if (name != null) {
                    return name.Name == "Exception" || name.Name == "BaseException" || name.Name == "ImportError";
                }

                var tuple = expr as TupleExpression;
                if (tuple != null) {
                    return tuple.Items.Any(IsImportError);
                }

                return false;
            }

            private static bool ShouldWalkNormally(TryStatement node) {
                if (node.Handlers == null) {
                    return true;
                }

                foreach (var handler in node.Handlers) {
                    if (handler.Test == null || IsImportError(handler.Test)) {
                        return false;
                    }
                }

                return true;
            }

            public override bool Walk(TryStatement node) {
                if (ShouldWalkNormally(node)) {
                    return base.Walk(node);
                }

                // Don't walk 'try' body, but walk everything else
                if (node.Handlers != null) {
                    foreach (var handler in node.Handlers) {
                        handler.Walk(this);
                    }
                }
                if (node.Else != null) {
                    node.Else.Walk(this);
                }
                if (node.Finally != null) {
                    node.Finally.Walk(this);
                }

                return false;
            }
        }
    }
}
