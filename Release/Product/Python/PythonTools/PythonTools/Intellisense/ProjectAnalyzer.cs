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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Refactoring;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.PythonTools.Intellisense {
#if INTERACTIVE_WINDOW
    using IReplEvaluator = IInteractiveEngine;
#endif

    /// <summary>
    /// Performs centralized parsing and analysis of Python source code within Visual Studio.
    /// 
    /// This class is responsible for maintaining the up-to-date analysis of the active files being worked
    /// on inside of a Visual Studio project.  
    /// 
    /// This class is built upon the core PythonAnalyzer class which provides basic analysis services.  This class
    /// maintains the thread safety invarients of working with that class, handles parsing of files as they're
    /// updated via interfacing w/ the Visual Studio editor APIs, and supports adding additional files to the 
    /// analysis.
    /// 
    /// New in 1.5.
    /// </summary>
    public sealed class VsProjectAnalyzer : IDisposable {
        private readonly ParseQueue _queue;
        private readonly AnalysisQueue _analysisQueue;
        private readonly IPythonInterpreterFactory _interpreterFactory;
        private readonly Dictionary<BufferParser, IProjectEntry> _openFiles = new Dictionary<BufferParser, IProjectEntry>();
        private readonly IErrorProviderFactory _errorProvider;
        private readonly ConcurrentDictionary<string, IProjectEntry> _projectFiles;
        private readonly PythonAnalyzer _pyAnalyzer;
        private readonly PythonProjectNode _project;
        private readonly AutoResetEvent _queueActivityEvent = new AutoResetEvent(false);
        private readonly IPythonInterpreterFactory[] _allFactories;

        private static readonly Lazy<TaskProvider> _taskProvider = new Lazy<TaskProvider>(() => {
            var _errorList = (IVsTaskList)PythonToolsPackage.GetGlobalService(typeof(SVsErrorList));
            return new TaskProvider(_errorList);
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        private static char[] _invalidPathChars = Path.GetInvalidPathChars();

        private bool _unloading;

        internal VsProjectAnalyzer(IPythonInterpreterFactory factory, IPythonInterpreterFactory[] allFactories, IErrorProviderFactory errorProvider)
            : this(factory.CreateInterpreter(), factory, allFactories, errorProvider) {
        }

        internal VsProjectAnalyzer(IPythonInterpreter interpreter, IPythonInterpreterFactory factory, IPythonInterpreterFactory[] allFactories, IErrorProviderFactory errorProvider, PythonProjectNode project = null) {
            _errorProvider = errorProvider;

            _queue = new ParseQueue(this);
            _analysisQueue = new AnalysisQueue(this);
            _allFactories = allFactories;

            _interpreterFactory = factory;
            _project = project;

            _pyAnalyzer = new PythonAnalyzer(interpreter, factory.GetLanguageVersion());
            interpreter.ModuleNamesChanged += OnModulesChanged;
            _projectFiles = new ConcurrentDictionary<string, IProjectEntry>(StringComparer.OrdinalIgnoreCase);

            if (PythonToolsPackage.Instance != null) {
                _pyAnalyzer.CrossModuleAnalysisLimit = PythonToolsPackage.Instance.OptionsPage.CrossModuleAnalysisLimit;
            }
        }

        private void OnModulesChanged(object sender, EventArgs e) {
            lock (this) {
                _pyAnalyzer.ReloadModules();

                // re-analyze all of the modules when we get a new set of modules loaded...
                foreach (var nameAndEntry in _projectFiles) {
                    _queue.EnqueueFile(nameAndEntry.Value, nameAndEntry.Key);
                }
            }
        }

        /// <summary>
        /// Creates a new ProjectEntry for the collection of buffers.
        /// 
        /// _openFiles must be locked when calling this function.
        /// </summary>
        internal void ReAnalyzeTextBuffers(BufferParser bufferParser) {
            ITextBuffer[] buffers = bufferParser.Buffers;
            if (buffers.Length > 0) {
                var projEntry = CreateProjectEntry(buffers[0], new SnapshotCookie(buffers[0].CurrentSnapshot));
                foreach (var buffer in buffers) {
                    buffer.Properties.RemoveProperty(typeof(IProjectEntry));
                    buffer.Properties.AddProperty(typeof(IProjectEntry), projEntry);

                    var classifier = buffer.GetPythonClassifier();
                    if (classifier != null) {
                        ((PythonClassifier)classifier).NewVersion();
                    }
                }

                bufferParser._currentProjEntry = _openFiles[bufferParser] = projEntry;
                bufferParser._parser = this;

                foreach (var buffer in buffers) {
                    DropDownBarClient client;
                    if (buffer.Properties.TryGetProperty<DropDownBarClient>(typeof(DropDownBarClient), out client)) {
                        client.UpdateProjectEntry(projEntry);
                    }
                }

                bufferParser.Requeue();
            }
        }

        internal void SwitchAnalyzers(VsProjectAnalyzer oldAnalyzer) {
            lock (_openFiles) {
                // copy the Keys here as ReAnalyzeTextBuffers can mutuate the dictionary
                foreach (var bufferParser in oldAnalyzer._openFiles.Keys.ToArray()) {
                    ReAnalyzeTextBuffers(bufferParser);
                }
            }
        }

        /// <summary>
        /// Starts monitoring a buffer for changes so we will re-parse the buffer to update the analysis
        /// as the text changes.
        /// </summary>
        internal MonitoredBufferResult MonitorTextBuffer(ITextView textView, ITextBuffer buffer) {
            IProjectEntry projEntry = CreateProjectEntry(buffer, new SnapshotCookie(buffer.CurrentSnapshot));

            // kick off initial processing on the buffer        
            lock (_openFiles) {
                var bufferParser = _queue.EnqueueBuffer(projEntry, textView, buffer);
                _openFiles[bufferParser] = projEntry;
                return new MonitoredBufferResult(bufferParser, projEntry);
            }
        }

        private IProjectEntry CreateProjectEntry(ITextBuffer buffer, IAnalysisCookie analysisCookie) {
            var replEval = buffer.GetReplEvaluator();
            if (replEval != null) {
                // We have a repl window, create an untracked module.
                return _pyAnalyzer.AddModule(null, null, analysisCookie);
            }

            string path = buffer.GetFilePath();
            if (path == null) {
                return null;
            }

            IProjectEntry entry;
            if (!_projectFiles.TryGetValue(path, out entry)) {
                var modName = PythonAnalyzer.PathToModuleName(path);

                if (buffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                    entry = _pyAnalyzer.AddModule(
                        modName,
                        buffer.GetFilePath(),
                        analysisCookie
                    );

                } else if (buffer.ContentType.IsOfType("XAML")) {
                    entry = _pyAnalyzer.AddXamlFile(buffer.GetFilePath());
                } else {
                    return null;
                }

                _projectFiles[path] = entry;

                if (ImplicitProject && ShouldAnalyzePath(path)) { // don't analyze std lib
                    QueueDirectoryAnalysis(path);
                }
            }

            return entry;
        }

        private void QueueDirectoryAnalysis(string path) {
            ThreadPool.QueueUserWorkItem(x => { lock (this) { AnalyzeDirectory(CommonUtils.NormalizeDirectoryPath(Path.GetDirectoryName(path))); } });
        }

        private bool ShouldAnalyzePath(string path) {
            foreach (var fact in _allFactories) {
                if (!String.IsNullOrWhiteSpace(fact.Configuration.InterpreterPath) &&
                       fact.Configuration.InterpreterPath.IndexOfAny(_invalidPathChars) == -1 &&
                       CommonUtils.IsSubpathOf(Path.GetDirectoryName(fact.Configuration.InterpreterPath), path)) {
                    return false;
                }
            }
            return true;
        }

        internal void StopMonitoringTextBuffer(BufferParser bufferParser) {
            bufferParser.StopMonitoring();
            lock (_openFiles) {
                _openFiles.Remove(bufferParser);
            }
            if (ImplicitProject && _taskProvider.IsValueCreated && bufferParser._currentProjEntry.FilePath != null) {
                // remove the file from the error list
                _taskProvider.Value.Clear(bufferParser._currentProjEntry.FilePath);
            }
        }

        internal IProjectEntry AnalyzeFile(string path) {
            IProjectEntry item;
            if (!_projectFiles.TryGetValue(path, out item)) {
                if (PythonProjectNode.IsPythonFile(path)) {
                    var modName = PythonAnalyzer.PathToModuleName(path);

                    item = _pyAnalyzer.AddModule(
                        modName,
                        path,
                        null
                    );
                } else if (path.EndsWith(".xaml", StringComparison.Ordinal)) {
                    item = _pyAnalyzer.AddXamlFile(path, null);
                }

                if (item != null) {
                    _projectFiles[path] = item;
                    IPythonProjectEntry pyEntry = item as IPythonProjectEntry;
                    if (pyEntry != null) {
                        pyEntry.BeginParsingTree();
                    }
                    _queue.EnqueueFile(item, path);
                }
            }

            return item;
        }

        internal IEnumerable<KeyValuePair<string, IProjectEntry>> LoadedFiles {
            get {
                return _projectFiles;
            }
        }

        internal IProjectEntry GetAnalysisFromFile(string path) {
            IProjectEntry res;
            if (_projectFiles.TryGetValue(path, out res)) {
                return res;
            }
            return null;
        }

        /// <summary>
        /// Gets a ExpressionAnalysis for the expression at the provided span.  If the span is in
        /// part of an identifier then the expression is extended to complete the identifier.
        /// </summary>
        internal static ExpressionAnalysis AnalyzeExpression(ITextSnapshot snapshot, ITrackingSpan span, bool forCompletion = true) {
            var buffer = snapshot.TextBuffer;
            ReverseExpressionParser parser = new ReverseExpressionParser(snapshot, buffer, span);

            var loc = parser.Span.GetSpan(parser.Snapshot.Version);
            var exprRange = parser.GetExpressionRange(forCompletion);

            if (exprRange == null) {
                return ExpressionAnalysis.Empty;
            }

            string text = exprRange.Value.GetText();

            var applicableSpan = parser.Snapshot.CreateTrackingSpan(
                exprRange.Value.Span,
                SpanTrackingMode.EdgeExclusive
            );

            IProjectEntry analysisItem;
            if (buffer.TryGetAnalysis(out analysisItem)) {
                var analysis = ((IPythonProjectEntry)analysisItem).Analysis;
                if (analysis != null && text.Length > 0) {

                    var lineNo = parser.Snapshot.GetLineNumberFromPosition(loc.Start);
                    return new ExpressionAnalysis(
                        snapshot.TextBuffer.GetAnalyzer(),
                        text,
                        analysis,
                        loc.Start,
                        applicableSpan);
                }
            }

            return ExpressionAnalysis.Empty;
        }

        /// <summary>
        /// Gets a CompletionList providing a list of possible members the user can dot through.
        /// </summary>
        internal static CompletionAnalysis GetCompletions(ITextSnapshot snapshot, ITrackingSpan span, CompletionOptions options) {
            var buffer = snapshot.TextBuffer;
            ReverseExpressionParser parser = new ReverseExpressionParser(snapshot, buffer, span);

            var loc = span.GetSpan(snapshot.Version);
            var line = snapshot.GetLineFromPosition(loc.Start);
            var lineStart = line.Start;

            var textLen = loc.End - lineStart.Position;
            if (textLen <= 0) {
                // Ctrl-Space on an empty line, we just want to get global vars

                var classifier = buffer.GetPythonClassifier();
                if (classifier != null) {
                    var classSpans = classifier.GetClassificationSpans(line.ExtentIncludingLineBreak);
                    if (classSpans.Count > 0 &&
                        classSpans[0].ClassificationType.IsOfType(PredefinedClassificationTypeNames.String)) {
                        // unless we're in a string literal
                        return NormalCompletionAnalysis.EmptyCompletionContext;
                    }
                }

                return new NormalCompletionAnalysis(
                    snapshot.TextBuffer.GetAnalyzer(),
                    String.Empty,
                    loc.Start,
                    parser.Snapshot,
                    parser.Span,
                    parser.Buffer,
                    options
                );
            }

            return TrySpecialCompletions(snapshot, span, options) ??
                   GetNormalCompletionContext(parser, loc, options);
        }

        /// <summary>
        /// Gets a CompletionList providing a list of possible members the user can dot through.
        /// </summary>
        [Obsolete("Use GetCompletions with a CompletionOptions instance.")]
        internal static CompletionAnalysis GetCompletions(ITextSnapshot snapshot, ITrackingSpan span, bool intersectMembers = true, bool hideAdvancedMembers = false) {
            return GetCompletions(snapshot, span, new CompletionOptions {
                IntersectMembers = intersectMembers,
                HideAdvancedMembers = hideAdvancedMembers
            });
        }

        /// <summary>
        /// Gets a list of signatuers available for the expression at the provided location in the snapshot.
        /// </summary>
        internal static SignatureAnalysis GetSignatures(ITextSnapshot snapshot, ITrackingSpan span) {
            var buffer = snapshot.TextBuffer;
            ReverseExpressionParser parser = new ReverseExpressionParser(snapshot, buffer, span);

            var loc = parser.Span.GetSpan(parser.Snapshot.Version);

            int paramIndex;
            SnapshotPoint? sigStart;
            string lastKeywordArg;
            bool isParameterName;
            var exprRange = parser.GetExpressionRange(1, out paramIndex, out sigStart, out lastKeywordArg, out isParameterName);
            if (exprRange == null || sigStart == null) {
                return new SignatureAnalysis("", 0, new ISignature[0]);
            }

            Debug.Assert(sigStart != null);
            var text = new SnapshotSpan(exprRange.Value.Snapshot, new Span(exprRange.Value.Start, sigStart.Value.Position - exprRange.Value.Start)).GetText();
            var applicableSpan = parser.Snapshot.CreateTrackingSpan(exprRange.Value.Span, SpanTrackingMode.EdgeInclusive);

            if (snapshot.TextBuffer.GetAnalyzer().ShouldEvaluateForCompletion(text)) {
                var liveSigs = TryGetLiveSignatures(snapshot, paramIndex, text, applicableSpan, lastKeywordArg);
                if (liveSigs != null) {
                    return liveSigs;
                }
            }

            var start = Stopwatch.ElapsedMilliseconds;

            var analysisItem = buffer.GetAnalysis();
            if (analysisItem != null) {
                var analysis = ((IPythonProjectEntry)analysisItem).Analysis;
                if (analysis != null) {

                    int index = loc.Start;

                    IEnumerable<IOverloadResult> sigs;
                    lock (snapshot.TextBuffer.GetAnalyzer()) {
                        sigs = analysis.GetSignaturesByIndex(text, index);
                    }
                    var end = Stopwatch.ElapsedMilliseconds;

                    if (/*Logging &&*/ (end - start) > CompletionAnalysis.TooMuchTime) {
                        Trace.WriteLine(String.Format("{0} lookup time {1} for signatures", text, end - start));
                    }

                    var result = new List<ISignature>();
                    foreach (var sig in sigs) {
                        result.Add(new PythonSignature(applicableSpan, sig, paramIndex, lastKeywordArg));
                    }

                    return new SignatureAnalysis(
                        text,
                        paramIndex,
                        result,
                        lastKeywordArg
                    );
                }
            }
            return new SignatureAnalysis(text, paramIndex, new ISignature[0]);
        }

        internal static MissingImportAnalysis GetMissingImports(ITextSnapshot snapshot, ITrackingSpan span) {
            ReverseExpressionParser parser = new ReverseExpressionParser(snapshot, snapshot.TextBuffer, span);
            var loc = span.GetSpan(snapshot.Version);
            int dummy;
            SnapshotPoint? dummyPoint;
            string lastKeywordArg;
            bool isParameterName;
            var exprRange = parser.GetExpressionRange(0, out dummy, out dummyPoint, out lastKeywordArg, out isParameterName);
            if (exprRange == null || isParameterName) {
                return MissingImportAnalysis.Empty;
            }

            var analysis = ((IPythonProjectEntry)snapshot.TextBuffer.GetAnalysis()).Analysis;
            if (analysis == null) {
                return MissingImportAnalysis.Empty;
            }

            var text = exprRange.Value.GetText();
            var analyzer = analysis.ProjectState;
            var index = span.GetStartPoint(snapshot).Position;

            var expr = Statement.GetExpression(analysis.GetAstFromTextByIndex(text, index).Body);

            if (expr != null && expr is NameExpression) {
                var nameExpr = (NameExpression)expr;

                if (!IsImplicitlyDefinedName(nameExpr)) {
                    var applicableSpan = parser.Snapshot.CreateTrackingSpan(
                        exprRange.Value.Span,
                        SpanTrackingMode.EdgeExclusive
                    );

                    lock (snapshot.TextBuffer.GetAnalyzer()) {
                        var variables = analysis.GetVariablesByIndex(text, index).Where(IsDefinition).Count();
                        var values = analysis.GetValuesByIndex(text, index).ToArray();

                        // if we have type information or an assignment to the variable we won't offer 
                        // an import smart tag.
                        if (values.Length == 0 && variables == 0) {
                            string name = nameExpr.Name;
                            var imports = analysis.ProjectState.FindNameInAllModules(name);

                            return new MissingImportAnalysis(imports, applicableSpan);
                        }
                    }
                }
            }

            // if we have type information don't offer to add imports
            return MissingImportAnalysis.Empty;
        }

        private static bool IsDefinition(IAnalysisVariable variable) {
            return variable.Type == VariableType.Definition;
        }

        private static bool IsImplicitlyDefinedName(NameExpression nameExpr) {
            return nameExpr.Name == "__all__" ||
                nameExpr.Name == "__file__" ||
                nameExpr.Name == "__doc__" ||
                nameExpr.Name == "__name__";
        }

        internal bool IsAnalyzing {
            get {
                return _queue.IsParsing || _analysisQueue.IsAnalyzing;
            }
        }

        internal void WaitForCompleteAnalysis(Func<int, bool> itemsLeftUpdated) {
            if (_queue.IsParsing || _analysisQueue.IsAnalyzing) {
                while (_queue.IsParsing || _analysisQueue.IsAnalyzing) {
                    QueueActivityEvent.WaitOne(100);

                    int itemsLeft = _queue.ParsePending + _analysisQueue.AnalysisPending;

                    if (!itemsLeftUpdated(itemsLeft)) {
                        break;
                    }
                }
            } else {
                itemsLeftUpdated(0);
            }
        }

        internal AutoResetEvent QueueActivityEvent {
            get {
                return _queueActivityEvent;
            }
        }

        internal bool ImplicitProject {
            get {
                return _project == null;
            }
        }

        internal IPythonInterpreterFactory InterpreterFactory {
            get {
                return _interpreterFactory;
            }
        }

        internal IPythonInterpreter Interpreter {
            get {
                return _pyAnalyzer.Interpreter;
            }
        }

        internal PythonAnalyzer Project {
            get {
                return _pyAnalyzer;
            }
        }

        internal PythonAst ParseFile(ITextSnapshot snapshot) {
            var parser = Parser.CreateParser(
                new SnapshotSpanSourceCodeReader(
                    new SnapshotSpan(snapshot, 0, snapshot.Length)
                ),
                Project.LanguageVersion,
                new ParserOptions() { Verbatim = true, BindReferences = true }
            );

            var ast = parser.ParseFile();
            return ast;

        }

        internal void ParseFile(IProjectEntry projectEntry, string filename, FileStream content, Severity indentationSeverity) {
            IPythonProjectEntry pyEntry;
            IExternalProjectEntry externalEntry;

            if ((pyEntry = projectEntry as IPythonProjectEntry) != null) {
                PythonAst ast;
                CollectingErrorSink errorSink;
                ParsePythonCode(content, indentationSeverity, out ast, out errorSink);

                if (ast != null) {
                    pyEntry.UpdateTree(ast, new FileCookie(filename));
                    _analysisQueue.Enqueue(pyEntry, AnalysisPriority.Normal);
                } else {
                    // notify that we failed to update the existing analysis
                    pyEntry.UpdateTree(null, null);
                }

                if (errorSink.Warnings.Count > 0 || errorSink.Errors.Count > 0) {
                    TaskProvider provider = GetTaskProviderAndClearProjectItems(projectEntry);
                    if (provider != null) {
                        provider.AddWarnings(projectEntry.FilePath, errorSink.Warnings);
                        provider.AddErrors(projectEntry.FilePath, errorSink.Errors);

                        UpdateErrorList(errorSink, projectEntry.FilePath, provider);
                    }
                }
            } else if ((externalEntry = projectEntry as IExternalProjectEntry) != null) {
                externalEntry.ParseContent(new StreamReader(content), new FileCookie(filename));
                _analysisQueue.Enqueue(projectEntry, AnalysisPriority.Normal);
            }
        }

        internal void ParseBuffers(BufferParser bufferParser, Severity indentationSeverity, params ITextSnapshot[] snapshots) {
            IProjectEntry analysis = bufferParser._currentProjEntry;

            IPythonProjectEntry pyProjEntry = analysis as IPythonProjectEntry;
            List<PythonAst> asts = new List<PythonAst>();
            foreach (var snapshot in snapshots) {
                if (snapshot.TextBuffer.Properties.ContainsProperty(PythonReplEvaluator.InputBeforeReset)) {
                    continue;
                }

                if (pyProjEntry != null && snapshot.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                    if (!snapshot.IsReplBufferWithCommand()) {
                        PythonAst ast;
                        CollectingErrorSink errorSink;

                        var reader = new SnapshotSpanSourceCodeStream(new SnapshotSpan(snapshot, new Span(0, snapshot.Length)));
                        ParsePythonCode(reader, indentationSeverity, out ast, out errorSink);

                        if (ast != null) {
                            asts.Add(ast);
                        }

                        // update squiggles for the buffer
                        var buffer = snapshot.TextBuffer;

                        SimpleTagger<ErrorTag> squiggles = _errorProvider.GetErrorTagger(snapshot.TextBuffer);
                        TaskProvider provider = GetTaskProviderAndClearProjectItems(bufferParser._currentProjEntry);

                        // SimpleTagger says it's thread safe (http://msdn.microsoft.com/en-us/library/dd885186.aspx), but it's buggy...  
                        // Post the removing of squiggles to the UI thread so that we don't crash when we're racing with 
                        // updates to the buffer.  http://pytools.codeplex.com/workitem/142

                        var dispatcher = bufferParser.Dispatcher;
                        if (dispatcher != null) {   // not a UI element in completion context tests w/ mocks.                            
                            dispatcher.BeginInvoke((Action)new SquiggleUpdater(errorSink, snapshot, squiggles, bufferParser._currentProjEntry.FilePath, provider).DoUpdate);
                        }

                        string path = bufferParser._currentProjEntry.FilePath;
                        if (path != null) {
                            UpdateErrorList(errorSink, path, provider);
                        }
                    }
                } else {
                    // other file such as XAML
                    IExternalProjectEntry externalEntry;
                    if ((externalEntry = (analysis as IExternalProjectEntry)) != null) {
                        var snapshotContent = new SnapshotSpanSourceCodeReader(new SnapshotSpan(snapshot, new Span(0, snapshot.Length)));
                        externalEntry.ParseContent(snapshotContent, new SnapshotCookie(snapshotContent.Snapshot));
                        _analysisQueue.Enqueue(analysis, AnalysisPriority.High);
                    }
                }
            }

            if (pyProjEntry != null) {
                if (asts.Count > 0) {
                    PythonAst finalAst;
                    if (asts.Count == 1) {
                        finalAst = asts[0];
                    } else {
                        // multiple ASTs, merge them together
                        List<Statement> bodies = new List<Statement>();
                        foreach (var ast in asts) {
                            bodies.Add(ast.Body);
                        }
                        finalAst = new PythonAst(new SuiteStatement(bodies.ToArray()), new int[0]);
                    }

                    pyProjEntry.UpdateTree(finalAst, new SnapshotCookie(snapshots[0])); // SnapshotCookie is not entirely right, we should merge the snapshots
                    _analysisQueue.Enqueue(analysis, AnalysisPriority.High);
                } else {
                    // indicate that we are done parsing.
                    PythonAst prevTree;
                    IAnalysisCookie prevCookie;
                    pyProjEntry.GetTreeAndCookie(out prevTree, out prevCookie);
                    pyProjEntry.UpdateTree(prevTree, prevCookie);
                }
            }
        }

        class SquiggleUpdater {
            private CollectingErrorSink _errorSink;
            private ITextSnapshot _snapshot;
            private SimpleTagger<ErrorTag> _squiggles;
            private TaskProvider _provider;
            private readonly string _filename;

            public SquiggleUpdater(CollectingErrorSink errorSink, ITextSnapshot snapshot, SimpleTagger<ErrorTag> squiggles, string filename, TaskProvider provider) {
                _errorSink = errorSink;
                _snapshot = snapshot;
                _squiggles = squiggles;
                _provider = provider;
                _filename = filename;
            }

            public void DoUpdate() {
                _squiggles.RemoveTagSpans(x => true);

                if (_filename != null) {
                    AddWarnings(_snapshot, _errorSink, _squiggles, _filename);

                    AddErrors(_snapshot, _errorSink, _squiggles, _filename);

                    if (_provider != null) {
                        _provider.AddWarnings(_filename, _errorSink.Warnings);
                        _provider.AddErrors(_filename, _errorSink.Errors);
                    }
                }
            }
        }

        private static void AddErrors(ITextSnapshot snapshot, CollectingErrorSink errorSink, SimpleTagger<ErrorTag> squiggles, string filename) {
            foreach (ErrorResult error in errorSink.Errors) {
                var span = error.Span;
                var tspan = CreateSpan(snapshot, span);
                squiggles.CreateTagSpan(tspan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, error.Message));
            }
        }

        private static void AddWarnings(ITextSnapshot snapshot, CollectingErrorSink errorSink, SimpleTagger<ErrorTag> squiggles, string filename) {
            foreach (ErrorResult warning in errorSink.Warnings) {
                var span = warning.Span;
                var tspan = CreateSpan(snapshot, span);
                squiggles.CreateTagSpan(tspan, new ErrorTag(PredefinedErrorTypeNames.Warning, warning.Message));
            }
        }

        private TaskProvider GetTaskProviderAndClearProjectItems(IProjectEntry projEntry) {
            if (PythonToolsPackage.Instance != null) {
                if (projEntry.FilePath != null) {
                    _taskProvider.Value.Clear(projEntry.FilePath);
                }
            }
            return _taskProvider.Value;
        }

        private void UpdateErrorList(CollectingErrorSink errorSink, string filepath, TaskProvider provider) {
            if (_project != null && provider != null) {
                if (errorSink.Errors.Count > 0) {
                    _project.ErrorFiles.Add(filepath);
                } else {
                    _project.ErrorFiles.Remove(filepath);
                }
            }

            if (provider != null && (errorSink.Errors.Count > 0 || errorSink.Warnings.Count > 0)) {
                provider.UpdateTasks();
            }
        }

        private void ParsePythonCode(Stream content, Severity indentationSeverity, out PythonAst ast, out CollectingErrorSink errorSink) {
            ast = null;
            errorSink = new CollectingErrorSink();

            using (var parser = Parser.CreateParser(content, _interpreterFactory.GetLanguageVersion(), new ParserOptions() { ErrorSink = errorSink, IndentationInconsistencySeverity = indentationSeverity, BindReferences = true })) {
                ast = ParseOneFile(ast, parser);
            }
        }

        private void ParsePythonCode(TextReader content, Severity indentationSeverity, out PythonAst ast, out CollectingErrorSink errorSink) {
            ast = null;
            errorSink = new CollectingErrorSink();

            using (var parser = Parser.CreateParser(content, _interpreterFactory.GetLanguageVersion(), new ParserOptions() { ErrorSink = errorSink, IndentationInconsistencySeverity = indentationSeverity, BindReferences = true })) {
                ast = ParseOneFile(ast, parser);
            }
        }

        private static PythonAst ParseOneFile(PythonAst ast, Parser parser) {
            if (parser != null) {
                try {
                    ast = parser.ParseFile();
                } catch (BadSourceException) {
                } catch (Exception e) {
                    Debug.Assert(false, String.Format("Failure in Python parser: {0}", e.ToString()));
                }

            }
            return ast;
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

        #region Implementation Details

        private static Stopwatch _stopwatch = MakeStopWatch();

        internal static Stopwatch Stopwatch {
            get {
                return _stopwatch;
            }
        }

        private static SignatureAnalysis TryGetLiveSignatures(ITextSnapshot snapshot, int paramIndex, string text, ITrackingSpan applicableSpan, string lastKeywordArg) {
            IReplEvaluator eval;
            IPythonReplIntellisense dlrEval;
            if (snapshot.TextBuffer.Properties.TryGetProperty<IReplEvaluator>(typeof(IReplEvaluator), out eval) &&
                (dlrEval = eval as IPythonReplIntellisense) != null) {
                if (text.EndsWith("(")) {
                    text = text.Substring(0, text.Length - 1);
                }
                var liveSigs = dlrEval.GetSignatureDocumentation(text);

                if (liveSigs != null && liveSigs.Length > 0) {
                    return new SignatureAnalysis(text, paramIndex, GetLiveSignatures(text, liveSigs, paramIndex, applicableSpan, lastKeywordArg), lastKeywordArg);
                }
            }
            return null;
        }

        private static ISignature[] GetLiveSignatures(string text, ICollection<OverloadDoc> liveSigs, int paramIndex, ITrackingSpan span, string lastKeywordArg) {
            ISignature[] res = new ISignature[liveSigs.Count];
            int i = 0;
            foreach (var sig in liveSigs) {
                var parameters = new ParameterResult[sig.Parameters.Length];
                int j = 0;
                foreach (var param in sig.Parameters) {
                    parameters[j++] = new ParameterResult(param.Name);
                }

                res[i++] = new PythonSignature(
                    span,
                    new LiveOverloadResult(text, sig.Documentation, parameters),
                    paramIndex,
                    lastKeywordArg
                );
            }
            return res;
        }

        class LiveOverloadResult : IOverloadResult {
            private readonly string _name, _doc;
            private readonly ParameterResult[] _parameters;

            public LiveOverloadResult(string name, string documentation, ParameterResult[] parameters) {
                _name = name;
                _doc = documentation;
                _parameters = parameters;
            }

            #region IOverloadResult Members

            public string Name {
                get { return _name; }
            }

            public string Documentation {
                get { return _doc; }
            }

            public ParameterResult[] Parameters {
                get { return _parameters; }
            }

            #endregion
        }

        internal bool ShouldEvaluateForCompletion(string source) {
            if (PythonToolsPackage.Instance != null) {
                switch (PythonToolsPackage.Instance.InteractiveOptionsPage.GetOptions(_interpreterFactory).ReplIntellisenseMode) {
                    case ReplIntellisenseMode.AlwaysEvaluate: return true;
                    case ReplIntellisenseMode.NeverEvaluate: return false;
                    case ReplIntellisenseMode.DontEvaluateCalls:
                        var parser = Parser.CreateParser(new StringReader(source), _interpreterFactory.GetLanguageVersion());

                        var stmt = parser.ParseSingleStatement();
                        var exprWalker = new ExprWalker();

                        stmt.Walk(exprWalker);
                        return exprWalker.ShouldExecute;
                    default: throw new InvalidOperationException();
                }
            }
            return false;
        }

        class ExprWalker : PythonWalker {
            public bool ShouldExecute = true;

            public override bool Walk(CallExpression node) {
                ShouldExecute = false;
                return base.Walk(node);
            }
        }

        private static CompletionAnalysis TrySpecialCompletions(ITextSnapshot snapshot, ITrackingSpan span, CompletionOptions options) {
            var snapSpan = span.GetSpan(snapshot);
            var buffer = snapshot.TextBuffer;
            var classifier = (PythonClassifier)buffer.Properties.GetProperty(typeof(PythonClassifier));
            var tokens = classifier.GetClassificationSpans(new SnapshotSpan(snapSpan.Start.GetContainingLine().Start, snapSpan.End));
            if (tokens.Count > 0) {
                // Check for context-sensitive intellisense
                var lastClass = tokens[tokens.Count - 1];

                if (lastClass.ClassificationType == classifier.Provider.Comment) {
                    // No completions in comments
                    return CompletionAnalysis.EmptyCompletionContext;
                } else if (lastClass.ClassificationType == classifier.Provider.StringLiteral) {
                    // String completion
                    if (lastClass.Span.Start.GetContainingLine().LineNumber == lastClass.Span.End.GetContainingLine().LineNumber) {
                        return new StringLiteralCompletionList(lastClass.Span.GetText(), snapSpan.Start, span, buffer, options);
                    } else {
                        // multi-line string, no string completions.
                        return CompletionAnalysis.EmptyCompletionContext;
                    }
                } else if (lastClass.ClassificationType == classifier.Provider.Operator &&
                    lastClass.Span.GetText() == "@") {

                    return new DecoratorCompletionAnalysis(lastClass.Span.GetText(), snapSpan.Start, span, buffer, options);
                } else if (CompletionAnalysis.IsKeyword(lastClass, "raise") || CompletionAnalysis.IsKeyword(lastClass, "except")) {
                    return new ExceptionCompletionAnalysis(lastClass.Span.GetText(), snapSpan.Start, span, buffer, options);
                } else if (CompletionAnalysis.IsKeyword(lastClass, "def")) {
                    return new OverrideCompletionAnalysis(lastClass.Span.GetText(), lastClass.Span.Start, span, buffer, options);
                }

                // Import completions
                var first = tokens[0];
                if (CompletionAnalysis.IsKeyword(first, "import")) {
                    return ImportCompletionAnalysis.Make(first, lastClass, snapSpan, snapshot, span, buffer, IsSpaceCompletion(snapshot, snapSpan), options);
                } else if (CompletionAnalysis.IsKeyword(first, "from")) {
                    return FromImportCompletionAnalysis.Make(tokens, first, snapSpan, snapshot, span, buffer, IsSpaceCompletion(snapshot, snapSpan), options);
                }
                return null;
            }

            return null;
        }

        private static CompletionAnalysis GetNormalCompletionContext(ReverseExpressionParser parser, Span loc, CompletionOptions options) {
            var exprRange = parser.GetExpressionRange();
            if (exprRange == null) {
                return CompletionAnalysis.EmptyCompletionContext;
            }
            if (IsSpaceCompletion(parser.Snapshot, loc) && !IntellisenseController.ForceCompletions) {
                return CompletionAnalysis.EmptyCompletionContext;
            }

            var text = exprRange.Value.GetText();

            var applicableSpan = parser.Snapshot.CreateTrackingSpan(
                exprRange.Value.Span,
                SpanTrackingMode.EdgeExclusive
            );

            // check if we're at the beginning of a line, this includes having a single
            // name expression which we're hitting Ctrl-Space after.
            var enumerator = ReverseExpressionParser.ReverseClassificationSpanEnumerator(parser.Classifier, exprRange.Value.End);
            bool hitName = false, firstToken = true, hitOther = false;
            while (enumerator.MoveNext()) {
                if (enumerator.Current == null) {
                    break;
                } else if (enumerator.Current.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier) &&
                    !hitName) {
                    hitName = true;
                } else {
                    hitOther = true;
                    break;
                }

                firstToken = false;
            }

            options = options.Clone();
            options.IncludeStatementKeywords = false;

            if (!hitOther && (hitName || firstToken)) {
                // make sure we're not in a grouping
                var groupingParser = new ReverseExpressionParser(parser.Snapshot, parser.Buffer, applicableSpan);
                int paramIndex;
                SnapshotPoint? sigStart;
                string lastKeyword;
                bool isParamName;
                var groupingExprRange = groupingParser.GetExpressionRange(
                    1,
                    out paramIndex,
                    out sigStart,
                    out lastKeyword,
                    out isParamName,
                    false
                );

                if (groupingExprRange == null || groupingExprRange == exprRange) {
                    options.IncludeStatementKeywords = true;
                }
            }

            return new NormalCompletionAnalysis(
                parser.Snapshot.TextBuffer.GetAnalyzer(),
                text,
                loc.Start,
                parser.Snapshot,
                applicableSpan,
                parser.Buffer,
                options
            );
        }

        private static bool IsSpaceCompletion(ITextSnapshot snapshot, Span loc) {
            var keySpan = new SnapshotSpan(snapshot, loc.Start - 1, 1);
            return (keySpan.GetText() == " ");
        }

        private static Stopwatch MakeStopWatch() {
            var res = new Stopwatch();
            res.Start();
            return res;
        }

        /// <summary>
        /// Analyzes a complete directory including all of the contained files and packages.
        /// </summary>
        public void AnalyzeDirectory(string dir) {
            _analysisQueue.Enqueue(new AddDirectoryAnalysis(dir, this), AnalysisPriority.High);
        }

        class AddDirectoryAnalysis : IAnalyzable {
            private readonly string _dir;
            private readonly VsProjectAnalyzer _analyzer;

            public AddDirectoryAnalysis(string dir, VsProjectAnalyzer analyzer) {
                _dir = dir;
                _analyzer = analyzer;
            }

            #region IAnalyzable Members

            public void Analyze() {
                _analyzer.AnalyzeDirectoryWorker(_dir, true, ref _analyzer._analysisQueue._unload);
            }

            #endregion
        }

        private void AnalyzeDirectoryWorker(string dir, bool addDir, ref bool unload) {
            if (addDir) {
                lock (this) {
                    _pyAnalyzer.AddAnalysisDirectory(dir);
                }
            }

            try {
                foreach (string filename in Directory.GetFiles(dir, "*.py")) {
                    AnalyzeFile(filename);
                }
            } catch (IOException) {
                // We want to handle DirectoryNotFound, DriveNotFound, PathTooLong
            } catch (UnauthorizedAccessException) {
            }

            try {
                foreach (string filename in Directory.GetFiles(dir, "*.pyw")) {
                    if (unload) {
                        break;
                    }
                    AnalyzeFile(filename);
                }
            } catch (IOException) {
                // We want to handle DirectoryNotFound, DriveNotFound, PathTooLong
            } catch (UnauthorizedAccessException) {
            }

            try {
                foreach (string innerDir in Directory.GetDirectories(dir)) {
                    if (unload) {
                        break;
                    }
                    if (File.Exists(Path.Combine(innerDir, "__init__.py"))) {
                        AnalyzeDirectoryWorker(innerDir, false, ref unload);
                    }
                }
            } catch (IOException) {
                // We want to handle DirectoryNotFound, DriveNotFound, PathTooLong
            } catch (UnauthorizedAccessException) {
            }
        }

        internal void StopAnalyzingDirectory(string directory) {
            lock (this) {
                _pyAnalyzer.RemoveAnalysisDirectory(directory);
            }
        }

        internal void BeginUnload() {
            _unloading = true;
        }

        internal void EndUnload() {
            if (_unloading && _taskProvider.IsValueCreated) {
                _unloading = false;
                _taskProvider.Value.UpdateTasks();
            }
        }

        internal void UnloadFile(IProjectEntry entry) {
            if (entry != null && entry.FilePath != null) {
                if (_taskProvider.IsValueCreated) {
                    // _taskProvider may not be created if we've never opened a Python file and
                    // none of the project files have errors
                    _taskProvider.Value.Clear(entry.FilePath, !_unloading);
                }
                if (_project != null) {
                    _project.ErrorFiles.Remove(entry.FilePath);
                }
                _pyAnalyzer.RemoveModule(entry);
                IProjectEntry removed;
                _projectFiles.TryRemove(entry.FilePath, out removed);
            }
        }

        #endregion

        class TaskProvider : IVsTaskProvider {
            private readonly Dictionary<string, List<ErrorResult>> _warnings = new Dictionary<string, List<ErrorResult>>();
            private readonly Dictionary<string, List<ErrorResult>> _errors = new Dictionary<string, List<ErrorResult>>();
            private readonly uint _cookie;
            private readonly IVsTaskList _errorList;


            private class WorkerMessage {
                public enum MessageType { Clear, Warnings, Errors, Update }
                public MessageType Type;
                public string Filename;
                public List<ErrorResult> Errors;

                public readonly static WorkerMessage Update = new WorkerMessage { Type = MessageType.Update };
            }
            private bool _hasWorker;
            private readonly BlockingCollection<WorkerMessage> _workerQueue;

            public TaskProvider(IVsTaskList errorList) {
                _errorList = errorList;
                if (_errorList != null) {
                    ErrorHandler.ThrowOnFailure(_errorList.RegisterTaskProvider(this, out _cookie));
                }
                _workerQueue = new BlockingCollection<WorkerMessage>();
            }

            private void Worker(object param) {
                bool changed = false;
                WorkerMessage msg;
                List<ErrorResult> existing;

                for (; ; ) {
                    while (_workerQueue.TryTake(out msg, 5000)) {
                        switch (msg.Type) {
                            case WorkerMessage.MessageType.Clear:
                                lock (this) {
                                    changed = _errors.Remove(msg.Filename) || changed;
                                    changed = _warnings.Remove(msg.Filename) || changed;
                                }
                                break;
                            case WorkerMessage.MessageType.Warnings:
                                lock (this) {
                                    if (_warnings.TryGetValue(msg.Filename, out existing)) {
                                        existing.AddRange(msg.Errors);
                                    } else {
                                        _warnings[msg.Filename] = msg.Errors;
                                    }
                                }
                                changed = true;
                                break;
                            case WorkerMessage.MessageType.Errors:
                                lock (this) {
                                    if (_errors.TryGetValue(msg.Filename, out existing)) {
                                        existing.AddRange(msg.Errors);
                                    } else {
                                        _errors[msg.Filename] = msg.Errors;
                                    }
                                }
                                changed = true;
                                break;
                            case WorkerMessage.MessageType.Update:
                                changed = true;
                                break;
                        }
                    }

                    lock (_workerQueue) {
                        if (_workerQueue.Count == 0) {
                            _hasWorker = false;
                            break;
                        }
                    }
                }
            }

            private void SendMessage(WorkerMessage msg) {
                lock (_workerQueue) {
                    _workerQueue.Add(msg);
                    if (!_hasWorker) {
                        _hasWorker = true;
                        ThreadPool.QueueUserWorkItem(Worker);
                    }
                }
            }

            public void UpdateTasks() {
                if (_errorList != null) {
                    SendMessage(WorkerMessage.Update);
                }
            }

            public uint Cookie {
                get {
                    return _cookie;
                }
            }

            #region IVsTaskProvider Members

            public int EnumTaskItems(out IVsEnumTaskItems ppenum) {
                lock (this) {
                    ppenum = new TaskEnum(CopyErrorList(_warnings), CopyErrorList(_errors));
                }
                return VSConstants.S_OK;
            }

            private static Dictionary<string, ErrorResult[]> CopyErrorList(Dictionary<string, List<ErrorResult>> input) {
                Dictionary<string, ErrorResult[]> errors = new Dictionary<string, ErrorResult[]>(input.Count);
                foreach (var keyvalue in input) {
                    errors[keyvalue.Key] = keyvalue.Value.ToArray();
                }
                return errors;
            }

            public int ImageList(out IntPtr phImageList) {
                // not necessary if we report our category as build compile.
                phImageList = IntPtr.Zero;
                return VSConstants.E_NOTIMPL;
            }

            public int OnTaskListFinalRelease(IVsTaskList pTaskList) {
                return VSConstants.S_OK;
            }

            public int ReRegistrationKey(out string pbstrKey) {
                pbstrKey = null;
                return VSConstants.E_NOTIMPL;
            }

            public int SubcategoryList(uint cbstr, string[] rgbstr, out uint pcActual) {
                pcActual = 0;
                return VSConstants.S_OK;
            }

            #endregion

            internal void AddErrors(string filename, List<ErrorResult> errors) {
                if (errors.Count > 0) {
                    SendMessage(new WorkerMessage { Type = WorkerMessage.MessageType.Errors, Filename = filename, Errors = errors });
                }
            }

            internal void AddWarnings(string filename, List<ErrorResult> warnings) {
                if (warnings.Count > 0) {
                    SendMessage(new WorkerMessage { Type = WorkerMessage.MessageType.Warnings, Filename = filename, Errors = warnings });
                }
            }

            internal void Clear(string filename) {
                Clear(filename, true);
            }

            internal void Clear(string filename, bool updateList) {
                SendMessage(new WorkerMessage { Type = WorkerMessage.MessageType.Clear, Filename = filename });
                if (updateList) {
                    SendMessage(WorkerMessage.Update);
                }
            }

            class TaskEnum : IVsEnumTaskItems {
                private readonly Dictionary<string, ErrorResult[]> _warnings;
                private readonly Dictionary<string, ErrorResult[]> _errors;
                private IEnumerator<ErrorInfo> _enum;

                public TaskEnum(Dictionary<string, ErrorResult[]> warnings, Dictionary<string, ErrorResult[]> errors) {
                    _warnings = warnings;
                    _errors = errors;
                    _enum = Enumerator(warnings, errors);
                }

                struct ErrorInfo {
                    public readonly string Filename;
                    public readonly ErrorResult Error;
                    public readonly bool IsError;

                    public ErrorInfo(string filename, ErrorResult error, bool isError) {
                        Filename = filename;
                        Error = error;
                        IsError = isError;
                    }
                }

                IEnumerator<ErrorInfo> Enumerator(Dictionary<string, ErrorResult[]> warnings, Dictionary<string, ErrorResult[]> errors) {
                    foreach (var fileAndErrorList in warnings) {
                        foreach (var error in fileAndErrorList.Value) {
                            yield return new ErrorInfo(fileAndErrorList.Key, error, false);
                        }
                    }

                    foreach (var fileAndErrorList in errors) {
                        foreach (var error in fileAndErrorList.Value) {
                            yield return new ErrorInfo(fileAndErrorList.Key, error, true);
                        }
                    }
                }

                #region IVsEnumTaskItems Members

                public int Clone(out IVsEnumTaskItems ppenum) {
                    ppenum = new TaskEnum(_warnings, _errors);
                    return VSConstants.S_OK;
                }

                public int Next(uint celt, IVsTaskItem[] rgelt, uint[] pceltFetched = null) {
                    for (int i = 0; i < celt && _enum.MoveNext(); i++) {
                        var next = _enum.Current;
                        pceltFetched[0] = (uint)i + 1;
                        rgelt[i] = new TaskItem(next.Error, next.Filename, next.IsError);
                    }

                    return VSConstants.S_OK;
                }

                public int Reset() {
                    _enum = Enumerator(_warnings, _errors);
                    return VSConstants.S_OK;
                }

                public int Skip(uint celt) {
                    while (celt != 0 && _enum.MoveNext()) {
                        celt--;
                    }
                    return VSConstants.S_OK;
                }

                #endregion

                class TaskItem : IVsTaskItem {
                    private readonly ErrorResult _error;
                    private readonly string _path;
                    private readonly bool _isError;

                    public TaskItem(ErrorResult error, string path, bool isError) {
                        _error = error;
                        _path = path;
                        _isError = isError;
                    }

                    public SourceSpan Span {
                        get {
                            return _error.Span;
                        }
                    }

                    public string Message {
                        get {
                            return _error.Message;
                        }
                    }

                    #region IVsTaskItem Members

                    public int CanDelete(out int pfCanDelete) {
                        pfCanDelete = 0;
                        return VSConstants.S_OK;
                    }

                    public int Category(VSTASKCATEGORY[] pCat) {
                        pCat[0] = VSTASKCATEGORY.CAT_BUILDCOMPILE;
                        return VSConstants.S_OK;
                    }

                    public int Column(out int piCol) {
                        if (Span.Start.Line == 1 && Span.Start.Column == 1 && Span.Start.Index != 0) {
                            // we don't have the column number calculated
                            piCol = 0;
                            return VSConstants.E_FAIL;
                        }
                        piCol = Span.Start.Column - 1;
                        return VSConstants.S_OK;
                    }

                    public int Document(out string pbstrMkDocument) {
                        pbstrMkDocument = _path;
                        return VSConstants.S_OK;
                    }

                    public int HasHelp(out int pfHasHelp) {
                        pfHasHelp = 0;
                        return VSConstants.S_OK;
                    }

                    public int ImageListIndex(out int pIndex) {
                        pIndex = 0;
                        return VSConstants.E_NOTIMPL;
                    }

                    public int IsReadOnly(VSTASKFIELD field, out int pfReadOnly) {
                        pfReadOnly = 1;
                        return VSConstants.S_OK;
                    }

                    public int Line(out int piLine) {
                        if (Span.Start.Line == 1 && Span.Start.Column == 1 && Span.Start.Index != 0) {
                            // we don't have the line number calculated
                            piLine = 0;
                            return VSConstants.E_FAIL;
                        }
                        piLine = Span.Start.Line - 1;
                        return VSConstants.S_OK;
                    }

                    public int NavigateTo() {
                        if (Span.Start.Line == 1 && Span.Start.Column == 1 && Span.Start.Index != 0) {
                            // we have just an absolute index, use that to naviagte
                            PythonToolsPackage.NavigateTo(_path, Guid.Empty, Span.Start.Index);
                        } else {
                            PythonToolsPackage.NavigateTo(_path, Guid.Empty, Span.Start.Line - 1, Span.Start.Column - 1);
                        }
                        return VSConstants.S_OK;
                    }

                    public int NavigateToHelp() {
                        return VSConstants.E_NOTIMPL;
                    }

                    public int OnDeleteTask() {
                        return VSConstants.E_NOTIMPL;
                    }

                    public int OnFilterTask(int fVisible) {
                        return VSConstants.E_NOTIMPL;
                    }

                    public int SubcategoryIndex(out int pIndex) {
                        pIndex = 0;
                        return VSConstants.E_NOTIMPL;
                    }

                    public int get_Checked(out int pfChecked) {
                        pfChecked = 0;
                        return VSConstants.S_OK;
                    }

                    public int get_Priority(VSTASKPRIORITY[] ptpPriority) {
                        ptpPriority[0] = _isError ? VSTASKPRIORITY.TP_HIGH : VSTASKPRIORITY.TP_NORMAL;
                        return VSConstants.S_OK;
                    }

                    public int get_Text(out string pbstrName) {
                        pbstrName = Message;
                        return VSConstants.S_OK;
                    }

                    public int put_Checked(int fChecked) {
                        return VSConstants.E_NOTIMPL;
                    }

                    public int put_Priority(VSTASKPRIORITY tpPriority) {
                        return VSConstants.E_NOTIMPL;
                    }

                    public int put_Text(string bstrName) {
                        return VSConstants.E_NOTIMPL;
                    }

                    #endregion
                }
            }
        }

        #region IDisposable Members

        public void Dispose() {
            _analysisQueue.Stop();
            lock (this) {
                ((IDisposable)_pyAnalyzer).Dispose();
            }
        }

        #endregion

        internal void RemoveReference(ProjectAssemblyReference reference) {
            lock (this) {
                IPythonInterpreter2 interp2 = Interpreter as IPythonInterpreter2;
                if (interp2 != null) {
                    interp2.RemoveReference(reference);
                }
            }
        }
    }
}
