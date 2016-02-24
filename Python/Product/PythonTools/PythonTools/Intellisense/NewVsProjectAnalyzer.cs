using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Communication;
using Microsoft.PythonTools.Cdp;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    using System.Windows;
    using System.Windows.Threading;
    using Repl;

    public sealed class VsProjectAnalyzer : IDisposable {
        private readonly Connection _conn;
        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_zipFileName] contains the full path to that archive.
        private static readonly object _zipFileName = new { Name = "ZipFileName" };

        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_pathInZipFile] contains the path of the item inside the archive.
        private static readonly object _pathInZipFile = new { Name = "PathInZipFile" };

        private readonly Dictionary<BufferParser, ProjectFileInfo> _openFiles = new Dictionary<BufferParser, ProjectFileInfo>();
        private readonly bool _implicitProject;
        private readonly IPythonInterpreterFactory _interpreterFactory;
        private readonly ConcurrentDictionary<string, ProjectFileInfo> _projectFiles;
        private readonly ConcurrentDictionary<int, ProjectFileInfo> _projectFilesById;

        internal readonly HashSet<ProjectFileInfo> _hasParseErrors = new HashSet<ProjectFileInfo>();
        internal readonly object _hasParseErrorsLock = new object();

        private const string ParserTaskMoniker = "Parser";
        internal const string UnresolvedImportMoniker = "UnresolvedImport";

        private Process _analysisProcess;
        private ErrorTaskProvider _errorProvider;
        private CommentTaskProvider _commentTaskProvider;

        private int _userCount;

        private readonly UnresolvedImportSquiggleProvider _unresolvedSquiggles;
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;

        internal Task ReloadTask;
        internal int _analysisPending;


        internal VsProjectAnalyzer(
            IServiceProvider serviceProvider,
            IPythonInterpreterFactory factory,
            IPythonInterpreterFactory[] allFactories)
            : this(serviceProvider, factory.CreateInterpreter(), factory, allFactories) {
        }

        internal VsProjectAnalyzer(
            IServiceProvider serviceProvider,
            IPythonInterpreter interpreter,
            IPythonInterpreterFactory factory,
            IPythonInterpreterFactory[] allFactories,
            bool implicitProject = true
        ) {
            _errorProvider = (ErrorTaskProvider)serviceProvider.GetService(typeof(ErrorTaskProvider));
            _commentTaskProvider = (CommentTaskProvider)serviceProvider.GetService(typeof(CommentTaskProvider));
            _unresolvedSquiggles = new UnresolvedImportSquiggleProvider(serviceProvider, _errorProvider);

            _implicitProject = implicitProject;
            _serviceProvider = serviceProvider;
            _interpreterFactory = factory;

            _projectFiles = new ConcurrentDictionary<string, ProjectFileInfo>();
            _projectFilesById = new ConcurrentDictionary<int, ProjectFileInfo>();

            var libAnalyzer = typeof(FileChangedEvent).Assembly.Location;
            var psi = new ProcessStartInfo(libAnalyzer, 
                String.Format(
                    "/id {0} /version {1} /interactive", 
                    factory.Id, 
                    factory.Configuration.Version
                )
            );
            _pyService = serviceProvider.GetPythonToolsService();
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            var process = _analysisProcess = Process.Start(psi);

            _conn = new Connection(
                process.StandardInput.BaseStream,
                process.StandardOutput.BaseStream,
                null,
                Requests.RegisteredTypes
            );
            process.Exited += Process_Exited;
            Task.Run(async () => {
                while (!process.HasExited) {
                    var line = await process.StandardError.ReadLineAsync();
                    Debug.WriteLine(line);
                }
            });
            _conn.EventReceived += _conn_EventReceived;
            _userCount = 1;

            Task.Run(() => _conn.ProcessMessages());

            _conn.SendEventAsync(
                new OptionsChangedEvent() {
                    implicitProject = implicitProject,
                    indentation_inconsistency_severity = _pyService.GeneralOptions.IndentationInconsistencySeverity
                }
            );
        }

        private void Process_Exited(object sender, EventArgs e) {
            Console.WriteLine("Analysis process has exited");
        }

        private void _conn_EventReceived(object sender, EventReceivedEventArgs e) {
            Debug.WriteLine(String.Format("Event received: {0}", e.Event.name));

            switch (e.Event.name) {
                case AnalysisCompleteEvent.Name:
                    OnAnalysisComplete(e);
                    break;
            }

        }

        private void OnAnalysisComplete(EventReceivedEventArgs e) {
            var analysisComplete = (AnalysisCompleteEvent)e.Event;
            ProjectFileInfo info;
            if (_projectFilesById.TryGetValue(analysisComplete.fileId, out info)) {
                info.RaiseOnNewAnalysis();
            }
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


        internal static string GetZipFileName(ProjectFileInfo entry) {
            object result;
            entry.Properties.TryGetValue(_zipFileName, out result);
            return (string)result;
        }

        private static void SetZipFileName(ProjectFileInfo entry, string value) {
            entry.Properties[_zipFileName] = value;
        }

        internal static string GetPathInZipFile(ProjectFileInfo entry) {
            object result;
            entry.Properties.TryGetValue(_pathInZipFile, out result);
            return (string)result;
        }

        private static void SetPathInZipFile(ProjectFileInfo entry, string value) {
            entry.Properties[_pathInZipFile] = value;
        }

        private void OnModulesChanged(object sender, EventArgs e) {
            _conn.SendEventAsync(new ModulesChangedEvent()).DoNotWait();
        }

        /// <summary>
        /// Creates a new ProjectEntry for the collection of buffers.
        /// 
        /// _openFiles must be locked when calling this function.
        /// </summary>
        internal async void ReAnalyzeTextBuffers(BufferParser bufferParser) {
            ITextBuffer[] buffers = bufferParser.Buffers;
            if (buffers.Length > 0) {
                _errorProvider.ClearErrorSource(bufferParser._currentProjEntry, ParserTaskMoniker);
                _errorProvider.ClearErrorSource(bufferParser._currentProjEntry, UnresolvedImportMoniker);
                _commentTaskProvider.ClearErrorSource(bufferParser._currentProjEntry, ParserTaskMoniker);
                _unresolvedSquiggles.StopListening(bufferParser._currentProjEntry);

                var projEntry = await CreateProjectEntry(buffers[0], new SnapshotCookie(buffers[0].CurrentSnapshot));

                bool doSquiggles = !buffers[0].Properties.ContainsProperty(typeof(IInteractiveEvaluator));
                if (doSquiggles) {
                    _unresolvedSquiggles.ListenForNextNewAnalysis(projEntry);
                }

                foreach (var buffer in buffers) {
                    buffer.Properties.RemoveProperty(typeof(IProjectEntry));
                    buffer.Properties.AddProperty(typeof(IProjectEntry), projEntry);

                    var classifier = buffer.GetPythonClassifier();
                    if (classifier != null) {
                        classifier.NewVersion();
                    }
                    var classifier2 = buffer.GetPythonAnalysisClassifier();
                    if (classifier2 != null) {
                        classifier2.NewVersion();
                    }

                    ConnectErrorList(projEntry, buffer);
                    if (doSquiggles) {
                        _errorProvider.AddBufferForErrorSource(projEntry, UnresolvedImportMoniker, buffer);
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
#if FALSE
                            client.UpdateProjectEntry(projEntry);
#endif
                        }
                    }
                }

                bufferParser.Requeue();
            }
        }

        public void ConnectErrorList(ProjectFileInfo projEntry, ITextBuffer buffer) {
            _errorProvider.AddBufferForErrorSource(projEntry, ParserTaskMoniker, buffer);
            _commentTaskProvider.AddBufferForErrorSource(projEntry, ParserTaskMoniker, buffer);
        }

        public void DisconnectErrorList(ProjectFileInfo projEntry, ITextBuffer buffer) {
            _errorProvider.RemoveBufferForErrorSource(projEntry, ParserTaskMoniker, buffer);
            _commentTaskProvider.RemoveBufferForErrorSource(projEntry, ParserTaskMoniker, buffer);
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
        /// Parses the specified text buffer.  Continues to monitor the parsed buffer and updates
        /// the parse tree asynchronously as the buffer changes.
        /// </summary>
        /// <param name="buffer"></param>
        internal BufferParser EnqueueBuffer(ProjectFileInfo projEntry, ITextView textView, ITextBuffer buffer) {
            // only attach one parser to each buffer, we can get multiple enqueue's
            // for example if a document is already open when loading a project.
            BufferParser bufferParser;
            if (!buffer.Properties.TryGetProperty<BufferParser>(typeof(BufferParser), out bufferParser)) {
                Dispatcher dispatcher = null;
                var uiElement = textView as UIElement;
                if (uiElement != null) {
                    dispatcher = uiElement.Dispatcher;
                }
                bufferParser = new BufferParser(dispatcher, projEntry, this, buffer);

                var curSnapshot = buffer.CurrentSnapshot;
                var severity = PyService.GeneralOptions.IndentationInconsistencySeverity;
                bufferParser.EnqueingEntry();
                ParseBuffers(bufferParser, severity, curSnapshot);
            } else {
                bufferParser.AttachedViews++;
            }

            return bufferParser;
        }

        /// <summary>
        /// Starts monitoring a buffer for changes so we will re-parse the buffer to update the analysis
        /// as the text changes.
        /// </summary>
        internal async Task<MonitoredBufferResult> MonitorTextBuffer(ITextView textView, ITextBuffer buffer) {
            var projEntry = await CreateProjectEntry(buffer, new SnapshotCookie(buffer.CurrentSnapshot));

            if (!buffer.Properties.ContainsProperty(typeof(IInteractiveEvaluator))) {
                ConnectErrorList(projEntry, buffer);
                _errorProvider.AddBufferForErrorSource(projEntry, UnresolvedImportMoniker, buffer);
                _unresolvedSquiggles.ListenForNextNewAnalysis(projEntry);
            }

            // kick off initial processing on the buffer
            lock (_openFiles) {
                _conn.SendEventAsync(
                    new FileContentEvent() { fileId = projEntry._fileId, content = buffer.CurrentSnapshot.GetText() });

                var bufferParser = EnqueueBuffer(projEntry, textView, buffer);
                _openFiles[bufferParser] = projEntry;
                return new MonitoredBufferResult(bufferParser, textView, projEntry);
            }
        }

        internal void StopMonitoringTextBuffer(BufferParser bufferParser, ITextView textView) {
            bufferParser.StopMonitoring();
            lock (_openFiles) {
                _openFiles.Remove(bufferParser);
            }

            _unresolvedSquiggles.StopListening(bufferParser._currentProjEntry);

            _errorProvider.ClearErrorSource(bufferParser._currentProjEntry, ParserTaskMoniker);
            _errorProvider.ClearErrorSource(bufferParser._currentProjEntry, UnresolvedImportMoniker);

            if (ImplicitProject) {
                // remove the file from the error list
                _errorProvider.Clear(bufferParser._currentProjEntry, ParserTaskMoniker);
                _errorProvider.Clear(bufferParser._currentProjEntry, UnresolvedImportMoniker);
            }

            _commentTaskProvider.ClearErrorSource(bufferParser._currentProjEntry, ParserTaskMoniker);
            if (ImplicitProject) {
                // remove the file from the error list
                _commentTaskProvider.Clear(bufferParser._currentProjEntry, ParserTaskMoniker);
            }
        }

        private async Task<ProjectFileInfo> CreateProjectEntry(ITextBuffer buffer, IAnalysisCookie analysisCookie) {
            // TODO: Analysis Cookie is getting lost...
            if (_conn == null) {
                // We aren't able to analyze code, so don't create an entry.
                return null;
            }

#if FALSE
            var replEval = buffer.GetReplEvaluator();
            if (replEval != null) {
                // We have a repl window, create an untracked module.
                return _pyAnalyzer.AddModule(null, null, analysisCookie);
            }
#endif

            string path = buffer.GetFilePath();
            if (path == null) {
                return null;
            }

            ProjectFileInfo entry;
            if (!_projectFiles.TryGetValue(path, out entry)) {
                var res = await _conn.SendRequestAsync(new AddFileRequest() { path = path });
                var id = res.fileId;

                _projectFilesById[id] = _projectFiles[path] = new ProjectFileInfo(this, path, id);
                
            }

            return entry;
        }

        private void QueueDirectoryAnalysis(string path) {
            AnalyzeDirectory(PathUtils.NormalizeDirectoryPath(Path.GetDirectoryName(path))).DoNotWait();
        }


        internal async Task<ProjectFileInfo> AnalyzeFile(string path, string addingFromDirectory = null) {
            if (_conn == null) {
                // We aren't able to analyze code, so don't create an entry.
                return null;
            }

            var res = await _conn.SendRequestAsync(new AddFileRequest() { path = path }).ConfigureAwait(false);

            return _projectFilesById[res.fileId] = _projectFiles[path] = new ProjectFileInfo(this, path, res.fileId);
        }

        internal IEnumerable<KeyValuePair<string, ProjectFileInfo>> LoadedFiles {
            get {
                return _projectFiles;
            }
        }

        internal ProjectFileInfo GetEntryFromFile(string path) {
            ProjectFileInfo res;
            if (_projectFiles.TryGetValue(path, out res)) {
                return res;
            }
            return null;
        }

        /// <summary>
        /// Gets a ExpressionAnalysis for the expression at the provided span.  If the span is in
        /// part of an identifier then the expression is extended to complete the identifier.
        /// </summary>
        internal static ExpressionAnalysis AnalyzeExpression(IServiceProvider serviceProvider, ITextSnapshot snapshot, ITrackingSpan span, bool forCompletion = true) {
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

            ProjectFileInfo entry;
            if (buffer.TryGetPythonProjectEntry(out entry) && text.Length > 0) {
                var lineNo = parser.Snapshot.GetLineNumberFromPosition(loc.Start);
                return new ExpressionAnalysis(
                    snapshot.TextBuffer.GetAnalyzer(serviceProvider),
                    text,
                    entry,
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
        internal static CompletionAnalysis GetCompletions(IServiceProvider serviceProvider, ITextSnapshot snapshot, ITrackingSpan span, ITrackingPoint point, CompletionOptions options) {
            return TrySpecialCompletions(serviceProvider, snapshot, span, point, options) ??
                   GetNormalCompletionContext(serviceProvider, snapshot, span, point, options);
        }

        /// <summary>
        /// Gets a list of signatuers available for the expression at the provided location in the snapshot.
        /// </summary>
        internal static Task<SignatureAnalysis> GetSignatures(IServiceProvider serviceProvider, ITextSnapshot snapshot, ITrackingSpan span) {
            var analyzer = snapshot.TextBuffer.GetAnalyzer(serviceProvider);

            return analyzer.GetSignatures(snapshot, span);
        }

        private async Task<SignatureAnalysis> GetSignatures(ITextSnapshot snapshot, ITrackingSpan span) {
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

            if (ShouldEvaluateForCompletion(text)) {
                var liveSigs = TryGetLiveSignatures(snapshot, paramIndex, text, applicableSpan, lastKeywordArg);
                if (liveSigs != null) {
                    return liveSigs;
                }
            }

            var projEntry = snapshot.TextBuffer.GetProjectEntry();
            var result = new List<ISignature>();
            if (projEntry != null) {
                var start = Stopwatch.ElapsedMilliseconds;
                // TODO: Need to deal with version here...
                var location = TranslateIndex(loc.Start, snapshot, projEntry);
                var sigs = await _conn.SendRequestAsync(
                    new SignaturesRequest() {
                        text = text,
                        location = location.Index,
                        column = location.Column,
                        fileId = projEntry.FileId
                    }
                ).ConfigureAwait(false);

                var end = Stopwatch.ElapsedMilliseconds;

                if (/*Logging &&*/ (end - start) > CompletionAnalysis.TooMuchTime) {
                    Trace.WriteLine(String.Format("{0} lookup time {1} for signatures", text, end - start));
                }

                foreach (var sig in sigs.sigs) {
                    result.Add(new PythonSignature(applicableSpan, sig, paramIndex, lastKeywordArg));
                }
            }

            return new SignatureAnalysis(
                text,
                paramIndex,
                result,
                lastKeywordArg
            );
        }

        internal static SourceLocation TranslateIndex(int index, ITextSnapshot fromSnapshot, ProjectFileInfo toAnalysisSnapshot) {
            SnapshotCookie snapshotCookie;
            // TODO: buffers differ in the REPL window case, in the future we should handle this better
            if (toAnalysisSnapshot != null &&
                fromSnapshot != null &&
                (snapshotCookie = toAnalysisSnapshot.AnalysisCookie as SnapshotCookie) != null &&
                snapshotCookie.Snapshot != null &&
                snapshotCookie.Snapshot.TextBuffer == fromSnapshot.TextBuffer) {

                var fromPoint = new SnapshotPoint(fromSnapshot, index);
                var fromLine = fromPoint.GetContainingLine();
                var toPoint = fromPoint.TranslateTo(snapshotCookie.Snapshot, PointTrackingMode.Negative);
                var toLine = toPoint.GetContainingLine();

                Debug.Assert(fromLine != null, "Unable to get 'from' line from " + fromPoint.ToString());
                Debug.Assert(toLine != null, "Unable to get 'to' line from " + toPoint.ToString());

                return new SourceLocation(
                    toPoint.Position,
                    (toLine != null ? toLine.LineNumber : fromLine != null ? fromLine.LineNumber : 0) + 1,
                    index - (fromLine != null ? fromLine.Start.Position : 0) + 1
                );
            } else if (fromSnapshot != null) {
                var fromPoint = new SnapshotPoint(fromSnapshot, index);
                var fromLine = fromPoint.GetContainingLine();

                return new SourceLocation(
                    index,
                    fromLine.LineNumber + 1,
                    index - fromLine.Start.Position + 1
                );
            } else {
                return new SourceLocation(index, 1, 1);
            }
        }

        internal static MissingImportAnalysis GetMissingImports(IServiceProvider serviceProvider, ITextSnapshot snapshot, ITrackingSpan span) {
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
#if FALSE
            IPythonProjectEntry entry;
            ModuleAnalysis analysis;
            if (!snapshot.TextBuffer.TryGetPythonProjectEntry(out entry) ||
                entry == null ||
                (analysis = entry.Analysis) == null) {
                return MissingImportAnalysis.Empty;
            }

            var text = exprRange.Value.GetText();
            if (string.IsNullOrEmpty(text)) {
                return MissingImportAnalysis.Empty;
            }

            var analyzer = analysis.ProjectState;
            var index = (parser.GetStatementRange() ?? span.GetSpan(snapshot)).Start.Position;

            var location = TranslateIndex(
                index,
                snapshot,
                analysis
            );
            var nameExpr = GetFirstNameExpression(analysis.GetAstFromText(text, location).Body);

            if (nameExpr != null && !IsImplicitlyDefinedName(nameExpr)) {
                var name = nameExpr.Name;
                lock (snapshot.TextBuffer.GetAnalyzer(serviceProvider)) {
                    var hasVariables = analysis.GetVariables(name, location).Any(IsDefinition);
                    var hasValues = analysis.GetValues(name, location).Any();

                    // if we have type information or an assignment to the variable we won't offer 
                    // an import smart tag.
                    if (!hasValues && !hasVariables) {
                        var applicableSpan = parser.Snapshot.CreateTrackingSpan(
                            exprRange.Value.Span,
                            SpanTrackingMode.EdgeExclusive
                        );
                        return new MissingImportAnalysis(name, analysis.ProjectState, applicableSpan);
                    }
                }
            }
#endif
            // if we have type information don't offer to add imports
            return MissingImportAnalysis.Empty;
        }

        private static NameExpression GetFirstNameExpression(Statement stmt) {
            return GetFirstNameExpression(Statement.GetExpression(stmt));
        }

        private static NameExpression GetFirstNameExpression(Microsoft.PythonTools.Parsing.Ast.Expression expr) {
            NameExpression nameExpr;
            CallExpression callExpr;
            MemberExpression membExpr;

            if ((nameExpr = expr as NameExpression) != null) {
                return nameExpr;
            }
            if ((callExpr = expr as CallExpression) != null) {
                return GetFirstNameExpression(callExpr.Target);
            }
            if ((membExpr = expr as MemberExpression) != null) {
                return GetFirstNameExpression(membExpr.Target);
            }

            return null;
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
                return false;
#if FALSE
                return _queue.IsParsing || _analysisQueue.IsAnalyzing;
#endif
            }
        }

        internal event EventHandler AnalysisStarted {
            add {
            }
            remove {
            }
        }

        internal void WaitForCompleteAnalysis(Func<int, bool> itemsLeftUpdated) {
            /*if (IsAnalyzing) {
                while (IsAnalyzing) {
                    QueueActivityEvent.WaitOne(100);

                    int itemsLeft = _queue.ParsePending + _analysisQueue.AnalysisPending;

                    if (!itemsLeftUpdated(itemsLeft)) {
                        break;
                    }
                }
            } else */{
                itemsLeftUpdated(0);
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
                return null;
#if FALSE
                return _pyAnalyzer != null ? _pyAnalyzer.Interpreter : null;
#endif
            }
        }

        internal PythonAst ParseSnapshot(ITextSnapshot snapshot) {
            return null;
#if FALSE
            using (var parser = Parser.CreateParser(
                new SnapshotSpanSourceCodeReader(
                    new SnapshotSpan(snapshot, 0, snapshot.Length)
                ),
                Project.LanguageVersion,
                new ParserOptions() { Verbatim = true, BindReferences = true }
            )) {
                return ParseOneFile(null, parser);
            }
#endif
        }

        internal ITextSnapshot GetOpenSnapshot(ProjectFileInfo entry) {
            if (entry == null) {
                return null;
            }

            lock (_openFiles) {
                var item = _openFiles.FirstOrDefault(kv => kv.Value == entry);
                if (item.Value == null) {
                    return null;
                }
                var document = item.Key.Document;
                if (document == null) {
                    return null;
                }

                var textBuffer = document.TextBuffer;
                // TextBuffer may be null if we are racing with file close
                return textBuffer != null ? textBuffer.CurrentSnapshot : null;
            }
        }

#if FALSE
        internal async void ParseFile(ProjectFileInfo entry, string filename, Stream content, Severity indentationSeverity) {
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
                await _conn.SendEventAsync(
                    new FileContentEvent() {
                        fileId = entry.FileId,
                        changes = GetChanges(curVersion)
                    }
                );
                for (var curVersion = pyProjEntry._currentAnalysisVersion.Version;
                        curVersion != null;
                        curVersion = curVersion.Next) {
                   
                }

                PythonAst ast;
                CollectingErrorSink errorSink;
                List<TaskProviderItem> commentTasks;
                if (reader != null) {
                    ParsePythonCode(snapshot, reader, indentationSeverity, out ast, out errorSink, out commentTasks);
                } else {
                    ParsePythonCode(snapshot, content, indentationSeverity, out ast, out errorSink, out commentTasks);
                }

                if (ast != null) {
                    pyEntry.UpdateTree(ast, cookie);
                } else {
                    // notify that we failed to update the existing analysis
                    pyEntry.UpdateTree(null, null);
                }

                // update squiggles for the buffer. snapshot may be null if we
                // are analyzing a file that is not open
                UpdateErrorsAndWarnings(entry, snapshot, errorSink, commentTasks);

                // enqueue analysis of the file
                if (ast != null) {
                    _analysisQueue.Enqueue(pyEntry, AnalysisPriority.Normal);
                }
            } else if ((externalEntry = entry as IExternalProjectEntry) != null) {
                /*
                externalEntry.ParseContent(reader ?? new StreamReader(content), cookie);
                _analysisQueue.Enqueue(entry, AnalysisPriority.Normal);
                */
            }
        }
#endif

        internal async void ParseBuffers(BufferParser bufferParser, Severity indentationSeverity, params ITextSnapshot[] snapshots) {
            ProjectFileInfo entry = bufferParser._currentProjEntry;

            var pyProjEntry = entry;
            List<PythonAst> asts = new List<PythonAst>();
            foreach (var snapshot in snapshots) {
                if (snapshot.TextBuffer.Properties.ContainsProperty(PythonReplEvaluator.InputBeforeReset)) {
                    continue;
                }

                if (snapshot.IsReplBufferWithCommand()) {
                    continue;
                }

                if (pyProjEntry != null && snapshot.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
#if FALSE
                    PythonAst ast;
                    CollectingErrorSink errorSink;
                    List<TaskProviderItem> commentTasks;
#endif

                    if (pyProjEntry._currentAnalysisVersion == null) {
                        await _conn.SendEventAsync(
                            new FileContentEvent() { fileId = entry.FileId, content = snapshot.GetText() }
                        );
                        pyProjEntry._currentAnalysisVersion = snapshot;
                    } else {
                        for (var curVersion = pyProjEntry._currentAnalysisVersion.Version;
                            curVersion != snapshot.Version;
                            curVersion = curVersion.Next) {
                            await _conn.SendEventAsync(
                                new FileChangedEvent() {
                                    fileId = entry.FileId,
                                    changes = GetChanges(curVersion)
                                }
                            );
                        }
                        pyProjEntry._currentAnalysisVersion = snapshot;
                    }

#if FALSE
                    var reader = new SnapshotSpanSourceCodeReader(new SnapshotSpan(snapshot, new Span(0, snapshot.Length)));
                    ParsePythonCode(snapshot, reader, indentationSeverity, out ast, out errorSink, out commentTasks);

                    if (ast != null) {
                        asts.Add(ast);
                    }

                    // update squiggles for the buffer
                    UpdateErrorsAndWarnings(entry, snapshot, errorSink, commentTasks);
#endif
                } else {
                    // other file such as XAML
#if FALSE
                    IExternalProjectEntry externalEntry;
                    if ((externalEntry = (entry as IExternalProjectEntry)) != null) {
                        var snapshotContent = new SnapshotSpanSourceCodeReader(new SnapshotSpan(snapshot, new Span(0, snapshot.Length)));
                        externalEntry.ParseContent(snapshotContent, new SnapshotCookie(snapshotContent.Snapshot));
                        _analysisQueue.Enqueue(entry, AnalysisPriority.High);
                    }
#endif
                }
            }

#if FALSE
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
#endif
        }

        private static ChangeInfo[] GetChanges(ITextVersion curVersion) {
            List<ChangeInfo> changes = new List<ChangeInfo>();
            foreach (var change in curVersion.Changes) {
                changes.Add(
                    new ChangeInfo() {
                        start = change.OldPosition,
                        length = change.OldLength,
                        newText = change.NewText
                    }
                );
            }
            return changes.ToArray();
        }

        private void UpdateErrorsAndWarnings(
            ProjectFileInfo entry,
            ITextSnapshot snapshot,
            CollectingErrorSink errorSink,
            List<TaskProviderItem> commentTasks
        ) {
            // Update the warn-on-launch state for this entry
            bool changed = false;
            lock (_hasParseErrorsLock) {
                changed = errorSink.Errors.Any() ? _hasParseErrors.Add(entry) : _hasParseErrors.Remove(entry);
            }
            if (changed) {
                OnShouldWarnOnLaunchChanged(entry);
            }
#if FALSE
            // Update the parser warnings/errors.
            var factory = new TaskProviderItemFactory(snapshot);
            if (errorSink.Warnings.Any() || errorSink.Errors.Any()) {
                _errorProvider.ReplaceItems(
                    entry,
                    ParserTaskMoniker,
                    errorSink.Warnings
                        .Select(er => factory.FromErrorResult(_serviceProvider, er, VSTASKPRIORITY.TP_NORMAL, VSTASKCATEGORY.CAT_BUILDCOMPILE))
                        .Concat(errorSink.Errors.Select(er => factory.FromErrorResult(_serviceProvider, er, VSTASKPRIORITY.TP_HIGH, VSTASKCATEGORY.CAT_BUILDCOMPILE)))
                        .ToList()
                );
            } else {
                _errorProvider.Clear(entry, ParserTaskMoniker);
            }

            // Update comment tasks.
            if (commentTasks.Count != 0) {
                _commentTaskProvider.ReplaceItems(entry, ParserTaskMoniker, commentTasks);
            } else {
                _commentTaskProvider.Clear(entry, ParserTaskMoniker);
            }
#endif
        }

#region Implementation Details

        private static Stopwatch _stopwatch = MakeStopWatch();

        internal static Stopwatch Stopwatch {
            get {
                return _stopwatch;
            }
        }

        private static SignatureAnalysis TryGetLiveSignatures(ITextSnapshot snapshot, int paramIndex, string text, ITrackingSpan applicableSpan, string lastKeywordArg) {
            IInteractiveEvaluator eval;
            IPythonReplIntellisense dlrEval;
            if (snapshot.TextBuffer.Properties.TryGetProperty<IInteractiveEvaluator>(typeof(IInteractiveEvaluator), out eval) &&
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
                    new Signature() {
                        name = text,
                        doc = sig.Documentation,
                        parameters = sig.Parameters
                            .Select(
                                x => new Analysis.Communication.Parameter() {
                                    name = x.Name,
                                    doc = x.Documentation,
                                    type = x.Type,
                                    defaultValue = x.DefaultValue,
                                    optional = x.IsOptional
                                }
                            ).ToArray()
                    },
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

        internal PythonToolsService PyService {
            get {
                return _pyService;
            }
        }

        public PythonLanguageVersion LanguageVersion {
            get {
                return _interpreterFactory.GetLanguageVersion();
            }
        }

        internal bool ShouldEvaluateForCompletion(string source) {
            switch (_pyService.GetInteractiveOptions(_interpreterFactory).ReplIntellisenseMode) {
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

        class ExprWalker : PythonWalker {
            public bool ShouldExecute = true;

            public override bool Walk(CallExpression node) {
                ShouldExecute = false;
                return base.Walk(node);
            }
        }

        private static CompletionAnalysis TrySpecialCompletions(IServiceProvider serviceProvider, ITextSnapshot snapshot, ITrackingSpan span, ITrackingPoint point, CompletionOptions options) {
            var snapSpan = span.GetSpan(snapshot);
            var buffer = snapshot.TextBuffer;
            var classifier = buffer.GetPythonClassifier();
            if (classifier == null) {
                return null;
            }

            var parser = new ReverseExpressionParser(snapshot, buffer, span);
            var statementRange = parser.GetStatementRange();
            if (!statementRange.HasValue) {
                statementRange = snapSpan.Start.GetContainingLine().Extent;
            }
            if (snapSpan.Start < statementRange.Value.Start) {
                return null;
            }

            var tokens = classifier.GetClassificationSpans(new SnapshotSpan(statementRange.Value.Start, snapSpan.Start));
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

                    if (tokens.Count == 1) {
                        return new DecoratorCompletionAnalysis(span, buffer, options);
                    }
                    // TODO: Handle completions automatically popping up
                    // after '@' when it is used as a binary operator.
                } else if (CompletionAnalysis.IsKeyword(lastClass, "def")) {
                    return new OverrideCompletionAnalysis(span, buffer, options);
                }

                // Import completions
                var first = tokens[0];
                if (CompletionAnalysis.IsKeyword(first, "import")) {
                    return ImportCompletionAnalysis.Make(tokens, span, buffer, options);
                } else if (CompletionAnalysis.IsKeyword(first, "from")) {
                    return FromImportCompletionAnalysis.Make(tokens, span, buffer, options);
                } else if (CompletionAnalysis.IsKeyword(first, "raise") || CompletionAnalysis.IsKeyword(first, "except")) {
                    if (tokens.Count == 1 ||
                        lastClass.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Comma) ||
                        (lastClass.IsOpenGrouping() && tokens.Count < 3)) {
                        return new ExceptionCompletionAnalysis(span, buffer, options);
                    }
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

        private static CompletionAnalysis GetNormalCompletionContext(IServiceProvider serviceProvider, ITextSnapshot snapshot, ITrackingSpan applicableSpan, ITrackingPoint point, CompletionOptions options) {
            var span = applicableSpan.GetSpan(snapshot);

            var parser = new ReverseExpressionParser(snapshot, snapshot.TextBuffer, applicableSpan);
            if (parser.IsInGrouping()) {
                options = options.Clone();
                options.IncludeStatementKeywords = false;
            }

            return new NormalCompletionAnalysis(
                snapshot.TextBuffer.GetAnalyzer(serviceProvider),
                snapshot,
                applicableSpan,
                snapshot.TextBuffer,
                options,
                serviceProvider
            );
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
        public async Task AnalyzeDirectory(string dir, Action<ProjectFileInfo> onFileAnalyzed = null) {
            await _conn.SendRequestAsync(new AddDirectoryRequest() {  dir = dir });

            // TODO: Need to deal with onFileAnalyzed event
        }

        /// <summary>
        /// Analyzes a .zip file including all of the contained files and packages.
        /// </summary>
        /// <param name="dir">.zip file to analyze.</param>
        /// <param name="onFileAnalyzed">If specified, this callback is invoked for every <see cref="IProjectEntry"/>
        /// that is analyzed while analyzing this directory.</param>
        /// <remarks>The callback may be invoked on a thread different from the one that this function was originally invoked on.</remarks>
        public async Task AnalyzeZipArchive(string zipFileName, Action<ProjectFileInfo> onFileAnalyzed = null) {
            await _conn.SendRequestAsync(
                new AddZipArchiveRequest() { archive = zipFileName }
            );
            //_analysisQueue.Enqueue(new AddZipArchiveAnalysis(zipFileName, onFileAnalyzed, this), AnalysisPriority.High);
            // TODO: Need to deal with onFileAnalyzed event
        }

        internal async Task StopAnalyzingDirectory(string directory) {
            await _conn.SendRequestAsync(new RemoveDirectoryRequest() { dir = directory });
        }

        internal void Cancel() {
#if FALSE
            _analysisQueue.Stop();
#endif
        }

        internal async void UnloadFile(ProjectFileInfo entry) {
            // TODO: Need to get file ID
            await _conn.SendRequestAsync(new UnloadFileRequest() { fileId = entry._fileId });
        }

        internal void ClearParserTasks(ProjectFileInfo entry) {
            if (entry != null) {
                _errorProvider.Clear(entry, ParserTaskMoniker);
                _commentTaskProvider.Clear(entry, ParserTaskMoniker);
                _unresolvedSquiggles.StopListening(entry);

                bool removed = false;
                lock (_hasParseErrorsLock) {
                    removed = _hasParseErrors.Remove(entry);
                }
                if (removed) {
                    OnShouldWarnOnLaunchChanged(entry);
                }
            }
        }

        internal void ClearAllTasks() {
            _errorProvider.ClearAll();
            _commentTaskProvider.ClearAll();

            lock (_hasParseErrorsLock) {
                _hasParseErrors.Clear();
            }
        }

        internal bool ShouldWarnOnLaunch(ProjectFileInfo entry) {
            lock (_hasParseErrorsLock) {
                return _hasParseErrors.Contains(entry);
            }
        }

        private void OnShouldWarnOnLaunchChanged(ProjectFileInfo entry) {
            var evt = ShouldWarnOnLaunchChanged;
            if (evt != null) {
                evt(this, new EntryEventArgs(entry));
            }
        }

        internal event EventHandler<EntryEventArgs> ShouldWarnOnLaunchChanged;

#endregion

#region IDisposable Members

        public void Dispose() {
#if FALSE
            foreach (var entry in _projectFiles.Values) {
                _errorProvider.Clear(entry, ParserTaskMoniker);
                _errorProvider.Clear(entry, UnresolvedImportMoniker);
                _commentTaskProvider.Clear(entry, ParserTaskMoniker);
            }
#endif

            _analysisProcess.Kill();
            _analysisProcess.Dispose();
        }

#endregion

        internal void RemoveReference(ProjectAssemblyReference reference) {
            var interp = Interpreter as IPythonInterpreterWithProjectReferences;
            if (interp != null) {
                interp.RemoveReference(reference);
            }
        }

        internal IEnumerable<MemberResult> GetAllAvailableMembers(ProjectFileInfo file, SourceLocation location, GetMemberOptions options) {
            var members = _conn.SendRequestAsync(new TopLevelCompletionsRequest() {
                fileId = file.FileId,
                options = options,
                location = location.Index,
                column = location.Column
            }).Result;

            foreach (var member in members.completions) {
                yield return ToMemberResult(member);
            }
        }

        private static MemberResult ToMemberResult(Analysis.Communication.Completion member) {
            return new MemberResult(
                member.name, 
                member.completion,
                new AnalysisValue[0],
                member.memberType
            );
        }

        internal IEnumerable<MemberResult> GetMembers(ProjectFileInfo file, string text, SourceLocation location, GetMemberOptions options) {
            var members = _conn.SendRequestAsync(new CompletionsRequest() {
                fileId = file.FileId,
                text = text,
                options = options,
                location = location.Index,
                column = location.Column
            }).Result;


            foreach (var member in members.completions) {
                yield return ToMemberResult(member);
            }
        }

        internal IEnumerable<MemberResult> GetModules(ProjectFileInfo file, bool v) {
            // TODO: Deal with this v option
            var members = _conn.SendRequestAsync(new GetModulesRequest() {
                fileId = file.FileId,
            }).Result;


            foreach (var member in members.completions) {
                yield return ToMemberResult(member);
            }
        }

        internal IEnumerable<MemberResult> GetModuleMembers(ProjectFileInfo file, string[] package, bool v) {
            var members = _conn.SendRequestAsync(new GetModuleMembers() {
                fileId = file.FileId,
                package = package
            }).Result;


            foreach (var member in members.completions) {
                yield return ToMemberResult(member);
            }
        }

        internal IEnumerable<IAnalysisVariable> GetVariables(ProjectFileInfo file, string expr, SourceLocation translatedLocation) {
            return new IAnalysisVariable[0];
        }

        internal IEnumerable<AnalysisValue> GetValues(ProjectFileInfo projectFileInfo, string expr, SourceLocation translatedLocation) {
            return new AnalysisValue[0];
        }
    }
}
