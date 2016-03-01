using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Intellisense {
    using Repl;
    using AP = AnalysisProtocol;

    public sealed class VsProjectAnalyzer : IDisposable {
        internal readonly Connection _conn;
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
        private TaskProvider _taskProvider;

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

        internal async Task<AP.BufferUnresolvedImports[]> GetMissingImports(ProjectFileInfo projectFile) {
            var resp = await _conn.SendRequestAsync(new AP.UnresolvedImportsRequest() {
                fileId = projectFile.FileId
            });
            return resp.buffers;
        }

        internal VsProjectAnalyzer(
            IServiceProvider serviceProvider,
            IPythonInterpreter interpreter,
            IPythonInterpreterFactory factory,
            IPythonInterpreterFactory[] allFactories,
            bool implicitProject = true
        ) {
            _taskProvider = (TaskProvider)serviceProvider.GetService(typeof(TaskProvider));
            _unresolvedSquiggles = new UnresolvedImportSquiggleProvider(serviceProvider, _taskProvider);

            _implicitProject = implicitProject;
            _serviceProvider = serviceProvider;
            _interpreterFactory = factory;

            _projectFiles = new ConcurrentDictionary<string, ProjectFileInfo>();
            _projectFilesById = new ConcurrentDictionary<int, ProjectFileInfo>();

            var libAnalyzer = typeof(AP.FileChangedResponse).Assembly.Location;
            _pyService = serviceProvider.GetPythonToolsService();

            _taskProvider.TokensChanged += CommentTaskTokensChanged;

            _conn = StartConnection(factory, libAnalyzer);
            _userCount = 1;

            Task.Run(() => _conn.ProcessMessages());

            _conn.SendEventAsync(
                new AP.OptionsChangedEvent() {
                    implicitProject = implicitProject,
                    indentation_inconsistency_severity = _pyService.GeneralOptions.IndentationInconsistencySeverity
                }
            ).DoNotWait();
            CommentTaskTokensChanged(null, EventArgs.Empty);
        }

        internal async Task<AP.FormatCodeResponse> FormatCode(SnapshotSpan span, string newLine, CodeFormattingOptions options) {
            var fileInfo = span.Snapshot.TextBuffer.GetProjectEntry();
            var buffer = span.Snapshot.TextBuffer;

            await fileInfo.BufferParser.EnsureCodeSynced(buffer);

            return await _conn.SendRequestAsync(
                new AP.FormatCodeRequest() {
                    fileId = fileInfo.FileId,
                    bufferId = fileInfo.BufferParser.GetBufferId(buffer),
                    startIndex = span.Start,
                    endIndex = span.End,
                    options = options,
                    newLine = newLine
                }
            );
        }

        internal async Task<AP.ChangeInfo[]> RemoveImports(ITextBuffer buffer, bool allScopes) {
            var fileInfo = buffer.GetProjectEntry();
            await fileInfo.BufferParser.EnsureCodeSynced(buffer);

            var res = await _conn.SendRequestAsync(
                new AP.RemoveImportsRequest() {
                    fileId = fileInfo.FileId,
                    bufferId = fileInfo.BufferParser.GetBufferId(buffer),
                    allScopes = allScopes
                }
            );

            return res.changes;
        }

        internal async Task<AP.ImportInfo[]> FindNameInAllModules(string name, CancellationToken cancel = default(CancellationToken)) {
            return (await _conn.SendRequestAsync(new AP.AvailableImportsRequest() {
                name = name
            }, cancel)).imports;
        }

        internal async Task<AP.ChangeInfo[]> AddImport(ProjectFileInfo projectFile, string fromModule, string name, string newLine) {
            ITextBuffer buffer = projectFile.BufferParser.Buffers.Last();
            var bufferId = projectFile.BufferParser.GetBufferId(buffer);
            
            var res = await _conn.SendRequestAsync(
                new AP.AddImportRequest() {
                    fromModule = fromModule,
                    name = name,
                    fileId = projectFile.FileId,
                    bufferId = bufferId,
                    newLine = newLine
                }
            );

            return res.changes;
        }

        private Connection StartConnection(IPythonInterpreterFactory factory, string libAnalyzer) {
            var psi = new ProcessStartInfo(libAnalyzer,
                String.Format(
                    "/id {0} /version {1} /interactive",
                    factory.Id,
                    factory.Configuration.Version
                )
            );
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            var process = _analysisProcess = Process.Start(psi);

            var conn = new Connection(
                process.StandardInput.BaseStream,
                process.StandardOutput.BaseStream,
                null,
                AP.RegisteredTypes
            );

            process.Exited += Process_Exited;
            Task.Run(async () => {
                while (!process.HasExited) {
                    var line = await process.StandardError.ReadLineAsync();
                    Debug.WriteLine(line);
                }
            });
            conn.EventReceived += _conn_EventReceived;
            return conn;
        }

        private void CommentTaskTokensChanged(object sender, EventArgs e) {
            Dictionary<string, AP.TaskPriority> priorities = new Dictionary<string, AP.TaskPriority>();
            foreach (var keyValue in _taskProvider.Tokens) {
                priorities[keyValue.Key] = GetPriority(keyValue.Value);
            }
            _conn.SendEventAsync(
                new AP.SetCommentTaskTokens() {
                    tokens = priorities
                }
            ).DoNotWait();
        }

        private void Process_Exited(object sender, EventArgs e) {
            Console.WriteLine("Analysis process has exited");
        }

        private void _conn_EventReceived(object sender, EventReceivedEventArgs e) {
            Debug.WriteLine(String.Format("Event received: {0}", e.Event.name));

            switch (e.Event.name) {
                case AP.AnalysisCompleteEvent.Name:
                    OnAnalysisComplete(e);
                    break;
                case AP.FileParsedEvent.Name:
                    var parsed = (AP.FileParsedEvent)e.Event;
                    ProjectFileInfo projFile;
                    if (_projectFilesById.TryGetValue(parsed.fileId, out projFile)) {
                        UpdateErrorsAndWarnings(projFile, parsed);
                    } else {
                        Debug.WriteLine("Unknown file id for fileParsed event: {0}", parsed.fileId);
                    }
                    break;
            }
        }

        private void OnAnalysisComplete(EventReceivedEventArgs e) {
            var analysisComplete = (AP.AnalysisCompleteEvent)e.Event;
            ProjectFileInfo info;
            if (_projectFilesById.TryGetValue(analysisComplete.fileId, out info)) {
                info.OnAnalysisComplete();
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
            _conn.SendEventAsync(new AP.ModulesChangedEvent()).DoNotWait();
        }

        /// <summary>
        /// Creates a new ProjectEntry for the collection of buffers.
        /// 
        /// _openFiles must be locked when calling this function.
        /// </summary>
        internal async void ReAnalyzeTextBuffers(BufferParser bufferParser) {
            ITextBuffer[] buffers = bufferParser.Buffers;
            if (buffers.Length > 0) {
                _taskProvider.ClearErrorSource(bufferParser._currentProjEntry, ParserTaskMoniker);
                _taskProvider.ClearErrorSource(bufferParser._currentProjEntry, UnresolvedImportMoniker);
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
                        _taskProvider.AddBufferForErrorSource(projEntry, UnresolvedImportMoniker, buffer);
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

        public void ConnectErrorList(ProjectFileInfo projEntry, ITextBuffer buffer) {
            _taskProvider.AddBufferForErrorSource(projEntry, ParserTaskMoniker, buffer);
        }

        public void DisconnectErrorList(ProjectFileInfo projEntry, ITextBuffer buffer) {
            _taskProvider.RemoveBufferForErrorSource(projEntry, ParserTaskMoniker, buffer);
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
                bufferParser = new BufferParser(projEntry, this, buffer);
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
            var projEntry = await CreateProjectEntry(buffer, new SnapshotCookie(buffer.CurrentSnapshot)).ConfigureAwait(false);

            if (!buffer.Properties.ContainsProperty(typeof(IInteractiveEvaluator))) {
                ConnectErrorList(projEntry, buffer);
                _taskProvider.AddBufferForErrorSource(projEntry, UnresolvedImportMoniker, buffer);
                _unresolvedSquiggles.ListenForNextNewAnalysis(projEntry);
            }

            // kick off initial processing on the buffer
            lock (_openFiles) {
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

            _taskProvider.ClearErrorSource(bufferParser._currentProjEntry, ParserTaskMoniker);
            _taskProvider.ClearErrorSource(bufferParser._currentProjEntry, UnresolvedImportMoniker);

            if (ImplicitProject) {
                // remove the file from the error list
                _taskProvider.Clear(bufferParser._currentProjEntry, ParserTaskMoniker);
                _taskProvider.Clear(bufferParser._currentProjEntry, UnresolvedImportMoniker);
            }
        }

        private async Task<ProjectFileInfo> CreateProjectEntry(ITextBuffer buffer, IIntellisenseCookie intellisenseCookie) {
            if (_conn == null) {
                // We aren't able to analyze code, so don't create an entry.
                return null;
            }

            string path;
            var replEval = buffer.GetReplEvaluator();
            if (replEval != null) {
                path = Guid.NewGuid().ToString() + ".py";
            } else {
                path = buffer.GetFilePath();
            }

            if (path == null) {
                return null;
            }

            ProjectFileInfo entry;
            if (!_projectFiles.TryGetValue(path, out entry)) {
                var res = await _conn.SendRequestAsync(
                    new AP.AddFileRequest() {
                        path = path
                    }).ConfigureAwait(false);

                var id = res.fileId;

                entry = _projectFilesById[id] = _projectFiles[path] = new ProjectFileInfo(this, path, id);

            }
            entry.AnalysisCookie = intellisenseCookie;

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

            var response = await _conn.SendRequestAsync(new AP.AddFileRequest() { path = path }).ConfigureAwait(false);

            var res = _projectFilesById[response.fileId] = _projectFiles[path] = new ProjectFileInfo(this, path, response.fileId);
            res.AnalysisCookie = new FileCookie(path);
            return res;
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

        class ApplicableExpression {
            public readonly string Text;
            public readonly ProjectFileInfo File;
            public readonly ITrackingSpan Span;
            public readonly SourceLocation Location;

            public ApplicableExpression(ProjectFileInfo file, string text, ITrackingSpan span, SourceLocation location) {
                File = file;
                Text = text;
                Span = span;
                Location = location;
            }
        }

        internal async Task<NavigationInfo> GetNavigations(ITextSnapshot snapshot) {
            ProjectFileInfo entry;
            if (snapshot.TextBuffer.TryGetPythonProjectEntry(out entry)) {
                var navigations = await _conn.SendRequestAsync(
                    new AP.NavigationRequest() {
                        fileId = entry.FileId
                    }
                );
                foreach (var buffer in navigations.buffers) {
                    var lastParsed = entry.BufferParser.GetLastParsedSnapshot(buffer.bufferId);
                    if (lastParsed.Version.VersionNumber == buffer.version) {
                        return new NavigationInfo(
                            null,
                            NavigationKind.None,
                            new Span(),
                            ConvertNavigations(snapshot, lastParsed, buffer.navigations)
                        );
                    }

                    // race with another vesion being parsed, we should get called again when
                    // we get notified of another new parse tree
                    Debug.Assert(buffer.version > lastParsed.Version.VersionNumber);
                }
            }

            return new NavigationInfo(null, NavigationKind.None, new Span(), Array.Empty<NavigationInfo>());
        }

        private NavigationInfo[] ConvertNavigations(ITextSnapshot currentSnapshot, ITextSnapshot lastParsedSnapshot, AP.Navigation[] navigations) {
            if (navigations == null) {
                return null;
            }

            List<NavigationInfo> res = new List<NavigationInfo>();
            foreach (var nav in navigations) {
                // translate the span from the version we last parsed to the current version
                var span = Tracking.TrackSpanForwardInTime(
                    SpanTrackingMode.EdgeInclusive,
                    Span.FromBounds(nav.startIndex, nav.endIndex),
                    lastParsedSnapshot.Version,
                    currentSnapshot.Version
                );

                res.Add(
                    new NavigationInfo(
                        nav.name,
                        GetNavigationKind(nav.type),
                        span,
                        ConvertNavigations(currentSnapshot, lastParsedSnapshot, nav.children)
                    )
                );
            }
            return res.ToArray();
        }

        private NavigationKind GetNavigationKind(string type) {
            switch (type) {
                case "property": return NavigationKind.Property;
                case "function": return NavigationKind.Function;
                case "class": return NavigationKind.Class;
                case "classmethod": return NavigationKind.ClassMethod;
                case "staticmethod": return NavigationKind.StaticMethod;
                default: return NavigationKind.None;
            }
        }

        internal async Task<IEnumerable<OutliningTaggerProvider.TagSpan>> GetOutliningTags(ITextSnapshot snapshot) {
            ProjectFileInfo entry;
            if (snapshot.TextBuffer.TryGetPythonProjectEntry(out entry)) {
                var outliningTags = await _conn.SendRequestAsync(
                    new AP.OutlingRegionsRequest() {
                        fileId = entry.FileId
                    }
                );

                foreach (var bufferTags in outliningTags.buffers) {
                    var lastParsed = entry.BufferParser.GetLastParsedSnapshot(bufferTags.bufferId);
                    if (lastParsed.Version.VersionNumber == bufferTags.version) {
                        return ConvertOutliningTags(snapshot, lastParsed, bufferTags);
                    }

                    // race with another vesion being parsed, we should get called again when
                    // we get notified of another new parse tree
                    Debug.Assert(bufferTags.version > lastParsed.Version.VersionNumber);
                }

            }
            return Enumerable.Empty<OutliningTaggerProvider.TagSpan>();
        }

        private static IEnumerable<OutliningTaggerProvider.TagSpan> ConvertOutliningTags(ITextSnapshot currentSnapshot, ITextSnapshot lastParsedSnapshot, AP.BufferOutliningTags outliningTags) {
            foreach (var tag in outliningTags.tags) {
                // translate the span from the version we last parsed to the current version
                var span = Tracking.TrackSpanForwardInTime(
                    SpanTrackingMode.EdgeInclusive,
                    Span.FromBounds(tag.startIndex, tag.endIndex),
                    lastParsedSnapshot.Version,
                    currentSnapshot.Version
                );

                int headerIndex = tag.headerIndex;
                if (tag.headerIndex != -1) {
                    headerIndex = Tracking.TrackPositionForwardInTime(
                        PointTrackingMode.Positive,
                        headerIndex,
                        lastParsedSnapshot.Version,
                        currentSnapshot.Version
                    );
                }

                yield return OutliningTaggerProvider.OutliningTagger.GetTagSpan(
                    currentSnapshot,
                    span.Start,
                    span.End,
                    headerIndex
                );
            }
        }

        private static ApplicableExpression GetApplicableExpression(ITextSnapshot snapshot, SnapshotPoint point) {
            var buffer = snapshot.TextBuffer;
            var span = snapshot.CreateTrackingSpan(
                point.Position == snapshot.Length ?
                    new Span(point.Position, 0) :
                    new Span(point.Position, 1),
                SpanTrackingMode.EdgeInclusive
            );

            ReverseExpressionParser parser = new ReverseExpressionParser(snapshot, buffer, span);

            var exprRange = parser.GetExpressionRange(false);
            if (exprRange != null) {
                string text = exprRange.Value.GetText();

                var applicableTo = parser.Snapshot.CreateTrackingSpan(
                    exprRange.Value.Span,
                    SpanTrackingMode.EdgeExclusive
                );

                ProjectFileInfo entry;
                if (buffer.TryGetPythonProjectEntry(out entry) && text.Length > 0) {
                    var loc = parser.Span.GetSpan(parser.Snapshot.Version);
                    var lineNo = parser.Snapshot.GetLineNumberFromPosition(loc.Start);

                    var location = TranslateIndex(loc.Start, parser.Snapshot, entry);
                    return new ApplicableExpression(
                        entry,
                        text,
                        applicableTo,
                        location
                    );
                }
            }

            return null;
        }

        internal static async Task<QuickInfo> GetQuickInfo(ITextSnapshot snapshot, SnapshotPoint point) {
            var analysis = GetApplicableExpression(snapshot, point);

            if (analysis != null) {
                var location = analysis.Location;
                var req = new AP.QuickInfoRequest() {
                    expr = analysis.Text,
                    column = location.Column,
                    index = location.Index,
                    line = location.Line,
                    fileId = analysis.File.FileId
                };

                return new QuickInfo(
                    (await analysis.File.ProjectState._conn.SendRequestAsync(req)).text,
                    analysis.Span
                );
            }

            return null;
        }

        internal AnalysisVariable ToAnalysisVariable(AP.Reference arg) {
            VariableType type = VariableType.None;
            switch (arg.kind) {
                case "definition": type = VariableType.Definition; break;
                case "reference": type = VariableType.Reference; break;
                case "value": type = VariableType.Value; break;
            }

            ProjectFileInfo projectFile;
            _projectFiles.TryGetValue(arg.file, out projectFile);
            var location = new AnalysisLocation(
                projectFile,
                arg.line,
                arg.column
            );
            return new AnalysisVariable(type, location);
        }

        internal static async Task<ExpressionAnalysis> AnalyzeExpression(ITextSnapshot snapshot, SnapshotPoint point) {
            var analysis = GetApplicableExpression(snapshot, point);

            if (analysis != null) {
                var location = analysis.Location;
                var req = new AP.AnalyzeExpressionRequest() {
                    expr = analysis.Text,
                    column = location.Column,
                    index = location.Index,
                    line = location.Line,
                    fileId = analysis.File.FileId
                };

                var definitions = await analysis.File.ProjectState._conn.SendRequestAsync(req);

                return new ExpressionAnalysis(
                    analysis.File.ProjectState,
                    analysis.Text,
                    analysis.Span,
                    definitions.variables.Select(analysis.File.ProjectState.ToAnalysisVariable).ToArray(),
                    definitions.privatePrefix,
                    definitions.memberName
                );
            }

            return null;
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
                    new AP.SignaturesRequest() {
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
                    result.Add(new PythonSignature(this, applicableSpan, sig, paramIndex, lastKeywordArg));
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

        internal static async Task<MissingImportAnalysis> GetMissingImports(IServiceProvider serviceProvider, ITextSnapshot snapshot, ITrackingSpan span) {
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


            ProjectFileInfo entry;
            if (!snapshot.TextBuffer.TryGetPythonProjectEntry(out entry)) {
                return MissingImportAnalysis.Empty;
            }

            var text = exprRange.Value.GetText();
            if (string.IsNullOrEmpty(text)) {
                return MissingImportAnalysis.Empty;
            }

            var analyzer = entry.ProjectState;
            var index = (parser.GetStatementRange() ?? span.GetSpan(snapshot)).Start.Position;

            var location = TranslateIndex(
                index,
                snapshot,
                entry
            );

            var res = await analyzer._conn.SendRequestAsync(
                new AP.IsMissingImportRequest() {
                    fileId = entry.FileId,
                    text = text,
                    index = location.Index,
                    line = location.Line,
                    column = location.Column
                }
            );

            if (res.isMissing) {
                var applicableSpan = parser.Snapshot.CreateTrackingSpan(
                    exprRange.Value.Span,
                    SpanTrackingMode.EdgeExclusive
                );
                return new MissingImportAnalysis(text, analyzer, applicableSpan);
            }

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
            } else */
            {
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

        private void UpdateErrorsAndWarnings(ProjectFileInfo entry, AP.FileParsedEvent parsedEvent) {
            bool hasErrors = false;

            // Update the warn-on-launch state for this entry

            foreach (var buffer in parsedEvent.buffers) {
                hasErrors |= buffer.errors.Any();

                Debug.WriteLine("Received updated parse {0} {1}", parsedEvent.fileId, buffer.version);

                var bufferParser = entry.BufferParser;
                ITextSnapshot snapshot = null;
                if (bufferParser!= null) {
                    snapshot = bufferParser.SnapshotParsed(buffer.bufferId, buffer.version);
                    var lastParsed = bufferParser.GetLastParsedSnapshot(buffer.bufferId);

                    if (lastParsed != null &&
                        snapshot.Version.VersionNumber < lastParsed.Version.VersionNumber) {
                        // ignore receiving responses out of order...
                        Debug.WriteLine("Ignoring out of order parse {0} {1}",
                            snapshot.Version.VersionNumber,
                            lastParsed.Version.VersionNumber
                        );
                        return;
                    }

                    Debug.Assert(snapshot.Version.VersionNumber == buffer.version);
                    entry.BufferParser.SetLastParsedSnapshot(snapshot);
                }

                // Update the parser warnings/errors.
                var factory = new TaskProviderItemFactory(snapshot);
                if (buffer.errors.Any() || buffer.warnings.Any() || buffer.tasks.Any()) {
                    var warningItems = buffer.warnings.Select(er => factory.FromErrorResult(
                        _serviceProvider,
                        er,
                        VSTASKPRIORITY.TP_NORMAL,
                        VSTASKCATEGORY.CAT_BUILDCOMPILE)
                    );
                    var errorItems = buffer.errors.Select(er => factory.FromErrorResult(
                        _serviceProvider,
                        er,
                        VSTASKPRIORITY.TP_HIGH,
                        VSTASKCATEGORY.CAT_BUILDCOMPILE)
                    );

                    var taskItems = buffer.tasks.Select(x => new TaskProviderItem(
                            _serviceProvider,
                            x.message,
                            TaskProviderItemFactory.GetSpan(x),
                            GetPriority(x.priority),
                            GetCategory(x.category),
                            x.squiggle,
                            snapshot
                        )
                    );

                    _taskProvider.ReplaceItems(
                        entry,
                        ParserTaskMoniker,
                        errorItems.Concat(warningItems).Concat(taskItems).ToList()
                    );
                } else {
                    _taskProvider.Clear(entry, ParserTaskMoniker);
                }
            }

            bool changed = false;
            lock (_hasParseErrorsLock) {
                changed = hasErrors ? _hasParseErrors.Add(entry) : _hasParseErrors.Remove(entry);
            }
            if (changed) {
                OnShouldWarnOnLaunchChanged(entry);
            }

            entry.OnParseComplete();
        }

        private static VSTASKCATEGORY GetCategory(AP.TaskCategory category) {
            switch (category) {
                case AP.TaskCategory.buildCompile: return VSTASKCATEGORY.CAT_BUILDCOMPILE;
                case AP.TaskCategory.comments: return VSTASKCATEGORY.CAT_COMMENTS;
                default: return VSTASKCATEGORY.CAT_MISC;
            }
        }

        private static VSTASKPRIORITY GetPriority(AP.TaskPriority priority) {
            switch (priority) {
                case AP.TaskPriority.high: return VSTASKPRIORITY.TP_HIGH;
                case AP.TaskPriority.low: return VSTASKPRIORITY.TP_LOW;
                default: return VSTASKPRIORITY.TP_NORMAL;
            }
        }

        private AP.TaskPriority GetPriority(VSTASKPRIORITY value) {
            switch (value) {
                case VSTASKPRIORITY.TP_HIGH: return AP.TaskPriority.high;
                case VSTASKPRIORITY.TP_LOW: return AP.TaskPriority.low;
                default: return AP.TaskPriority.normal;
            }
        }

        #region Implementation Details

        private static Stopwatch _stopwatch = MakeStopWatch();

        internal static Stopwatch Stopwatch {
            get {
                return _stopwatch;
            }
        }

        private SignatureAnalysis TryGetLiveSignatures(ITextSnapshot snapshot, int paramIndex, string text, ITrackingSpan applicableSpan, string lastKeywordArg) {
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

        private ISignature[] GetLiveSignatures(string text, ICollection<OverloadDoc> liveSigs, int paramIndex, ITrackingSpan span, string lastKeywordArg) {
            ISignature[] res = new ISignature[liveSigs.Count];
            int i = 0;
            foreach (var sig in liveSigs) {
                res[i++] = new PythonSignature(
                    this,
                    span,
                    new AP.Signature() {
                        name = text,
                        doc = sig.Documentation,
                        parameters = sig.Parameters
                            .Select(
                                x => new AP.Parameter() {
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
            await _conn.SendRequestAsync(new AP.AddDirectoryRequest() { dir = dir }).ConfigureAwait(false);

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
                new AP.AddZipArchiveRequest() { archive = zipFileName }
            ).ConfigureAwait(false);
            //_analysisQueue.Enqueue(new AddZipArchiveAnalysis(zipFileName, onFileAnalyzed, this), AnalysisPriority.High);
            // TODO: Need to deal with onFileAnalyzed event
        }

        internal async Task StopAnalyzingDirectory(string directory) {
            await _conn.SendRequestAsync(new AP.RemoveDirectoryRequest() { dir = directory }).ConfigureAwait(false);
        }

        internal void Cancel() {
#if FALSE
            _analysisQueue.Stop();
#endif
        }

        internal async void UnloadFile(ProjectFileInfo entry) {
            // TODO: Need to get file ID
            await _conn.SendRequestAsync(new AP.UnloadFileRequest() { fileId = entry._fileId }).ConfigureAwait(false);
        }

        internal void ClearParserTasks(ProjectFileInfo entry) {
            if (entry != null) {
                _taskProvider.Clear(entry, ParserTaskMoniker);
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
            _taskProvider.ClearAll();

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
            foreach (var entry in _projectFiles.Values) {
                _taskProvider.Clear(entry, ParserTaskMoniker);
                _taskProvider.Clear(entry, UnresolvedImportMoniker);
            }

            _taskProvider.TokensChanged -= CommentTaskTokensChanged;
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
            var members = Task.Run(() => _conn.SendRequestAsync(new AP.TopLevelCompletionsRequest() {
                fileId = file.FileId,
                options = options,
                location = location.Index,
                column = location.Column
            }).Result).Result;

            foreach (var member in members.completions) {
                yield return ToMemberResult(member);
            }
        }

        private static MemberResult ToMemberResult(AP.Completion member) {
            return new MemberResult(
                member.name,
                member.completion,
                new AnalysisValue[0],
                member.memberType
            );
        }

        internal IEnumerable<MemberResult> GetMembers(ProjectFileInfo file, string text, SourceLocation location, GetMemberOptions options) {
            var members = Task.Run(() => _conn.SendRequestAsync(new AP.CompletionsRequest() {
                fileId = file.FileId,
                text = text,
                options = options,
                location = location.Index,
                column = location.Column
            }).Result).Result;


            foreach (var member in members.completions) {
                yield return ToMemberResult(member);
            }
        }

        internal IEnumerable<MemberResult> GetModules(ProjectFileInfo file, bool v) {
            // TODO: Deal with this v option
            var members = _conn.SendRequestAsync(new AP.GetModulesRequest() {
                fileId = file.FileId,
            }).Result;


            foreach (var member in members.completions) {
                yield return ToMemberResult(member);
            }
        }

        internal IEnumerable<MemberResult> GetModuleMembers(ProjectFileInfo file, string[] package, bool v) {
            var members = _conn.SendRequestAsync(new AP.GetModuleMembers() {
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
