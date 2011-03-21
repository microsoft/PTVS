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
        private readonly PythonAnalyzer _analysisState;
        private readonly Dictionary<Type, Command> _commands = new Dictionary<Type, Command>();
        private readonly IVsErrorList _errorList;
        private readonly PythonProjectNode _project;

        public ProjectAnalyzer(IPythonInterpreterFactory factory, IErrorProviderFactory errorProvider)
            : this(factory.CreateInterpreter(), factory, errorProvider) {
        }

        public ProjectAnalyzer(IPythonInterpreter interpreter, IPythonInterpreterFactory factory, IErrorProviderFactory errorProvider, PythonProjectNode project = null) {
            _errorProvider = errorProvider;            

            _queue = new ParseQueue(this);
            _analysisQueue = new AnalysisQueue(this);

            _interpreterFactory = factory;
            _project = project;

            _analysisState = new PythonAnalyzer(interpreter, factory.GetLanguageVersion());
            _projectFiles = new Dictionary<string, IProjectEntry>(StringComparer.OrdinalIgnoreCase);

            if (PythonToolsPackage.Instance != null) {
                _errorList = (IVsErrorList)PythonToolsPackage.GetGlobalService(typeof(SVsErrorList));
            }
        }

        /// <summary>
        /// Creates a new ProjectEntry for the collection of buffers.
        /// </summary>
        private void ReAnalyzeTextBuffers(BufferParser bufferParser) {
            ITextBuffer[] buffers = bufferParser.Buffers;

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
        public MonitoredBufferResult MonitorTextBuffer(ITextBuffer buffer) {
            IProjectEntry projEntry = CreateProjectEntry(buffer, new SnapshotCookie(buffer.CurrentSnapshot));

            // kick off initial processing on the buffer        
            lock (_openFiles) {
                var bufferParser = _queue.EnqueueBuffer(projEntry, buffer);
                _openFiles[bufferParser] = projEntry;
                return new MonitoredBufferResult(bufferParser, projEntry);
            }
        }

        private IProjectEntry CreateProjectEntry(ITextBuffer buffer, IAnalysisCookie analysisCookie) {
            var replEval = buffer.GetReplEvaluator();
            if (replEval != null) {
                // We have a repl window, create an untracked module.
                return _analysisState.AddModule(null, null, analysisCookie);
            }

            string path = buffer.GetFilePath();
            if (path == null) {
                return null;
            }

            IProjectEntry entry;
            if (!_projectFiles.TryGetValue(path, out entry)) {
                var modName = PythonAnalyzer.PathToModuleName(path);

                if (buffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                    entry = _analysisState.AddModule(
                        modName,
                        buffer.GetFilePath(),
                        analysisCookie
                    );
                    
                }else if(buffer.ContentType.IsOfType("XAML")) {
                    entry = _analysisState.AddXamlFile(buffer.GetFilePath());
                } else {
                    return null;
                }

                _projectFiles[path] = entry;

                if (ImplicitProject && 
                    !Path.GetFullPath(path).StartsWith(Path.GetDirectoryName(_interpreterFactory.Configuration.InterpreterPath), StringComparison.OrdinalIgnoreCase)) { // don't analyze std lib
                    // TODO: We're doing this on the UI thread and when we end up w/ a lot to queue here we hang for a while...
                    // But this adds files to the analyzer so it's not as simple as queueing this onto another thread.
                    AddImplicitFiles(Path.GetDirectoryName(Path.GetFullPath(path)));
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

                    item = _analysisState.AddModule(
                        modName,
                        path,
                        null
                    );
                } else if (path.EndsWith(".xaml", StringComparison.Ordinal)) {
                    item = _analysisState.AddXamlFile(path, null);
                }

                if (item != null) {
                    _projectFiles[path] = item;
                }
            }

            if (item != null) {
                _queue.EnqueueFile(item, path);
            }

            return item;
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
                if (parser.Tokens.Count > 0) {
                    var lastToken = parser.Tokens[parser.Tokens.Count - 1];
                    string wholeToken = GetWholeTokenRight(snapshot, lastToken.Span);
                    if (wholeToken != null) {
                        return new ExpressionAnalysis(wholeToken, null, lastToken.Span.Start.GetContainingLine().LineNumber, null);
                    }
                }
                return ExpressionAnalysis.Empty;
            }

            // extend right for any partial expression the user is hovering on, for example:
            // "x.Baz" where the user is hovering over the B in baz we want the complete
            // expression.
            string text = GetWholeTokenRight(snapshot, exprRange.Value);
            if (text == null) {
                return ExpressionAnalysis.Empty;
            }

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
        /// Moves right to get the full token
        /// </summary>
        private static string GetWholeTokenRight(ITextSnapshot snapshot, SnapshotSpan exprRange) {
            string text = exprRange.GetText();
            var endingLine = exprRange.End.GetContainingLine();
            if (endingLine.End.Position - exprRange.End.Position < 0) {
                return null;
            }
            var endText = snapshot.GetText(exprRange.End.Position, endingLine.End.Position - exprRange.End.Position);
            bool allChars = true;
            for (int i = 0; i < endText.Length; i++) {
                if (!Char.IsLetterOrDigit(endText[i]) && endText[i] != '_') {
                    text += endText.Substring(0, i);
                    allChars = false;
                    break;
                }
            }
            if (allChars) {
                text += endText;
            }
            return text;
        }

        /// <summary>
        /// Gets a CompletionList providing a list of possible members the user can dot through.
        /// </summary>
        public static CompletionAnalysis GetCompletions(ITextSnapshot snapshot, ITrackingSpan span, bool intersectMembers = true, bool hideAdvancedMembers = false) {
            var buffer = snapshot.TextBuffer;
            ReverseExpressionParser parser = new ReverseExpressionParser(snapshot, buffer, span);

            var loc = parser.Span.GetSpan(parser.Snapshot.Version);
            var line = parser.Snapshot.GetLineFromPosition(loc.Start);
            var lineStart = line.Start;

            var textLen = loc.End - lineStart.Position;
            if (textLen <= 0) {
                // Ctrl-Space on an empty line, we just want to get global vars
                return new NormalCompletionAnalysis(String.Empty, loc.Start, parser.Snapshot, parser.Span, parser.Buffer, 0);
            }

            return TrySpecialCompletions(parser, loc) ??
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
                return _analysisState.Interpreter;
            }
        }

        public PythonAnalyzer Project {
            get {
                return _analysisState;
            }
        }

        public void ParseFile(IProjectEntry projectEntry, string filename, TextReader content, Severity indentationSeverity) {
            IProjectEntry analysis;
            IExternalProjectEntry externalEntry;
            if (PythonProjectNode.IsPythonFile(filename)) {
                PythonAst ast;
                CollectingErrorSink errorSink;
                ParsePythonCode(content, indentationSeverity, out ast, out errorSink);

                if (ast != null) {
                    IPythonProjectEntry pyAnalysis;
                    if (_projectFiles.TryGetValue(filename, out analysis) &&
                        (pyAnalysis = analysis as IPythonProjectEntry) != null) {

                        pyAnalysis.UpdateTree(ast, new FileCookie(filename));
                        _analysisQueue.Enqueue(analysis, AnalysisPriority.Normal);
                    }
                }

                TaskProvider provider = GetTaskListProviderForProject(projectEntry);
                if (provider != null) {
                    foreach (ErrorResult warning in errorSink.Warnings) {
                        provider.AddWarning(warning);
                    }

                    foreach (ErrorResult error in errorSink.Errors) {
                        provider.AddError(error);
                    }

                    UpdateErrorList(errorSink, projectEntry.FilePath, provider);
                }

            } else if (_projectFiles.TryGetValue(filename, out analysis) && (externalEntry = (analysis as IExternalProjectEntry)) != null) {
                externalEntry.ParseContent(content, new FileCookie(filename));
                _analysisQueue.Enqueue(analysis, AnalysisPriority.Normal);
            }
        }

        public void ParseBuffers(BufferParser bufferParser, Severity indentationSeverity, params ITextSnapshot[] snapshots) {
            IProjectEntry analysis;
            lock (_openFiles) {
                if (!_openFiles.TryGetValue(bufferParser, out analysis)) {
                    return;
                }
            }

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
                            squiggles.RemoveTagSpans(x => true);

                            TaskProvider provider = GetTaskListProviderForProject(bufferParser._currentProjEntry);

                            AddWarnings(snapshot, errorSink, squiggles, provider);

                            AddErrors(snapshot, errorSink, squiggles, provider);

                            UpdateErrorList(errorSink, buffer.GetFilePath(), provider);
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
            }
        }

        private static void AddErrors(ITextSnapshot snapshot, CollectingErrorSink errorSink, SimpleTagger<ErrorTag> squiggles, TaskProvider provider) {
            foreach (ErrorResult error in errorSink.Errors) {
                var span = error.Span;                
                var tspan = CreateSpan(snapshot, span);
                squiggles.CreateTagSpan(tspan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, error.Message));
                if (provider != null) {
                    provider.AddError(error);
                }
            }
        }

        private static void AddWarnings(ITextSnapshot snapshot, CollectingErrorSink errorSink, SimpleTagger<ErrorTag> squiggles, TaskProvider provider) {
            foreach (ErrorResult warning in errorSink.Warnings) {
                var span = warning.Span;
                var tspan = CreateSpan(snapshot, span);
                squiggles.CreateTagSpan(tspan, new ErrorTag(PredefinedErrorTypeNames.Warning, warning.Message));
                if (provider != null) {
                    provider.AddWarning(warning);
                }
            }
        }

        private TaskProvider GetTaskListProviderForProject(IProjectEntry projEntry) {
            TaskProvider provider = null;
            object providerObj = null;
            if (_errorList != null && projEntry.FilePath != null) {
                if (!projEntry.Properties.TryGetValue(typeof(TaskProvider), out providerObj)) {
                    uint cookie;
                    projEntry.Properties[typeof(TaskProvider)] = provider = new TaskProvider(projEntry.FilePath);

                    ErrorHandler.ThrowOnFailure(((IVsTaskList)_errorList).RegisterTaskProvider(provider, out cookie));
                    provider.Cookie = cookie;

                    // unregister ourselves when the project entry is collected (and therefore our unregister becomes eligble for finalization)
                    projEntry.Properties[typeof(ErrorUnRegister)] = new ErrorUnRegister(provider, _errorList, cookie, _project, projEntry.FilePath);
                } else {
                    provider = providerObj as TaskProvider;
                }

                provider.Clear();
            }
            return provider;
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

        private void ParsePythonCode(TextReader content, Severity indentationSeverity, out PythonAst ast, out CollectingErrorSink errorSink) {
            ast = null;
            errorSink = new CollectingErrorSink();

            using (var parser = MakeParser(content, errorSink, indentationSeverity)) {
                if (parser != null) {
                    try {
                        ast = parser.ParseFile();
                    } catch (Exception e) {
                        Debug.Assert(false, String.Format("Failure in Python parser: {0}", e.ToString()));
                    }

                }
            }
        }

        private Parser MakeParser(TextReader reader, ErrorSink sink, Severity indentationSeverity) {
            for (int i = 0; i < 10; i++) {
                try {
                    return Parser.CreateParser(reader, sink, _interpreterFactory.GetLanguageVersion(), indentationSeverity);                        
                } catch (IOException) {
                    // file being copied, try again...
                    Thread.Sleep(100);
                }
            }
            return null;
        }

        private static ITrackingSpan CreateSpan(ITextSnapshot snapshot, SourceSpan span) {
            var tspan = snapshot.CreateTrackingSpan(
                new Span(
                    span.Start.Index,
                    Math.Min(span.End.Index - span.Start.Index, Math.Max(snapshot.Length - span.Start.Index, 0))
                ),
                SpanTrackingMode.EdgeInclusive
            );
            return tspan;
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
                        var parser = Parser.CreateParser(new StringReader(source), ErrorSink.Null, _interpreterFactory.GetLanguageVersion());

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

        private static CompletionAnalysis TrySpecialCompletions(ReverseExpressionParser parser, Span loc) {
            if (parser.Tokens.Count > 0) {
                // Check for context-sensitive intellisense
                var lastClass = parser.Tokens[parser.Tokens.Count - 1];

                if (lastClass.ClassificationType == parser.Classifier.Provider.Comment) {
                    // No completions in comments
                    return CompletionAnalysis.EmptyCompletionContext;
                } else if (lastClass.ClassificationType == parser.Classifier.Provider.StringLiteral) {
                    // String completion
                    return new StringLiteralCompletionList(lastClass.Span.GetText(), loc.Start, parser.Span, parser.Buffer);
                }

                // Import completions
                var first = parser.Tokens[0];
                if (CompletionAnalysis.IsKeyword(first, "import")) {
                    return ImportCompletionAnalysis.Make(first, lastClass, loc, parser.Snapshot, parser.Span, parser.Buffer, IsSpaceCompletion(parser, loc));
                } else if (CompletionAnalysis.IsKeyword(first, "from")) {
                    return FromImportCompletionAnalysis.Make(parser.Tokens, first, loc, parser.Snapshot, parser.Span, parser.Buffer, IsSpaceCompletion(parser, loc));
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
            if (IsSpaceCompletion(parser, loc)) {
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

        private static bool IsSpaceCompletion(ReverseExpressionParser parser, Span loc) {
            var keySpan = new SnapshotSpan(parser.Snapshot, loc.Start - 1, 1);
            return (keySpan.GetText() == " ");
        }

        private static Stopwatch MakeStopWatch() {
            var res = new Stopwatch();
            res.Start();
            return res;
        }

        private void AddImplicitFiles(string dir) {
            foreach (string filename in Directory.GetFiles(dir, "*.py")) {
                AnalyzeFile(filename);
            }

            foreach (string innerDir in Directory.GetDirectories(dir)) {
                if (File.Exists(Path.Combine(innerDir, "__init__.py"))) {
                    AddImplicitFiles(innerDir);
                }
            }
        }

        internal void UnloadFile(IProjectEntry entry) {
            object value;
            if (entry.Properties.TryGetValue(typeof(ErrorUnRegister), out value)) {
                ErrorUnRegister error = value as ErrorUnRegister;
                if (error != null) {
                    error.Unload();
                }
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
            private readonly List<ErrorResult> _warnings = new List<ErrorResult>();
            private readonly List<ErrorResult> _errors = new List<ErrorResult>();
            private uint _cookie;


            public TaskProvider(string filePath) {
                _path = filePath;
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
                ppenum = new TaskEnum(_path, _warnings.ToArray(), _errors.ToArray());
                return VSConstants.S_OK;
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

            internal void AddError(ErrorResult error) {
                _errors.Add(error);
            }

            internal void AddWarning(ErrorResult error) {
                _warnings.Add(error);
            }

            internal void Clear() {
                _errors.Clear();
                _warnings.Clear();
            }

            class TaskEnum : IVsEnumTaskItems {
                private int _curIndex;
                private readonly ErrorResult[] _warnings;
                private readonly ErrorResult[] _errors;
                private readonly string _path;

                public TaskEnum(string filePath, ErrorResult[] warnings, ErrorResult[] errors) {
                    _warnings = warnings;
                    _errors = errors;
                    _path = filePath;
                }

                #region IVsEnumTaskItems Members

                public int Clone(out IVsEnumTaskItems ppenum) {
                    ppenum = new TaskEnum(_path, _warnings, _errors);
                    return VSConstants.S_OK;
                }

                public int Next(uint celt, IVsTaskItem[] rgelt, uint[] pceltFetched = null) {
                    for (int i = 0; i < celt; i++) {
                        var next = EnumOne();
                        if (next == null) {
                            if (pceltFetched != null) {
                                pceltFetched[0] = (uint)i;
                            }
                            return VSConstants.S_OK;
                        }

                        rgelt[i] = next;
                    }
                    if (pceltFetched != null) {
                        pceltFetched[0] = celt;
                    }
                    return VSConstants.S_OK;
                }

                public int Reset() {
                    _curIndex = 0;
                    return VSConstants.S_OK;
                }

                public int Skip(uint celt) {
                    _curIndex += (int)celt;
                    return VSConstants.S_OK;
                }

                private TaskItem EnumOne() {
                    if (_curIndex < _warnings.Length) {
                        return new TaskItem(_warnings[_curIndex++], _path, false);
                    } else if (_curIndex < _errors.Length - _warnings.Length) {
                        return new TaskItem(_errors[_curIndex++ - _warnings.Length], _path, true);
                    }

                    return null;
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

        class ErrorUnRegister {
            private readonly IVsErrorList _errorList;
            private readonly uint _cookie;
            private readonly PythonProjectNode _project;
            private readonly string _filename;
            private readonly TaskProvider _provider;

            public ErrorUnRegister(TaskProvider provider, IVsErrorList errorList, uint cookie, PythonProjectNode project, string filename) {
                _errorList = errorList;
                _cookie = cookie;
                _project = project;
                _filename = filename;
                _provider = provider;
            }

            ~ErrorUnRegister() {
                Unload();
            }


            internal void Unload() {
                _provider.Clear();
                try {
                    ((IVsTaskList)_errorList).RefreshTasks(_cookie);
                    ((IVsTaskList)_errorList).UnregisterTaskProvider(_cookie);
                } catch (InvalidComObjectException) {
                    // when shutting down our com object can be disposed of before we hit this.
                }

                if (_project != null) {
                    _project.ErrorFiles.Remove(_filename);
                }
                GC.SuppressFinalize(this);
            }
        }
    }
}
