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
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

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
        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_zipFileName] contains the full path to that archive.
        private static readonly object _zipFileName = new { Name = "ZipFileName" };

        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_pathInZipFile] contains the path of the item inside the archive.
        private static readonly object _pathInZipFile = new { Name = "PathInZipFile" };

        private readonly ParseQueue _queue;
        private readonly AnalysisQueue _analysisQueue;
        private readonly IPythonInterpreterFactory _interpreterFactory;
        private readonly Dictionary<BufferParser, IProjectEntry> _openFiles = new Dictionary<BufferParser, IProjectEntry>();
        private readonly ConcurrentDictionary<string, IProjectEntry> _projectFiles;
        private readonly PythonAnalyzer _pyAnalyzer;
        private readonly bool _implicitProject;
        private readonly AutoResetEvent _queueActivityEvent = new AutoResetEvent(false);
        private readonly IPythonInterpreterFactory[] _allFactories;

        private int _userCount;

        internal readonly HashSet<IProjectEntry> _hasParseErrors = new HashSet<IProjectEntry>();

        // Moniker strings allow the task provider to distinguish between
        // different sources of items for the same file.
        private const string ParserTaskMoniker = "Parser";
        internal const string UnresolvedImportMoniker = "UnresolvedImport";


        internal static Lazy<TaskProvider> ReplaceTaskProviderForTests(Lazy<TaskProvider> newProvider) {
            return Interlocked.Exchange(ref _taskProvider, newProvider);
        }

        private static Lazy<TaskProvider> _taskProvider;
        private static readonly Lazy<TaskProvider> _defaultTaskProvider = new Lazy<TaskProvider>(() => {
            var errorList = PythonToolsPackage.GetGlobalService(typeof(SVsErrorList)) as IVsTaskList;
            var model = PythonToolsPackage.ComponentModel;
            var errorProvider = model != null ? model.GetService<IErrorProviderFactory>() : null;
            return new TaskProvider(errorList, errorProvider);
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        private static Lazy<TaskProvider> TaskProvider {
            get {
                return _taskProvider ?? _defaultTaskProvider;
            }
        }

        private readonly UnresolvedImportSquiggleProvider _unresolvedSquiggles;

        private object _contentsLock = new object();

        internal VsProjectAnalyzer(
            IPythonInterpreterFactory factory,
            IPythonInterpreterFactory[] allFactories
        )
            : this(factory.CreateInterpreter(), factory, allFactories) {
        }

        internal VsProjectAnalyzer(
            IPythonInterpreter interpreter,
            IPythonInterpreterFactory factory,
            IPythonInterpreterFactory[] allFactories,
            bool implicitProject = true
        ) {
            _unresolvedSquiggles = new UnresolvedImportSquiggleProvider(TaskProvider);

            _queue = new ParseQueue(this);
            _analysisQueue = new AnalysisQueue(this);
            _allFactories = allFactories;

            _interpreterFactory = factory;
            _implicitProject = implicitProject;

            if (interpreter != null) {
                _pyAnalyzer = new PythonAnalyzer(factory, interpreter);
                interpreter.ModuleNamesChanged += OnModulesChanged;
            }
            _projectFiles = new ConcurrentDictionary<string, IProjectEntry>(StringComparer.OrdinalIgnoreCase);

            if (PythonToolsPackage.Instance != null && _pyAnalyzer != null) {
                _pyAnalyzer.Limits.CrossModule = PythonToolsPackage.Instance.DebuggingOptionsPage.CrossModuleAnalysisLimit;
                // TODO: Load other limits from options
            }

            _userCount = 1;
        }

        public void AddUser() {
            Interlocked.Increment(ref _userCount);
        }

        /// <summary>
        /// Reduces the number of known users by one and returns true if the
        /// analyzer should be disposed.
        /// </summary>
        public bool RemoveUser() {
            return Interlocked.Decrement(ref _userCount) == 0;
        }

        internal static string GetZipFileName(IProjectEntry entry) {
            object result;
            entry.Properties.TryGetValue(_zipFileName, out result);
            return (string)result;
        }

        private static void SetZipFileName(IProjectEntry entry, string value) {
            entry.Properties[_zipFileName] = value;
        }

        internal static string GetPathInZipFile(IProjectEntry entry) {
            object result;
            entry.Properties.TryGetValue(_pathInZipFile, out result);
            return (string)result;
        }

        private static void SetPathInZipFile(IProjectEntry entry, string value) {
            entry.Properties[_pathInZipFile] = value;
        }

        private void OnModulesChanged(object sender, EventArgs e) {
            Debug.Assert(_pyAnalyzer != null, "Should not have null _pyAnalyzer here");
            if (_pyAnalyzer == null) {
                return;
            }

            lock (_contentsLock) {
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
                TaskProvider.Value.ClearErrorSource(bufferParser._currentProjEntry, ParserTaskMoniker);
                TaskProvider.Value.ClearErrorSource(bufferParser._currentProjEntry, UnresolvedImportMoniker);
                _unresolvedSquiggles.StopListening(bufferParser._currentProjEntry as IPythonProjectEntry);

                var projEntry = CreateProjectEntry(buffers[0], new SnapshotCookie(buffers[0].CurrentSnapshot));

                bool doSquiggles = !buffers[0].Properties.ContainsProperty(typeof(IReplEvaluator));
                if (doSquiggles) {
                    _unresolvedSquiggles.ListenForNextNewAnalysis(projEntry as IPythonProjectEntry);
                }

                foreach (var buffer in buffers) {
                    buffer.Properties.RemoveProperty(typeof(IProjectEntry));
                    buffer.Properties.AddProperty(typeof(IProjectEntry), projEntry);

                    var classifier = buffer.GetPythonClassifier();
                    if (classifier != null) {
                        classifier.NewVersion();
                    }

                    ConnectErrorList(projEntry, buffer);
                    if (doSquiggles) {
                        TaskProvider.Value.AddBufferForErrorSource(projEntry, UnresolvedImportMoniker, buffer);
                    }
                }
                bufferParser._currentProjEntry = _openFiles[bufferParser] = projEntry;
                bufferParser._parser = this;

                foreach (var buffer in buffers) {
                    // A buffer may have multiple DropDownBarClients, given one may open multiple CodeWindows
                    // over a single buffer using Window/New Window
                    List<DropDownBarClient> clients;
                    if (buffer.Properties.TryGetProperty<List<DropDownBarClient>>(typeof(DropDownBarClient), out clients)) {
                        foreach (var client in clients) {
                            client.UpdateProjectEntry(projEntry);
                        }
                    }
                }

                bufferParser.Requeue();
            }
        }

        public static void ConnectErrorList(IProjectEntry projEntry, ITextBuffer buffer) {
            TaskProvider.Value.AddBufferForErrorSource(projEntry, ParserTaskMoniker, buffer);
        }

        public static void DisconnectErrorList(IProjectEntry projEntry, ITextBuffer buffer) {
            TaskProvider.Value.RemoveBufferForErrorSource(projEntry, ParserTaskMoniker, buffer);
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

            ConnectErrorList(projEntry, buffer);
            
            if (!buffer.Properties.ContainsProperty(typeof(IReplEvaluator))) {
                TaskProvider.Value.AddBufferForErrorSource(projEntry, UnresolvedImportMoniker, buffer);
                _unresolvedSquiggles.ListenForNextNewAnalysis(projEntry as IPythonProjectEntry);
            }

            // kick off initial processing on the buffer
            lock (_openFiles) {
                var bufferParser = _queue.EnqueueBuffer(projEntry, textView, buffer);
                _openFiles[bufferParser] = projEntry;
                return new MonitoredBufferResult(bufferParser, textView, projEntry);
            }
        }

        internal void StopMonitoringTextBuffer(BufferParser bufferParser, ITextView textView) {
            bufferParser.StopMonitoring();
            lock (_openFiles) {
                _openFiles.Remove(bufferParser);
            }

            _unresolvedSquiggles.StopListening(bufferParser._currentProjEntry as IPythonProjectEntry);

            if (TaskProvider.IsValueCreated) {
                TaskProvider.Value.ClearErrorSource(bufferParser._currentProjEntry, ParserTaskMoniker);
                TaskProvider.Value.ClearErrorSource(bufferParser._currentProjEntry, UnresolvedImportMoniker);

                if (ImplicitProject) {
                    // remove the file from the error list
                    TaskProvider.Value.Clear(bufferParser._currentProjEntry, ParserTaskMoniker);
                    TaskProvider.Value.Clear(bufferParser._currentProjEntry, UnresolvedImportMoniker);
                }
            }
        }

        private IProjectEntry CreateProjectEntry(ITextBuffer buffer, IAnalysisCookie analysisCookie) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code, so don't create an entry.
                return null;
            }

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
                    var reanalyzeEntries = Project.GetEntriesThatImportModule(modName, true).ToArray();

                    entry = _pyAnalyzer.AddModule(
                        modName,
                        buffer.GetFilePath(),
                        analysisCookie
                    );
                    foreach (var entryRef in reanalyzeEntries) {
                        _analysisQueue.Enqueue(entryRef, AnalysisPriority.Low);
                    }
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
            ThreadPool.QueueUserWorkItem(x => { lock (_contentsLock) { AnalyzeDirectory(CommonUtils.NormalizeDirectoryPath(Path.GetDirectoryName(path))); } });
        }

        private bool ShouldAnalyzePath(string path) {
            foreach (var fact in _allFactories) {
                if (CommonUtils.IsValidPath(fact.Configuration.InterpreterPath) &&
                    CommonUtils.IsSubpathOf(Path.GetDirectoryName(fact.Configuration.InterpreterPath), path)) {
                    return false;
                }
            }
            return true;
        }

        internal IProjectEntry AnalyzeFile(string path) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code, so don't create an entry.
                return null;
            }

            IProjectEntry item;
            if (!_projectFiles.TryGetValue(path, out item)) {
                if (PythonProjectNode.IsPythonFile(path)) {
                    var modName = PythonAnalyzer.PathToModuleName(path);
                    var reanalyzeEntries = Project.GetEntriesThatImportModule(modName, true).ToArray();

                    var pyEntry = _pyAnalyzer.AddModule(
                        modName,
                        path,
                        null
                    );

                    pyEntry.BeginParsingTree();

                    foreach (var entryRef in reanalyzeEntries) {
                        _analysisQueue.Enqueue(entryRef, AnalysisPriority.Low);
                    }

                    item = pyEntry;
                } else if (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) {
                    item = _pyAnalyzer.AddXamlFile(path, null);
                }

                if (item != null) {
                    _projectFiles[path] = item;
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

        internal IProjectEntry GetEntryFromFile(string path) {
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

            IPythonProjectEntry entry;
            if (buffer.TryGetPythonProjectEntry(out entry) && entry.Analysis != null && text.Length > 0) {
                var lineNo = parser.Snapshot.GetLineNumberFromPosition(loc.Start);
                return new ExpressionAnalysis(
                    snapshot.TextBuffer.GetAnalyzer(),
                    text,
                    entry.Analysis,
                    loc.Start,
                    applicableSpan,
                    parser.Snapshot
                );
            }

            return ExpressionAnalysis.Empty;
        }

        /// <summary>
        /// Gets a CompletionList providing a list of possible members the user can dot through.
        /// </summary>
        internal static CompletionAnalysis GetCompletions(ITextSnapshot snapshot, ITrackingSpan span, ITrackingPoint point, CompletionOptions options) {
            return TrySpecialCompletions(snapshot, span, point, options) ??
                   GetNormalCompletionContext(snapshot, span, point, options);
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

            var text = new SnapshotSpan(exprRange.Value.Snapshot, new Span(exprRange.Value.Start, sigStart.Value.Position - exprRange.Value.Start)).GetText();
            var applicableSpan = parser.Snapshot.CreateTrackingSpan(exprRange.Value.Span, SpanTrackingMode.EdgeInclusive);

            if (snapshot.TextBuffer.GetAnalyzer().ShouldEvaluateForCompletion(text)) {
                var liveSigs = TryGetLiveSignatures(snapshot, paramIndex, text, applicableSpan, lastKeywordArg);
                if (liveSigs != null) {
                    return liveSigs;
                }
            }

            var start = Stopwatch.ElapsedMilliseconds;

            var analysisItem = buffer.GetProjectEntry();
            if (analysisItem != null) {
                var analysis = ((IPythonProjectEntry)analysisItem).Analysis;
                if (analysis != null) {
                    int index = TranslateIndex(loc.Start, snapshot, analysis);

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

        internal static int TranslateIndex(int index, ITextSnapshot fromSnapshot, ModuleAnalysis toAnalysisSnapshot) {
            var snapshotCookie = toAnalysisSnapshot.AnalysisCookie as SnapshotCookie;
            // TODO: buffers differ in the REPL window case, in the future we should handle this better
            if (snapshotCookie != null &&
                fromSnapshot != null &&
                snapshotCookie.Snapshot.TextBuffer == fromSnapshot.TextBuffer) {

                index = new SnapshotPoint(fromSnapshot, index).TranslateTo(
                    snapshotCookie.Snapshot,
                    PointTrackingMode.Negative
                ).Position;
            }
            return index;
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

            var analysis = ((IPythonProjectEntry)snapshot.TextBuffer.GetProjectEntry()).Analysis;
            if (analysis == null) {
                return MissingImportAnalysis.Empty;
            }

            var text = exprRange.Value.GetText();
            var analyzer = analysis.ProjectState;
            var index = span.GetStartPoint(snapshot).Position;

            var expr = Statement.GetExpression(
                analysis.GetAstFromTextByIndex(
                    text,
                    TranslateIndex(
                        index,
                        snapshot,
                        analysis
                    )
                ).Body
            );

            if (expr != null && expr is NameExpression) {
                var nameExpr = (NameExpression)expr;

                if (!IsImplicitlyDefinedName(nameExpr)) {
                    var applicableSpan = parser.Snapshot.CreateTrackingSpan(
                        exprRange.Value.Span,
                        SpanTrackingMode.EdgeExclusive
                    );

                    lock (snapshot.TextBuffer.GetAnalyzer()) {
                        index = TranslateIndex(
                            index,
                            snapshot,
                            analysis
                        );
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
            if (IsAnalyzing) {
                while (IsAnalyzing) {
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

        /// <summary>
        /// True if the project is an implicit project and it should model files on disk in addition
        /// to files which are explicitly added.
        /// </summary>
        internal bool ImplicitProject {
            get {
                return _implicitProject;
            }
        }

        internal IPythonInterpreterFactory InterpreterFactory {
            get {
                return _interpreterFactory;
            }
        }

        internal IPythonInterpreter Interpreter {
            get {
                return _pyAnalyzer != null ? _pyAnalyzer.Interpreter : null;
            }
        }

        public PythonAnalyzer Project {
            get {
                return _pyAnalyzer;
            }
        }

        internal PythonAst ParseSnapshot(ITextSnapshot snapshot) {
            using (var parser = Parser.CreateParser(
                new SnapshotSpanSourceCodeReader(
                    new SnapshotSpan(snapshot, 0, snapshot.Length)
                ),
                Project.LanguageVersion,
                new ParserOptions() { Verbatim = true, BindReferences = true }
            )) {
                return ParseOneFile(null, parser);
            }
        }

        internal ITextSnapshot GetOpenSnapshot(IProjectEntry entry) {
            if (entry == null) {
                return null;
            }

            lock (_openFiles) {
                var item = _openFiles.FirstOrDefault(kv => kv.Value == entry);
                if (item.Value == null) {
                    return null;
                }
                var document = item.Key.Document;

                return document != null ? document.TextBuffer.CurrentSnapshot : null;
            }
        }

        internal void ParseFile(IProjectEntry entry, string filename, Stream content, Severity indentationSeverity) {
            IPythonProjectEntry pyEntry;
            IExternalProjectEntry externalEntry;

            TextReader reader = null;
            ITextSnapshot snapshot = GetOpenSnapshot(entry);
            string zipFileName = GetZipFileName(entry);
            string pathInZipFile = GetPathInZipFile(entry);
            IAnalysisCookie cookie;
            if (snapshot != null) {
                cookie = new SnapshotCookie(snapshot);
                reader = new SnapshotSpanSourceCodeReader(new SnapshotSpan(snapshot, 0, snapshot.Length));
            } else if (zipFileName != null) {
                cookie = new ZipFileCookie(zipFileName, pathInZipFile);
            } else {
                cookie = new FileCookie(filename);
            }

            if ((pyEntry = entry as IPythonProjectEntry) != null) {
                PythonAst ast;
                CollectingErrorSink errorSink;
                if (reader != null) {
                    ParsePythonCode(reader, indentationSeverity, out ast, out errorSink);
                } else {
                    ParsePythonCode(content, indentationSeverity, out ast, out errorSink);
                }

                if (ast != null) {
                    pyEntry.UpdateTree(ast, cookie);
                } else {
                    // notify that we failed to update the existing analysis
                    pyEntry.UpdateTree(null, null);
                }

                // update squiggles for the buffer. snapshot may be null if we
                // are analyzing a file that is not open
                UpdateErrorsAndWarnings(entry, snapshot, errorSink);

                // enqueue analysis of the file
                if (ast != null) {
                    _analysisQueue.Enqueue(pyEntry, AnalysisPriority.Normal);
                }
            } else if ((externalEntry = entry as IExternalProjectEntry) != null) {
                externalEntry.ParseContent(reader ?? new StreamReader(content), cookie);
                _analysisQueue.Enqueue(entry, AnalysisPriority.Normal);
            }
        }

        internal void ParseBuffers(BufferParser bufferParser, Severity indentationSeverity, params ITextSnapshot[] snapshots) {
            IProjectEntry entry = bufferParser._currentProjEntry;

            IPythonProjectEntry pyProjEntry = entry as IPythonProjectEntry;
            List<PythonAst> asts = new List<PythonAst>();
            foreach (var snapshot in snapshots) {
                if (snapshot.TextBuffer.Properties.ContainsProperty(PythonReplEvaluator.InputBeforeReset)) {
                    continue;
                }

                if (snapshot.IsReplBufferWithCommand()) {
                    continue;
                }

                if (pyProjEntry != null && snapshot.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                    PythonAst ast;
                    CollectingErrorSink errorSink;

                    var reader = new SnapshotSpanSourceCodeReader(new SnapshotSpan(snapshot, new Span(0, snapshot.Length)));
                    ParsePythonCode(reader, indentationSeverity, out ast, out errorSink);

                    if (ast != null) {
                        asts.Add(ast);
                    }

                    // update squiggles for the buffer
                    UpdateErrorsAndWarnings(entry, snapshot, errorSink);
                } else {
                    // other file such as XAML
                    IExternalProjectEntry externalEntry;
                    if ((externalEntry = (entry as IExternalProjectEntry)) != null) {
                        var snapshotContent = new SnapshotSpanSourceCodeReader(new SnapshotSpan(snapshot, new Span(0, snapshot.Length)));
                        externalEntry.ParseContent(snapshotContent, new SnapshotCookie(snapshotContent.Snapshot));
                        _analysisQueue.Enqueue(entry, AnalysisPriority.High);
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
                        finalAst = new PythonAst(
                            new SuiteStatement(asts.Select(ast => ast.Body).ToArray()),
                            new int[0],
                            asts[0].LanguageVersion
                        );
                    }

                    pyProjEntry.UpdateTree(finalAst, new SnapshotCookie(snapshots[0])); // SnapshotCookie is not entirely right, we should merge the snapshots
                    _analysisQueue.Enqueue(entry, AnalysisPriority.High);
                } else {
                    // indicate that we are done parsing.
                    PythonAst prevTree;
                    IAnalysisCookie prevCookie;
                    pyProjEntry.GetTreeAndCookie(out prevTree, out prevCookie);
                    pyProjEntry.UpdateTree(prevTree, prevCookie);
                }
            }
        }

        private void ParsePythonCode(Stream content, Severity indentationSeverity, out PythonAst ast, out CollectingErrorSink errorSink) {
            ast = null;
            errorSink = new CollectingErrorSink();

            using (var parser = Parser.CreateParser(
                content,
                Project.LanguageVersion,
                new ParserOptions() {
                    ErrorSink = errorSink,
                    IndentationInconsistencySeverity = indentationSeverity,
                    BindReferences = true
                }
            )) {
                ast = ParseOneFile(ast, parser);
            }
        }

        private void ParsePythonCode(TextReader content, Severity indentationSeverity, out PythonAst ast, out CollectingErrorSink errorSink) {
            ast = null;
            errorSink = new CollectingErrorSink();

            using (var parser = Parser.CreateParser(
                content,
                Project.LanguageVersion,
                new ParserOptions() {
                    ErrorSink = errorSink,
                    IndentationInconsistencySeverity = indentationSeverity,
                    BindReferences = true
                }
            )) {
                ast = ParseOneFile(ast, parser);
            }
        }

        private static PythonAst ParseOneFile(PythonAst ast, Parser parser) {
            if (parser != null) {
                try {
                    ast = parser.ParseFile();
                } catch (BadSourceException) {
                } catch (Exception e) {
                    if (e.IsCriticalException()) {
                        throw;
                    }
                    Debug.Assert(false, String.Format("Failure in Python parser: {0}", e.ToString()));
                }

            }
            return ast;
        }

        private void UpdateErrorsAndWarnings(
            IProjectEntry entry,
            ITextSnapshot snapshot,
            CollectingErrorSink errorSink
        ) {
            // Update the warn-on-launch state for this entry
            if (errorSink.Errors.Any() ? _hasParseErrors.Add(entry) : _hasParseErrors.Remove(entry)) {
                OnShouldWarnOnLaunchChanged(entry);
            }

            var f = new TaskProviderItemFactory(snapshot);

            // Update the parser warnings/errors
            if (errorSink.Warnings.Any() || errorSink.Errors.Any()) {
                TaskProvider.Value.ReplaceItems(
                    entry,
                    ParserTaskMoniker,
                    errorSink.Warnings.Select(er => f.FromParseWarning(er))
                        .Concat(errorSink.Errors.Select(er => f.FromParseError(er)))
                        .ToList()
                );
            } else if (TaskProvider.IsValueCreated) {
                TaskProvider.Value.Clear(entry, ParserTaskMoniker);
            }
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
                res[i++] = new PythonSignature(
                    span,
                    new LiveOverloadResult(text, sig.Documentation, sig.Parameters),
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

        private static CompletionAnalysis TrySpecialCompletions(ITextSnapshot snapshot, ITrackingSpan span, ITrackingPoint point, CompletionOptions options) {
            var snapSpan = span.GetSpan(snapshot);
            var buffer = snapshot.TextBuffer;
            var classifier = buffer.GetPythonClassifier();
            if (classifier == null) {
                return null;
            }
            var start = snapSpan.Start;

            var parser = new ReverseExpressionParser(snapshot, buffer, span);
            if (parser.IsInGrouping()) {
                var range = parser.GetExpressionRange(nesting: 1);
                if (range != null) {
                    start = range.Value.Start;
                }
            }

            var tokens = classifier.GetClassificationSpans(new SnapshotSpan(start.GetContainingLine().Start, snapSpan.Start));
            if (tokens.Count > 0) {
                // Check for context-sensitive intellisense
                var lastClass = tokens[tokens.Count - 1];

                if (lastClass.ClassificationType == classifier.Provider.Comment) {
                    // No completions in comments
                    return CompletionAnalysis.EmptyCompletionContext;
                } else if (lastClass.ClassificationType == classifier.Provider.StringLiteral) {
                    // String completion
                    if (lastClass.Span.Start.GetContainingLine().LineNumber == lastClass.Span.End.GetContainingLine().LineNumber) {
                        return new StringLiteralCompletionList(span, buffer, options);
                    } else {
                        // multi-line string, no string completions.
                        return CompletionAnalysis.EmptyCompletionContext;
                    }
                } else if (lastClass.ClassificationType == classifier.Provider.Operator &&
                    lastClass.Span.GetText() == "@") {

                    return new DecoratorCompletionAnalysis(span, buffer, options);
                } else if (CompletionAnalysis.IsKeyword(lastClass, "raise") || CompletionAnalysis.IsKeyword(lastClass, "except")) {
                    return new ExceptionCompletionAnalysis(span, buffer, options);
                } else if (CompletionAnalysis.IsKeyword(lastClass, "def")) {
                    return new OverrideCompletionAnalysis(span, buffer, options);
                }

                // Import completions
                var first = tokens[0];
                if (CompletionAnalysis.IsKeyword(first, "import")) {
                    return ImportCompletionAnalysis.Make(tokens, span, buffer, options);
                } else if (CompletionAnalysis.IsKeyword(first, "from")) {
                    return FromImportCompletionAnalysis.Make(tokens, span, buffer, options);
                }
                return null;
            } else if ((tokens = classifier.GetClassificationSpans(snapSpan.Start.GetContainingLine().ExtentIncludingLineBreak)).Count > 0 &&
               tokens[0].ClassificationType == classifier.Provider.StringLiteral) {
                // multi-line string, no string completions.
                return CompletionAnalysis.EmptyCompletionContext;
            } else if (snapshot.IsReplBufferWithCommand()) {
                return CompletionAnalysis.EmptyCompletionContext;
            }

            return null;
        }

        private static CompletionAnalysis GetNormalCompletionContext(ITextSnapshot snapshot, ITrackingSpan applicableSpan, ITrackingPoint point, CompletionOptions options) {
            var span = applicableSpan.GetSpan(snapshot);

            if (IsSpaceCompletion(snapshot, point) && !IntellisenseController.ForceCompletions) {
                return CompletionAnalysis.EmptyCompletionContext;
            }

            var parser = new ReverseExpressionParser(snapshot, snapshot.TextBuffer, applicableSpan);
            if (parser.IsInGrouping()) {
                options = options.Clone();
                options.IncludeStatementKeywords = false;
            }

            return new NormalCompletionAnalysis(
                snapshot.TextBuffer.GetAnalyzer(),
                snapshot,
                applicableSpan,
                snapshot.TextBuffer,
                options
            );
        }

        private static bool IsSpaceCompletion(ITextSnapshot snapshot, ITrackingPoint loc) {
            var pos = loc.GetPosition(snapshot);
            if (pos > 0) {
                return snapshot.GetText(pos - 1, 1) == " ";
            }
            return false;
        }

        private static Stopwatch MakeStopWatch() {
            var res = new Stopwatch();
            res.Start();
            return res;
        }

        /// <summary>
        /// Analyzes a complete directory including all of the contained files and packages.
        /// </summary>
        /// <param name="dir">Directory to analyze.</param>
        /// <param name="onFileAnalyzed">If specified, this callback is invoked for every <see cref="IProjectEntry"/>
        /// that is analyzed while analyzing this directory.</param>
        /// <remarks>The callback may be invoked on a thread different from the one that this function was originally invoked on.</remarks>
        public void AnalyzeDirectory(string dir, Action<IProjectEntry> onFileAnalyzed = null) {
            _analysisQueue.Enqueue(new AddDirectoryAnalysis(dir, onFileAnalyzed, this), AnalysisPriority.High);
        }

        class AddDirectoryAnalysis : IAnalyzable {
            private readonly string _dir;
            private readonly Action<IProjectEntry> _onFileAnalyzed;
            private readonly VsProjectAnalyzer _analyzer;

            public AddDirectoryAnalysis(string dir, Action<IProjectEntry> onFileAnalyzed, VsProjectAnalyzer analyzer) {
                _dir = dir;
                _onFileAnalyzed = onFileAnalyzed;
                _analyzer = analyzer;
            }

            #region IAnalyzable Members

            public void Analyze(CancellationToken cancel) {
                if (cancel.IsCancellationRequested) {
                    return;
                }

                _analyzer.AnalyzeDirectoryWorker(_dir, true, _onFileAnalyzed, cancel);
            }

            #endregion
        }

        private void AnalyzeDirectoryWorker(string dir, bool addDir, Action<IProjectEntry> onFileAnalyzed, CancellationToken cancel) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code.
                return;
            }

            if (string.IsNullOrEmpty(dir)) {
                Debug.Assert(false, "Unexpected empty dir");
                return;
            }

            if (addDir) {
                lock (_contentsLock) {
                    _pyAnalyzer.AddAnalysisDirectory(dir);
                }
            }

            try {
                foreach (string filename in Directory.GetFiles(dir, "*.py")) {
                    if (cancel.IsCancellationRequested) {
                        break;
                    }
                    IProjectEntry entry = AnalyzeFile(filename);
                    if (onFileAnalyzed != null) {
                        onFileAnalyzed(entry);
                    }
                }
            } catch (IOException) {
                // We want to handle DirectoryNotFound, DriveNotFound, PathTooLong
            } catch (UnauthorizedAccessException) {
            }

            try {
                foreach (string filename in Directory.GetFiles(dir, "*.pyw")) {
                    if (cancel.IsCancellationRequested) {
                        break;
                    }
                    IProjectEntry entry = AnalyzeFile(filename);
                    if (onFileAnalyzed != null) {
                        onFileAnalyzed(entry);
                    }
                }
            } catch (IOException) {
                // We want to handle DirectoryNotFound, DriveNotFound, PathTooLong
            } catch (UnauthorizedAccessException) {
            }

            try {
                foreach (string innerDir in Directory.GetDirectories(dir)) {
                    if (cancel.IsCancellationRequested) {
                        break;
                    }
                    if (File.Exists(Path.Combine(innerDir, "__init__.py"))) {
                        AnalyzeDirectoryWorker(innerDir, false, onFileAnalyzed, cancel);
                    }
                }
            } catch (IOException) {
                // We want to handle DirectoryNotFound, DriveNotFound, PathTooLong
            } catch (UnauthorizedAccessException) {
            }
        }

        /// <summary>
        /// Analyzes a .zip file including all of the contained files and packages.
        /// </summary>
        /// <param name="dir">.zip file to analyze.</param>
        /// <param name="onFileAnalyzed">If specified, this callback is invoked for every <see cref="IProjectEntry"/>
        /// that is analyzed while analyzing this directory.</param>
        /// <remarks>The callback may be invoked on a thread different from the one that this function was originally invoked on.</remarks>
        public void AnalyzeZipArchive(string zipFileName, Action<IProjectEntry> onFileAnalyzed = null) {
            _analysisQueue.Enqueue(new AddZipArchiveAnalysis(zipFileName, onFileAnalyzed, this), AnalysisPriority.High);
        }

        private class AddZipArchiveAnalysis : IAnalyzable {
            private readonly string _zipFileName;
            private readonly Action<IProjectEntry> _onFileAnalyzed;
            private readonly VsProjectAnalyzer _analyzer;

            public AddZipArchiveAnalysis(string zipFileName, Action<IProjectEntry> onFileAnalyzed, VsProjectAnalyzer analyzer) {
                _zipFileName = zipFileName;
                _onFileAnalyzed = onFileAnalyzed;
                _analyzer = analyzer;
            }

            #region IAnalyzable Members

            public void Analyze(CancellationToken cancel) {
                if (cancel.IsCancellationRequested) {
                    return;
                }

                _analyzer.AnalyzeZipArchiveWorker(_zipFileName, _onFileAnalyzed, cancel);
            }

            #endregion
        }


        private void AnalyzeZipArchiveWorker(string zipFileName, Action<IProjectEntry> onFileAnalyzed, CancellationToken cancel) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code.
                return;
            }

            lock (_contentsLock) {
                _pyAnalyzer.AddAnalysisDirectory(zipFileName);
            }

            ZipArchive archive = null;
            Queue<ZipArchiveEntry> entryQueue = null;
            try {
                archive = ZipFile.Open(zipFileName, ZipArchiveMode.Read);

                // We only want to scan files in directories that are packages - i.e. contain __init__.py. So enumerate
                // entries in the archive, and build a list of such directories, so that later on we can compare file
                // paths against that to see if we should scan them.
                var packageDirs = new HashSet<string>(
                    from entry in archive.Entries
                    where entry.Name == "__init__.py"
                    select Path.GetDirectoryName(entry.FullName));
                packageDirs.Add(""); // we always want to scan files on the top level of the archive

                entryQueue = new Queue<ZipArchiveEntry>(
                    from entry in archive.Entries
                    let ext = Path.GetExtension(entry.Name)
                    where ext == ".py" || ext == ".pyw"
                    let path = Path.GetDirectoryName(entry.FullName)
                    where packageDirs.Contains(path)
                    select entry);
            } catch (InvalidDataException ex) {
                Debug.Fail(ex.Message);
                return;
            } catch (IOException ex) {
                Debug.Fail(ex.Message);
                return;
            } catch (UnauthorizedAccessException ex) {
                Debug.Fail(ex.Message);
                return;
            } finally {
                if (archive != null && entryQueue == null) {
                    archive.Dispose();
                }
            }

            // ZipArchive is not thread safe, and so we cannot analyze entries in parallel. Instead, use completion
            // callbacks to queue the next one for analysis only after the preceding one is fully analyzed.
            Action analyzeNextEntry = null;
            analyzeNextEntry = () => {
                try {
                    if (entryQueue.Count == 0 || cancel.IsCancellationRequested) {
                        archive.Dispose();
                        return;
                    }

                    ZipArchiveEntry zipEntry = entryQueue.Dequeue();
                    IProjectEntry projEntry = AnalyzeZipArchiveEntry(zipFileName, zipEntry, analyzeNextEntry);
                    if (onFileAnalyzed != null) {
                        onFileAnalyzed(projEntry);
                    }
                } catch (InvalidDataException ex) {
                    Debug.Fail(ex.Message);
                } catch (IOException ex) {
                    Debug.Fail(ex.Message);
                } catch (UnauthorizedAccessException ex) {
                    Debug.Fail(ex.Message);
                }
            };
            analyzeNextEntry();
        }

        private IProjectEntry AnalyzeZipArchiveEntry(string zipFileName, ZipArchiveEntry entry, Action onComplete) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code, so don't create an entry.
                return null;
            }
            try {
                string pathInZip = entry.FullName.Replace('/', '\\');
                string path = Path.Combine(zipFileName, pathInZip);

                IProjectEntry item;
                if (_projectFiles.TryGetValue(path, out item)) {
                    return item;
                }

                if (PythonProjectNode.IsPythonFile(path)) {
                    // Use the entry path relative to the root of the archive to determine module name - this boundary
                    // should never be crossed, even if the parent directory of the zip is itself a package.
                    var modName = PythonAnalyzer.PathToModuleName(pathInZip,
                        fileExists: fileName => entry.Archive.GetEntry(fileName.Replace('\\', '/')) != null);
                    item = _pyAnalyzer.AddModule(modName, path, null);
                }
                if (item == null) {
                    return null;
                }

                SetZipFileName(item, zipFileName);
                SetPathInZipFile(item, pathInZip);
                _projectFiles[path] = item;
                IPythonProjectEntry pyEntry = item as IPythonProjectEntry;
                if (pyEntry != null) {
                    pyEntry.BeginParsingTree();
                }

                _queue.EnqueueZipArchiveEntry(item, zipFileName, entry, onComplete);
                onComplete = null;
                return item;
            } finally {
                if (onComplete != null) {
                    onComplete();
                }
            }
        }

        internal void StopAnalyzingDirectory(string directory) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code.
                return;
            }

            lock (_contentsLock) {
                _pyAnalyzer.RemoveAnalysisDirectory(directory);
            }
        }

        internal void Cancel() {
            _analysisQueue.Stop();
        }

        internal void UnloadFile(IProjectEntry entry) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code.
                return;
            }

            if (entry != null) {
                // If we remove a Python module, reanalyze any other modules
                // that referenced it.
                IPythonProjectEntry[] reanalyzeEntries = null;
                var pyEntry = entry as IPythonProjectEntry;
                if (pyEntry != null && !string.IsNullOrEmpty(pyEntry.ModuleName)) {
                    reanalyzeEntries = _pyAnalyzer.GetEntriesThatImportModule(pyEntry.ModuleName, false).ToArray();
                }

                ClearParserTasks(entry);
                _pyAnalyzer.RemoveModule(entry);
                IProjectEntry removed;
                _projectFiles.TryRemove(entry.FilePath, out removed);

                if (reanalyzeEntries != null) {
                    foreach (var existing in reanalyzeEntries) {
                        _analysisQueue.Enqueue(existing, AnalysisPriority.Normal);
                    }
                }
            }
        }

        internal void ClearParserTasks(IProjectEntry entry) {
            if (entry != null) {
                if (TaskProvider.IsValueCreated) {
                    // TaskProvider may not be created if we've never opened a
                    // Python file and none of the project files have errors
                    TaskProvider.Value.Clear(entry, ParserTaskMoniker);
                }
                if (_hasParseErrors.Remove(entry)) {
                    OnShouldWarnOnLaunchChanged(entry);
                }
            }
        }

        internal void ClearAllTasks() {
            if (TaskProvider.IsValueCreated) {
                TaskProvider.Value.ClearAll();
            }
            _hasParseErrors.Clear();
        }

        internal bool ShouldWarnOnLaunch(IProjectEntry entry) {
            return _hasParseErrors.Contains(entry);
        }

        private void OnShouldWarnOnLaunchChanged(IProjectEntry entry) {
            var evt = ShouldWarnOnLaunchChanged;
            if (evt != null) {
                evt(this, new EntryEventArgs(entry));
            }
        }

        internal event EventHandler<EntryEventArgs> ShouldWarnOnLaunchChanged;

        #endregion

        #region IDisposable Members

        public void Dispose() {
            if (TaskProvider.IsValueCreated) {
                foreach (var entry in _projectFiles.Values) {
                    TaskProvider.Value.Clear(entry, ParserTaskMoniker);
                    TaskProvider.Value.Clear(entry, UnresolvedImportMoniker);
                }
            }

            _analysisQueue.Stop();
            if (_pyAnalyzer != null) {
                lock (_contentsLock) {
                    _pyAnalyzer.Interpreter.ModuleNamesChanged -= OnModulesChanged;
                    ((IDisposable)_pyAnalyzer).Dispose();
                }
            }
        }

        #endregion

        internal void RemoveReference(ProjectAssemblyReference reference) {
            lock (_contentsLock) {
                var interp = Interpreter as IPythonInterpreterWithProjectReferences;
                if (interp != null) {
                    interp.RemoveReference(reference);
                }
            }
        }
    }
}
