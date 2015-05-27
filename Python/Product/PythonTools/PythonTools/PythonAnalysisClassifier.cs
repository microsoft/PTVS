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
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools {
    struct CachedClassification {
        public ITrackingSpan Span;
        public string Classification;

        public CachedClassification(ITrackingSpan span, string classification) {
            Span = span;
            Classification = classification;
        }
    }

    /// <summary>
    /// Provides classification based upon the AST and analysis.
    /// </summary>
    internal class PythonAnalysisClassifier : IClassifier {
        private List<List<CachedClassification>> _spanCache;
        private readonly object _spanCacheLock = new object();
        private readonly PythonAnalysisClassifierProvider _provider;
        private readonly ITextBuffer _buffer;
        private IPythonProjectEntry _entry;

        internal PythonAnalysisClassifier(PythonAnalysisClassifierProvider provider, ITextBuffer buffer) {
            buffer.Changed += BufferChanged;
            buffer.ContentTypeChanged += BufferContentTypeChanged;

            _provider = provider;
            _buffer = buffer;
            EnsureAnalysis();
        }

        private void EnsureAnalysis() {
            if (_entry == null) {
                _entry = _buffer.GetPythonProjectEntry();
                if (_entry != null) {
                    _entry.OnNewAnalysis += OnNewAnalysis;
                }
            }
        }

        private void OnNewAnalysis(object sender, EventArgs e) {
            if (_provider._serviceProvider.GetPythonToolsService().AdvancedOptions.ColorNames == false) {
                lock (_spanCacheLock) {
                    if (_spanCache != null) {
                        _spanCache = null;
                        OnNewClassifications(_buffer.CurrentSnapshot);
                    }
                }
                return;
            }

            PythonAst tree;
            IAnalysisCookie cookie;
            _entry.GetTreeAndCookie(out tree, out cookie);
            var sCookie = cookie as SnapshotCookie;
            var snapshot = sCookie != null ? sCookie.Snapshot : null;
            if (tree == null || snapshot == null) {
                return;
            }


            var moduleAnalysis = (_provider._serviceProvider.GetPythonToolsService().AdvancedOptions.ColorNamesWithAnalysis)
                ? _entry.Analysis
                : null;

            var walker = new ClassifierWalker(tree, moduleAnalysis, snapshot, Provider.CategoryMap);
            tree.Walk(walker);
            var newCache = walker.Spans;

            lock (_spanCacheLock) {
                if (snapshot == snapshot.TextBuffer.CurrentSnapshot) {
                    // Ensure we have not raced with another update
                    _spanCache = newCache;
                } else {
                    snapshot = null;
                }
            }
            if (snapshot != null) {
                OnNewClassifications(snapshot);
            }
        }

        private void OnNewClassifications(ITextSnapshot snapshot) {
            var changed = ClassificationChanged;
            if (changed != null) {
                changed(this, new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
            }
        }

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
            EnsureAnalysis();
            
            var classifications = new List<ClassificationSpan>();
            var snapshot = span.Snapshot;
            var spans = _spanCache;

            if (span.Length <= 0 || span.Snapshot.IsReplBufferWithCommand() || spans == null) {
                return classifications;
            }


            var firstLine = 0; // span.Start.GetContainingLine().LineNumber;
            var lastLine = int.MaxValue; // span.End.GetContainingLine().LineNumber;
            for (int line = firstLine; line <= lastLine && line < spans.Count; ++line) {
                var lineSpan = spans[line];
                if (lineSpan != null) {
                    foreach (var cc in lineSpan) {
                        if (cc.Span.TextBuffer != snapshot.TextBuffer) {
                            continue;
                        }
                        var cs = cc.Span.GetSpan(snapshot);
                        if (!cs.IntersectsWith(span)) {
                            continue;
                        }

                        IClassificationType classification;
                        if (_provider.CategoryMap.TryGetValue(cc.Classification, out classification)) {
                            Debug.Assert(classification != null, "Did not find " + cc.Classification);
                            classifications.Add(new ClassificationSpan(cc.Span.GetSpan(snapshot), classification));
                        }
                    }
                }
            }

            return classifications;
        }

        public PythonAnalysisClassifierProvider Provider {
            get {
                return _provider;
            }
        }

        #region Private Members

        private void BufferContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
            _spanCache = null;
            _buffer.Changed -= BufferChanged;
            _buffer.ContentTypeChanged -= BufferContentTypeChanged;
            if (_entry != null) {
                _entry.OnNewAnalysis -= OnNewAnalysis;
                _entry = null;
            }
            _buffer.Properties.RemoveProperty(typeof(PythonAnalysisClassifier));
        }

        private void BufferChanged(object sender, TextContentChangedEventArgs e) {
        }

        #endregion
    }

    internal static partial class ClassifierExtensions {
        public static PythonAnalysisClassifier GetPythonAnalysisClassifier(this ITextBuffer buffer) {
            PythonAnalysisClassifier res;
            if (buffer.Properties.TryGetProperty<PythonAnalysisClassifier>(typeof(PythonAnalysisClassifier), out res)) {
                return res;
            }
            return null;
        }
    }

    class ClassifierWalker : PythonWalker {
        class StackData {
            public readonly string Name;
            public readonly HashSet<string> Parameters;
            public readonly HashSet<string> Functions;
            public readonly HashSet<string> Types;
            public readonly HashSet<string> Modules;
            public readonly List<Tuple<string, Span>> Names;
            public readonly StackData Previous;

            public StackData(string name, StackData previous) {
                Name = name;
                Previous = previous;
                Parameters = new HashSet<string>();
                Functions = new HashSet<string>();
                Types = new HashSet<string>();
                Modules = new HashSet<string>();
                Names = new List<Tuple<string, Span>>();
            }

            public IEnumerable<StackData> EnumerateTowardsGlobal {
                get {
                    for (var sd = this; sd != null; sd = sd.Previous) {
                        yield return sd;
                    }
                }
            }
        }
        
        private readonly PythonAst _ast;
        private readonly ModuleAnalysis _analysis;
        private readonly ITextSnapshot _snapshot;
        private readonly Dictionary<string, IClassificationType> _formatMap;
        private StackData _head;
        public readonly List<List<CachedClassification>> Spans;

        public ClassifierWalker(PythonAst ast, ModuleAnalysis analysis, ITextSnapshot snapshot, Dictionary<string, IClassificationType> formatMap) {
            _ast = ast;
            _analysis = analysis;
            _snapshot = snapshot;
            _formatMap = formatMap;
            Spans = new List<List<CachedClassification>>();
        }

        private void AddSpan(Tuple<string, Span> node, string type) {
            int lineNo;
            try {
                lineNo = _snapshot.GetLineNumberFromPosition(node.Item2.Start);
            } catch (ArgumentException) {
                return;
            }
            var existing = lineNo < Spans.Count ? Spans[lineNo] : null;
            if (existing == null) {
                while (lineNo >= Spans.Count) {
                    Spans.Add(null);
                }
                Spans[lineNo] = existing = new List<CachedClassification>();
            }
            existing.Add(new CachedClassification(
                _snapshot.CreateTrackingSpan(node.Item2, SpanTrackingMode.EdgeExclusive),
                type
            ));
        }

        private void BeginScope(string name = null) {
            if (_head != null) {
                if (name == null) {
                    name = _head.Name;
                } else if (_head.Name != null) {
                    name = _head.Name + "." + name;
                }
            }
            _head = new StackData(name, _head);
        }

        private void AddParameter(Parameter node) {
            Debug.Assert(_head != null);
            _head.Parameters.Add(node.Name);
            _head.Names.Add(Tuple.Create(node.Name, new Span(node.StartIndex, node.Name.Length)));
        }

        private void AddParameter(Node node) {
            NameExpression name;
            TupleExpression tuple;
            Debug.Assert(_head != null);
            if ((name = node as NameExpression) != null) {
                _head.Parameters.Add(name.Name);
            } else if ((tuple = node as TupleExpression) != null) {
                foreach (var expr in tuple.Items) {
                    AddParameter(expr);
                }
            } else {
                Trace.TraceWarning("Unable to find parameter in {0}", node);
            }
        }

        public override bool Walk(NameExpression node) {
            _head.Names.Add(Tuple.Create(node.Name, Span.FromBounds(node.StartIndex, node.EndIndex)));
            return base.Walk(node);
        }

        private static string GetFullName(MemberExpression expr) {
            var ne = expr.Target as NameExpression;
            if (ne != null) {
                return ne.Name + "." + expr.Name ?? string.Empty;
            }
            var me = expr.Target as MemberExpression;
            if (me != null) {
                var baseName = GetFullName(me);
                if (baseName == null) {
                    return null;
                }
                return baseName + "." + expr.Name ?? string.Empty;
            }
            return null;
        }

        public override bool Walk(MemberExpression node) {
            var fullname = GetFullName(node);
            if (fullname != null) {
                _head.Names.Add(Tuple.Create(fullname, Span.FromBounds(node.NameHeader, node.EndIndex)));
            }
            return base.Walk(node);
        }

        public override bool Walk(DottedName node) {
            string totalName = "";
            foreach (var name in node.Names) {
                _head.Names.Add(Tuple.Create(totalName + name.Name, Span.FromBounds(name.StartIndex, name.EndIndex)));
                totalName += name.Name + ".";
            }
            return base.Walk(node);
        }

        private string ClassifyName(Tuple<string, Span> node) {
            var name = node.Item1;
            foreach (var sd in _head.EnumerateTowardsGlobal) {
                if (sd.Parameters.Contains(name)) {
                    return PythonPredefinedClassificationTypeNames.Parameter;
                } else if (sd.Functions.Contains(name)) {
                    return PythonPredefinedClassificationTypeNames.Function;
                } else if (sd.Types.Contains(name)) {
                    return PythonPredefinedClassificationTypeNames.Class;
                } else if (sd.Modules.Contains(name)) {
                    return PythonPredefinedClassificationTypeNames.Module;
                }
            }

            if (_analysis != null) {
                var memberType = PythonMemberType.Unknown;
                lock (_analysis) {
                    memberType = _analysis
                        .GetValuesByIndex(name, node.Item2.Start)
                        .Select(v => v.MemberType)
                        .DefaultIfEmpty(PythonMemberType.Unknown)
                        .Aggregate((a, b) => a == b ? a : PythonMemberType.Unknown);
                }

                if (memberType == PythonMemberType.Module) {
                    return PythonPredefinedClassificationTypeNames.Module;
                } else if (memberType == PythonMemberType.Class) {
                    return PythonPredefinedClassificationTypeNames.Class;
                } else if (memberType == PythonMemberType.Function || memberType == PythonMemberType.Method) {
                    return PythonPredefinedClassificationTypeNames.Function;
                }
            }

            return null;
        }

        private void EndScope(bool mergeNames) {
            var sd = _head;
            foreach (var node in sd.Names) {
                var classificationName = ClassifyName(node);
                if (classificationName != null) {
                    AddSpan(node, classificationName);
                    if (mergeNames && sd.Previous != null) {
                        if (classificationName == PythonPredefinedClassificationTypeNames.Module) {
                            sd.Previous.Modules.Add(sd.Name + "." + node.Item1);
                        } else if (classificationName == PythonPredefinedClassificationTypeNames.Class) {
                            sd.Previous.Types.Add(sd.Name + "." + node.Item1);
                        } else if (classificationName == PythonPredefinedClassificationTypeNames.Function) {
                            sd.Previous.Functions.Add(sd.Name + "." + node.Item1);
                        }
                    }
                }
            }
            _head = sd.Previous;
        }

        public override bool Walk(PythonAst node) {
            Debug.Assert(_head == null);
            _head = new StackData(string.Empty, null);
            return base.Walk(node);
        }

        public override void PostWalk(PythonAst node) {
            EndScope(false);
            Debug.Assert(_head == null);
            base.PostWalk(node);
        }

        public override bool Walk(ClassDefinition node) {
            Debug.Assert(_head != null);
            _head.Types.Add(node.NameExpression.Name);
            node.NameExpression.Walk(this);
            BeginScope(node.NameExpression.Name);
            return base.Walk(node);
        }

        public override bool Walk(FunctionDefinition node) {
            if (node.IsCoroutine) {
                AddSpan(Tuple.Create("", new Span(node.StartIndex, 5)), PredefinedClassificationTypeNames.Keyword);
            }

            Debug.Assert(_head != null);
            _head.Functions.Add(node.NameExpression.Name);
            node.NameExpression.Walk(this);
            BeginScope();
            return base.Walk(node);
        }

        public override bool Walk(DictionaryComprehension node) {
            BeginScope();
            return base.Walk(node);
        }

        public override bool Walk(ListComprehension node) {
            BeginScope();
            return base.Walk(node);
        }

        public override bool Walk(GeneratorExpression node) {
            BeginScope();
            return base.Walk(node);
        }

        public override bool Walk(ComprehensionFor node) {
            AddParameter(node.Left);
            return base.Walk(node);
        }

        public override bool Walk(Parameter node) {
            AddParameter(node);
            return base.Walk(node);
        }

        public override bool Walk(ImportStatement node) {
            Debug.Assert(_head != null);
            if (node.AsNames != null) {
                foreach (var name in node.AsNames) {
                    if (name != null && !string.IsNullOrEmpty(name.Name)) {
                        _head.Modules.Add(name.Name);
                        _head.Names.Add(Tuple.Create(name.Name, Span.FromBounds(name.StartIndex, name.EndIndex)));
                    }
                }
            }
            if (node.Names != null) {
                for (int i = 0; i < node.Names.Count; ++i) {
                    var dottedName = node.Names[i];
                    var hasAsName = (node.AsNames != null && node.AsNames.Count > i) ? node.AsNames[i] != null : false;
                    foreach (var name in dottedName.Names) {
                        if (name != null && !string.IsNullOrEmpty(name.Name)) {
                            if (!hasAsName) {
                                _head.Modules.Add(name.Name);
                                _head.Names.Add(Tuple.Create(name.Name, Span.FromBounds(name.StartIndex, name.EndIndex)));
                            } else {
                                // Only want to highlight this instance of the
                                // name, since it isn't going to be bound in the
                                // rest of the module.
                                AddSpan(Tuple.Create(name.Name, Span.FromBounds(name.StartIndex, name.EndIndex)), PythonPredefinedClassificationTypeNames.Module);
                            }
                        }
                    }
                }
            }
            return base.Walk(node);
        }

        public override bool Walk(FromImportStatement node) {
            Debug.Assert(_head != null);
            if (node.Root != null) {
                foreach (var name in node.Root.Names) {
                    if (name != null && !string.IsNullOrEmpty(name.Name)) {
                        AddSpan(Tuple.Create(name.Name, Span.FromBounds(name.StartIndex, name.EndIndex)), PythonPredefinedClassificationTypeNames.Module);
                    }
                }
            }
            if (node.Names != null) {
                foreach (var name in node.Names) {
                    if (name != null && !string.IsNullOrEmpty(name.Name)) {
                        _head.Names.Add(Tuple.Create(name.Name, Span.FromBounds(name.StartIndex, name.EndIndex)));
                    }
                }
            }
            return base.Walk(node);
        }



        public override void PostWalk(ClassDefinition node) {
            EndScope(true);
            Debug.Assert(_head != null);
            base.PostWalk(node);
        }

        public override void PostWalk(FunctionDefinition node) {
            EndScope(false);
            Debug.Assert(_head != null);
            base.PostWalk(node);
        }

        public override void PostWalk(DictionaryComprehension node) {
            EndScope(false);
            Debug.Assert(_head != null);
            base.PostWalk(node);
        }

        public override void PostWalk(ListComprehension node) {
            EndScope(false);
            Debug.Assert(_head != null);
            base.PostWalk(node);
        }

        public override void PostWalk(GeneratorExpression node) {
            EndScope(false);
            Debug.Assert(_head != null);
            base.PostWalk(node);
        }


        public override bool Walk(AwaitExpression node) {
            AddSpan(Tuple.Create("", new Span(node.StartIndex, 5)), PredefinedClassificationTypeNames.Keyword);
            return base.Walk(node);
        }

        public override bool Walk(ForStatement node) {
            if (node.IsAsync) {
                AddSpan(Tuple.Create("", new Span(node.StartIndex, 5)), PredefinedClassificationTypeNames.Keyword);
            }
            return base.Walk(node);
        }

        public override bool Walk(WithStatement node) {
            if (node.IsAsync) {
                AddSpan(Tuple.Create("", new Span(node.StartIndex, 5)), PredefinedClassificationTypeNames.Keyword);
            }
            return base.Walk(node);
        }
    }
}
