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
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Library.Intellisense;
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

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Performs centralized parsing and analysis of Python source code.
    /// </summary>
    internal class ProjectAnalyzer {
        private readonly ParseQueue _queue;
        private readonly AnalysisQueue _analysisQueue;
        private readonly IPythonInterpreterFactory _interpreterFactory;
        private readonly Dictionary<BufferParser, IProjectEntry> _openFiles = new Dictionary<BufferParser, IProjectEntry>();
        private readonly IErrorProviderFactory _errorProvider;
        private readonly Dictionary<string, IProjectEntry> _projectFiles;
        private readonly PythonAnalyzer _pyAnalyzer;
        private readonly Dictionary<Type, Command> _commands = new Dictionary<Type, Command>();
        private readonly IVsErrorList _errorList;
        private readonly PythonProjectNode _project;
        private readonly AutoResetEvent _queueActivityEvent = new AutoResetEvent(false);
        private static TaskProvider _taskProvider;

        public ProjectAnalyzer(IPythonInterpreterFactory factory, IErrorProviderFactory errorProvider)
            : this(factory.CreateInterpreter(), factory, errorProvider) {
        }

        public ProjectAnalyzer(IPythonInterpreter interpreter, IPythonInterpreterFactory factory, IErrorProviderFactory errorProvider, PythonProjectNode project = null) {
            _errorProvider = errorProvider;

            _queue = new ParseQueue(this);
            _analysisQueue = new AnalysisQueue(this);

            _interpreterFactory = factory;
            _project = project;

            _pyAnalyzer = new PythonAnalyzer(interpreter, factory.GetLanguageVersion());
            _projectFiles = new Dictionary<string, IProjectEntry>(StringComparer.OrdinalIgnoreCase);

            if (PythonToolsPackage.Instance != null) {
                _errorList = (IVsErrorList)PythonToolsPackage.GetGlobalService(typeof(SVsErrorList));
                _pyAnalyzer.CrossModulAnalysisLimit = PythonToolsPackage.Instance.OptionsPage.CrossModuleAnalysisLimit;
            }
        }

        /// <summary>
        /// Creates a new ProjectEntry for the collection of buffers.
        /// 
        /// _openFiles must be locked when calling this function.
        /// </summary>
        private void ReAnalyzeTextBuffers(BufferParser bufferParser) {
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

        public void SwitchAnalyzers(ProjectAnalyzer oldAnalyzer) {
            lock (_openFiles) {
                foreach (var bufferParser in oldAnalyzer._openFiles.Keys) {
                    ReAnalyzeTextBuffers(bufferParser);
                }
            }
        }

        /// <summary>
        /// Starts monitoring a buffer for changes so we will re-parse the buffer to update the analysis
        /// as the text changes.
        /// </summary>
        public MonitoredBufferResult MonitorTextBuffer(ITextView textView, ITextBuffer buffer) {
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

                if (ImplicitProject &&
                    !Path.GetFullPath(path).StartsWith(Path.GetDirectoryName(_interpreterFactory.Configuration.InterpreterPath), StringComparison.OrdinalIgnoreCase)) { // don't analyze std lib
                    // TODO: We're doing this on the UI thread and when we end up w/ a lot to queue here we hang for a while...
                    // But this adds files to the analyzer so it's not as simple as queueing this onto another thread.
                    AnalyzeDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                }
            }

            return entry;
        }

        public void StopMonitoringTextBuffer(BufferParser bufferParser) {
            bufferParser.StopMonitoring();
            lock (_openFiles) {
                _openFiles.Remove(bufferParser);
            }
        }

        public IProjectEntry AnalyzeFile(string path) {
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

        public IEnumerable<KeyValuePair<string, IProjectEntry>> LoadedFiles {
            get {
                return _projectFiles;
            }
        }

        public IProjectEntry GetAnalysisFromFile(string path) {
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
        public static ExpressionAnalysis AnalyzeExpression(ITextSnapshot snapshot, ITrackingSpan span, bool forCompletion = true) {
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
                        text,
                        analysis,
                        lineNo + 1,
                        applicableSpan);
                }
            }

            return ExpressionAnalysis.Empty;
        }

        /// <summary>
        /// Gets a CompletionList providing a list of possible members the user can dot through.
        /// </summary>
        public static CompletionAnalysis GetCompletions(ITextSnapshot snapshot, ITrackingSpan span, bool intersectMembers = true, bool hideAdvancedMembers = false) {
            var buffer = snapshot.TextBuffer;
            ReverseExpressionParser parser = new ReverseExpressionParser(snapshot, buffer, span);

            var loc = span.GetSpan(snapshot.Version);
            var line = snapshot.GetLineFromPosition(loc.Start);
            var lineStart = line.Start;

            var textLen = loc.End - lineStart.Position;
            if (textLen <= 0) {
                // Ctrl-Space on an empty line, we just want to get global vars
                return new NormalCompletionAnalysis(String.Empty, loc.Start, parser.Snapshot, parser.Span, parser.Buffer, 0);
            }

            return TrySpecialCompletions(snapshot, span) ??
                   GetNormalCompletionContext(parser, loc, intersectMembers, hideAdvancedMembers);
        }

        /// <summary>
        /// Gets a list of signatuers available for the expression at the provided location in the snapshot.
        /// </summary>
        public static SignatureAnalysis GetSignatures(ITextSnapshot snapshot, ITrackingSpan span) {
            var buffer = snapshot.TextBuffer;
            ReverseExpressionParser parser = new ReverseExpressionParser(snapshot, buffer, span);

            var loc = parser.Span.GetSpan(parser.Snapshot.Version);

            int paramIndex;
            SnapshotPoint? sigStart;
            var exprRange = parser.GetExpressionRange(1, out paramIndex, out sigStart);
            if (exprRange == null || sigStart == null) {
                return new SignatureAnalysis("", 0, new ISignature[0]);
            }

            Debug.Assert(sigStart != null);
            var text = new SnapshotSpan(exprRange.Value.Snapshot, new Span(exprRange.Value.Start, sigStart.Value.Position - exprRange.Value.Start)).GetText();
            //var text = exprRange.Value.GetText();
            var applicableSpan = parser.Snapshot.CreateTrackingSpan(exprRange.Value.Span, SpanTrackingMode.EdgeInclusive);

            if (snapshot.TextBuffer.GetAnalyzer().ShouldEvaluateForCompletion(text)) {
                var liveSigs = TryGetLiveSignatures(snapshot, paramIndex, text, applicableSpan);
                if (liveSigs != null) {
                    return liveSigs;
                }
            }

            var start = Stopwatch.ElapsedMilliseconds;

            var analysisItem = buffer.GetAnalysis();
            if (analysisItem != null) {
                var analysis = ((IPythonProjectEntry)analysisItem).Analysis;
                if (analysis != null) {

                    var lineNo = parser.Snapshot.GetLineNumberFromPosition(loc.Start);

                    var sigs = analysis.GetSignatures(text, lineNo + 1);
                    var end = Stopwatch.ElapsedMilliseconds;

                    if (/*Logging &&*/ (end - start) > CompletionAnalysis.TooMuchTime) {
                        Trace.WriteLine(String.Format("{0} lookup time {1} for signatures", text, end - start));
                    }

                    var result = new List<ISignature>();
                    foreach (var sig in sigs) {
                        result.Add(new PythonSignature(applicableSpan, sig, paramIndex));
                    }

                    return new SignatureAnalysis(
                        text,
                        paramIndex,
                        result
                    );
                }
            }
            return new SignatureAnalysis(text, paramIndex, new ISignature[0]);
        }

        public bool IsAnalyzing {
            get {
                return _queue.IsParsing || _analysisQueue.IsAnalyzing;
            }
        }

        public void WaitForCompleteAnalysis(Func<int, bool> itemsLeftUpdated) {
            if (_queue.IsParsing || _analysisQueue.IsAnalyzing) {
                while (_queue.IsParsing || _analysisQueue.IsAnalyzing) {
                    _queueActivityEvent.WaitOne(1000);

                    int itemsLeft = _queue.ParsePending + _analysisQueue.AnalysisPending;

                    if (!itemsLeftUpdated(itemsLeft)) {
                        break;
                    }
                }
            } else {
                itemsLeftUpdated(0);
            }
        }

        public AutoResetEvent QueueActivityEvent {
            get {
                return _queueActivityEvent;
            }
        }

        public bool ImplicitProject {
            get {
                return _project == null;
            }
        }

        public IPythonInterpreterFactory InterpreterFactory {
            get {
                return _interpreterFactory;
            }
        }

        public IPythonInterpreter Interpreter {
            get {
                return _pyAnalyzer.Interpreter;
            }
        }

        public PythonAnalyzer Project {
            get {
                return _pyAnalyzer;
            }
        }

        public void ParseFile(IProjectEntry projectEntry, string filename, FileStream content, Severity indentationSeverity) {
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

        public void ParseBuffers(BufferParser bufferParser, Severity indentationSeverity, params ITextSnapshot[] snapshots) {
            IProjectEntry analysis = bufferParser._currentProjEntry;

            IPythonProjectEntry pyProjEntry = analysis as IPythonProjectEntry;
            List<PythonAst> asts = new List<PythonAst>();
            bool hasErrors = false;
            foreach (var snapshot in snapshots) {
                var snapshotContent = new SnapshotSpanSourceCodeReader(new SnapshotSpan(snapshot, new Span(0, snapshot.Length)));

                if (pyProjEntry != null && snapshot.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                    if (!snapshot.IsReplBufferWithCommand()) {
                        PythonAst ast;
                        CollectingErrorSink errorSink;

                        ParsePythonCode(snapshotContent, indentationSeverity, out ast, out errorSink);
                        if (ast != null) {
                            asts.Add(ast);

                            if (errorSink.Errors.Count != 0) {
                                hasErrors = true;
                            }

                            // update squiggles for the buffer
                            var buffer = snapshot.TextBuffer;

                            SimpleTagger<ErrorTag> squiggles = _errorProvider.GetErrorTagger(snapshot.TextBuffer);
                            TaskProvider provider = GetTaskProviderAndClearProjectItems(bufferParser._currentProjEntry);

                            // SimpleTagger says it's thread safe (http://msdn.microsoft.com/en-us/library/dd885186.aspx), but it's buggy...  
                            // Post the removing of squiggles to the UI thread so that we don't crash when we're racing with 
                            // updates to the buffer.  http://pytools.codeplex.com/workitem/142
                            var uiTextView = bufferParser.TextView as UIElement;
                            if (uiTextView != null) {   // not a UI element in completion context tests w/ mocks.
                                uiTextView.Dispatcher.BeginInvoke((Action)new SquiggleUpdater(errorSink, snapshot, squiggles, bufferParser._currentProjEntry.FilePath, provider).DoUpdate);
                            }

                            string path = bufferParser._currentProjEntry.FilePath;
                            if (path != null) {
                                UpdateErrorList(errorSink, path, provider);
                            }
                        }
                    }
                } else {
                    // other file such as XAML
                    IExternalProjectEntry externalEntry;
                    if ((externalEntry = (analysis as IExternalProjectEntry)) != null) {
                        externalEntry.ParseContent(snapshotContent, new SnapshotCookie(snapshotContent.Snapshot));
                        _analysisQueue.Enqueue(analysis, AnalysisPriority.High);
                    }
                }
            }

            if (pyProjEntry != null) {
                if ((!hasErrors && asts.Count > 0) || asts.Count > 1) {
                    // only update the AST when we're error free, this way we don't remove
                    // a useful analysis with an incomplete and useless analysis.
                    // If we have more than one AST we're in the REPL - we'll update the
                    // AST in that case as errors won't go away.

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

                    pyProjEntry.UpdateTree(finalAst, new SnapshotCookie(snapshots[0])); // SnapshotCookie is ot entirely right, we should merge the snapshots
                    _analysisQueue.Enqueue(analysis, AnalysisPriority.High);
                } else {
                    // indicate that we are done parsing.
                    pyProjEntry.UpdateTree(null, null);
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
                    AddWarnings(_snapshot, _errorSink, _squiggles, _filename, _provider);

                    AddErrors(_snapshot, _errorSink, _squiggles, _filename, _provider);
                }
            }
        }

        private static void AddErrors(ITextSnapshot snapshot, CollectingErrorSink errorSink, SimpleTagger<ErrorTag> squiggles, string filename, TaskProvider provider) {
            foreach (ErrorResult error in errorSink.Errors) {
                var span = error.Span;
                var tspan = CreateSpan(snapshot, span);
                squiggles.CreateTagSpan(tspan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, error.Message));
                if (provider != null) {
                    provider.AddErrors(filename, new[] { error });
                }
            }
        }

        private static void AddWarnings(ITextSnapshot snapshot, CollectingErrorSink errorSink, SimpleTagger<ErrorTag> squiggles, string filename, TaskProvider provider) {
            foreach (ErrorResult warning in errorSink.Warnings) {
                var span = warning.Span;
                var tspan = CreateSpan(snapshot, span);
                squiggles.CreateTagSpan(tspan, new ErrorTag(PredefinedErrorTypeNames.Warning, warning.Message));
                if (provider != null) {
                    provider.AddWarnings(filename, new[] { warning });
                }
            }
        }

        private TaskProvider GetTaskProviderAndClearProjectItems(IProjectEntry projEntry) {
            if (_taskProvider == null) {
                _taskProvider = new TaskProvider();

                uint cookie;
                ErrorHandler.ThrowOnFailure(((IVsTaskList)_errorList).RegisterTaskProvider(_taskProvider, out cookie));
                _taskProvider.Cookie = cookie;
            }

            if (projEntry.FilePath != null) {
                _taskProvider.Clear(projEntry.FilePath);
            }
            return _taskProvider;
        }

        private void UpdateErrorList(CollectingErrorSink errorSink, string filepath, TaskProvider provider) {
            if (_project != null && provider != null) {
                if (errorSink.Errors.Count > 0) {
                    _project.ErrorFiles.Add(filepath);
                } else {
                    _project.ErrorFiles.Remove(filepath);
                }
            }

            if (provider != null) {
                ((IVsTaskList)_errorList).RefreshTasks(provider.Cookie);
            }
        }

        private void ParsePythonCode(FileStream content, Severity indentationSeverity, out PythonAst ast, out CollectingErrorSink errorSink) {
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

        private static SignatureAnalysis TryGetLiveSignatures(ITextSnapshot snapshot, int paramIndex, string text, ITrackingSpan applicableSpan) {
            IReplEvaluator eval;
            PythonReplEvaluator dlrEval;
            if (snapshot.TextBuffer.Properties.TryGetProperty<IReplEvaluator>(typeof(IReplEvaluator), out eval) &&
                (dlrEval = eval as PythonReplEvaluator) != null) {
                if (text.EndsWith("(")) {
                    text = text.Substring(0, text.Length - 1);
                }
                var liveSigs = dlrEval.GetSignatureDocumentation(snapshot.TextBuffer.GetAnalyzer(), text);

                if (liveSigs != null && liveSigs.Length > 0) {
                    return new SignatureAnalysis(text, paramIndex, GetLiveSignatures(text, liveSigs, paramIndex, applicableSpan));
                }
            }
            return null;
        }

        private static ISignature[] GetLiveSignatures(string text, ICollection<OverloadDoc> liveSigs, int paramIndex, ITrackingSpan span) {
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
                    paramIndex
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

        private static CompletionAnalysis TrySpecialCompletions(ITextSnapshot snapshot, ITrackingSpan span) {
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
                    return new StringLiteralCompletionList(lastClass.Span.GetText(), snapSpan.Start, span, buffer);
                } else if (lastClass.ClassificationType == classifier.Provider.Operator &&
                    lastClass.Span.GetText() == "@") {

                    return new DecoratorCompletionAnalysis(lastClass.Span.GetText(), snapSpan.Start, span, buffer);
                }

                // Import completions
                var first = tokens[0];
                if (CompletionAnalysis.IsKeyword(first, "import")) {
                    return ImportCompletionAnalysis.Make(first, lastClass, snapSpan, snapshot, span, buffer, IsSpaceCompletion(snapshot, snapSpan));
                } else if (CompletionAnalysis.IsKeyword(first, "from")) {
                    return FromImportCompletionAnalysis.Make(tokens, first, snapSpan, snapshot, span, buffer, IsSpaceCompletion(snapshot, snapSpan));
                }
                return null;
            }

            return CompletionAnalysis.EmptyCompletionContext;
        }

        private static CompletionAnalysis GetNormalCompletionContext(ReverseExpressionParser parser, Span loc, bool intersectMembers = true, bool hideAdvancedMembers = false) {
            var exprRange = parser.GetExpressionRange();
            if (exprRange == null) {
                return CompletionAnalysis.EmptyCompletionContext;
            }
            if (IsSpaceCompletion(parser.Snapshot, loc)) {
                return CompletionAnalysis.EmptyCompletionContext;
            }

            var text = exprRange.Value.GetText();

            var applicableSpan = parser.Snapshot.CreateTrackingSpan(
                exprRange.Value.Span,
                SpanTrackingMode.EdgeExclusive
            );

            return new NormalCompletionAnalysis(
                text,
                loc.Start,
                parser.Snapshot,
                applicableSpan,
                parser.Buffer,
                -1,
                intersectMembers,
                hideAdvancedMembers
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
            foreach (string filename in Directory.GetFiles(dir, "*.py")) {
                AnalyzeFile(filename);
            }

            foreach (string filename in Directory.GetFiles(dir, "*.pyw")) {
                AnalyzeFile(filename);
            }

            foreach (string innerDir in Directory.GetDirectories(dir)) {
                if (File.Exists(Path.Combine(innerDir, "__init__.py"))) {
                    AnalyzeDirectory(innerDir);
                }
            }
        }

        internal void UnloadFile(IProjectEntry entry) {
            if (entry.FilePath != null) {
                _taskProvider.Clear(entry.FilePath);
                _pyAnalyzer.RemoveModule(entry);
            }
        }

        #endregion

        internal T GetCommand<T>() where T : Command {
            return (T)_commands[typeof(T)];
        }

        public IEnumerable<Command> Commands {
            get {
                return _commands.Values;
            }
        }

        class TaskProvider : IVsTaskProvider {
            private readonly string _path;
            private readonly Dictionary<string, List<ErrorResult>> _warnings = new Dictionary<string,List<ErrorResult>>();
            private readonly Dictionary<string, List<ErrorResult>> _errors = new Dictionary<string,List<ErrorResult>>();
            private uint _cookie;


            public TaskProvider() {
            }

            public uint Cookie {
                get {
                    return _cookie;
                }
                set {
                    _cookie = value;
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
                Dictionary<string, ErrorResult[]> errors = new Dictionary<string, ErrorResult[]>();
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

            internal void AddErrors(string filename, IList<ErrorResult> errors) {
                if (errors.Count > 0) {
                    lock (this) {
                        List<ErrorResult> errorList;
                        if (!_errors.TryGetValue(filename, out errorList)) {
                            _errors[filename] = errorList = new List<ErrorResult>();
                        }
                        errorList.AddRange(errors);
                    }
                }
            }

            internal void AddWarnings(string filename, IList<ErrorResult> errors) {
                if (_errors.Count > 0) {
                    lock (this) {
                        List<ErrorResult> errorList;
                        if (!_warnings.TryGetValue(filename, out errorList)) {
                            _warnings[filename] = errorList = new List<ErrorResult>();
                        }
                        errorList.AddRange(errors);
                    }
                }
            }

            internal void Clear(string filename) {
                lock (this) {
                    _warnings.Remove(filename);
                    _errors.Remove(filename);
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
                        piLine = Span.Start.Line - 1;
                        return VSConstants.S_OK;
                    }

                    public int NavigateTo() {
                        PythonToolsPackage.NavigateTo(_path, Guid.Empty, Span.Start.Line - 1, Span.Start.Column - 1);
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
    }
}
