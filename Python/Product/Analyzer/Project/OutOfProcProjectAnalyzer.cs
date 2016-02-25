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
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Communication;
using Microsoft.PythonTools.Cdp;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
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
    sealed class OutOfProcProjectAnalyzer : IDisposable {
        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_zipFileName] contains the full path to that archive.
        private static readonly object _zipFileName = new { Name = "ZipFileName" };

        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_pathInZipFile] contains the path of the item inside the archive.
        private static readonly object _pathInZipFile = new { Name = "PathInZipFile" };

        private readonly AnalysisQueue _analysisQueue;
        private readonly IPythonInterpreterFactory _interpreterFactory;
        //private readonly Dictionary<BufferParser, IProjectEntry> _openFiles = new Dictionary<BufferParser, IProjectEntry>();
        private readonly ProjectEntryMap _projectFiles;
        private readonly PythonAnalyzer _pyAnalyzer;
        private readonly AutoResetEvent _queueActivityEvent = new AutoResetEvent(false);
        private readonly IPythonInterpreterFactory[] _allFactories;
        private volatile Dictionary<string, TaskPriority> _commentPriorityMap = new Dictionary<string, TaskPriority>() {
            { "TODO", TaskPriority.normal },
            { "HACK", TaskPriority.high },
        };
        private OptionsChangedEvent _options;
        internal int _analysisPending;

        // Moniker strings allow the task provider to distinguish between
        // different sources of items for the same file.
        private const string ParserTaskMoniker = "Parser";
        internal const string UnresolvedImportMoniker = "UnresolvedImport";

#if PORT
        private readonly UnresolvedImportSquiggleProvider _unresolvedSquiggles;
#endif

        private readonly Connection _connection;
        internal Task ReloadTask;

        internal OutOfProcProjectAnalyzer(
            Stream writer, Stream reader,
            IPythonInterpreterFactory factory,
            IPythonInterpreterFactory[] allFactories)
            : this(writer, reader, factory.CreateInterpreter(), factory, allFactories) {
        }

        internal OutOfProcProjectAnalyzer(
            Stream writer, Stream reader,
            IPythonInterpreter interpreter,
            IPythonInterpreterFactory factory,
            IPythonInterpreterFactory[] allFactories
        ) {
#if PORT
            _errorProvider = (ErrorTaskProvider)serviceProvider.GetService(typeof(ErrorTaskProvider));
            _commentTaskProvider = (CommentTaskProvider)serviceProvider.GetService(typeof(CommentTaskProvider));
            _unresolvedSquiggles = new UnresolvedImportSquiggleProvider(serviceProvider, _errorProvider);
#endif

            _analysisQueue = new AnalysisQueue(this);
            _analysisQueue.AnalysisStarted += AnalysisQueue_AnalysisStarted;
            _allFactories = allFactories;
            _options = new OptionsChangedEvent() {
                implicitProject = false,
                indentation_inconsistency_severity = Severity.Ignore
            };
            _interpreterFactory = factory;

            if (interpreter != null) {
                _pyAnalyzer = PythonAnalyzer.Create(factory, interpreter);
                ReloadTask = _pyAnalyzer.ReloadModulesAsync()/*.HandleAllExceptions(_serviceProvider, GetType())*/;
                ReloadTask.ContinueWith(_ => ReloadTask = null);
                interpreter.ModuleNamesChanged += OnModulesChanged;
            }

            _projectFiles = new ProjectEntryMap();
            _connection = new Connection(writer, reader, RequestHandler, Requests.RegisteredTypes);
            _connection.EventReceived += ConectionReceivedEvent;
        }

        private void ConectionReceivedEvent(object sender, EventReceivedEventArgs e) {
            switch (e.Event.name) {
                case ModulesChangedEvent.Name: OnModulesChanged(this, EventArgs.Empty); break;
                case FileChangedEvent.Name: ApplyChanges((FileChangedEvent)e.Event); break;
                case FileContentEvent.Name: SetContent((FileContentEvent)e.Event); break;
                case OptionsChangedEvent.Name: SetOptions((OptionsChangedEvent)e.Event); break;
                case SetCommentTaskTokens.Name: _commentPriorityMap = ((SetCommentTaskTokens)e.Event).tokens; break;
            }
        }

        private void SetOptions(OptionsChangedEvent options) {
            _pyAnalyzer.Limits.CrossModule = options.crossModuleAnalysisLimit;
            _options = options;
        }

        private async Task<Response> RequestHandler(RequestArgs requestArgs) {
            await Task.FromResult((object)null);
            var command = requestArgs.Command;
            var request = requestArgs.Request;

            switch (command) {
                case UnloadFileRequest.Command: return UnloadFile((UnloadFileRequest)request);
                case AddFileRequest.Command: return AnalyzeFile((AddFileRequest)request);

                case TopLevelCompletionsRequest.Command: return GetTopLevelCompletions(request);
                case CompletionsRequest.Command: return GetCompletions(request);
                case GetModulesRequest.Command: return GetModules(request);
                case GetModuleMembers.Command: return GeModuleMembers(request);
                case SignaturesRequest.Command: return GetSignatures((SignaturesRequest)request);
                case QuickInfoRequest.Command: return GetQuickInfo((QuickInfoRequest)request);
                case AnalyzeExpressionRequest.Command: return AnalyzeExpression((AnalyzeExpressionRequest)request);
                //return _analyzer.Qneue((HasErrorsRequest)request);
                default:
                    throw new InvalidOperationException("Unknown command");
            }

        }

        private Response AnalyzeExpression(AnalyzeExpressionRequest request) {
            var pyEntry = _projectFiles[request.fileId] as IPythonProjectEntry;
            Reference[] references;
            string privatePrefix = null;
            string memberName = null;
            if (pyEntry.Analysis != null) {
                var variables = pyEntry.Analysis.GetVariables(
                    request.expr,
                    new SourceLocation(
                        request.index,
                        request.line,
                        request.column
                    )
                );

                var ast = variables.Ast;
                var expr = Statement.GetExpression(ast.Body);

                NameExpression ne = expr as NameExpression;
                MemberExpression me;
                if (ne != null) {
                    memberName = ne.Name;
                } else if ((me = expr as MemberExpression) != null) {
                    memberName = me.Name;
                }

                privatePrefix = variables.Ast.PrivatePrefix;
                references = variables.Select(MakeReference).ToArray();
            } else {
                references = new Reference[0];
            }

            return new AnalyzeExpressionResponse() {
                variables = references,
                privatePrefix = privatePrefix,
                memberName = memberName
            };
        }

        private Reference MakeReference(IAnalysisVariable arg) {
            return new Reference() {
                column = arg.Location.Column,
                line = arg.Location.Line,
                kind = GetVariableType(arg.Type),
                file = GetFile(arg.Location.ProjectEntry)
            };
        }

        private string GetFile(IProjectEntry projectEntry) {
            if (projectEntry != null) {
                return projectEntry.FilePath;
            }
            return null;
        }

        private static string GetVariableType(VariableType type) {
            switch (type) {
                case VariableType.Definition: return "definition";
                case VariableType.Reference: return "reference";
                case VariableType.Value: return "value";
            }
            return null;
        }

        private Response GetQuickInfo(QuickInfoRequest request) {
            var pyEntry = _projectFiles[request.fileId] as IPythonProjectEntry;
            string text = null;
            if (pyEntry.Analysis != null) {
                var values = pyEntry.Analysis.GetValues(
                    request.expr,
                    new SourceLocation(
                        request.index,
                        request.line,
                        request.column
                    )
                );

                bool first = true;
                var result = new StringBuilder();
                int count = 0;
                List<AnalysisValue> listVars = new List<AnalysisValue>(values);
                HashSet<string> descriptions = new HashSet<string>();
                bool multiline = false;
                foreach (var v in listVars) {
                    string description = null;
                    if (listVars.Count == 1) {
                        if (!String.IsNullOrWhiteSpace(v.Description)) {
                            description = v.Description;
                        }
                    } else {
                        if (!String.IsNullOrWhiteSpace(v.ShortDescription)) {
                            description = v.ShortDescription;
                        }
                    }

                    description = LimitLines(description);

                    if (description != null && descriptions.Add(description)) {
                        if (first) {
                            first = false;
                        } else {
                            if (result.Length == 0 || result[result.Length - 1] != '\n') {
                                result.Append(", ");
                            } else {
                                multiline = true;
                            }
                        }
                        result.Append(description);
                        count++;
                    }
                }

                string expr = request.expr;
                if (expr.Length > 4096) {
                    expr = expr.Substring(0, 4093) + "...";
                }
                if (multiline) {
                    result.Insert(0, expr + ": " + Environment.NewLine);
                } else if (result.Length > 0) {
                    result.Insert(0, expr + ": ");
                } else {
                    result.Append(expr);
                    result.Append(": ");
                    result.Append("<unknown type>");
                }

                text = result.ToString();
            }

            return new QuickInfoResponse() {
                text = text
            };
        }


        internal static string LimitLines(
            string str,
            int maxLines = 30,
            int charsPerLine = 200,
            bool ellipsisAtEnd = true,
            bool stopAtFirstBlankLine = false
        ) {
            if (string.IsNullOrEmpty(str)) {
                return str;
            }

            int lineCount = 0;
            var prettyPrinted = new StringBuilder();
            bool wasEmpty = true;

            using (var reader = new StringReader(str)) {
                for (var line = reader.ReadLine(); line != null && lineCount < maxLines; line = reader.ReadLine()) {
                    if (string.IsNullOrWhiteSpace(line)) {
                        if (wasEmpty) {
                            continue;
                        }
                        wasEmpty = true;
                        if (stopAtFirstBlankLine) {
                            lineCount = maxLines;
                            break;
                        }
                        lineCount += 1;
                        prettyPrinted.AppendLine();
                    } else {
                        wasEmpty = false;
                        lineCount += (line.Length / charsPerLine) + 1;
                        prettyPrinted.AppendLine(line);
                    }
                }
            }
            if (ellipsisAtEnd && lineCount >= maxLines) {
                prettyPrinted.AppendLine("...");
            }
            return prettyPrinted.ToString().Trim();
        }

        private Response GetSignatures(SignaturesRequest request) {
            var pyEntry = _projectFiles[request.fileId] as IPythonProjectEntry;
            IEnumerable<IOverloadResult> sigs;
            if (pyEntry.Analysis != null) {
                sigs = pyEntry.Analysis.GetSignaturesByIndex(
                    request.text,
                    request.location
                );
            } else {
                sigs = Enumerable.Empty<IOverloadResult>();
            }

            return new SignaturesResponse() {
                sigs = ToSignatures(sigs)
            };

        }

        private Response GetTopLevelCompletions(Request request) {
            var topLevelCompletions = (TopLevelCompletionsRequest)request;

            var pyEntry = _projectFiles[topLevelCompletions.fileId] as IPythonProjectEntry;
            IEnumerable<MemberResult> members;
            if (pyEntry.Analysis != null) {

                members = pyEntry.Analysis.GetAllAvailableMembers(
                    new SourceLocation(topLevelCompletions.location, 1, topLevelCompletions.column),
                    topLevelCompletions.options
                );
            } else {
                members = Enumerable.Empty<MemberResult>();
            }

            return new CompletionsResponse() {
                completions = ToCompletions(members.ToArray())
            };
        }

        private Response GeModuleMembers(Request request) {
            var getModuleMembers = (GetModuleMembers)request;

            return new CompletionsResponse() {
                completions = ToCompletions(_pyAnalyzer.GetModuleMembers(
                    null, // TODO: ModuleContext
                    getModuleMembers.package,
                    getModuleMembers.includeMembers
                ))
            };
        }

        private Response GetModules(Request request) {
            var getModules = (GetModulesRequest)request;

            return new CompletionsResponse() {
                completions = ToCompletions(_pyAnalyzer.GetModules(
                    getModules.topLevelOnly
                ))
            };
        }

        private Response GetCompletions(Request request) {
            var completions = (CompletionsRequest)request;

            var pyEntry = _projectFiles[completions.fileId] as IPythonProjectEntry;
            IEnumerable<MemberResult> members;
            if (pyEntry.Analysis != null) {

                members = pyEntry.Analysis.GetMembersByIndex(
                    completions.text,
                    completions.location,
                    completions.options
                );
            } else {
                members = Enumerable.Empty<MemberResult>();
            }

            return new CompletionsResponse() {
                completions = ToCompletions(members.ToArray())
            };
        }

        private Signature[] ToSignatures(IEnumerable<IOverloadResult> sigs) {
            return sigs.Select(
                sig => new Signature() {
                    name = sig.Name,
                    doc = sig.Documentation,
                    parameters = sig.Parameters.Select(
                        param => new Analysis.Communication.Parameter() {
                            name = param.Name,
                            defaultValue = param.DefaultValue,
                            optional = param.IsOptional,
                            doc = param.Documentation,
                            type = param.Type,
                            variables = param.Variables != null ? param.Variables.Select(MakeReference).ToArray() : null
                        }
                    ).ToArray()
                }
            ).ToArray();
        }

        private Completion[] ToCompletions(MemberResult[] memberResult) {
            Completion[] res = new Completion[memberResult.Length];
            for (int i = 0; i < memberResult.Length; i++) {
                res[i] = new Completion() {
                    name = memberResult[i].Name,
                    completion = memberResult[i].Completion,
                    memberType = memberResult[i].MemberType
                };
            }
            return res;
        }

        private Response AnalyzeFile(AddFileRequest request) {
            var entry = AnalyzeFile(request.path, request.addingFromDir);

            if (entry != null) {
                return new AddFileResponse() {
                    fileId = ProjectEntryMap.GetId(entry)
                };
            }

            throw new InvalidOperationException("Failed to add item");
        }

        private Response UnloadFile(UnloadFileRequest command) {
            var entry = _projectFiles[command.fileId];
            if (entry == null) {
                throw new InvalidOperationException("Unknown project entry");
            }

            UnloadFile(entry);
            return new Response();
        }

        public void SetContent(FileContentEvent request) {
            var entry = _projectFiles[request.fileId];
            if (entry != null) {
                entry.SetCurrentCode(new StringBuilder(request.content));

                EnqueWorker(() => ParseFile(entry, entry.FilePath, request.version));
            }
        }

        public void ApplyChanges(FileChangedEvent request) {
            var entry = _projectFiles[request.fileId];
            if (entry != null) {
                var curCode = entry.GetCurrentCode();
                if (curCode == null) {
                    entry.SetCurrentCode(curCode = new StringBuilder());
                }

                foreach (var change in request.changes) {
                    curCode.Remove(change.start, change.length);
                    curCode.Insert(change.start, change.newText);
                }

                EnqueWorker(() => ParseFile(entry, entry.FilePath, request.version));
            }
        }

        /// <summary>
        /// Gets a CompletionList providing a list of possible members the user can dot through.
        /// </summary>
        internal CompletionsResponse GetCompletions(CompletionsRequest request) {
            var file = _projectFiles[request.fileId];
            if (file == null) {
                throw new InvalidOperationException("Unknown project entry");
            }

            return //TrySpecialCompletions(serviceProvider, snapshot, span, point, options) ??
                GetNormalCompletions(file, request);
            //return 
            //         GetNormalCompletionContext(request);
        }

        internal Task ProcessMessages() {
            return _connection.ProcessMessages();
        }

        public OptionsChangedEvent Options {
            get {
                return _options;
            }
        }

        private void AnalysisQueue_AnalysisStarted(object sender, EventArgs e) {
            var evt = AnalysisStarted;
            if (evt != null) {
                evt(this, e);
            }
        }

#if PORT
        internal PythonToolsService PyService {
            get {
                return _pyService;
            }
        }
#endif

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

        private async void OnModulesChanged(object sender, EventArgs args) {
            Debug.Assert(_pyAnalyzer != null, "Should not have null _pyAnalyzer here");
            if (_pyAnalyzer == null) {
                return;
            }

            await _pyAnalyzer.ReloadModulesAsync();

            // re-analyze all of the modules when we get a new set of modules loaded...
            foreach (var nameAndEntry in _projectFiles) {
                EnqueueFile(nameAndEntry.Value, nameAndEntry.Key);
            }
        }

#if PORT
        /// <summary>
        /// Creates a new ProjectEntry for the collection of buffers.
        /// 
        /// _openFiles must be locked when calling this function.
        /// </summary>
        internal void ReAnalyzeTextBuffers(BufferParser bufferParser) {
            ITextBuffer[] buffers = bufferParser.Buffers;
            if (buffers.Length > 0) {
                _errorProvider.ClearErrorSource(bufferParser._currentProjEntry, ParserTaskMoniker);
                _errorProvider.ClearErrorSource(bufferParser._currentProjEntry, UnresolvedImportMoniker);
                _commentTaskProvider.ClearErrorSource(bufferParser._currentProjEntry, ParserTaskMoniker);
                _unresolvedSquiggles.StopListening(bufferParser._currentProjEntry as IPythonProjectEntry);

                var projEntry = CreateProjectEntry(buffers[0], new SnapshotCookie(buffers[0].CurrentSnapshot));

                bool doSquiggles = !buffers[0].Properties.ContainsProperty(typeof(IInteractiveEvaluator));
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
                            client.UpdateProjectEntry(projEntry);
                        }
                    }
                }

            bufferParser.Requeue();
            }
    }
#endif

#if PORT
        public void ConnectErrorList(IProjectEntry projEntry, ITextBuffer buffer) {
            _errorProvider.AddBufferForErrorSource(projEntry, ParserTaskMoniker, buffer);
            _commentTaskProvider.AddBufferForErrorSource(projEntry, ParserTaskMoniker, buffer);
        }

        public void DisconnectErrorList(IProjectEntry projEntry, ITextBuffer buffer) {
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
#endif
#if PORT
        /// <summary>
        /// Starts monitoring a buffer for changes so we will re-parse the buffer to update the analysis
        /// as the text changes.
        /// </summary>
        internal MonitoredBufferResult MonitorTextBuffer(ITextView textView, ITextBuffer buffer) {
            IProjectEntry projEntry = CreateProjectEntry(buffer, new SnapshotCookie(buffer.CurrentSnapshot));

            if (!buffer.Properties.ContainsProperty(typeof(IInteractiveEvaluator))) {
                ConnectErrorList(projEntry, buffer);
                _errorProvider.AddBufferForErrorSource(projEntry, UnresolvedImportMoniker, buffer);
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
                if (buffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                    string modName;
                    try {
                        modName = ModulePath.FromFullPath(path).ModuleName;
                    } catch (ArgumentException) {
                        modName = null;
                    }

                    IPythonProjectEntry[] reanalyzeEntries = null;
                    if (!string.IsNullOrEmpty(modName)) {
                        reanalyzeEntries = Project.GetEntriesThatImportModule(modName, true).ToArray();
                    }

                    entry = _pyAnalyzer.AddModule(
                        modName,
                        buffer.GetFilePath(),
                        analysisCookie
                    );

                    if (reanalyzeEntries != null) {
                        foreach (var entryRef in reanalyzeEntries) {
                            _analysisQueue.Enqueue(entryRef, AnalysisPriority.Low);
                        }
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
#endif
        private void QueueDirectoryAnalysis(string path) {
            ThreadPool.QueueUserWorkItem(x => {
                AnalyzeDirectory(PathUtils.NormalizeDirectoryPath(Path.GetDirectoryName(path)));
            });
        }

        private bool ShouldAnalyzePath(string path) {
            foreach (var fact in _allFactories) {
                if (PathUtils.IsValidPath(fact.Configuration.InterpreterPath) &&
                    PathUtils.IsSubpathOf(Path.GetDirectoryName(fact.Configuration.InterpreterPath), path)) {
                    return false;
                }
            }
            return true;
        }

        internal IProjectEntry AnalyzeFile(string path, string addingFromDirectory = null) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code, so don't create an entry.
                return null;
            }

            IProjectEntry item;
            if (!_projectFiles.TryGetValue(path, out item)) {
                if (ModulePath.IsPythonSourceFile(path)) {
                    string modName;
                    try {
                        modName = ModulePath.FromFullPath(path, addingFromDirectory).ModuleName;
                    } catch (ArgumentException) {
                        // File is not a valid module, but we can still add an
                        // entry for it.
                        modName = null;
                    }

                    IPythonProjectEntry[] reanalyzeEntries = null;
                    if (!string.IsNullOrEmpty(modName)) {
                        reanalyzeEntries = Project.GetEntriesThatImportModule(modName, true).ToArray();
                    }

                    var pyEntry = _pyAnalyzer.AddModule(
                        modName,
                        path,
                        null
                    );

                    pyEntry.BeginParsingTree();

                    if (reanalyzeEntries != null) {
                        foreach (var entryRef in reanalyzeEntries) {
                            _analysisQueue.Enqueue(entryRef, AnalysisPriority.Low);
                        }
                    }

                    item = pyEntry;
                } else if (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) {
                    item = _pyAnalyzer.AddXamlFile(path, null);
                }

                if (item != null) {
                    _projectFiles.Add(path, item);
                    EnqueueFile(item, path);
                }
            } else if (addingFromDirectory != null) {
                var module = item as IPythonProjectEntry;
                if (module != null && ModulePath.IsPythonSourceFile(path)) {
                    string modName = null;
                    try {
                        modName = ModulePath.FromFullPath(path, addingFromDirectory).ModuleName;
                    } catch (ArgumentException) {
                        // Module does not have a valid name, so we can't make
                        // an alias for it.
                    }

                    if (modName != null && module.ModuleName != modName) {
                        _pyAnalyzer.AddModuleAlias(module.ModuleName, modName);

                        var reanalyzeEntries = Project.GetEntriesThatImportModule(modName, true).ToArray();
                        foreach (var entryRef in reanalyzeEntries) {
                            _analysisQueue.Enqueue(entryRef, AnalysisPriority.Low);
                        }
                    }
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
#if PORT
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

            IPythonProjectEntry entry;
            if (buffer.TryGetPythonProjectEntry(out entry) && entry.Analysis != null && text.Length > 0) {
                var lineNo = parser.Snapshot.GetLineNumberFromPosition(loc.Start);
                return new ExpressionAnalysis(
                    snapshot.TextBuffer.GetAnalyzer(serviceProvider),
                    text,
                    entry.Analysis,
                    loc.Start,
                    applicableSpan,
                    parser.Snapshot
                );
            }

            return ExpressionAnalysis.Empty;
        }
#endif
#if PORT
        /// <summary>
        /// Gets a list of signatuers available for the expression at the provided location in the snapshot.
        /// </summary>
        internal static SignatureAnalysis GetSignatures(IServiceProvider serviceProvider, ITextSnapshot snapshot, ITrackingSpan span) {
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

            if (snapshot.TextBuffer.GetAnalyzer(serviceProvider).ShouldEvaluateForCompletion(text)) {
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
                    var location = TranslateIndex(loc.Start, snapshot, analysis);

                    IEnumerable<IOverloadResult> sigs;
                    lock (snapshot.TextBuffer.GetAnalyzer(serviceProvider)) {
                        sigs = analysis.GetSignatures(text, location);
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

        internal static SourceLocation TranslateIndex(int index, ITextSnapshot fromSnapshot, ModuleAnalysis toAnalysisSnapshot) {
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

            // if we have type information don't offer to add imports
            return MissingImportAnalysis.Empty;
        }
#endif
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
                return IsParsing || _analysisQueue.IsAnalyzing;
            }
        }

        internal event EventHandler AnalysisStarted;

        internal void WaitForCompleteAnalysis(Func<int, bool> itemsLeftUpdated) {
            if (IsAnalyzing) {
                while (IsAnalyzing) {
                    QueueActivityEvent.WaitOne(100);

                    int itemsLeft = ParsePending + _analysisQueue.AnalysisPending;

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
                return _options.implicitProject;
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

        internal void ParseFile(IProjectEntry entry, string filename, int version, Stream content = null) {
            IPythonProjectEntry pyEntry;
            IExternalProjectEntry externalEntry;

            TextReader reader = null;
            var snapshot = entry.GetCurrentCode();
            string zipFileName = GetZipFileName(entry);
            string pathInZipFile = GetPathInZipFile(entry);
            IAnalysisCookie cookie;
            if (snapshot != null) {
                cookie = new FileCookie(filename);
#if FALSE
            cookie = new SnapshotCookie(snapshot);
#endif
                reader = new StringReader(snapshot.ToString());
            } else if (zipFileName != null) {
                cookie = new ZipFileCookie(zipFileName, pathInZipFile);
            } else {
                cookie = new FileCookie(filename);
            }

            if ((pyEntry = entry as IPythonProjectEntry) != null) {
                ParseResult result;
                if (reader != null) {
                    result = ParsePythonCode(reader);
                } else {
                    result = ParsePythonCode(content);
                }

                FinishParse(pyEntry, cookie, result, version);
            } else if ((externalEntry = entry as IExternalProjectEntry) != null) {
                externalEntry.ParseContent(reader ?? new StreamReader(content), cookie);
                _analysisQueue.Enqueue(entry, AnalysisPriority.Normal);
            }
        }

        private void FinishParse(IPythonProjectEntry pyEntry, IAnalysisCookie cookie, ParseResult result, int version) {
            if (result.Ast != null) {
                pyEntry.UpdateTree(result.Ast, cookie);
            } else {
                // notify that we failed to update the existing analysis
                pyEntry.UpdateTree(null, null);
            }

            // update squiggles for the buffer. snapshot may be null if we
            // are analyzing a file that is not open
            UpdateErrorsAndWarnings(pyEntry, result.Errors, result.Tasks, version);

            // enqueue analysis of the file
            if (result.Ast != null) {
                _analysisQueue.Enqueue(pyEntry, AnalysisPriority.Normal);
            }
        }

#if PORT
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
                    List<TaskProviderItem> commentTasks;
                    var reader = new SnapshotSpanSourceCodeReader(new SnapshotSpan(snapshot, new Span(0, snapshot.Length)));
                    ParsePythonCode(snapshot, reader, indentationSeverity, out ast, out errorSink, out commentTasks);

                    if (ast != null) {
                        asts.Add(ast);
                    }

                    // update squiggles for the buffer
                    UpdateErrorsAndWarnings(entry, snapshot, errorSink, commentTasks);
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
#endif

        class ParseResult {
            public readonly PythonAst Ast;
            public readonly CollectingErrorSink Errors;
            public readonly List<TaskItem> Tasks;

            public ParseResult(PythonAst ast, CollectingErrorSink errors, List<TaskItem> tasks) {
                Ast = ast;
                Errors = errors;
                Tasks = tasks;
            }
        }

        private async void UpdateErrorsAndWarnings(IProjectEntry entry, CollectingErrorSink errorSink, List<TaskItem> commentTasks, int version) {
            // Update the warn-on-launch state for this entry
            await _connection.SendEventAsync(
                new FileParsedEvent() {
                    fileId = ProjectEntryMap.GetId(entry),
                    hasErrors = errorSink.Errors.Any(),
                    tasks = commentTasks.ToArray(),
                    errors = errorSink.Errors.Select(MakeError).ToArray(),
                    warnings = errorSink.Warnings.Select(MakeError).ToArray(),
                    version = version
                }
            );
        }

        private static Error MakeError(ErrorResult error) {
            return new Error() {
                message = error.Message,
                startLine = error.Span.Start.Line,
                startColumn = error.Span.Start.Column,
                startIndex = error.Span.Start.Index,
                endLine = error.Span.End.Line,
                endColumn = error.Span.End.Column,
                length = error.Span.Length
            };
        }

        private ParseResult ParsePythonCode(Stream content) {
            var errorSink = new CollectingErrorSink();
            var tasks = new List<TaskItem>();

            ParserOptions options = MakeParserOptions(errorSink, tasks);

            using (var parser = Parser.CreateParser(content, Project.LanguageVersion, options)) {
                return new ParseResult(
                    ParseOneFile(parser),
                    errorSink,
                    tasks
                );
            }
        }

        private ParseResult ParsePythonCode(TextReader content) {
            var errorSink = new CollectingErrorSink();
            var tasks = new List<TaskItem>();

            ParserOptions options = MakeParserOptions(errorSink, tasks);

            using (var parser = Parser.CreateParser(content, Project.LanguageVersion, options)) {
                return new ParseResult(
                    ParseOneFile(parser),
                    errorSink,
                    tasks
                );
            }
        }


        private ParserOptions MakeParserOptions(CollectingErrorSink errorSink, List<TaskItem> tasks) {
            var options = new ParserOptions {
                ErrorSink = errorSink,
                IndentationInconsistencySeverity = _options.indentation_inconsistency_severity,
                BindReferences = true
            };
            options.ProcessComment += (sender, e) => ProcessComment(tasks, e.Span, e.Text);
            return options;
        }

        private static PythonAst ParseOneFile(Parser parser) {
            if (parser != null) {
                try {
                    return parser.ParseFile();
                } catch (BadSourceException) {
                } catch (Exception e) {
                    if (e.IsCriticalException()) {
                        throw;
                    }
                    Debug.Assert(false, String.Format("Failure in Python parser: {0}", e.ToString()));
                }

            }
            return null;
        }

        // Tokenizer callback. Extracts comment tasks (like "TODO" or "HACK") from comments.
        private void ProcessComment(List<TaskItem> commentTasks, SourceSpan span, string text) {
            if (text.Length > 0) {
                var tokens = _commentPriorityMap;
                if (tokens != null) {
                    foreach (var kv in tokens) {
                        if (text.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0) {
                            commentTasks.Add(
                                new TaskItem() {
                                    message = text.Substring(1).Trim(),
                                    startLine = span.Start.Line,
                                    startColumn = span.Start.Column,
                                    startIndex = span.Start.Index,
                                    endLine = span.End.Line,
                                    endColumn = span.End.Column,
                                    length = span.Length,
                                    priority = kv.Value,
                                    category = TaskCategory.comments,
                                    squiggle = false
                                }
                            );
                        }
                    }
                }
            }
        }

        #region Implementation Details

        private static Stopwatch _stopwatch = MakeStopWatch();

        internal static Stopwatch Stopwatch {
            get {
                return _stopwatch;
            }
        }
#if PORT
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

        private static CompletionAnalysis TrySpecialCompletions(CompletionsRequest request) {
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
#endif
        private CompletionsResponse GetNormalCompletions(IProjectEntry projectEntry, CompletionsRequest request /*IServiceProvider serviceProvider, ITextSnapshot snapshot, ITrackingSpan applicableSpan, ITrackingPoint point, CompletionOptions options*/) {
            var code = projectEntry.GetCurrentCode();

            if (IsSpaceCompletion(code, request.location) && !request.forceCompletions) {
                return new CompletionsResponse() {
                    completions = new Completion[0]
                };
            }

            var analysis = ((IPythonProjectEntry)projectEntry).Analysis;
            var members = analysis.GetMembers(
                request.text,
                new SourceLocation(
                    request.location,
                    1,
                    request.column
                ),
                request.options
            );

            return new CompletionsResponse() {
                completions = ToCompletions(members.ToArray())
            };
        }

        private bool IsSpaceCompletion(StringBuilder text, int location) {
            if (location > 0 && location < text.Length - 1) {
                return text[location - 1] == ' ';
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
            private readonly OutOfProcProjectAnalyzer _analyzer;

            public AddDirectoryAnalysis(string dir, Action<IProjectEntry> onFileAnalyzed, OutOfProcProjectAnalyzer analyzer) {
                _dir = dir;
                _onFileAnalyzed = onFileAnalyzed;
                _analyzer = analyzer;
            }

            #region IAnalyzable Members

            public void Analyze(CancellationToken cancel) {
                if (cancel.IsCancellationRequested) {
                    return;
                }

                AnalyzeDirectoryWorker(_dir, true, _onFileAnalyzed, cancel);
            }

            #endregion

            private void AnalyzeDirectoryWorker(string dir, bool addDir, Action<IProjectEntry> onFileAnalyzed, CancellationToken cancel) {
                if (_analyzer._pyAnalyzer == null) {
                    // We aren't able to analyze code.
                    return;
                }

                if (string.IsNullOrEmpty(dir)) {
                    Debug.Assert(false, "Unexpected empty dir");
                    return;
                }

                if (addDir) {
                    _analyzer._pyAnalyzer.AddAnalysisDirectory(dir);
                }

                try {
                    var filenames = Directory.GetFiles(dir, "*.py").Concat(Directory.GetFiles(dir, "*.pyw"));
                    foreach (string filename in filenames) {
                        if (cancel.IsCancellationRequested) {
                            break;
                        }
                        IProjectEntry entry = _analyzer.AnalyzeFile(filename, _dir);
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
                        if (File.Exists(PathUtils.GetAbsoluteFilePath(innerDir, "__init__.py"))) {
                            AnalyzeDirectoryWorker(innerDir, false, onFileAnalyzed, cancel);
                        }
                    }
                } catch (IOException) {
                    // We want to handle DirectoryNotFound, DriveNotFound, PathTooLong
                } catch (UnauthorizedAccessException) {
                }
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
            private readonly OutOfProcProjectAnalyzer _analyzer;

            public AddZipArchiveAnalysis(string zipFileName, Action<IProjectEntry> onFileAnalyzed, OutOfProcProjectAnalyzer analyzer) {
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

            _pyAnalyzer.AddAnalysisDirectory(zipFileName);

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
                string path;
                try {
                    path = PathUtils.GetAbsoluteFilePath(zipFileName, pathInZip);
                } catch (ArgumentException) {
                    return null;
                }

                IProjectEntry item;
                if (_projectFiles.TryGetValue(path, out item)) {
                    return item;
                }

                if (ModulePath.IsPythonSourceFile(path)) {
                    // Use the entry path relative to the root of the archive to determine module name - this boundary
                    // should never be crossed, even if the parent directory of the zip is itself a package.
                    string modName;
                    try {
                        modName = ModulePath.FromFullPath(
                            pathInZip,
                            isPackage: dir => entry.Archive.GetEntry(
                                (PathUtils.EnsureEndSeparator(dir) + "__init__.py").Replace('\\', '/')
                            ) != null).ModuleName;
                    } catch (ArgumentException) {
                        return null;
                    }
                    item = _pyAnalyzer.AddModule(modName, path, null);
                }
                if (item == null) {
                    return null;
                }

                SetZipFileName(item, zipFileName);
                SetPathInZipFile(item, pathInZip);
                _projectFiles.Add(path, item);
                IPythonProjectEntry pyEntry = item as IPythonProjectEntry;
                if (pyEntry != null) {
                    pyEntry.BeginParsingTree();
                }

                EnqueueZipArchiveEntry(item, zipFileName, entry, onComplete);
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

            _pyAnalyzer.RemoveAnalysisDirectory(directory);
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

                _pyAnalyzer.RemoveModule(entry);
                _projectFiles.Remove(entry);

                if (reanalyzeEntries != null) {
                    foreach (var existing in reanalyzeEntries) {
                        _analysisQueue.Enqueue(existing, AnalysisPriority.Normal);
                    }
                }
            }
        }

        /// <summary>
        /// Parses the specified file on disk.
        /// </summary>
        /// <param name="filename"></param>
        public void EnqueueFile(IProjectEntry projEntry, string filename) {
            EnqueWorker(() => {
                for (int i = 0; i < 10; i++) {
                    try {
                        if (!File.Exists(filename)) {
                            break;
                        }
                        using (var reader = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                            ParseFile(projEntry, filename, 0, reader);
                            return;
                        }
                    } catch (IOException) {
                        // file being copied, try again...
                        Thread.Sleep(100);
                    } catch (UnauthorizedAccessException) {
                        // file is inaccessible, try again...
                        Thread.Sleep(100);
                    }
                }

                IPythonProjectEntry pyEntry = projEntry as IPythonProjectEntry;
                if (pyEntry != null) {
                    // failed to parse, keep the UpdateTree calls balanced
                    pyEntry.UpdateTree(null, null);
                }
            });
        }

        public void EnqueueZipArchiveEntry(IProjectEntry projEntry, string zipFileName, ZipArchiveEntry entry, Action onComplete) {
            var pathInArchive = entry.FullName.Replace('/', '\\');
            var fileName = Path.Combine(zipFileName, pathInArchive);
            EnqueWorker(() => {
                try {
                    using (var stream = entry.Open()) {
                        ParseFile(projEntry, fileName, 0, stream);
                        return;
                    }
                } catch (IOException ex) {
                    Debug.Fail(ex.Message);
                } catch (InvalidDataException ex) {
                    Debug.Fail(ex.Message);
                } finally {
                    onComplete();
                }

                IPythonProjectEntry pyEntry = projEntry as IPythonProjectEntry;
                if (pyEntry != null) {
                    // failed to parse, keep the UpdateTree calls balanced
                    pyEntry.UpdateTree(null, null);
                }
            });
        }

        private void EnqueWorker(Action parser) {
            Interlocked.Increment(ref _analysisPending);

            ThreadPool.QueueUserWorkItem(
                dummy => {
                    try {
                        parser();
                    } finally {
                        Interlocked.Decrement(ref _analysisPending);
                    }
                }
            );
        }

        public bool IsParsing {
            get {
                return _analysisPending > 0;
            }
        }

        public int ParsePending {
            get {
                return _analysisPending;
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
            foreach (var pathAndEntry in _projectFiles) {
#if PORT
                _errorProvider.Clear(pathAndEntry.Value, ParserTaskMoniker);
                _errorProvider.Clear(pathAndEntry.Value, UnresolvedImportMoniker);
                _commentTaskProvider.Clear(pathAndEntry.Value, ParserTaskMoniker);
#endif
            }

            _analysisQueue.AnalysisStarted -= AnalysisQueue_AnalysisStarted;
            _analysisQueue.Dispose();
            if (_pyAnalyzer != null) {
                _pyAnalyzer.Interpreter.ModuleNamesChanged -= OnModulesChanged;
                _pyAnalyzer.Dispose();
            }

            _queueActivityEvent.Dispose();
        }

        #endregion

        internal void RemoveReference(ProjectAssemblyReference reference) {
            var interp = Interpreter as IPythonInterpreterWithProjectReferences;
            if (interp != null) {
                interp.RemoveReference(reference);
            }
        }
    }
}
