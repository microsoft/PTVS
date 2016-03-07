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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Cdp;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    public sealed class VsProjectAnalyzer : IDisposable {
        private readonly Process _analysisProcess;
        private readonly Connection _conn;
        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_zipFileName] contains the full path to that archive.
        private static readonly object _zipFileName = new { Name = "ZipFileName" };

        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_pathInZipFile] contains the path of the item inside the archive.
        private static readonly object _pathInZipFile = new { Name = "PathInZipFile" };

        private readonly Dictionary<BufferParser, AnalysisEntry> _openFiles = new Dictionary<BufferParser, AnalysisEntry>();
        private readonly bool _implicitProject;
        private readonly StringBuilder _stdErr = new StringBuilder();

        private readonly IPythonInterpreterFactory _interpreterFactory;

        private readonly ConcurrentDictionary<string, AnalysisEntry> _projectFiles;
        private readonly ConcurrentDictionary<int, AnalysisEntry> _projectFilesById;

        internal readonly HashSet<AnalysisEntry> _hasParseErrors = new HashSet<AnalysisEntry>();
        internal readonly object _hasParseErrorsLock = new object();

        private const string ParserTaskMoniker = "Parser";
        internal const string UnresolvedImportMoniker = "UnresolvedImport";
        internal bool _analysisComplete;

        private TaskProvider _taskProvider;

        private int _userCount;

        private readonly UnresolvedImportSquiggleProvider _unresolvedSquiggles;
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;
        private readonly CancellationTokenSource _processExitedCancelSource = new CancellationTokenSource();
        private bool _disposing;

        internal Task ReloadTask;
        internal int _parsePending;


        internal VsProjectAnalyzer(
            IServiceProvider serviceProvider,
            IPythonInterpreterFactory factory,
            IPythonInterpreterFactory[] allFactories)
            : this(serviceProvider, factory.CreateInterpreter(), factory, allFactories) {
        }

        internal async Task<AP.BufferUnresolvedImports[]> GetMissingImports(AnalysisEntry projectFile) {
            var resp = await SendRequestAsync(
                new AP.UnresolvedImportsRequest() {
                    fileId = projectFile.FileId
                }
            );
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

            _projectFiles = new ConcurrentDictionary<string, AnalysisEntry>();
            _projectFilesById = new ConcurrentDictionary<int, AnalysisEntry>();

            var libAnalyzer = typeof(AP.FileChangedResponse).Assembly.Location;
            _pyService = serviceProvider.GetPythonToolsService();

            _taskProvider.TokensChanged += CommentTaskTokensChanged;

            _conn = StartConnection(factory, libAnalyzer, out _analysisProcess);
            _userCount = 1;

            Task.Run(() => _conn.ProcessMessages());

            _conn.SendEventAsync(
                new AP.OptionsChangedEvent() {
                    indentation_inconsistency_severity = _pyService.GeneralOptions.IndentationInconsistencySeverity
                }
            ).DoNotWait();
            CommentTaskTokensChanged(null, EventArgs.Empty);
        }

        #region Public API

        public PythonLanguageVersion LanguageVersion {
            get {
                return _interpreterFactory.GetLanguageVersion();
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

        #region IDisposable Members

        public void Dispose() {
            _disposing = true;
            foreach (var entry in _projectFiles.Values) {
                _taskProvider.Clear(entry, ParserTaskMoniker);
                _taskProvider.Clear(entry, UnresolvedImportMoniker);
            }

            _taskProvider.TokensChanged -= CommentTaskTokensChanged;
            try {
                if (!_analysisProcess.HasExited) {
                    _analysisProcess.Kill();
                }
            } catch (InvalidOperationException) {
                // race w/ process exit...
            }
            _analysisProcess.Dispose();
        }

        #endregion

        /// <summary>
        /// Analyzes a complete directory including all of the contained files and packages.
        /// </summary>
        /// <param name="dir">Directory to analyze.</param>
        /// <param name="onFileAnalyzed">If specified, this callback is invoked for every <see cref="IProjectEntry"/>
        /// that is analyzed while analyzing this directory.</param>
        /// <remarks>The callback may be invoked on a thread different from the one that this function was originally invoked on.</remarks>
        public async Task AnalyzeDirectory(string dir) {
            await SendRequestAsync(new AP.AddDirectoryRequest() { dir = dir }).ConfigureAwait(false);
        }

        /// <summary>
        /// Analyzes a .zip file including all of the contained files and packages.
        /// </summary>
        /// <param name="dir">.zip file to analyze.</param>
        /// <param name="onFileAnalyzed">If specified, this callback is invoked for every <see cref="IProjectEntry"/>
        /// that is analyzed while analyzing this directory.</param>
        /// <remarks>The callback may be invoked on a thread different from the one that this function was originally invoked on.</remarks>
        public async Task AnalyzeZipArchive(string zipFileName) {
            await SendRequestAsync(
                new AP.AddZipArchiveRequest() { archive = zipFileName }
            ).ConfigureAwait(false);
        }

        #endregion

        private Connection StartConnection(IPythonInterpreterFactory factory, string libAnalyzer, out Process proc) {
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

            var process = Process.Start(psi);

            var conn = new Connection(
                process.StandardInput.BaseStream,
                process.StandardOutput.BaseStream,
                null,
                AP.RegisteredTypes
            );

            process.Exited += OnAnalysisProcessExited;
            Task.Run(async () => {
                try {
                    while (!process.HasExited) {
                        var line = await process.StandardError.ReadLineAsync();
                        _stdErr.AppendLine(line);
                        Debug.WriteLine("Analysis Std Err: " + line);
                    }
                } catch (InvalidOperationException) {
                    // can race with dispose of the process...
                }
            });
            conn.EventReceived += ConnectionEventReceived;
            proc = process;
            return conn;
        }

        private void OnAnalysisProcessExited(object sender, EventArgs e) {
            _processExitedCancelSource.Cancel();
            if (!_disposing) {
                AbnormalAnalysisExit?.Invoke(
                    this,
                    new AbnormalAnalysisExitEventArgs(
                        _stdErr.ToString(),
                        _analysisProcess.ExitCode
                    )
                );
            }
        }

        internal event EventHandler<AbnormalAnalysisExitEventArgs> AbnormalAnalysisExit;
        internal event EventHandler AnalysisStarted;

        private void ConnectionEventReceived(object sender, EventReceivedEventArgs e) {
            Debug.WriteLine(String.Format("Event received: {0}", e.Event.name));

            switch (e.Event.name) {
                case AP.AnalysisCompleteEvent.Name:
                    _analysisComplete = true;
                    break;
                case AP.FileAnalysisCompleteEvent.Name:
                    OnAnalysisComplete(e);
                    break;
                case AP.FileParsedEvent.Name:
                    Interlocked.Decrement(ref _parsePending);

                    var parsed = (AP.FileParsedEvent)e.Event;
                    AnalysisEntry projFile;
                    if (_projectFilesById.TryGetValue(parsed.fileId, out projFile)) {
                        UpdateErrorsAndWarnings(projFile, parsed);
                    } else {
                        Debug.WriteLine("Unknown file id for fileParsed event: {0}", parsed.fileId);
                    }
                    break;
                case AP.ChildFileAnalyzed.Name:
                    var childFile = (AP.ChildFileAnalyzed)e.Event;
                    var entry = new AnalysisEntry(
                        this,
                        childFile.filename,
                        childFile.fileId
                    );
                    entry.SearchPathEntry = childFile.parent;
                    _projectFilesById[childFile.fileId] = _projectFiles[childFile.filename] = entry;
                    break;
            }
        }

        private void OnAnalysisComplete(EventReceivedEventArgs e) {
            var analysisComplete = (AP.FileAnalysisCompleteEvent)e.Event;
            AnalysisEntry info;
            if (_projectFilesById.TryGetValue(analysisComplete.fileId, out info)) {

                info.OnAnalysisComplete();

                if (info.BufferParser != null) {
                    foreach (var version in analysisComplete.versions) {
                        info.BufferParser.Analyzed(version.bufferId, version.version);
                    }
                }
            }
        }

        internal static string GetZipFileName(AnalysisEntry entry) {
            object result;
            entry.Properties.TryGetValue(_zipFileName, out result);
            return (string)result;
        }

        private static void SetZipFileName(AnalysisEntry entry, string value) {
            entry.Properties[_zipFileName] = value;
        }

        internal static string GetPathInZipFile(AnalysisEntry entry) {
            object result;
            entry.Properties.TryGetValue(_pathInZipFile, out result);
            return (string)result;
        }

        private static void SetPathInZipFile(AnalysisEntry entry, string value) {
            entry.Properties[_pathInZipFile] = value;
        }

        internal async Task<AP.ModuleInfo[]> GetEntriesThatImportModule(string moduleName, bool includeUnresolved) {
            return (await _conn.SendRequestAsync(
                new AP.ModuleImportsRequest() {
                    includeUnresolved = includeUnresolved,
                    moduleName = moduleName
                }
            ).ConfigureAwait(false)).modules;
        }

        private void OnModulesChanged(object sender, EventArgs e) {
            _conn.SendEventAsync(new AP.ModulesChangedEvent()).DoNotWait();
        }

        /// <summary>
        /// Creates a new ProjectEntry for the collection of buffers.
        /// </summary>
        internal async void ReAnalyzeTextBuffers(BufferParser oldParser) {
            ITextBuffer[] buffers = oldParser.Buffers;
            if (buffers.Length > 0) {
                _taskProvider.ClearErrorSource(oldParser._currentProjEntry, ParserTaskMoniker);
                _taskProvider.ClearErrorSource(oldParser._currentProjEntry, UnresolvedImportMoniker);
                
                foreach (var buffer in buffers) {
                    oldParser.UninitBuffer(buffer);
                }

                var monitoredResult = await MonitorTextBuffer(buffers[0]);

                for (int i = 1; i < buffers.Length; i++) {
                    monitoredResult.BufferParser.AddBuffer(buffers[i]);
                }

                oldParser._currentProjEntry.OnNewAnalysisEntry();
            }
        }

        internal void ConnectErrorList(AnalysisEntry projEntry, ITextBuffer buffer) {
            _taskProvider.AddBufferForErrorSource(projEntry, ParserTaskMoniker, buffer);
        }

        internal void DisconnectErrorList(AnalysisEntry projEntry, ITextBuffer buffer) {
            _taskProvider.RemoveBufferForErrorSource(projEntry, ParserTaskMoniker, buffer);
        }

        internal void SwitchAnalyzers(VsProjectAnalyzer oldAnalyzer) {
            BufferParser[] parsers;
            lock (_openFiles) {
                parsers = oldAnalyzer._openFiles.Keys.ToArray();
            }

            foreach (var bufferParser in parsers) {
                ReAnalyzeTextBuffers(bufferParser);
            }
        }

        /// <summary>
        /// Parses the specified text buffer.  Continues to monitor the parsed buffer and updates
        /// the parse tree asynchronously as the buffer changes.
        /// </summary>
        /// <param name="buffer"></param>
        internal BufferParser EnqueueBuffer(AnalysisEntry projEntry, ITextBuffer buffer) {
            // only attach one parser to each buffer, we can get multiple enqueue's
            // for example if a document is already open when loading a project.
            BufferParser bufferParser;
            if (!buffer.Properties.TryGetProperty(typeof(BufferParser), out bufferParser)) {
                bufferParser = new BufferParser(projEntry, this, buffer);
            } else {
                bufferParser.AttachedViews++;
            }

            return bufferParser;
        }

        internal void OnAnalysisStarted() {
            AnalysisStarted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Starts monitoring a buffer for changes so we will re-parse the buffer to update the analysis
        /// as the text changes.
        /// </summary>
        internal async Task<MonitoredBufferResult> MonitorTextBuffer(ITextBuffer buffer) {
            var entry = await CreateProjectEntry(buffer, new SnapshotCookie(buffer.CurrentSnapshot)).ConfigureAwait(false);

            if (!buffer.Properties.ContainsProperty(typeof(IInteractiveEvaluator))) {
                ConnectErrorList(entry, buffer);
                _taskProvider.AddBufferForErrorSource(entry, UnresolvedImportMoniker, buffer);
                _unresolvedSquiggles.ListenForNextNewAnalysis(entry, buffer);
            }

            // kick off initial processing on the buffer
            lock (_openFiles) {
                var bufferParser = EnqueueBuffer(entry, buffer);
                _openFiles[bufferParser] = entry;
                return new MonitoredBufferResult(bufferParser, entry);
            }
        }

        internal void StopMonitoringTextBuffer(BufferParser bufferParser, ITextView textView) {
            bufferParser.StopMonitoring();
            lock (_openFiles) {
                _openFiles.Remove(bufferParser);
            }

            _taskProvider.ClearErrorSource(bufferParser._currentProjEntry, ParserTaskMoniker);
            _taskProvider.ClearErrorSource(bufferParser._currentProjEntry, UnresolvedImportMoniker);

            if (ImplicitProject) {
                // remove the file from the error list
                _taskProvider.Clear(bufferParser._currentProjEntry, ParserTaskMoniker);
                _taskProvider.Clear(bufferParser._currentProjEntry, UnresolvedImportMoniker);
            }
        }

        private async Task<AnalysisEntry> CreateProjectEntry(ITextBuffer buffer, IIntellisenseCookie intellisenseCookie) {
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

            AnalysisEntry entry;
            if (!_projectFiles.TryGetValue(path, out entry)) {
                _analysisComplete = false;
                Interlocked.Increment(ref _parsePending);

                var res = await SendRequestAsync(
                    new AP.AddFileRequest() {
                        path = path
                    }).ConfigureAwait(false);

                OnAnalysisStarted();

                var id = res.fileId;

                entry = _projectFilesById[id] = _projectFiles[path] = new AnalysisEntry(this, path, id);

            }
            entry.AnalysisCookie = intellisenseCookie;

            return entry;
        }


        internal async Task<AnalysisEntry> AnalyzeFile(string path, string addingFromDirectory = null) {
            if (_conn == null) {
                // We aren't able to analyze code, so don't create an entry.
                return null;
            }

            AnalysisEntry res;
            if (!_projectFiles.TryGetValue(path, out res)) {
                Interlocked.Increment(ref _parsePending);

                var response = await SendRequestAsync(new AP.AddFileRequest() { path = path }).ConfigureAwait(false);

                res = _projectFilesById[response.fileId] = _projectFiles[path] = new AnalysisEntry(this, path, response.fileId);
                res.AnalysisCookie = new FileCookie(path);
            }

            return res;
        }


        internal AnalysisEntry GetEntryFromFile(string path) {
            AnalysisEntry res;
            if (_projectFiles.TryGetValue(path, out res)) {
                return res;
            }
            return null;
        }

        internal IEnumerable<KeyValuePair<string, AnalysisEntry>> LoadedFiles {
            get {
                return _projectFiles;
            }
        }

        internal static async Task<string[]> GetValueDescriptions(AnalysisEntry file, string expr, SnapshotPoint point) {
            var analysis = GetApplicableExpression(point);

            if (analysis != null) {
                var location = analysis.Location;
                var req = new AP.ValueDescriptionRequest() {
                    expr = analysis.Text,
                    column = location.Column,
                    index = location.Index,
                    line = location.Line,
                    fileId = analysis.File.FileId
                };

                var res = await analysis.File.Analyzer.SendRequestAsync(req).ConfigureAwait(false);
                return res.descriptions;
            }

            return Array.Empty<string>();
        }

        internal static async Task<ExpressionAnalysis> AnalyzeExpression(AnalysisEntry file, string expr, SourceLocation location) {
            var req = new AP.AnalyzeExpressionRequest() {
                expr = expr,
                column = location.Column,
                index = location.Index,
                line = location.Line,
                fileId = file.FileId
            };

            var definitions = await file.Analyzer.SendRequestAsync(req);

            return new ExpressionAnalysis(
                expr,
                null,
                definitions.variables.Select(file.Analyzer.ToAnalysisVariable).ToArray(),
                definitions.privatePrefix,
                definitions.memberName
            );
        }

        internal static async Task<ExpressionAnalysis> AnalyzeExpression(SnapshotPoint point) {
            var analysis = GetApplicableExpression(point);

            if (analysis != null) {
                var location = analysis.Location;
                var req = new AP.AnalyzeExpressionRequest() {
                    expr = analysis.Text,
                    column = location.Column,
                    index = location.Index,
                    line = location.Line,
                    fileId = analysis.File.FileId
                };

                var definitions = await analysis.File.Analyzer.SendRequestAsync(req);

                return new ExpressionAnalysis(
                    analysis.Text,
                    analysis.Span,
                    definitions.variables.Select(analysis.File.Analyzer.ToAnalysisVariable).ToArray(),
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

            var projEntry = snapshot.TextBuffer.GetAnalysisEntry();
            var result = new List<ISignature>();
            if (projEntry != null) {
                var start = Stopwatch.ElapsedMilliseconds;
                // TODO: Need to deal with version here...
                var location = TranslateIndex(loc.Start, snapshot, projEntry);
                var sigs = await SendRequestAsync(
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

        internal static SourceLocation TranslateIndex(int index, ITextSnapshot fromSnapshot, AnalysisEntry toAnalysisSnapshot) {
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

            AnalysisEntry entry;
            if (!snapshot.TextBuffer.TryGetPythonProjectEntry(out entry)) {
                return MissingImportAnalysis.Empty;
            }

            var text = exprRange.Value.GetText();
            if (string.IsNullOrEmpty(text)) {
                return MissingImportAnalysis.Empty;
            }

            var analyzer = entry.Analyzer;
            var index = (parser.GetStatementRange() ?? span.GetSpan(snapshot)).Start.Position;

            var location = TranslateIndex(
                index,
                snapshot,
                entry
            );

            var isMissing = await analyzer.IsMissingImport(entry, text, location);

            if (isMissing) {
                var applicableSpan = parser.Snapshot.CreateTrackingSpan(
                    exprRange.Value.Span,
                    SpanTrackingMode.EdgeExclusive
                );
                return new MissingImportAnalysis(text, analyzer, applicableSpan);
            }

            // if we have type information don't offer to add imports
            return MissingImportAnalysis.Empty;
        }

        internal static async Task AddImport(AnalysisEntry projectFile, string fromModule, string name, ITextView view, ITextBuffer buffer) {
            var changes = await projectFile.Analyzer.AddImport(
                projectFile,
                fromModule,
                name,
                view.Options.GetNewLineCharacter()
            );

            ApplyChanges(
                changes.changes,
                projectFile,
                projectFile.BufferParser.GetBufferId(buffer),
                changes.version
            );
        }

        internal async Task<bool> IsMissingImport(AnalysisEntry entry, string text, SourceLocation location) {
            return (await SendRequestAsync(
                new AP.IsMissingImportRequest() {
                    fileId = entry.FileId,
                    text = text,
                    index = location.Index,
                    line = location.Line,
                    column = location.Column
                }
            ).ConfigureAwait(false)).isMissing;
        }

        private static NameExpression GetFirstNameExpression(Statement stmt) {
            return GetFirstNameExpression(Statement.GetExpression(stmt));
        }

        private static NameExpression GetFirstNameExpression(Expression expr) {
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

                return _parsePending > 0 || !_analysisComplete;
            }
        }

        internal void WaitForCompleteAnalysis(Func<int, bool> itemsLeftUpdated) {
            if (IsAnalyzing) {
                while (IsAnalyzing) {
                    var res = SendRequestAsync(new AP.AnalysisStatusRequest()).Result;

                    if (!itemsLeftUpdated(res.itemsLeft)) {
                        break;
                    }
                }
            } else {
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

        private void UpdateErrorsAndWarnings(AnalysisEntry entry, AP.FileParsedEvent parsedEvent) {
            bool hasErrors = false;

            // Update the warn-on-launch state for this entry

            foreach (var buffer in parsedEvent.buffers) {
                hasErrors |= buffer.errors.Any();

                Debug.WriteLine("Received updated parse {0} {1}", parsedEvent.fileId, buffer.version);

                var bufferParser = entry.BufferParser;
                LocationTracker translator = null;
                if (bufferParser != null) {
                    if (bufferParser.IsOldSnapshot(buffer.bufferId, buffer.version)) {
                        // ignore receiving responses out of order...
                        Debug.WriteLine("Ignoring out of order parse {0}", buffer.version);
                        return;
                    }
                    translator = new LocationTracker(entry, buffer.bufferId, buffer.version);
                }

                // Update the parser warnings/errors.
                var factory = new TaskProviderItemFactory(translator);
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
                            translator
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

            if (IsSpaceCompletion(snapshot, point) && !IntellisenseController.ForceCompletions) {
                return CompletionAnalysis.EmptyCompletionContext;
            }

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

        internal async Task StopAnalyzingDirectory(string directory) {
            await SendRequestAsync(new AP.RemoveDirectoryRequest() { dir = directory }).ConfigureAwait(false);
        }

        internal async void UnloadFile(AnalysisEntry entry) {
            // TODO: Need to get file ID
            await SendRequestAsync(new AP.UnloadFileRequest() { fileId = entry.FileId }).ConfigureAwait(false);
        }

        internal void ClearAllTasks() {
            _taskProvider.ClearAll();

            lock (_hasParseErrorsLock) {
                _hasParseErrors.Clear();
            }
        }

        internal bool ShouldWarnOnLaunch(AnalysisEntry entry) {
            lock (_hasParseErrorsLock) {
                return _hasParseErrors.Contains(entry);
            }
        }

        private void OnShouldWarnOnLaunchChanged(AnalysisEntry entry) {
            var evt = ShouldWarnOnLaunchChanged;
            if (evt != null) {
                evt(this, new EntryEventArgs(entry));
            }
        }

        internal event EventHandler<EntryEventArgs> ShouldWarnOnLaunchChanged;

        #endregion

        internal async Task<T> SendRequestAsync<T>(Request<T> request) where T : Response, new() {
            Debug.WriteLine(String.Format("Sending request {0}", request.command));
            return await _conn.SendRequestAsync(request, _processExitedCancelSource.Token).ConfigureAwait(false);
        }

        internal IEnumerable<CompletionResult> GetAllAvailableMembers(AnalysisEntry file, SourceLocation location, GetMemberOptions options) {
            var members = Task.Run(() => SendRequestAsync(new AP.TopLevelCompletionsRequest() {
                fileId = file.FileId,
                options = options,
                location = location.Index,
                column = location.Column
            }).Result).Result;

            foreach (var member in members.completions) {
                yield return ToMemberResult(member);
            }
        }

        private static CompletionResult ToMemberResult(AP.Completion member) {
            return new CompletionResult(
                member.name,
                member.completion,
                member.doc,
                member.memberType,
                member.detailedValues
            );
        }

        internal async Task<IEnumerable<CompletionResult>> GetMembers(AnalysisEntry file, string text, SourceLocation location, GetMemberOptions options) {
            var members = await SendRequestAsync(new AP.CompletionsRequest() {
                fileId = file.FileId,
                text = text,
                options = options,
                location = location.Index,
                column = location.Column
            }).ConfigureAwait(false);

            return ConvertMembers(members.completions);
        }

        private IEnumerable<CompletionResult> ConvertMembers(AP.Completion[] completions) {
            foreach (var member in completions) {
                yield return ToMemberResult(member);
            }
        }

        internal IEnumerable<CompletionResult> GetModules(bool topLevelOnly) {
            var members = SendRequestAsync(new AP.GetModulesRequest() {
                topLevelOnly = topLevelOnly
            }).Result;


            foreach (var member in members.completions) {
                yield return ToMemberResult(member);
            }
        }

        internal IEnumerable<CompletionResult> GetModuleMembers(AnalysisEntry file, string[] package, bool includeMembers) {
            var members = SendRequestAsync(new AP.GetModuleMembers() {
                fileId = file.FileId,
                package = package,
                includeMembers = includeMembers
            }).Result;

            foreach (var member in members.completions) {
                yield return ToMemberResult(member);
            }
        }

        internal IEnumerable<IAnalysisVariable> GetVariables(AnalysisEntry file, string expr, SourceLocation translatedLocation) {
            return new IAnalysisVariable[0];
        }

        internal IEnumerable<AnalysisValue> GetValues(AnalysisEntry projectFileInfo, string expr, SourceLocation translatedLocation) {
            return new AnalysisValue[0];
        }
        internal async Task<string[]> FindMethods(AnalysisEntry fileInfo, ITextBuffer textBuffer, string className, int? paramCount) {
            return (await SendRequestAsync(
                new AP.FindMethodsRequest() {
                    fileId = fileInfo.FileId,
                    bufferId = fileInfo.BufferParser.GetBufferId(textBuffer),
                    className = className,
                    paramCount = paramCount
                }
            ).ConfigureAwait(false)).names;
        }

        internal async Task<AP.MethodInsertionLocationResponse> GetInsertionPoint(AnalysisEntry fileInfo, ITextBuffer textBuffer, string className) {
            try {
                return await SendRequestAsync(
                    new AP.MethodInsertionLocationRequest() {
                        fileId = fileInfo.FileId,
                        bufferId = fileInfo.BufferParser.GetBufferId(textBuffer),
                        className = className
                    }
                ).ConfigureAwait(false);
            } catch (FailedRequestException) {
                return null;
            }
        }

        internal async Task<AP.MethodInfoResponse> GetMethodInfo(AnalysisEntry fileInfo, ITextBuffer textBuffer, string className, string methodName) {
            return await SendRequestAsync(
                new AP.MethodInfoRequest() {
                    fileId = fileInfo.FileId,
                    bufferId = fileInfo.BufferParser.GetBufferId(textBuffer),
                    className = className,
                    methodName = methodName
                }
            ).ConfigureAwait(false);
        }


        internal async Task<AP.AnalysisClassificationsResponse> GetAnalysisClassifications(AnalysisEntry projFile, ITextBuffer buffer, bool colorNames) {
            try {
                return await SendRequestAsync(
                    new AP.AnalysisClassificationsRequest() {
                        fileId = projFile.FileId,
                        bufferId = projFile.BufferParser.GetBufferId(buffer),
                        colorNames = colorNames
                    }
                ).ConfigureAwait(false);
            } catch (TaskCanceledException) {
                return null;
            }
        }

        internal async Task<AP.LocationNameResponse> GetNameOfLocation(AnalysisEntry projFile, ITextBuffer buffer, int line, int column) {
            return await SendRequestAsync(
                new AP.LocationNameRequest() {
                    fileId = projFile.FileId,
                    bufferId = projFile.BufferParser.GetBufferId(buffer),
                    line = line,
                    column = column
                }
            ).ConfigureAwait(false);
        }


        internal async Task<string[]> GetProximityExpressions(AnalysisEntry projFile, ITextBuffer buffer, int line, int column, int lineCount) {
            return (await SendRequestAsync(
                new AP.ProximityExpressionsRequest() {
                    fileId = projFile.FileId,
                    bufferId = projFile.BufferParser.GetBufferId(buffer),
                    line = line,
                    column = column,
                    lineCount = lineCount
                }
            ).ConfigureAwait(false)).names;
        }

        internal async Task FormatCode(SnapshotSpan span, ITextView view, CodeFormattingOptions options, bool selectResult) {
            var fileInfo = span.Snapshot.TextBuffer.GetAnalysisEntry();
            var buffer = span.Snapshot.TextBuffer;

            await fileInfo.BufferParser.EnsureCodeSynced(buffer);

            var bufferId = fileInfo.BufferParser.GetBufferId(buffer);

            var res = await SendRequestAsync(
                new AP.FormatCodeRequest() {
                    fileId = fileInfo.FileId,
                    bufferId = fileInfo.BufferParser.GetBufferId(buffer),
                    startIndex = span.Start,
                    endIndex = span.End,
                    options = options,
                    newLine = view.Options.GetNewLineCharacter()
                }
            );

            ITrackingSpan selectionSpan = null;
            if (selectResult) {
                selectionSpan = view.TextBuffer.CurrentSnapshot.CreateTrackingSpan(
                    Span.FromBounds(res.startIndex, res.endIndex),
                    SpanTrackingMode.EdgeInclusive
                );
            }

            ApplyChanges(res.changes, fileInfo, bufferId, res.version);

            if (selectResult) {
                view.Selection.Select(selectionSpan.GetSpan(view.TextBuffer.CurrentSnapshot), false);
            }
        }

        internal async Task RemoveImports(ITextBuffer buffer, bool allScopes) {
            var fileInfo = buffer.GetAnalysisEntry();
            await fileInfo.BufferParser.EnsureCodeSynced(buffer);

            var res = await SendRequestAsync(
                new AP.RemoveImportsRequest() {
                    fileId = fileInfo.FileId,
                    bufferId = fileInfo.BufferParser.GetBufferId(buffer),
                    allScopes = allScopes
                }
            );

            ApplyChanges(
                res.changes,
                fileInfo,
                fileInfo.BufferParser.GetBufferId(buffer),
                res.version
            );
        }

        internal async Task<IEnumerable<ExportedMemberInfo>> FindNameInAllModules(string name, CancellationToken cancel = default(CancellationToken)) {
            CancellationTokenRegistration registration1 = default(CancellationTokenRegistration), registration2 = default(CancellationTokenRegistration);
            if (cancel.CanBeCanceled) {
                CancellationTokenSource source = new CancellationTokenSource();
                registration1 = cancel.Register(() => source.Cancel());
                registration2 = _processExitedCancelSource.Token.Register(() => source.Cancel());
                cancel = source.Token;
            } else {
                cancel = _processExitedCancelSource.Token;
            }

            try {
                return (await _conn.SendRequestAsync(new AP.AvailableImportsRequest() {
                    name = name
                }, cancel)).imports.Select(x => new ExportedMemberInfo(x.fromName, x.importName));
            } finally {
                registration1.Dispose();
                registration2.Dispose();
            }
        }

        internal async Task<AP.ExtractMethodResponse> ExtractMethod(AnalysisEntry projectFile, ITextView view, string name, string[] parameters, int? targetScope = null) {
            ITextBuffer buffer = projectFile.BufferParser.Buffers.Last();
            var bufferId = projectFile.BufferParser.GetBufferId(buffer);

            await projectFile.BufferParser.EnsureCodeSynced(buffer);

            var res = await SendRequestAsync(new AP.ExtractMethodRequest() {
                fileId = projectFile.FileId,
                bufferId = bufferId,
                indentSize = view.Options.GetIndentSize(),
                convertTabsToSpaces = view.Options.IsConvertTabsToSpacesEnabled(),
                newLine = view.Options.GetNewLineCharacter(),
                startIndex = view.Selection.Start.Position,
                endIndex = view.Selection.End.Position,
                name = name,
                scope = targetScope,
                shouldExpandSelection = true

            });
            return res;
        }

        internal async Task<AP.AddImportResponse> AddImport(AnalysisEntry projectFile, string fromModule, string name, string newLine) {
            ITextBuffer buffer = projectFile.BufferParser.Buffers.Last();
            var bufferId = projectFile.BufferParser.GetBufferId(buffer);

            var res = await SendRequestAsync(
                new AP.AddImportRequest() {
                    fromModule = fromModule,
                    name = name,
                    fileId = projectFile.FileId,
                    bufferId = bufferId,
                    newLine = newLine
                }
            ).ConfigureAwait(false);

            return res;
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

        internal async Task<NavigationInfo> GetNavigations(ITextBuffer textBuffer) {
            AnalysisEntry entry;
            if (textBuffer.TryGetPythonProjectEntry(out entry)) {
                var navigations = await SendRequestAsync(
                    new AP.NavigationRequest() {
                        fileId = entry.FileId
                    }
                );

                List<NavigationInfo> bufferNavs = new List<NavigationInfo>();
                foreach (var buffer in navigations.buffers) {
                    LocationTracker translator = new LocationTracker(
                        entry,
                        buffer.bufferId,
                        buffer.version
                    );
                    bufferNavs.AddRange(ConvertNavigations(textBuffer.CurrentSnapshot, translator, buffer.navigations));
                }

                return new NavigationInfo(
                    null,
                    NavigationKind.None,
                    new SnapshotSpan(),
                    bufferNavs.ToArray()
                );
            }

            return new NavigationInfo(null, NavigationKind.None, new SnapshotSpan(), Array.Empty<NavigationInfo>());
        }

        private NavigationInfo[] ConvertNavigations(ITextSnapshot snapshot, LocationTracker translator, AP.Navigation[] navigations) {
            if (navigations == null) {
                return null;
            }

            List<NavigationInfo> res = new List<NavigationInfo>();
            foreach (var nav in navigations) {
                // translate the span from the version we last parsed to the current version
                var span = translator.TranslateForward(Span.FromBounds(nav.startIndex, nav.endIndex));

                res.Add(
                    new NavigationInfo(
                        nav.name,
                        GetNavigationKind(nav.type),
                        new SnapshotSpan(snapshot, span),
                        ConvertNavigations(snapshot, translator, nav.children)
                    )
                );
            }
            return res.ToArray();
        }

        internal async Task<ProjectReference[]> GetReferences() {
            var res = await SendRequestAsync(new AP.GetReferencesRequest()).ConfigureAwait(false);

            return res.references.Select(AP.ProjectReference.Convert).ToArray();
        }

        internal async Task<AP.AddReferenceResponse> AddReference(ProjectReference reference, CancellationToken token = default(CancellationToken)) {
            return await SendRequestAsync(new AP.AddReferenceRequest() {
                reference = AP.ProjectReference.Convert(reference)
            }).ConfigureAwait(false);
        }

        internal async Task<AP.RemoveReferenceResponse> RemoveReference(ProjectReference reference) {
            return await SendRequestAsync(
                new AP.RemoveReferenceRequest() {
                    reference = AP.ProjectReference.Convert(reference)
                }
            ).ConfigureAwait(false);
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
            AnalysisEntry entry;
            var res = Enumerable.Empty<OutliningTaggerProvider.TagSpan>();
            if (snapshot.TextBuffer.TryGetPythonProjectEntry(out entry)) {
                var outliningTags = await SendRequestAsync(
                    new AP.OutlingRegionsRequest() {
                        fileId = entry.FileId
                    }
                );

                foreach (var bufferTags in outliningTags.buffers) {
                    var translator = new LocationTracker(
                        entry,
                        bufferTags.bufferId,
                        bufferTags.version
                    );

                    res = res.Concat(ConvertOutliningTags(snapshot, translator, bufferTags));
                }
            }

            return res;
        }

        private static IEnumerable<OutliningTaggerProvider.TagSpan> ConvertOutliningTags(ITextSnapshot currentSnapshot, LocationTracker translator, AP.BufferOutliningTags outliningTags) {
            foreach (var tag in outliningTags.tags) {
                // translate the span from the version we last parsed to the current version
                var span = translator.TranslateForward(Span.FromBounds(tag.startIndex, tag.endIndex));

                int headerIndex = tag.headerIndex;
                if (tag.headerIndex != -1) {
                    headerIndex = translator.TranslateForward(headerIndex);
                }

                yield return OutliningTaggerProvider.OutliningTagger.GetTagSpan(
                    currentSnapshot,
                    span.Start,
                    span.End,
                    headerIndex
                );
            }
        }

        internal async Task<AP.OverridesCompletionResponse> GetOverrideCompletions(AnalysisEntry file, ITextBuffer buffer, SourceLocation location, string indentation) {
            var res = await SendRequestAsync(
                new AP.OverridesCompletionRequest() {
                    fileId = file.FileId,
                    bufferId = file.BufferParser.GetBufferId(buffer),
                    column = location.Column,
                    index = location.Index,
                    line = location.Line,
                    indentation = indentation
                }
            ).ConfigureAwait(false);

            return res;
        }

        internal static void ApplyChanges(AP.ChangeInfo[] changes, AnalysisEntry file, int bufferId, int fromVersion) {
            var buffer = file.BufferParser.GetBuffer(bufferId);
            var translator = new LocationTracker(file, bufferId, fromVersion);

            using (var edit = buffer.CreateEdit()) {
                foreach (var change in changes) {
                    edit.Replace(
                        translator.TranslateForward(new Span(change.start, change.length)),
                        change.newText
                    );
                }
                edit.Apply();
            }
        }

        internal static async Task<QuickInfo> GetQuickInfo(SnapshotPoint point) {
            var analysis = GetApplicableExpression(point);

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
                    (await analysis.File.Analyzer.SendRequestAsync(req).ConfigureAwait(false)).text,
                    analysis.Span
                );
            }

            return null;
        }

        internal AnalysisVariable ToAnalysisVariable(AP.AnalysisReference arg) {
            VariableType type = VariableType.None;
            switch (arg.kind) {
                case "definition": type = VariableType.Definition; break;
                case "reference": type = VariableType.Reference; break;
                case "value": type = VariableType.Value; break;
            }

            AnalysisEntry projectFile;
            _projectFiles.TryGetValue(arg.file, out projectFile);
            var location = new AnalysisLocation(
                projectFile,
                arg.line,
                arg.column
            );
            return new AnalysisVariable(type, location);
        }

        class ApplicableExpression {
            public readonly string Text;
            public readonly AnalysisEntry File;
            public readonly ITrackingSpan Span;
            public readonly SourceLocation Location;

            public ApplicableExpression(AnalysisEntry file, string text, ITrackingSpan span, SourceLocation location) {
                File = file;
                Text = text;
                Span = span;
                Location = location;
            }
        }

        internal static void ApplyChanges(AP.ChangeInfo[] changes, AnalysisEntry file, ITextBuffer buffer, int fromVersion) {
            ApplyChanges(changes, file, file.BufferParser.GetBufferId(buffer), fromVersion);
        }

        private static ApplicableExpression GetApplicableExpression(SnapshotPoint point) {
            var snapshot = point.Snapshot;
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

                AnalysisEntry entry;
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

    }
}
