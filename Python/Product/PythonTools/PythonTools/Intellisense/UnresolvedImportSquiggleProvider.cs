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
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.PythonTools.Intellisense {
    sealed class UnresolvedImportSquiggleProvider {
        private readonly PythonAnalyzer _analyzer;
        private readonly SimpleTagger<ErrorTag> _squiggles;
        private readonly Dispatcher _dispatcher;

        public UnresolvedImportSquiggleProvider(
            PythonAnalyzer analyzer,
            SimpleTagger<ErrorTag> squiggles,
            Dispatcher dispatcher
        ) {
            _analyzer = analyzer;
            _squiggles = squiggles;
            _dispatcher = dispatcher;
        }

        public void ListenForNextNewAnalysis(IPythonProjectEntry entry) {
            entry.OnNewAnalysis += OnNewAnalysis;
        }

        private void OnNewAnalysis(object sender, EventArgs e) {
            var entry = sender as IPythonProjectEntry;
            if (entry == null) {
                return;
            }
            entry.OnNewAnalysis -= OnNewAnalysis;

            if (string.IsNullOrEmpty(entry.ModuleName) || string.IsNullOrEmpty(entry.FilePath)) {
                return;
            }

            PythonAst ast;
            IAnalysisCookie cookie;
            entry.GetTreeAndCookie(out ast, out cookie);
            var snapshotCookie = cookie as SnapshotCookie;
            if (snapshotCookie == null) {
                return;
            }

            var walker = new ImportStatementWalker(entry, _analyzer);
            ast.Walk(walker);

            var tags = walker.Imports.Select(t => Tuple.Create(
                t.Item2.GetSpan(ast),
                new ErrorTag(PredefinedErrorTypeNames.Warning, GetToolTip(t.Item1))
            )).ToArray();

            if (_dispatcher == null) {
                DoUpdate(snapshotCookie.Snapshot, tags);
            } else {
                _dispatcher.BeginInvoke(
                    (Action)(() => DoUpdate(snapshotCookie.Snapshot, tags))
                );
            }
        }

        private void DoUpdate(ITextSnapshot snapshot, IEnumerable<Tuple<SourceSpan, ErrorTag>> tags) {
            foreach (var tag in tags) {
                _squiggles.CreateTagSpan(
                    CreateSpan(snapshot, tag.Item1),
                    tag.Item2
                );
            }
        }

        private static ITrackingSpan CreateSpan(ITextSnapshot snapshot, SourceSpan span) {
            Debug.Assert(span.Start.Index >= 0);
            var newSpan = new Span(
                span.Start.Index,
                Math.Min(span.End.Index - span.Start.Index, Math.Max(snapshot.Length - span.Start.Index, 0))
            );
            Debug.Assert(newSpan.End <= snapshot.Length);
            return snapshot.CreateTrackingSpan(newSpan, SpanTrackingMode.EdgeInclusive);
        }

        private static object GetToolTip(string module) {
            return SR.GetString(SR.UnresolvedModuleTooltip, module);
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
        }
    }
}
