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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Ipc.Json;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Projects;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudioTools;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using LS = Microsoft.Python.LanguageServer;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    sealed class VsProjectAnalyzer : ProjectAnalyzer, IDisposable {
        internal readonly PythonEditorServices _services;
        private AnalysisProcessInfo _analysisProcess;
        private Connection _conn;
        private Task _processingTask;

        // Enables analyzers to be put directly into ITextBuffer.Properties for the purposes of testing
        internal static readonly object _testAnalyzer = new { Name = "TestAnalyzer" };
        internal static readonly object _testFilename = new { Name = "TestFilename" };
        internal static readonly object _testDocumentUri = new { Name = "DocumentUri" };

        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_zipFileName] contains the full path to that archive.
        private static readonly object _zipFileName = new { Name = "ZipFileName" };

        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_pathInZipFile] contains the path of the item inside the archive.
        private static readonly object _pathInZipFile = new { Name = "PathInZipFile" };

        private readonly StringBuilder _stdErr = new StringBuilder();

        private readonly IPythonInterpreterFactory _interpreterFactory;

        private readonly ConcurrentDictionary<string, AnalysisEntry> _projectFiles;
        private readonly ConcurrentDictionary<Uri, AnalysisEntry> _projectFilesByUri;

        internal readonly HashSet<AnalysisEntry> _hasParseErrors = new HashSet<AnalysisEntry>();
        internal readonly object _hasParseErrorsLock = new object();

        internal const string PythonMoniker = "Python";
        internal const string InvalidEncodingMoniker = "InvalidEncoding";
        internal const string TaskCommentMoniker = "Task comment";
        internal bool _analysisComplete;

        private AP.AnalysisOptions _analysisOptions;

        private int _userCount;

        private readonly CancellationTokenSource _processExitedCancelSource = new CancellationTokenSource();
        private readonly HashSet<ProjectReference> _references = new HashSet<ProjectReference>();
        private bool _disposing;

        private readonly ConcurrentDictionary<object, object> _activeRequests = new ConcurrentDictionary<object, object>();

        private readonly ConcurrentDictionary<string, long> _requestCounts = new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, long> _timeoutCounts = new ConcurrentDictionary<string, long>();

        private readonly IPythonToolsLogger _logger;

        internal int _parsePending;
        private TaskCompletionSource<object> _waitForCompleteAnalysis;

        private const string AnalysisLimitsKey = @"Software\Microsoft\PythonTools\" + AssemblyVersionInfo.VSVersion +
            @"\Analysis\Project";

        // Used by tests to avoid creating TaskProvider objects
        internal static bool SuppressTaskProvider = false;

        internal static async Task<VsProjectAnalyzer> CreateDefaultAsync(
            PythonEditorServices services,
            IPythonInterpreterFactory factory,
            bool inProcess = false
        ) {
            try {
                services.Python?.Logger?.LogEvent(PythonLogEvent.AnalysisInitializing, new AnalysisInitialize() {
                    InterpreterId = factory.Configuration.Id,
                    Architecture = factory.Configuration.Architecture.ToString(),
                    Version = factory.Configuration.Version.ToString(),
                    IsIronPython = factory.Configuration.IsIronPython(),
                    Reason = AnalysisInitializeReasons.Default,
                });
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(VsProjectAnalyzer)));
            }

            var analyzer = new VsProjectAnalyzer(services, factory);
            await analyzer.InitializeAsync(!inProcess, null, null, false);
            return analyzer;
        }

        internal static async Task<VsProjectAnalyzer> CreateForProjectAsync(
            PythonEditorServices services,
            IPythonInterpreterFactory factory,
            string projectPath,
            string projectHome,
            bool inProcess = false
        ) {
            try {
                services.Python?.Logger?.LogEvent(PythonLogEvent.AnalysisInitializing, new AnalysisInitialize() {
                    InterpreterId = factory.Configuration.Id,
                    Architecture = factory.Configuration.Architecture.ToString(),
                    Version = factory.Configuration.Version.ToString(),
                    IsIronPython = factory.Configuration.IsIronPython(),
                    Reason = AnalysisInitializeReasons.Project,
                });
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(VsProjectAnalyzer)));
            }

            var analyzer = new VsProjectAnalyzer(services, factory);
            await analyzer.InitializeAsync(!inProcess, projectPath, projectHome, false);
            return analyzer;
        }

        internal static async Task<VsProjectAnalyzer> CreateForWorkspaceAsync(
            PythonEditorServices services,
            IPythonInterpreterFactory factory,
            string workspacePath,
            bool inProcess = false
        ) {
            try {
                services.Python?.Logger?.LogEvent(PythonLogEvent.AnalysisInitializing, new AnalysisInitialize() {
                    InterpreterId = factory.Configuration.Id,
                    Architecture = factory.Configuration.Architecture.ToString(),
                    Version = factory.Configuration.Version.ToString(),
                    IsIronPython = factory.Configuration.IsIronPython(),
                    Reason = AnalysisInitializeReasons.Workspace,
                });
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(VsProjectAnalyzer)));
            }

            var analyzer = new VsProjectAnalyzer(services, factory);
            await analyzer.InitializeAsync(!inProcess, workspacePath, workspacePath, false);
            return analyzer;
        }

        internal static async Task<VsProjectAnalyzer> CreateForInteractiveAsync(
            PythonEditorServices services,
            IPythonInterpreterFactory factory,
            string displayName,
            bool inProcess = false
        ) {
            try {
                services.Python?.Logger?.LogEvent(PythonLogEvent.AnalysisInitializing, new AnalysisInitialize() {
                    InterpreterId = factory.Configuration.Id,
                    Architecture = factory.Configuration.Architecture.ToString(),
                    Version = factory.Configuration.Version.ToString(),
                    IsIronPython = factory.Configuration.IsIronPython(),
                    Reason = AnalysisInitializeReasons.Interactive,
                });
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(VsProjectAnalyzer)));
            }

            var analyzer = new VsProjectAnalyzer(services, factory);
            await analyzer.InitializeAsync(!inProcess, $"{displayName} Interactive", null, false);
            return analyzer;
        }

        internal static async Task<VsProjectAnalyzer> CreateForTestsAsync(
            PythonEditorServices services,
            IPythonInterpreterFactory factory,
            bool inProcess = true
        ) {
            var analyzer = new VsProjectAnalyzer(services, factory);
            await analyzer.InitializeAsync(!inProcess, "PTVS_TEST", null, false);
            return analyzer;
        }

        private VsProjectAnalyzer(
            PythonEditorServices services,
            IPythonInterpreterFactory factory
        ) {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _interpreterFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            _toString = $"<{GetType().Name}:{_interpreterFactory.Configuration.Id}:unstarted>";

            _analysisOptions = new AP.AnalysisOptions {
                indentationInconsistencySeverity = Severity.Ignore,
#if DEBUG
                traceLevel = LS.MessageType.Info,
#else
                traceLevel = LS.MessageType.Warning,
#endif
                commentTokens = new Dictionary<string, Analysis.DiagnosticSeverity>() {
                    { "TODO", Analysis.DiagnosticSeverity.Warning },
                    { "HACK", Analysis.DiagnosticSeverity.Warning },
                }
            };

            _projectFiles = new ConcurrentDictionary<string, AnalysisEntry>();
            _projectFilesByUri = new ConcurrentDictionary<Uri, AnalysisEntry>(UriEqualityComparer.IncludeFragment);

            _logger = _services.Python?.Logger;

            if (_services.CommentTaskProvider != null) {
                _services.CommentTaskProvider.TokensChanged += CommentTaskTokensChanged;
            }
        }

        private string _toString;
        public override string ToString() => _toString;


        private string DefaultComment => "Global Analysis";

        private async Task InitializeAsync(bool outOfProc, string comment, string rootDir, bool analyzeAllFiles) {
            var lso = _services.Python?.LanguageServerOptions;
            if (lso?.ServerDisabled == true) {
                _conn = null;
                _userCount = 1;
                _processingTask = null;
                _analysisOptions = new AP.AnalysisOptions();
                _analysisProcess = null;
                return;
            }

            _conn = outOfProc 
                ? StartSubprocessConnection(comment.IfNullOrEmpty(DefaultComment), out _analysisProcess) 
                : StartThreadConnection(comment.IfNullOrEmpty(DefaultComment), out _analysisProcess);

            if (!string.IsNullOrEmpty(_conn.LogFilename)) {
                Trace.TraceInformation($"Connection log: {_conn.LogFilename}");
            }

            _processingTask = Task.Run(() => _conn.ProcessMessages().HandleAllExceptions(_services.Site, allowUI: false));

            _toString = $"<{GetType().Name}:{_interpreterFactory.Configuration.Id}:{_analysisProcess}:{comment.IfNullOrEmpty(DefaultComment)}>";
            _userCount = 1;

            var interp = new AP.InterpreterInfo();
            _services.InterpreterRegistryService.GetSerializationInfo(_interpreterFactory, out interp.assembly, out interp.typeName, out interp.properties);
            var initialize = new AP.InitializeRequest {
                interpreter = interp,
                rootUri = string.IsNullOrEmpty(rootDir) ? null : new Uri(rootDir),
                analyzeAllFiles = analyzeAllFiles
            };

            if (comment?.Contains("PTVS_TEST") ?? false) {
                initialize.traceLogging = true;
                _analysisOptions.traceLevel = LS.MessageType.Log;
            }

            if (lso != null) {
                lso.Changed += OnOptionsChanged;
                _analysisOptions.typeStubPaths = GetTypeStubPaths(lso);
            }
            
            var go = _services.Python?.GeneralOptions;
            if (go != null) {
                go.Changed += OnOptionsChanged;
                _analysisOptions.enableUnresolvedImportWarning = go.UnresolvedImportWarning;
                _analysisOptions.enableUseBeforeDefWarning = false;
            }

            if (_analysisOptions.analysisLimits == null) {
                using (var key = Registry.CurrentUser.OpenSubKey(AnalysisLimitsKey)) {
                    _analysisOptions.analysisLimits = AnalysisLimitsConverter.LoadFromStorage(key).ToDictionary();
                }
            }
            using (var key = Registry.CurrentUser.OpenSubKey(PythonCoreConstants.LoggingRegistrySubkey)) {
                var traceLogging = key?.GetValue("AnalyzerTrace", null);
                if ((traceLogging is int i && i != 0) || (traceLogging is string s && s.IsTrue())) {
                    initialize.traceLogging = true;
                    _analysisOptions.traceLevel = LS.MessageType.Log;
                }
            }

            var initResponse = await SendRequestAsync(initialize);
            if (initResponse == null || !string.IsNullOrWhiteSpace(initResponse.error)) {
                if (initResponse?.error != null) {
                    _logger?.LogEvent(PythonLogEvent.AnalysisOperationFailed, "Initialization: " + initResponse.error);
                } else {
                    _logger?.LogEvent(PythonLogEvent.AnalysisOperationFailed, "Initialization");
                }
                _analysisProcess.Kill();
                _analysisProcess.Dispose();
                _analysisProcess = null;
                _conn.Dispose();
                _conn = null;
                throw new InvalidOperationException("Failed to initialize analyzer");
            }

            _analysisOptions.indentationInconsistencySeverity = _services.Python?.GeneralOptions.IndentationInconsistencySeverity ?? Severity.Ignore;

            if (_services.CommentTaskProvider != null) {
                _analysisOptions.commentTokens.Clear();
                foreach (var keyValue in (_services.CommentTaskProvider.Tokens).MaybeEnumerate()) {
                    var sev = Analysis.DiagnosticSeverity.Suppressed;
                    switch (keyValue.Value) {
                        case VSTASKPRIORITY.TP_HIGH:
                            sev = Analysis.DiagnosticSeverity.Error;
                            break;
                        case VSTASKPRIORITY.TP_NORMAL:
                            sev = Analysis.DiagnosticSeverity.Warning;
                            break;
                        case VSTASKPRIORITY.TP_LOW:
                            sev = Analysis.DiagnosticSeverity.Information;
                            break;
                    }
                    _analysisOptions.commentTokens[keyValue.Key] = sev;
                }
            }

            var resp = await SendRequestAsync(new AP.SetAnalysisOptionsRequest {
                options = _analysisOptions
            });
        }

        private void OnOptionsChanged(object sender, EventArgs e) {
            switch (sender) {
                case LanguageServerOptions lso:
                    _analysisOptions.typeStubPaths = GetTypeStubPaths(lso);
                    break;
                case GeneralOptions go:
                    _analysisOptions.enableUnresolvedImportWarning = go.UnresolvedImportWarning;
                    _analysisOptions.enableUseBeforeDefWarning = false;
                    break;
                default:
                    return;
            }

            SendRequestAsync(new AP.SetAnalysisOptionsRequest {
                options = _analysisOptions
            }).HandleAllExceptions(_services.Site, GetType()).DoNotWait();
        }

        private static string[] GetTypeStubPaths(Options.LanguageServerOptions lso) {
            if (lso.SuppressTypeShed) {
                return null;
            }

            if (string.IsNullOrEmpty(lso.TypeShedPath)) {
                var license = PythonToolsInstallPath.TryGetFile("Typeshed\\LICENSE", typeof(VsProjectAnalyzer).Assembly);
                if (string.IsNullOrEmpty(license)) {
                    return null;
                }
                return new[] { PathUtils.GetParent(license) };
            } else if (Directory.Exists(lso.TypeShedPath)) {
                return new[] { lso.TypeShedPath };
            }

            return null;
        }

        internal IServiceProvider Site => _services.Site;

        public bool IsActive => _conn != null;

        #region Asynchronous request handling
        /// <summary>
        /// The recommended timeout to use when waiting on analysis information.
        /// </summary>
        /// <remarks>
        /// This has been adjusted based on telemetry to minimize the "never
        /// responsive" time.
        /// </remarks>
        internal static int DefaultTimeout = 250;

        internal static bool AssertOnRequestFailure = false;

        public T WaitForRequest<T>(Task<T> request, string requestName) {
            return WaitForRequest(request, requestName, default(T), 1);
        }

        public T WaitForRequest<T>(Task<T> request, string requestName, T defaultValue) {
            return WaitForRequest(request, requestName, defaultValue, 1);
        }

        public T WaitForRequest<T>(Task<T> request, string requestName, T defaultValue, int timeoutScale) {
            bool timeout = true;
            var timer = new Stopwatch();
            timer.Start();
            T result = defaultValue;
            try {
                if (request.Wait(System.Diagnostics.Debugger.IsAttached ? Timeout.Infinite : DefaultTimeout * timeoutScale)) {
                    result = request.Result;
                    timeout = false;
                }
            } catch (AggregateException ae) {
                if (ae.InnerException != null) {
                    ExceptionDispatchInfo.Capture(ae.InnerException).Throw();
                }
                throw;
            }
            LogTimingEvent(requestName, timer.ElapsedMilliseconds, DefaultTimeout * timeoutScale);
            if (timeout && AssertOnRequestFailure) {
                Debug.Fail($"{requestName} timed out after {timer.ElapsedMilliseconds}ms");
            }
            return result;
        }

        private static void Increment(ConcurrentDictionary<string, long> dict, string key) {
            long existing;
            if (dict.TryGetValue(key, out existing) ||
                !(dict.TryAdd(key, 1) && dict.TryGetValue(key, out existing))) {
                // There is already a count, so we need to increment it
                while (!dict.TryUpdate(key, existing + 1, existing)) {
                    existing = dict[key];
                }
            }
        }

        private void LogTimingEvent(string requestName, long milliseconds, long timeout) {
            try {
                Increment(_requestCounts, requestName);
                if (milliseconds > timeout) {
                    Increment(_timeoutCounts, requestName);
                }

                int elapsed = (int)Math.Min(int.MaxValue, milliseconds);
                if (elapsed > 100) {
                    _logger?.LogEvent(PythonLogEvent.AnalysisRequestTiming, new AnalysisTimingInfo {
                        RequestName = requestName,
                        Milliseconds = elapsed,
                        Timeout = (milliseconds > timeout)
                    });
                }
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
            }
        }

        #endregion

        #region ProjectAnalyzer overrides

        public override async Task RegisterExtensionAsync(Type extensionType) {
            var extensionName = extensionType.GetCustomAttributes(false)
                .OfType<AnalysisExtensionNameAttribute>()
                .FirstOrDefault()?.Name ?? extensionType.Name;

            await SendRequestAsync(new AP.LoadExtensionRequest {
                assembly = extensionType.Assembly.Location,
                typeName = extensionType.FullName,
                extension = extensionName
            });
        }

        /// <summary>
        /// Send a command to an extension.  The extension should have been loaded using a 
        /// RegisterExtension call.  The extension is implemented using IAnalysisExtension.
        /// 
        /// The extension name is provided by decorating the exported value with 
        /// AnalysisExtensionNameAttribute.  The command ID and body are free form values
        /// defined by the extension.
        /// 
        /// Returns null if the extension command fails or the remote process exits unexpectedly.
        /// </summary>
        public override async Task<string> SendExtensionCommandAsync(string extensionName, string commandId, string body) {
            var res = await SendRequestAsync(new AP.ExtensionRequest() {
                extension = extensionName,
                commandId = commandId,
                body = body
            }).ConfigureAwait(false);

            if (res != null) {
                return res.response;
            }

            return null;
        }

        /// <summary>
        /// Raised when any file has a new analysis.
        /// </summary>
        public override event EventHandler<AnalysisCompleteEventArgs> AnalysisComplete;

        public override IEnumerable<string> Files {
            get {
                return _projectFiles.Keys;
            }
        }
        #endregion

        #region Public API

        public PythonLanguageVersion LanguageVersion {
            get {
                return _interpreterFactory.GetLanguageVersion();
            }
        }

        /// <summary>
        /// Increases the number of known users by one.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The analyzer is being or has been disposed.
        /// </exception>
        public void AddUser() {
            if (_disposing) {
                throw new ObjectDisposedException(GetType().Name);
            }
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

            Interlocked.Exchange(ref _waitForCompleteAnalysis, null)?.TrySetCanceled();

            foreach (var path in _projectFiles.Keys) {
                _services.ErrorTaskProvider?.Clear(path, PythonMoniker);
                _services.ErrorTaskProvider?.Clear(path, InvalidEncodingMoniker);
                _services.CommentTaskProvider?.Clear(path, TaskCommentMoniker);
            }

            if (_analysisProcess != null) {
                Debug.WriteLine(String.Format("Disposing of parser {0}", _analysisProcess));
            }

            if (_services.CommentTaskProvider != null) {
                _services.CommentTaskProvider.TokensChanged -= CommentTaskTokensChanged;
            }

            foreach (var openFile in _projectFiles) {
                openFile.Value.Dispose();
            }

            if (_logger != null) {
                var info = new Dictionary<string, object>();
                foreach (var entry in _requestCounts) {
                    info[entry.Key] = entry.Value;
                    long timeouts;
                    if (_timeoutCounts.TryGetValue(entry.Key, out timeouts)) {
                        info[entry.Key + ".Timeouts"] = timeouts;
                    } else {
                        info[entry.Key + ".Timeouts"] = 0L;
                    }
                }
                _logger.LogEvent(PythonLogEvent.AnalysisRequestSummary, info);
            }

            if (_analysisProcess != null) {
                SendRequestAsync(new AP.ExitRequest()).ContinueWith(t => {
                    // give the task a chance to exit cleanly
                    _processingTask?.Wait(1000);

                    try {
                        if (!_analysisProcess.WaitForExit(500)) {
                            _analysisProcess.Kill();
                        }
                    } catch (Win32Exception) {
                        // access denied
                    } catch (InvalidOperationException) {
                        // race w/ process exit...
                    }
                    _analysisProcess.Dispose();
                    _conn?.Dispose();
                    _conn = null;
                });
            }

            try {
                _processExitedCancelSource.Cancel();
                _processExitedCancelSource.Dispose();
            } catch (ObjectDisposedException) {
            }
        }

        public bool IsDisposed => _disposing;

        #endregion

        public async Task SetSearchPathsAsync(IEnumerable<string> absolutePaths) {
            await SendRequestAsync(new AP.SetSearchPathRequest { dir = absolutePaths.ToArray() }).ConfigureAwait(false);
            await SendEventAsync(new AP.ModulesChangedEvent()).ConfigureAwait(false);
        }

        public async Task NotifyModulesChangedAsync() {
            await SendEventAsync(new AP.ModulesChangedEvent()).ConfigureAwait(false);
        }

        public async Task NotifyFileChangesAsync(IEnumerable<Uri> newFiles, IEnumerable<Uri> deletedFiles, IEnumerable<Uri> changedFiles) {
            await SendEventAsync(new AP.FileChangedEvent {
                changes = newFiles.Select(f => new AP.FileEvent { kind = LS.FileChangeType.Created, documentUri = f })
                .Concat(deletedFiles.Select(f => new AP.FileEvent { kind = LS.FileChangeType.Deleted, documentUri = f }))
                .Concat(changedFiles.Select(f => new AP.FileEvent { kind = LS.FileChangeType.Changed, documentUri = f }))
                .ToArray()
            });
        }

        #endregion

        internal abstract class AnalysisProcessInfo : IDisposable {
            public abstract bool HasExited { get; }
            public abstract int ExitCode { get; }
            public abstract bool WaitForExit(int millisecondsTimeout);
            public abstract void Kill();
            public abstract void Dispose();
        }

        class AnalysisProcessSubprocessInfo : AnalysisProcessInfo {
            private readonly Process _proc;

            public AnalysisProcessSubprocessInfo(Process process) {
                _proc = process;
            }

            public override bool HasExited => _proc.HasExited;
            public override int ExitCode => _proc.ExitCode;
            public override bool WaitForExit(int millisecondsTimeout) => _proc.WaitForExit(millisecondsTimeout);
            public override void Dispose() => _proc.Dispose();
            public override void Kill() {
                if (!_proc.HasExited) {
                    try {
                        _proc.Kill();
                    } catch (InvalidOperationException) {
                        if (!_proc.HasExited) {
                            throw;
                        }
                    }
                }
            }
            public override string ToString() => $"<Process {_proc.Id}>";
        }

        class AnalysisProcessThreadInfo : AnalysisProcessInfo {
            private readonly Thread _thread;
            private readonly CancellationTokenSource _onKill;
            private readonly AnonymousPipeClientStream _stdOut, _stdIn;
            private int _exitCode;

            public AnalysisProcessThreadInfo(
                VsProjectAnalyzer vsAnalyzer,
                Thread thread,
                CancellationTokenSource onKill,
                SafePipeHandle stdOutClientHandle,
                SafePipeHandle stdInClientHandle
            ) {
                VsAnalyzer = vsAnalyzer;
                _thread = thread;
                _onKill = onKill;
                _stdOut = new AnonymousPipeClientStream(PipeDirection.Out, stdOutClientHandle);
                _stdIn = new AnonymousPipeClientStream(PipeDirection.In, stdInClientHandle);
            }

            public void SetExitCode(int exitCode) {
                _exitCode = exitCode;
            }

            public CancellationToken CancellationToken => _onKill.Token;

            public VsProjectAnalyzer VsAnalyzer { get; }
            public Stream StandardOutput => _stdOut;
            public Stream StandardInput => _stdIn;

            public bool IsUnitTest { get; set; }

            public override bool HasExited => !_thread.IsAlive;

            public override int ExitCode => _exitCode;

            public override void Dispose() {
                Kill();
                _stdOut.Dispose();
                _stdIn.Dispose();
                _onKill.Dispose();
            }

            public override void Kill() {
                try {
                    _onKill.Cancel();
                } catch (ObjectDisposedException) {
                }
            }

            public override bool WaitForExit(int millisecondsTimeout) => _thread.Join(millisecondsTimeout);

            public override string ToString() => $"<Thread: {_thread.ManagedThreadId}>";
        }

        private Connection StartSubprocessConnection(string comment, out AnalysisProcessInfo proc) {
            var libAnalyzer = typeof(AP.AddFileRequest).Assembly.Location;
#if DEBUG
            var testOption = Debug.Listeners["Microsoft.PythonTools.AssertListener"] != null ? "/unittest " : "";
#else
            var testOption = "";
#endif
            var psi = new ProcessStartInfo(libAnalyzer, testOption + "/comment \"" + comment + "\"");
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            Process process;
            var oldEncoding = Console.InputEncoding;
            try {
                Trace.TraceInformation("Starting analyzer process: {0} {1}", psi.FileName, psi.Arguments);

                // .NET's Process class takes the current stdin encoding and has
                // no way to override it. When this encoding is set to UTF-8, the
                // BOM gets written to stdin immediately, which our analyzer does
                // not understand.
                // Force the encoding to ASCII temporarily to avoid this.
                try {
                    Console.InputEncoding = Encoding.ASCII;
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    // Failed to set it - oh well, let's hope
                }
                process = Process.Start(psi);
            } finally {
                try {
                    Console.InputEncoding = oldEncoding;
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                }
            }

            var conn = new Connection(
                process.StandardInput.BaseStream,
                true,
                process.StandardOutput.BaseStream,
                true,
                null,
                AP.RegisteredTypes,
                "ProjectAnalyzer"
            );

            if (process.HasExited) {
                _stdErr.Append(process.StandardError.ReadToEnd());
                OnAnalysisProcessExited();
            } else {
                Task.Run(async () => {
                    try {
                        while (!process.HasExited) {
                            var line = await process.StandardError.ReadLineAsync();
                            if (line == null) {
                                OnAnalysisProcessExited();
                                break;
                            }
                            _stdErr.AppendLine(line);
                            if (_stdErr.Length > 102400) {
                                _stdErr.Remove(0, 12800);
                            }
                            Debug.WriteLine("Analysis Std Err: " + line);
                        }
                    } catch (InvalidOperationException) {
                        // can race with dispose of the process...
                    }
                });
            }
            conn.EventReceived += ConnectionEventReceived;
            proc = new AnalysisProcessSubprocessInfo(process);
            return conn;
        }

        private static void ThreadConnectionWorker(object o) {
            var info = (AnalysisProcessThreadInfo)o;
            OutOfProcProjectAnalyzer analyzer;
            int exitCode = 0;
            try {
                analyzer = new OutOfProcProjectAnalyzer(info.StandardOutput, info.StandardInput, m => Trace.WriteLine($"Analysis Std Err: {m}"));
                info.CancellationToken.Register(() => {
                    analyzer.Dispose();
                });
                using (analyzer) {
                    analyzer.ProcessMessages().WaitAndUnwrapExceptions();
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.ToUnhandledExceptionMessage(typeof(VsProjectAnalyzer)));
                try {
                    using (var sw = new StreamWriter(info.StandardOutput, new UTF8Encoding(false), 4096, true)) {
                        sw.WriteLine(ex.ToString());
                        sw.Flush();
                    }
                } catch (Exception ex2) {
                    Console.WriteLine(ex2.ToUnhandledExceptionMessage(typeof(VsProjectAnalyzer)));
                }
                exitCode = 1;
            } finally {
                info.SetExitCode(exitCode);
            }
        }

        private Connection StartThreadConnection(string comment, out AnalysisProcessInfo info) {
            var writer = new AnonymousPipeServerStream(PipeDirection.Out);
            var reader = new AnonymousPipeServerStream(PipeDirection.In);

            Trace.TraceInformation("Starting analyzer thread");
            var thread = new Thread(ThreadConnectionWorker);
            var cts = new CancellationTokenSource();
            info = new AnalysisProcessThreadInfo(this, thread, cts, reader.ClientSafePipeHandle, writer.ClientSafePipeHandle) {
                IsUnitTest = comment?.Contains("PTVS_TEST") ?? false
            };
            thread.Start(info);

            var conn = new Connection(
                writer,
                true,
                reader,
                true,
                null,
                AP.RegisteredTypes,
                "ProjectAnalyzer"
            );

            conn.EventReceived += ConnectionEventReceived;
            return conn;
        }

        private void OnAnalysisProcessExited() {
            _processExitedCancelSource.Cancel();
            if (!_disposing) {
                _abnormalAnalysisExit?.Invoke(
                    this,
                    new AbnormalAnalysisExitEventArgs(
                        _stdErr.ToString(),
                        _analysisProcess.ExitCode
                    )
                );
            }
        }

        private event EventHandler<AbnormalAnalysisExitEventArgs> _abnormalAnalysisExit;
        internal event EventHandler<AbnormalAnalysisExitEventArgs> AbnormalAnalysisExit {
            add {
                if ((_analysisProcess?.HasExited ?? false) && !_disposing) {
                    value?.Invoke(this, new AbnormalAnalysisExitEventArgs(_stdErr.ToString(), _analysisProcess.ExitCode));
                }
                _abnormalAnalysisExit += value;
            }
            remove {
                _abnormalAnalysisExit -= value;
            }
        }
        internal event EventHandler AnalysisStarted;

        private void ConnectionEventReceived(object sender, EventReceivedEventArgs e) {
            Debug.WriteLine(String.Format("Event received: {0}", e.Event.name));

            AnalysisEntry entry;
            switch (e.Event.name) {
                case AP.AnalysisCompleteEvent.Name:
                    _analysisComplete = true;
                    Interlocked.Exchange(ref _waitForCompleteAnalysis, null)?.TrySetResult(null);
                    break;
                case AP.FileAnalysisCompleteEvent.Name:
                    OnAnalysisComplete(e);
                    break;
                case AP.FileParsedEvent.Name:
                    Interlocked.Decrement(ref _parsePending);

                    var parsed = (AP.FileParsedEvent)e.Event;
                    if (_projectFilesByUri.TryGetValue(parsed.documentUri, out entry)) {
                        OnParseComplete(entry, parsed);
                    } else {
                        Debug.WriteLine("Unknown file id for fileParsed event: {0}", parsed.documentUri);
                    }
                    break;
                case AP.DiagnosticsEvent.Name:
                    var diagnostics = (AP.DiagnosticsEvent)e.Event;
                    if (_projectFilesByUri.TryGetValue(diagnostics.documentUri, out entry)) {
                        OnDiagnostics(entry, diagnostics);
                    } else {
                        Debug.WriteLine("Unknown file id for diagnostics event: {0}", diagnostics.documentUri);
                    }
                    break;
                case AP.AnalyzerWarningEvent.Name:
                    var warning = (AP.AnalyzerWarningEvent)e.Event;
                    _logger?.LogEvent(PythonLogEvent.AnalysisWarning, warning.message);
                    break;
                case AP.UnhandledExceptionEvent.Name:
                    Debug.Fail("Unhandled exception from analyzer");
                    var exception = (AP.UnhandledExceptionEvent)e.Event;
                    _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationFailed, exception.message);
                    break;
            }
        }

        private void OnAnalysisComplete(EventReceivedEventArgs e) {
            var analysisComplete = (AP.FileAnalysisCompleteEvent)e.Event;
            AnalysisEntry entry;
            if (_projectFilesByUri.TryGetValue(analysisComplete.documentUri, out entry)) {
                // Notify buffer parser about the new versions
                var buffer = entry.TryGetBufferParser()?.DefaultBufferInfo;
                if (buffer != null) {
                    buffer.UpdateLastReceivedAnalysis(analysisComplete.version);
                }

                entry.OnAnalysisComplete();
                AnalysisComplete?.Invoke(this, new AnalysisCompleteEventArgs(entry.Path));
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

        internal async Task<AP.ModuleInfo[]> GetEntriesThatImportModuleAsync(string moduleName, bool includeUnresolved) {
            var modules = await SendRequestAsync(
                new AP.ModuleImportsRequest() {
                    includeUnresolved = includeUnresolved,
                    moduleName = moduleName
                }
            ).ConfigureAwait(false);

            if (modules != null) {
                return modules.modules;
            }
            return Array.Empty<AP.ModuleInfo>();
        }

        internal async Task TransferFromOldAnalyzer(VsProjectAnalyzer oldAnalyzer) {
            var oldFileAndEntry = oldAnalyzer.LoadedFiles.ToArray();
            var oldFiles = oldFileAndEntry.Select(kv => kv.Key);
            var oldEntries = oldFileAndEntry.Select(kv => kv.Value);

            var oldReferences = oldAnalyzer.GetReferences();

            var oldBuffers = oldEntries.ToDictionary(e => e.Path, e => e.TryGetBufferParser()?.AllBuffers);

            foreach (var file in oldEntries) {
                await oldAnalyzer.UnloadFileAsync(file);
            }

            var oldBulkEntries = oldEntries
                .Where(e => !e.IsTemporaryFile && !e.SuppressErrorList)
                .Select(e => e.Path)
                .ToArray();

            foreach (var reference in oldReferences) {
                await AddReferenceAsync(reference);
            }

            var entries = (await AnalyzeFileAsync(oldBulkEntries)).ToList();
            foreach (var e in oldEntries) {
                if (!IsActive) {
                    // We've been shut down - abort
                    return;
                }

                if (e.IsTemporaryFile || e.SuppressErrorList) {
                    var entry = await AnalyzeFileAsync(e.Path, null, e.IsTemporaryFile, e.SuppressErrorList);
                    for (int retries = 3; retries > 0 && entry == null && IsActive; --retries) {
                        // Likely in the process of changing analyzer, so we'll delay slightly and retry.
                        await Task.Delay(100);
                        entry = await AnalyzeFileAsync(e.Path, null, e.IsTemporaryFile, e.SuppressErrorList);
                    }
                    if (!IsActive) {
                        // We've been shut down - abort
                        return;
                    }
                    if (entry == null) {
                        Debug.Fail($"Failed to analyze file {e.Path}");
                        continue;
                    }
                    entries.Add(entry);
                }
            }

            foreach (var e in entries) {
                if (e == null) {
                    continue;
                }
                if (!IsActive) {
                    // We've been shut down - abort
                    return;
                }

                if (oldBuffers.TryGetValue(e.Path, out ITextBuffer[] buffers)) {
                    foreach (var b in buffers.MaybeEnumerate()) {
                        if (!IsActive) {
                            // We've been shut down - abort
                            return;
                        }

                        PythonTextBufferInfo.MarkForReplacement(b);
                        var bi = _services.GetBufferInfo(b);
                        var actualEntry = bi.TrySetAnalysisEntry(e, null);
                        var bp = actualEntry?.GetOrCreateBufferParser(_services);
                        if (bp != null) {
                            try {
                                bp.AddBuffer(b);
                                await bp.EnsureCodeSyncedAsync(b);
                            } catch (InvalidOperationException) {
                            }
                        }
                    }
                }
            }
        }

        internal async Task TransferFileFromOldAnalyzer(AnalysisEntry oldEntry, string newPath = null) {
            var oldSnapshots = oldEntry?.TryGetBufferParser()?
                .AllBuffers.Select(b => b.CurrentSnapshot).ToArray();

            var oldAnalyzer = oldEntry?.Analyzer;
            if (oldAnalyzer != null) {
                await oldAnalyzer.UnloadFileAsync(oldEntry);
            }

            var entry = await AnalyzeFileAsync(
                newPath ?? oldEntry?.Path ?? throw new ArgumentNullException(nameof(newPath)),
                isTemporaryFile: oldEntry?.IsTemporaryFile ?? false,
                suppressErrorList: oldEntry?.SuppressErrorList ?? false
            );

            if (entry == null) {
                Debug.Fail("Failed to create new entry");
                return;
            }

            if (oldEntry.Properties.ContainsKey(IntellisenseController.FollowDefaultEnvironment)) {
                entry.Properties[IntellisenseController.FollowDefaultEnvironment] = true;
            }

            var bufferParser = entry.GetOrCreateBufferParser(_services);

            if (oldSnapshots != null && oldSnapshots.Length > 0) {
                var buffers = new HashSet<ITextBuffer>();
                foreach (var snapshot in oldSnapshots) {
                    if (buffers.Add(snapshot.TextBuffer)) {
                        PythonTextBufferInfo.MarkForReplacement(snapshot.TextBuffer);
                        var bi = _services.GetBufferInfo(snapshot.TextBuffer);
                        bi.TrySetAnalysisEntry(entry, null);
                    }
                    bufferParser.AddBuffer(snapshot.TextBuffer);
                }
                await BufferParser.ParseBuffersAsync(_services, this, oldSnapshots, true);
            }
        }

        internal static void ConnectErrorList(PythonTextBufferInfo buffer) {
            if (buffer.AnalysisEntry == null || buffer.AnalysisEntry.SuppressErrorList) {
                return;
            }

            buffer.Services.ErrorTaskProvider?.AddBufferForErrorSource(buffer.Filename, PythonMoniker, buffer.Buffer);
            buffer.Services.ErrorTaskProvider?.AddBufferForErrorSource(buffer.Filename, InvalidEncodingMoniker, buffer.Buffer);
            buffer.Services.CommentTaskProvider?.AddBufferForErrorSource(buffer.Filename, TaskCommentMoniker, buffer.Buffer);
            buffer.Services.InvalidEncodingSquiggleProvider?.AddBuffer(buffer);
        }

        internal static void DisconnectErrorList(PythonTextBufferInfo buffer) {
            if (buffer.AnalysisEntry == null || buffer.AnalysisEntry.SuppressErrorList) {
                return;
            }

            // Use Maybe* variants, since if they haven't been created we don't need to
            // remove our sources.
            buffer.Services.MaybeErrorTaskProvider?.RemoveBufferForErrorSource(buffer.Filename, PythonMoniker, buffer.Buffer);
            buffer.Services.MaybeErrorTaskProvider?.RemoveBufferForErrorSource(buffer.Filename, InvalidEncodingMoniker, buffer.Buffer);
            buffer.Services.MaybeCommentTaskProvider?.RemoveBufferForErrorSource(buffer.Filename, TaskCommentMoniker, buffer.Buffer);
            buffer.Services.MaybeInvalidEncodingSquiggleProvider?.RemoveBuffer(buffer);
        }

        internal void OnAnalysisStarted() {
            AnalysisStarted?.Invoke(this, EventArgs.Empty);
        }

        internal void BufferDetached(AnalysisEntry entry, ITextBuffer buffer) {
            if (entry == null) {
                return;
            }

            var bufferParser = entry.TryGetBufferParser();
            if (bufferParser == null) {
                return;
            }

            if (bufferParser.RemoveBuffer(buffer) == 0) {
                // No buffers remaining, so dispose everything
                bufferParser.Dispose();

                if (!entry.SuppressErrorList) {
                    _services.ErrorTaskProvider?.ClearErrorSource(entry.Path, PythonMoniker);
                    _services.ErrorTaskProvider?.ClearErrorSource(entry.Path, InvalidEncodingMoniker);
                    _services.CommentTaskProvider?.ClearErrorSource(entry.Path, TaskCommentMoniker);
                }

                if (entry.IsTemporaryFile) {
                    UnloadFileAsync(entry)
                        .HandleAllExceptions(_services.Site, GetType())
                        .DoNotWait();
                }
            }
        }

        internal async Task<AnalysisEntry> AnalyzeFileAsync(
            string path,
            string addingFromDirectory = null,
            bool isTemporaryFile = false,
            bool suppressErrorList = false
        ) {
            if (_conn == null) {
                // We aren't able to analyze code, so don't create an entry.
                return null;
            }

            if (string.IsNullOrEmpty(path)) {
                throw new ArgumentNullException(nameof(path));
            }

            AnalysisEntry entry;
            if (_projectFiles.TryGetValue(path, out entry)) {
                return entry;
            }

            Interlocked.Increment(ref _parsePending);
            var response = await SendRequestAsync(
                new AP.AddFileRequest {
                    path = path,
                    addingFromDir = addingFromDirectory,
                    isTemporaryFile = isTemporaryFile,
                    suppressErrorLists = suppressErrorList
                }
            ).ConfigureAwait(false);

            return FinishAnalyze(
                response,
                u => new AnalysisEntry(this, path, u, isTemporaryFile, suppressErrorList)
            );
        }

        internal async Task<AnalysisEntry> AnalyzeFileAsync(
            Uri uri,
            string path,
            bool isTemporaryFile = false,
            bool suppressErrorList = false
        ) {
            if (_conn == null) {
                // We aren't able to analyze code, so don't create an entry.
                return null;
            }

            if (uri == null) {
                throw new ArgumentNullException(nameof(uri));
            }

            AnalysisEntry entry;
            if (_projectFilesByUri.TryGetValue(uri, out entry)) {
                return entry;
            }

            if (string.IsNullOrEmpty(path) && uri.IsFile) {
                path = uri.LocalPath;
            }

            Interlocked.Increment(ref _parsePending);
            var response = await SendRequestAsync(
                new AP.AddFileRequest {
                    uri = uri,
                    isTemporaryFile = isTemporaryFile,
                    suppressErrorLists = suppressErrorList
                }
            ).ConfigureAwait(false);

            return FinishAnalyze(
                response,
                u => new AnalysisEntry(this, path, u, isTemporaryFile, suppressErrorList)
            );
        }

        private AnalysisEntry FinishAnalyze(AP.AddFileResponse response, Func<Uri, AnalysisEntry> entryCreator) {
            if (response?.documentUri == null) {
                Interlocked.Decrement(ref _parsePending);
                if (_conn == null || response == null) {
                    // Cannot analyze code because we have closed while working,
                    // or some other unhandleable error occurred and was logged.
                    // Return null rather than raising an exception
                    return null;
                }
                // TODO: Get SendRequestAsync to return more useful information
                Debug.Fail("Failed to create entry for file");
                return null;
            }

            AnalysisEntry created = null;
            var entry = _projectFilesByUri.GetOrAdd(response.documentUri, u => { return created = entryCreator(u); });

            // we awaited between the check and the AddFileRequest, another add could
            // have snuck in.  So we check again here, and we'll leave the other cookie in 
            // as it's likely a SnapshotCookie which we prefer over a FileCookie.
            if (created != entry) {
                Debug.Assert(entry?.DocumentUri == response.documentUri, $"raced on AnalyzeFile and path '{entry?.DocumentUri}' != '{response.documentUri}'");
                return entry;
            }

            OnAnalysisStarted();
            _projectFilesByUri[entry.DocumentUri] = entry;
            if (!string.IsNullOrEmpty(entry.Path)) {
                _projectFiles[entry.Path] = entry;
                entry.AnalysisCookie = new FileCookie(entry.Path);
            }

            return entry;
        }

        internal async Task<IReadOnlyList<AnalysisEntry>> AnalyzeFileAsync(string[] paths, string addingFromDirectory = null) {
            if (_conn == null) {
                // We aren't able to analyze code, so don't create an entry.
                return Array.Empty<AnalysisEntry>();
            }

            var req = new AP.AddBulkFileRequest { path = new string[paths.Length], addingFromDir = addingFromDirectory };
            bool anyAdded = false;
            AnalysisEntry[] res = new AnalysisEntry[paths.Length];
            for (int i = 0; i < paths.Length; ++i) {
                AnalysisEntry existing;
                if (string.IsNullOrEmpty(paths[i])) {
                    res[i] = null;
                } else if (_projectFiles.TryGetValue(paths[i], out existing)) {
                    res[i] = existing;
                } else {
                    anyAdded = true;
                    req.path[i] = paths[i];
                }
            }

            if (anyAdded) {
                Interlocked.Increment(ref _parsePending);
                var response = await SendRequestAsync(req).ConfigureAwait(false);
                if (response != null) {
                    for (int i = 0; i < paths.Length; ++i) {
                        AnalysisEntry entry = res[i];
                        var path = paths[i];
                        var uri = response.documentUri[i];
                        if (!string.IsNullOrEmpty(path) && uri != null && !_projectFilesByUri.TryGetValue(uri, out entry)) {
                            entry = _projectFilesByUri[uri] = _projectFiles[path] = new AnalysisEntry(this, path, uri);
                            entry.AnalysisCookie = new FileCookie(path);
                        }
                        Debug.Assert(entry != null);
                        res[i] = entry;
                    }
                }
            }

            return res.Where(n => n != null).ToArray();
        }


        internal AnalysisEntry GetAnalysisEntryFromPath(string path) {
            AnalysisEntry res;
            if (!string.IsNullOrEmpty(path) && _projectFiles.TryGetValue(path, out res)) {
                return res;
            }
            return null;
        }

        internal AnalysisEntry GetAnalysisEntryFromUri(Uri uri) {
            AnalysisEntry res;
            if (uri != null && _projectFilesByUri.TryGetValue(uri, out res)) {
                return res;
            }
            return null;
        }

        internal IEnumerable<KeyValuePair<string, AnalysisEntry>> LoadedFiles {
            get {
                return _projectFiles;
            }
        }

        internal async Task<string[]> GetValueDescriptionsAsync(AnalysisEntry file, string expr, SnapshotPoint point) {
            var analysis = await GetExpressionAtPointAsync(point, ExpressionAtPointPurpose.Evaluate, TimeSpan.FromSeconds(1.0)).ConfigureAwait(false);

            if (analysis != null) {
                return await GetValueDescriptionsAsync(file, analysis.Text, analysis.Location).ConfigureAwait(false);
            }

            return Array.Empty<string>();
        }

        internal async Task<string[]> GetValueDescriptionsAsync(AnalysisEntry file, string expr, SourceLocation location) {
            var req = new AP.ValueDescriptionRequest() {
                expr = expr,
                column = location.Column,
                line = location.Line,
                documentUri = file.DocumentUri
            };

            var res = await SendRequestAsync(req).ConfigureAwait(false);
            if (res != null) {
                return res.descriptions;
            }

            return Array.Empty<string>();
        }

        internal string[] GetValueDescriptions(AnalysisEntry entry, string expr, SourceLocation translatedLocation) {
            return entry.Analyzer.GetValueDescriptionsAsync(
                entry,
                expr,
                translatedLocation
            ).WaitOrDefault(DefaultTimeout) ?? Array.Empty<string>();
        }

        internal async Task<ExpressionAnalysis> AnalyzeExpressionAsync(AnalysisEntry entry, string expr, SourceLocation location) {
            Debug.Assert(entry.Analyzer == this);

            var req = new AP.AnalyzeExpressionRequest() {
                expr = expr,
                column = location.Column,
                line = location.Line,
                documentUri = entry.DocumentUri
            };

            var definitions = await SendRequestAsync(req).ConfigureAwait(false);
            if (definitions != null) {
                return new ExpressionAnalysis(
                    expr,
                    null,
                    definitions.variables
                        .Select(ToAnalysisVariable)
                        .Where(v => v != null)
                        .ToArray(),
                    definitions.privatePrefix
                );
            }
            return null;
        }

        internal async Task<ExpressionAnalysis> AnalyzeExpressionAsync(AnalysisEntry entry, SnapshotPoint point, ExpressionAtPointPurpose purpose = ExpressionAtPointPurpose.Evaluate) {
            Debug.Assert(entry.Analyzer == this);

            var analysis = await GetExpressionAtPointAsync(point, purpose, TimeSpan.FromSeconds(1.0)).ConfigureAwait(false);

            if (analysis != null) {
                var location = point.ToSourceLocation();
                var req = new AP.AnalyzeExpressionRequest() {
                    expr = analysis.Text,
                    column = location.Column,
                    line = location.Line,
                    documentUri = entry.DocumentUri
                };

                var definitions = await SendRequestAsync(req).ConfigureAwait(false);

                if (definitions != null) {
                    return new ExpressionAnalysis(
                        analysis.Text,
                        analysis.Span,
                        definitions.variables
                            .Select(ToAnalysisVariable)
                            .Where(x => x != null)
                            .ToArray(),
                        definitions.privatePrefix
                    );
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a list of signatures available for the expression at the provided location in the snapshot.
        /// </summary>
        internal async Task<SignatureAnalysis> GetSignaturesAsync(AnalysisEntry entry, ITextView view, ITextSnapshot snapshot, ITrackingSpan span) {
            var buffer = snapshot.TextBuffer;

            ReverseExpressionParser parser = new ReverseExpressionParser(snapshot, buffer, span);

            var loc = parser.Span.GetSpan(parser.Snapshot.Version);

            int paramIndex;
            SnapshotPoint? sigStart;
            string lastKeywordArg;
            bool isParameterName;
#pragma warning disable CS0618  // See https://github.com/Microsoft/PTVS/issues/3171
            var exprRange = parser.GetExpressionRange(1, out paramIndex, out sigStart, out lastKeywordArg, out isParameterName);
#pragma warning restore CS0618
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

            var result = new List<ISignature>();
            // TODO: Need to deal with version here...
            var location = TranslateIndex(loc.Start, snapshot, entry);
            AP.SignaturesResponse sigs;
            using (new DebugTimer("SignaturesRequest", CompletionAnalysis.TooMuchTime)) {
                sigs = await SendRequestAsync(
                    new AP.SignaturesRequest() {
                        text = text,
                        line = location.Line,
                        column = location.Column,
                        documentUri = entry.DocumentUri
                    }
                ).ConfigureAwait(false);
            }

            if (sigs != null) {
                foreach (var sig in sigs.sigs.MaybeEnumerate()) {
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
            ITextSnapshot analysisSnapshot;
            // TODO: buffers differ in the REPL window case, in the future we should handle this better
            if (toAnalysisSnapshot != null &&
                fromSnapshot != null &&
                (analysisSnapshot = (toAnalysisSnapshot.AnalysisCookie as SnapshotCookie)?.Snapshot) != null &&
                analysisSnapshot.TextBuffer == fromSnapshot.TextBuffer) {

                var fromPoint = new SnapshotPoint(fromSnapshot, index);
                var toPoint = fromPoint.TranslateTo(analysisSnapshot, PointTrackingMode.Negative);
                return new SourceLocation(
                    toPoint.GetContainingLine().LineNumber + 1,
                    (fromPoint - fromPoint.GetContainingLine().Start) + 1
                );
            } else if (fromSnapshot != null) {
                return new SnapshotPoint(fromSnapshot, index).ToSourceLocation();
            } else {
                Debug.Fail("Unable to translate index");
                return new SourceLocation(index, 1, index + 1);
            }
        }

        internal static void AddImport(ITextBuffer textBuffer, string fromModule, string name) {
            var bi = PythonTextBufferInfo.TryGetForBuffer(textBuffer);
            var entry = bi?.AnalysisEntry;
            if (entry == null) {
                Debug.Fail("Cannot add import to buffer with no buffer info");
                return;
            }

            var nl = bi.Services.EditorOptionsFactoryService?.GetOptions(bi.Buffer).GetNewLineCharacter() ?? "\r\n";

            var changes = entry.Analyzer.WaitForRequest(entry.Analyzer.AddImportAsync(
                entry,
                textBuffer,
                fromModule,
                name,
                nl
            ), "ProjectAnalyzer.AddImport");

            if (changes != null) {
                ApplyChanges(
                    changes.changes,
                    bi.Buffer,
                    bi.LocationTracker,
                    changes.version
                );
            }
        }

        internal async Task<bool> IsMissingImportAsync(AnalysisEntry entry, string text, SourceLocation location) {
            var res = await SendRequestAsync(
                new AP.IsMissingImportRequest() {
                    documentUri = entry.DocumentUri,
                    text = text,
                    line = location.Line,
                    column = location.Column
                }
            ).ConfigureAwait(false);

            return res?.isMissing ?? false;
        }

        internal bool IsAnalyzing {
            get {
                return _parsePending > 0 || !_analysisComplete;
            }
        }

        internal bool WaitForAnalysisStarted(TimeSpan timeout) {
            var mre = new ManualResetEventSlim();
            EventHandler evt = (s, e) => mre.Set();
            AnalysisStarted += evt;
            try {
                return mre.Wait(timeout);
            } finally {
                AnalysisStarted -= evt;
                mre.Dispose();
            }
        }

        internal Task WaitForNextCompleteAnalysis() {
            var tcs = Volatile.Read(ref _waitForCompleteAnalysis);
            if (tcs == null) {
                tcs = new TaskCompletionSource<object>();
                tcs = Interlocked.CompareExchange(ref _waitForCompleteAnalysis, tcs, null) ?? tcs;
            }
            return tcs.Task;
        }

        internal void WaitForCompleteAnalysis(Func<int, bool> itemsLeftUpdated) {
            if (IsAnalyzing) {
                while (IsAnalyzing) {
                    var res = SendRequestAsync(new AP.AnalysisStatusRequest()).Result;

                    if (res == null || res.itemsLeft == 0) {
                        itemsLeftUpdated(0);
                        return;
                    }

                    if (!itemsLeftUpdated(res.itemsLeft)) {
                        break;
                    }

                    Thread.Sleep(10);
                }
            } else {
                itemsLeftUpdated(0);
            }
        }

        internal IPythonInterpreterFactory InterpreterFactory {
            get {
                return _interpreterFactory;
            }
        }

        private void OnDiagnostics(AnalysisEntry entry, AP.DiagnosticsEvent diagnostics) {
            var bufferParser = entry.TryGetBufferParser();
            var bi = bufferParser?.DefaultBufferInfo;
            if (entry.Path == null) {
                return;
            }
            bi?.Trace("OnDiagnostics", diagnostics.diagnostics?.Length ?? 0);

            // Update the warn-on-launch state for this entry
            var hasErrors = diagnostics.diagnostics.MaybeEnumerate()
                .Any(d => d.severity == Analysis.DiagnosticSeverity.Error);

            // Update the parser warnings/errors.
            var errorTask = entry.SuppressErrorList ? null : _services.ErrorTaskProvider;
            var commentTask = entry.SuppressErrorList ? null : _services.CommentTaskProvider;

            if (errorTask != null || commentTask != null) {
                var pyErrors = diagnostics.diagnostics.MaybeEnumerate()
                    .GroupBy(d => d.source);

                var errorMonikers = new HashSet<string> { PythonMoniker };
                var commentMonikers = new HashSet<string> { TaskCommentMoniker };

                if (pyErrors.Any()) {
                    var factory = new TaskProviderItemFactory(bi?.LocationTracker, diagnostics.version);
                    foreach (var g in pyErrors) {
                        Debug.Assert(g.Key == PythonMoniker || g.Key == TaskCommentMoniker, $"Unexpected source {g.Key}");

                        bool isComment = (g.Key == TaskCommentMoniker);
                        var items = g.Select(e => factory.FromDiagnostic(
                            _services.Site,
                            e,
                            isComment ? VSTASKCATEGORY.CAT_COMMENTS : VSTASKCATEGORY.CAT_CODESENSE,
                            !isComment
                        )).ToList();

                        if (isComment) {
                            commentTask?.ReplaceItems(entry.Path, g.Key, items);
                            commentMonikers.Remove(g.Key);
                        } else {
                            errorTask?.ReplaceItems(entry.Path, g.Key, items);
                            errorMonikers.Remove(g.Key);
                        }
                    }
                }

                foreach (var moniker in errorMonikers) {
                    errorTask?.Clear(entry.Path, moniker);
                }
                foreach (var moniker in commentMonikers) {
                    commentTask?.Clear(entry.Path, moniker);
                }
            }

            bool changed = false;
            lock (_hasParseErrorsLock) {
                changed = hasErrors ? _hasParseErrors.Add(entry) : _hasParseErrors.Remove(entry);
            }
            if (changed) {
                ShouldWarnOnLaunchChanged?.Invoke(this, new EntryEventArgs(entry));
            }
        }

        private void OnParseComplete(AnalysisEntry entry, AP.FileParsedEvent parsedEvent) {
            var bufferParser = entry.TryGetBufferParser();

            Debug.WriteLine("Received updated parse {0} {1}", parsedEvent.documentUri, parsedEvent.version);

            if (bufferParser != null) {
                var textBuffer = bufferParser.DefaultBufferInfo;
                if (textBuffer == null) {
                    // ignore unexpected buffer ID
                    return;
                }

                if (!textBuffer.UpdateLastReceivedParse(parsedEvent.version)) {
                    // ignore receiving responses out of order...
                    Debug.WriteLine("Ignoring unexpected parse {0}", parsedEvent.version);
                    return;
                }
            }

            entry.OnParseComplete();
        }

        #region Implementation Details

        private SignatureAnalysis TryGetLiveSignatures(ITextSnapshot snapshot, int paramIndex, string text, ITrackingSpan applicableSpan, string lastKeywordArg) {
            var eval = snapshot.TextBuffer.GetInteractiveWindow()?.Evaluator as IPythonInteractiveIntellisense;
            if (eval != null) {
                if (text.EndsWithOrdinal("(")) {
                    text = text.Substring(0, text.Length - 1);
                }
                var liveSigs = eval.GetSignatureDocumentation(text);

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

        internal bool ShouldEvaluateForCompletion(string source) {
            switch (_services.Python.InteractiveOptions.CompletionMode) {
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

        private static Stopwatch MakeStopWatch() {
            var res = new Stopwatch();
            res.Start();
            return res;
        }

        internal async Task UnloadFileAsync(AnalysisEntry entry) {
            _analysisComplete = false;

            var bp = entry?.TryGetBufferParser();
            if (bp != null) {
                bp.ClearBuffers();
            } else {
                _services.MaybeErrorTaskProvider?.ClearErrorSource(entry.Path, PythonMoniker);
                _services.MaybeErrorTaskProvider?.ClearErrorSource(entry.Path, InvalidEncodingMoniker);
                _services.MaybeCommentTaskProvider?.ClearErrorSource(entry.Path, TaskCommentMoniker);
            }
            if (entry?.Path != null) {
                _projectFiles.TryRemove(entry.Path, out _);
            }
            if (entry?.DocumentUri != null) {
                _projectFilesByUri.TryRemove(entry.DocumentUri, out _);
                await SendRequestAsync(new AP.UnloadFileRequest() { documentUri = entry.DocumentUri }).ConfigureAwait(false);
            }
        }

        internal void ClearAllTasks() {
            _services.MaybeErrorTaskProvider?.ClearAll();
            _services.MaybeCommentTaskProvider?.ClearAll();

            lock (_hasParseErrorsLock) {
                _hasParseErrors.Clear();
            }
        }

        internal bool ShouldWarnOnLaunch(AnalysisEntry entry) {
            lock (_hasParseErrorsLock) {
                return _hasParseErrors.Contains(entry);
            }
        }

        internal event EventHandler<EntryEventArgs> ShouldWarnOnLaunchChanged;

        #endregion

        internal async Task<U> SendLanguageServerRequestAsync<T, U>(
            string name,
            T requestParams,
            U defaultValue = default(U),
            TimeSpan? timeout = null
        ) {
            var r = await SendRequestAsync(new AP.LanguageServerRequest {
                name = name,
                body = requestParams
            }).ConfigureAwait(false);
            if (r == null) {
                return defaultValue;
            }
            if (!string.IsNullOrEmpty(r.error)) {
                Debug.WriteLine($"Request failed: {r.error}");
                _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationFailed, r.error);
                return defaultValue;
            }

            if (typeof(U).IsArray && r.body is Newtonsoft.Json.Linq.JArray arr) {
                try {
                    var el = typeof(U).GetElementType();
                    var result = Array.CreateInstance(el, arr.Count);
                    for (int i = 0; i < arr.Count; ++i) {
                        result.SetValue(arr[i].ToObject(el), i);
                    }
                    var response = (U)(object)result;
                    return response;
                } catch (Exception e) {
                    Debug.WriteLine($"Response failed: {e}");
                    _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationFailed, e.Message);
                }
            }
            if (r.body is Newtonsoft.Json.Linq.JToken o) {
                try {
                    var response = o.ToObject<U>();
                    return response;
                } catch (Newtonsoft.Json.JsonException e) {
                    Debug.WriteLine($"Response failed: {e}");
                    _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationFailed, e.Message);
                }
            }
            return defaultValue;
        }

        internal async Task<T> SendRequestAsync<T>(
            Request<T> request,
            T defaultValue = default(T),
            TimeSpan? timeout = null
        ) where T : Response, new() {
            var conn = _conn;
            if (conn == null) {
                return default(T);
            }
            Debug.WriteLine(String.Format("{1} Sending request {0}", request, DateTime.Now));
            T res = defaultValue;
            CancellationToken cancel;
            CancellationTokenSource linkedSource = null, timeoutSource = null;

            try {
                cancel = _processExitedCancelSource.Token;
            } catch (ObjectDisposedException) {
                // Raced with disposal
                return res;
            }

            if (timeout.HasValue) {
                timeoutSource = new CancellationTokenSource(timeout.Value);
                linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancel, timeoutSource.Token);
                cancel = linkedSource.Token;
            }

            try {
                res = await conn.SendRequestAsync(request, cancel).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                Debug.WriteLine($"Request cancelled");
                _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationCancelled, null);
            } catch (IOException e) {
                Debug.WriteLine($"Request failed: {e}");
                _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationCancelled, null);
            } catch (FailedRequestException e) {
                Debug.WriteLine($"Request failed: {e}");
                _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationFailed, e.Message);
            } catch (ObjectDisposedException) {
                _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationCancelled, null);
            } finally {
                linkedSource?.Dispose();
                timeoutSource?.Dispose();
            }
            Debug.WriteLine(String.Format("{1} Done sending request {0}", request, DateTime.Now));
            return res;
        }

        internal async Task SendEventAsync(Event eventValue) {
            var conn = _conn;
            if (conn == null) {
                return;
            }
            Debug.WriteLine(String.Format("{1} Sending event {0}", eventValue.name, DateTime.Now));
            try {
                await conn.SendEventAsync(eventValue).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationCancelled, null);
            } catch (IOException) {
                _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationCancelled, null);
            } catch (FailedRequestException e) {
                _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationFailed, e.Message);
            }
            Debug.WriteLine(String.Format("{1} Done sending event {0}", eventValue.name, DateTime.Now));
        }

        internal async void SendEvent(Event eventValue) {
            try {
                await SendEventAsync(eventValue);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                // Nothing we can do now except crash. We'll log it instead
                _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationFailed, ex.ToString());
                Debug.Fail("Unexpected error sending event");
            }
        }

        internal async Task<LS.CompletionList> GetCompletionsAsync(AnalysisEntry entry, SourceLocation location, GetMemberOptions options, LS.CompletionTriggerKind triggerKind, string triggerChar) {
            return await SendLanguageServerRequestAsync<LS.CompletionParams, LS.CompletionList>(
                "textDocument/completion",
                new LS.CompletionParams {
                    textDocument = new Uri(entry.DocumentUri.AbsoluteUri),
                    position = location,
                    context = new LS.CompletionContext {
                        _intersection = options.Intersect(),
                        triggerKind = triggerKind,
                        triggerCharacter = triggerChar
                    }
                }
            ).ConfigureAwait(false);
        }

        internal async Task<IEnumerable<CompletionResult>> GetAllAvailableMembersAsync(AnalysisEntry entry, SourceLocation location, GetMemberOptions options) {
            var members = await SendRequestAsync(new AP.CompletionsRequest {
                documentUri = entry.DocumentUri,
                options = options,
                line = location.Line,
                column = location.Column
            }).ConfigureAwait(false);

            if (members != null) {
                return ConvertMembers(members.completions);
            }

            return Enumerable.Empty<CompletionResult>();
        }

        private static CompletionResult ToMemberResult(AP.Completion member) {
            return new CompletionResult(
                member.name,
                member.name,
                member.completion ?? member.name,
                member.doc,
                member.memberType,
                member.detailedValues
            );
        }

        internal async Task<IEnumerable<CompletionResult>> GetMembersAsync(AnalysisEntry entry, string text, SourceLocation location, GetMemberOptions options) {
            AP.CompletionsResponse members;
            using (new DebugTimer("GetMembersAsync")) {
                members = await SendRequestAsync(new AP.CompletionsRequest() {
                    documentUri = entry.DocumentUri,
                    text = text,
                    options = options,
                    line = location.Line,
                    column = location.Column
                }).ConfigureAwait(false);
            }

            if (members != null) {
                return ConvertMembers(members.completions);
            }

            return Enumerable.Empty<CompletionResult>();
        }

        private IEnumerable<CompletionResult> ConvertMembers(AP.Completion[] completions) {
            foreach (var member in completions.MaybeEnumerate()) {
                yield return ToMemberResult(member);
            }
        }

        internal Task<IEnumerable<CompletionResult>> GetModulesAsync() => GetModulesAsync(null, null);

        internal async Task<IEnumerable<CompletionResult>> GetModulesAsync(AnalysisEntry entry, string[] package) {
            var req = new AP.GetModulesRequest {
                package = package
            };
            if (entry != null) {
                req.documentUri = entry.DocumentUri;
            }

            var members = await SendRequestAsync(req).ConfigureAwait(false);

            if (members != null) {
                return ConvertMembers(members.completions);
            }

            return Enumerable.Empty<CompletionResult>();
        }

        internal async Task<string[]> FindMethodsAsync(AnalysisEntry entry, ITextBuffer textBuffer, string className, int? paramCount) {
            var res = await SendRequestAsync(
                new AP.FindMethodsRequest() {
                    documentUri = entry.DocumentUri,
                    className = className,
                    paramCount = paramCount
                }
            ).ConfigureAwait(false);

            return res?.names ?? Array.Empty<string>();
        }

        internal async Task<InsertionPoint> GetInsertionPointAsync(ITextSnapshot textSnapshot, string className, AnalysisEntry entry = null) {
            if (textSnapshot == null) {
                return null;
            }
            var bi = _services.GetBufferInfo(textSnapshot.TextBuffer);
            entry = entry ?? bi?.AnalysisEntry;
            if (entry == null) {
                return null;
            }

            var res = await SendRequestAsync(
                new AP.MethodInsertionLocationRequest() {
                    documentUri = entry.DocumentUri,
                    className = className
                }
            ).ConfigureAwait(false);
            if (res == null) {
                return null;
            }

            return new InsertionPoint(
                bi.LocationTracker.Translate(new SourceLocation(res.line, res.column), res.version, textSnapshot),
                res.column - 1
            );
        }

        internal async Task<AP.MethodInfoResponse> GetMethodInfoAsync(AnalysisEntry entry, ITextBuffer textBuffer, string className, string methodName) {
            return await SendRequestAsync(
                new AP.MethodInfoRequest() {
                    documentUri = entry.DocumentUri,
                    className = className,
                    methodName = methodName
                }
            ).ConfigureAwait(false);
        }


        private struct RequestInfo<T, U> {
            public T Value;
            public TaskCompletionSource<U> Task;
        }

        private async Task<U> EnsureSingleRequest<T, U>(
            object key,
            T value,
            Func<T, bool> valueMatches,
            Func<Task<U>> performRequest,
            U defaultValue = default(U)
        ) {
            object o;
            RequestInfo<T, U> info = default(RequestInfo<T, U>);
            // Spin on trying to get an existing request or add
            // a new one. We should not normally get through this
            // loop more than once, but it's possible under
            // certain race conditions.
            while (!_disposing) {
                if (_activeRequests.TryGetValue(key, out o)) {
                    var t = (RequestInfo<T, U>)o;
                    if (valueMatches(t.Value)) {
                        // Same request is already pending
                        Debug.WriteLine($"Warning: request {key}({value}) is already pending");
                        return await t.Task.Task.ConfigureAwait(false);
                    } else {
                        // Wait for the pending task and then
                        // start a new one
                        try {
                            await t.Task.Task.ConfigureAwait(false);
                        } catch (Exception ex) when (!ex.IsCriticalException()) {
                        }
                    }
                }
                if (info.Task == null) {
                    info.Value = value;
                    info.Task = new TaskCompletionSource<U>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
                if (_activeRequests.TryAdd(key, info)) {
                    // We are now the active task, so perform the request
                    // and then set the result of the stored Task.
                    try {
                        var result = await performRequest().ConfigureAwait(false);
                        info.Task.TrySetResult(result);
                        return result;
                    } catch (OperationCanceledException) {
                        info.Task.TrySetCanceled();
                        throw;
                    } catch (Exception ex) {
                        info.Task.TrySetException(ex);
                        throw;
                    } finally {
                        object removed;
                        _activeRequests.TryRemove(key, out removed);
                    }
                }
            }
            return defaultValue;
        }

        private bool EndRequest(object key) {
            object o;
            return _activeRequests.TryRemove(key, out o);
        }

        internal Task<AP.AnalysisClassificationsResponse> GetAnalysisClassificationsAsync(PythonTextBufferInfo buffer, bool colorNames, AnalysisEntry entry) {
            if (entry == null) {
                return Task.FromResult<AP.AnalysisClassificationsResponse>(null);
            }

            return EnsureSingleRequest(
                typeof(AP.AnalysisClassificationsRequest),
                entry,
                n => n == entry,
                async () => await SendRequestAsync(
                    new AP.AnalysisClassificationsRequest() {
                        documentUri = entry.DocumentUri,
                        colorNames = colorNames
                    }
                ).ConfigureAwait(false)
            );
        }

        internal async Task<AP.LocationNameResponse> GetNameOfLocationAsync(AnalysisEntry entry, ITextBuffer textBuffer, int line, int column) {
            return await SendRequestAsync(
                new AP.LocationNameRequest() {
                    documentUri = entry.DocumentUri,
                    line = line,
                    column = column
                }
            ).ConfigureAwait(false);
        }

        internal async Task<string[]> GetProximityExpressionsAsync(AnalysisEntry entry, ITextBuffer textBuffer, int line, int column, int lineCount) {
            return (await SendRequestAsync(
                new AP.ProximityExpressionsRequest() {
                    documentUri = entry.DocumentUri,
                    line = line,
                    column = column,
                    lineCount = lineCount
                }
            ).ConfigureAwait(false))?.names ?? Array.Empty<string>();
        }

        internal async Task FormatCodeAsync(SnapshotSpan span, ITextView view, CodeFormattingOptions options, bool selectResult) {
            var bi = _services.GetBufferInfo(span.Snapshot.TextBuffer);
            if (bi == null) {
                return;
            }

            var entry = await bi.GetAnalysisEntryAsync(CancellationToken.None);
            if (entry == null) {
                return;
            }

            await entry.EnsureCodeSyncedAsync(bi.Buffer);

            var lastSnapshot = bi.LastSentSnapshot;
            var formatSpan = span.Snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive).GetSpan(lastSnapshot).ToSourceSpan();

            var res = await SendRequestAsync(
                new AP.FormatCodeRequest {
                    documentUri = entry.DocumentUri,
                    startLine = formatSpan.Start.Line,
                    startColumn = formatSpan.Start.Column,
                    endLine = formatSpan.End.Line,
                    endColumn = formatSpan.End.Column,
                    options = options,
                    newLine = view.Options.GetNewLineCharacter()
                }
            );

            if (res != null && res.version >= 0) {
                var selectionSpan = span.Snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);

                ApplyChanges(res.changes, bi.Buffer, bi.LocationTracker, res.version);

                if (selectResult && !view.IsClosed) {
                    view.Selection.Select(selectionSpan.GetSpan(bi.CurrentSnapshot), false);
                }
            }
        }

        internal async Task RemoveImportsAsync(SnapshotPoint loc, bool allScopes) {
            var bi = _services.GetBufferInfo(loc.Snapshot.TextBuffer);
            if (bi == null) {
                return;
            }
            var entry = await bi.GetAnalysisEntryAsync(CancellationToken.None);
            if (entry == null) {
                Debug.Fail("Failed to get analysis entry");
                return;
            }

            await entry.EnsureCodeSyncedAsync(bi.Buffer);
            var sloc = loc.ToSourceLocation();

            var res = await SendRequestAsync(
                new AP.RemoveImportsRequest() {
                    documentUri = entry.DocumentUri,
                    version = loc.Snapshot.Version.VersionNumber,
                    allScopes = allScopes,
                    line = sloc.Line,
                    column = sloc.Column
                }
            );

            if (res != null) {
                ApplyChanges(
                    res.changes,
                    bi.Buffer,
                    bi.LocationTracker,
                    res.version
                );
            }
        }

        internal async Task<IEnumerable<ExportedMemberInfo>> FindNameInAllModulesAsync(string name, CancellationToken cancel = default(CancellationToken)) {
            CancellationTokenRegistration registration1 = default(CancellationTokenRegistration), registration2 = default(CancellationTokenRegistration);
            if (cancel.CanBeCanceled) {
                CancellationTokenSource source = new CancellationTokenSource();
                registration1 = cancel.Register(() => source.Cancel());
                registration2 = _processExitedCancelSource.Token.Register(() => source.Cancel());
                cancel = source.Token;
            } else {
                cancel = _processExitedCancelSource.Token;
            }

            var conn = _conn;
            if (conn == null) {
                return new ExportedMemberInfo[0];
            }

            try {
                return (await conn.SendRequestAsync(new AP.AvailableImportsRequest() {
                    name = name
                }, cancel)).imports.Select(x => new ExportedMemberInfo(x.fromName, x.importName));
            } catch (OperationCanceledException) {
                _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationCancelled, null);
            } catch (FailedRequestException e) {
                _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationFailed, e.Message);
            } catch (ObjectDisposedException) {
                _logger?.LogEvent(Logging.PythonLogEvent.AnalysisOperationCancelled, null);
            } finally {
                registration1.Dispose();
                registration2.Dispose();
            }
            return Enumerable.Empty<ExportedMemberInfo>();
        }

        internal async Task<AP.ExtractMethodResponse> ExtractMethodAsync(PythonTextBufferInfo buffer, ITextView view, string name, string[] parameters, int? targetScope = null) {
            await buffer.AnalysisEntry.EnsureCodeSyncedAsync(buffer.Buffer);
            var selection = view.GetPythonSelection().First();

            return await SendRequestAsync(new AP.ExtractMethodRequest {
                documentUri = buffer.AnalysisEntry.DocumentUri,
                indentSize = view.Options.GetIndentSize(),
                convertTabsToSpaces = view.Options.IsConvertTabsToSpacesEnabled(),
                newLine = view.Options.GetNewLineCharacter(),
                startIndex = selection.Start.Position,
                endIndex = selection.End.Position,
                parameters = parameters,
                name = name,
                scope = targetScope,
                shouldExpandSelection = true
            });
        }

        internal async Task<AP.AddImportResponse> AddImportAsync(AnalysisEntry entry, ITextBuffer textBuffer, string fromModule, string name, string newLine) {
            return await SendRequestAsync(
                new AP.AddImportRequest() {
                    fromModule = fromModule,
                    name = name,
                    documentUri = entry.DocumentUri,
                    newLine = newLine
                }
            ).ConfigureAwait(false);
        }

        private void CommentTaskTokensChanged(object sender, EventArgs e) {
            var provider = (CommentTaskProvider)sender;

            lock (_analysisOptions) {
                foreach (var keyValue in (provider?.Tokens).MaybeEnumerate()) {
                    var sev = Analysis.DiagnosticSeverity.Suppressed;
                    switch (keyValue.Value) {
                        case VSTASKPRIORITY.TP_HIGH:
                            sev = Analysis.DiagnosticSeverity.Error;
                            break;
                        case VSTASKPRIORITY.TP_NORMAL:
                            sev = Analysis.DiagnosticSeverity.Warning;
                            break;
                        case VSTASKPRIORITY.TP_LOW:
                            sev = Analysis.DiagnosticSeverity.Information;
                            break;
                    }
                    _analysisOptions.commentTokens[keyValue.Key] = sev;
                }
            }
            SendRequestAsync(new AP.SetAnalysisOptionsRequest { options = _analysisOptions })
                .HandleAllExceptions(Site, GetType())
                .DoNotWait();
        }

        internal async Task<NavigationInfo> GetNavigationsAsync(ITextSnapshot snapshot) {
            var bi = _services.GetBufferInfo(snapshot.TextBuffer);
            var entry = bi?.AnalysisEntry;
            if (entry == null) {
                return NavigationInfo.Empty;
            }

            var navigations = await SendRequestAsync(
                new AP.NavigationRequest() {
                    documentUri = entry.DocumentUri
                }
            );

            if (navigations?.navigations == null || !bi.LocationTracker.CanTranslateFrom(navigations.version)) {
                return NavigationInfo.Empty;
            }

            return new NavigationInfo(
                null,
                NavigationKind.None,
                new SnapshotSpan(),
                ConvertNavigations(navigations.navigations, bi.LocationTracker, navigations.version, snapshot)
            );
        }

        private NavigationInfo[] ConvertNavigations(AP.Navigation[] navigations, LocationTracker translator, int fromVersion, ITextSnapshot snapshot) {
            if (navigations == null || translator == null) {
                return null;
            }

            List<NavigationInfo> res = new List<NavigationInfo>();
            foreach (var nav in navigations.OrderBy(n => n.name, CompletionComparer.UnderscoresLast)) {
                // translate the span from the version we last parsed to the current version

                var span = translator.Translate(
                    new SourceSpan(
                        new SourceLocation(nav.startLine, nav.startColumn),
                        new SourceLocation(nav.endLine, nav.endColumn)
                    ),
                    fromVersion,
                    snapshot
                );

                res.Add(
                    new NavigationInfo(
                        nav.name,
                        GetNavigationKind(nav.type),
                        span,
                        ConvertNavigations(nav.children, translator, fromVersion, snapshot)
                    )
                );
            }
            return res.ToArray();
        }

        internal ProjectReference[] GetReferences() {
            lock (_references) {
                return _references.ToArray();
            }
        }

        internal async Task<AP.AddReferenceResponse> AddReferenceAsync(ProjectReference reference, CancellationToken token = default(CancellationToken)) {
            lock (_references) {
                _references.Add(reference);
            }
            return await SendRequestAsync(new AP.AddReferenceRequest() {
                reference = AP.ProjectReference.Convert(reference)
            }).ConfigureAwait(false);
        }

        internal async Task<AP.RemoveReferenceResponse> RemoveReferenceAsync(ProjectReference reference) {
            lock (_references) {
                _references.Remove(reference);
            }
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

        internal async Task<IEnumerable<OutliningTaggerProvider.TagSpan>> GetOutliningTagsAsync(ITextSnapshot snapshot) {
            var bi = _services.GetBufferInfo(snapshot.TextBuffer);
            var entry = bi?.AnalysisEntry;
            if (entry == null) {
                return null;
            }

            var outliningTags = await SendRequestAsync(
                new AP.OutliningRegionsRequest() {
                    documentUri = entry.DocumentUri
                }
            );

            if (outliningTags != null) {
                return ConvertOutliningTags(bi.LocationTracker, outliningTags.version, outliningTags.tags);
            }

            return null;
        }

        private static IEnumerable<OutliningTaggerProvider.TagSpan> ConvertOutliningTags(LocationTracker translator, int atVersion, AP.OutliningTag[] tags) {
            var snapshot = translator.TextBuffer.CurrentSnapshot;
            foreach (var tag in tags) {
                // translate the span from the version we last parsed to the current version
                SnapshotSpan span;

                var start = new SourceLocation(tag.startLine, tag.startCol);
                var end = new SourceLocation(tag.endLine, tag.endCol);

                try {
                    span = translator.Translate(new SourceSpan(start, end), atVersion, snapshot);
                } catch (ArgumentOutOfRangeException) {
                    // Failed to get the "correct" span, so skip this tag
                    continue;
                }

                if (span.Length > 0) {
                    yield return OutliningTaggerProvider.OutliningTagger.GetTagSpan(span.Start, span.End);
                }
            }
        }

        internal static void ApplyChanges(AP.ChangeInfo[] changes, ITextBuffer textBuffer, LocationTracker translator, int atVersion) {
            if (translator == null) {
                throw new ArgumentNullException(nameof(translator));
            }

            using (var edit = textBuffer.CreateEdit()) {
                foreach (var change in changes.MaybeEnumerate()) {
                    var span = translator.Translate(new SourceSpan(
                        new SourceLocation(change.startLine, change.startColumn),
                        new SourceLocation(change.endLine, change.endColumn)
                    ), atVersion, edit.Snapshot);

                    if (string.IsNullOrEmpty(change.newText)) {
                        edit.Delete(span);
                    } else {
                        edit.Replace(span, change.newText);
                    }
                }
                edit.Apply();
            }
        }

        internal async Task<QuickInfo> GetQuickInfoAsync(AnalysisEntry entry, ITextView view, SnapshotPoint point) {
            Debug.Assert(entry.Analyzer == this);

            var analysis = await GetExpressionAtPointAsync(point, ExpressionAtPointPurpose.Hover, TimeSpan.FromMilliseconds(200.0)).ConfigureAwait(false);
            
            if (analysis?.Entry != null) {
                var line = point.GetContainingLine();
                var req = new AP.QuickInfoRequest() {
                    expr = analysis.Text,
                    column = point.Position - line.Start + 1,
                    line = line.LineNumber + 1,
                    documentUri = analysis.Entry.DocumentUri
                };

                var quickInfo = await SendRequestAsync(req).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(quickInfo?.text)) {
                    return new QuickInfo(quickInfo.text, analysis.Span);
                }
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

            var file = arg.file;
            if (string.IsNullOrEmpty(file)) {
                try {
                    file = arg.documentUri?.LocalPath;
                } catch (InvalidOperationException) {
                }
                if (!File.Exists(file)) {
                    return null;
                }
            }

            var location = new LocationInfo(
                file,
                arg.documentUri,
                arg.startLine,
                arg.startColumn,
                arg.endLine,
                arg.endColumn
            );

            return new AnalysisVariable(type, location, arg.version ?? -1);
        }

        internal async Task<ExpressionAtPoint> GetExpressionAtPointAsync(SnapshotPoint point, ExpressionAtPointPurpose purpose, TimeSpan timeout) {
            var timer = MakeStopWatch();
            try {
                return await GetExpressionAtPointAsync_BypassTelemetry(point, (AP.ExpressionAtPointPurpose)purpose, timeout).ConfigureAwait(false);
            } finally {
                LogTimingEvent("GetExpressionAtPoint", timer.ElapsedMilliseconds, (long)timeout.TotalMilliseconds);
            }
        }

        internal async Task<SourceSpan?> GetExpressionSpanAtPointAsync(PythonTextBufferInfo buffer, SourceLocation point, ExpressionAtPointPurpose purpose, TimeSpan timeout) {
            var timer = MakeStopWatch();
            try {
                return await GetExpressionSpanAtPointAsync_BypassTelemetry(buffer, point, (AP.ExpressionAtPointPurpose)purpose, timeout).ConfigureAwait(false);
            } finally {
                LogTimingEvent("GetExpressionSpanAtPoint", timer.ElapsedMilliseconds, (long)timeout.TotalMilliseconds);
            }
        }

        private static async Task<ExpressionAtPoint> GetExpressionAtPointAsync_BypassTelemetry(SnapshotPoint point, AP.ExpressionAtPointPurpose purpose, TimeSpan timeout) {
            var bi = PythonTextBufferInfo.TryGetForBuffer(point.Snapshot.TextBuffer);
            if (bi == null) {
                return null;
            }

            var line = point.GetContainingLine();

            var sourceSpan = await GetExpressionSpanAtPointAsync_BypassTelemetry(
                bi,
                new SourceLocation(point.Position, line.LineNumber + 1, point - line.Start + 1),
                purpose,
                timeout
            );

            if (!sourceSpan.HasValue) {
                return null;
            }

            SnapshotSpan span;
            try {
                span = sourceSpan.Value.ToSnapshotSpan(point.Snapshot);
            } catch (ArgumentException) {
                return null;
            }
            return new ExpressionAtPoint(
                bi.AnalysisEntry,
                span.GetText(),
                span.Snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive),
                sourceSpan.Value
            );
        }

        private static async Task<SourceSpan?> GetExpressionSpanAtPointAsync_BypassTelemetry(PythonTextBufferInfo buffer, SourceLocation point, AP.ExpressionAtPointPurpose purpose, TimeSpan timeout) {
            if (buffer == null) {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (buffer.AnalysisEntry == null) {
                return null;
            }

            var r = await buffer.AnalysisEntry.Analyzer.SendRequestAsync(new AP.ExpressionAtPointRequest {
                purpose = purpose,
                documentUri = buffer.AnalysisEntry.DocumentUri,
                line = point.Line,
                column = point.Column
            }, timeout: timeout).ConfigureAwait(false);

            if (r == null) {
                return null;
            }

            return new SourceSpan(new SourceLocation(r.startLine, r.startColumn), new SourceLocation(r.endLine, r.endColumn));
        }
    }
}
