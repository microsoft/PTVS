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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Ipc.Json;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudioTools;
using LDP = Microsoft.PythonTools.Debugger.LegacyDebuggerProtocol;

namespace Microsoft.PythonTools.Debugger {
    /// <summary>
    /// Handles all interactions with a Python process which is being debugged.
    /// </summary>
    class PythonProcess : IDisposable {
        private readonly Process _process;
        private readonly ConcurrentDictionary<long, PythonThread> _threads = new ConcurrentDictionary<long, PythonThread>();
        private readonly ConcurrentDictionary<int, PythonBreakpoint> _breakpoints = new ConcurrentDictionary<int, PythonBreakpoint>();
        private readonly IdDispenser _ids = new IdDispenser();
        private readonly Dictionary<int, CompletionInfo> _pendingExecutes = new Dictionary<int, CompletionInfo>();
        private readonly Dictionary<int, ChildrenInfo> _pendingChildEnums = new Dictionary<int, ChildrenInfo>();
        private readonly List<TaskCompletionSource<int>> _pendingGetThreadFramesRequests = new List<TaskCompletionSource<int>>();
        private readonly object _pendingGetThreadFramesRequestsLock = new object();
        private readonly PythonLanguageVersion _langVersion;
        private readonly Guid _processGuid = Guid.NewGuid();
        private readonly List<string[]> _dirMapping;
        private readonly object _connectionLock = new object();
        private readonly Dictionary<string, PythonAst> _astCache = new Dictionary<string, PythonAst>();
        private readonly object _astCacheLock = new object();
        private readonly AutoResetEvent _connectedEvent = new AutoResetEvent(false);

        private int _pid;
        private bool _sentExited, _startedProcess;
        private DebugConnection _connection;
        private int _breakpointCounter;
        private bool _createdFirstThread;
        private bool _stoppedForException;
        private int _defaultBreakMode;
        private ICollection<KeyValuePair<string, int>> _breakOn;
        private bool _handleEntryPointHit = true;
        private bool _handleEntryPointBreakpoint = true;
        private bool _isDisposed;

        protected PythonProcess(int pid, PythonLanguageVersion languageVersion) {
            if (languageVersion < PythonLanguageVersion.V26 && !languageVersion.IsNone()) {
                throw new NotSupportedException(Strings.DebuggerPythonVersionNotSupported);
            }

            _pid = pid;
            _langVersion = languageVersion;
            _dirMapping = new List<string[]>();
        }

        private PythonProcess(int pid, PythonDebugOptions debugOptions) {
            _pid = pid;
            _process = Process.GetProcessById(pid);
            _process.EnableRaisingEvents = true;
            _process.Exited += new EventHandler(_process_Exited);

            ListenForConnection();

            using (var result = DebugAttach.AttachAD7(pid, DebugConnectionListener.ListenerPort, _processGuid, debugOptions.ToString())) {
                if (result.Error != ConnErrorMessages.None) {
                    throw new ConnectionException(result.Error);
                }

                _langVersion = (PythonLanguageVersion)result.LanguageVersion;
                if (!result.AttachDone.WaitOne(20000)) {
                    throw new ConnectionException(ConnErrorMessages.TimeOut);
                }
            }
        }

        public PythonProcess(PythonLanguageVersion languageVersion, string exe, string args, string dir, string env, string interpreterOptions, PythonDebugOptions options = PythonDebugOptions.None, List<string[]> dirMapping = null)
            : this(0, languageVersion) {

            ListenForConnection();

            if (dir.EndsWith("\\")) {
                dir = dir.Substring(0, dir.Length - 1);
            }
            _dirMapping = dirMapping;
            var processInfo = new ProcessStartInfo(exe);

            processInfo.CreateNoWindow = (options & PythonDebugOptions.CreateNoWindow) != 0;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = false;
            processInfo.RedirectStandardInput = (options & PythonDebugOptions.RedirectInput) != 0;

            processInfo.Arguments = 
                (String.IsNullOrWhiteSpace(interpreterOptions) ? "" : (interpreterOptions + " ")) +
                "\"" + PythonToolsInstallPath.GetFile("ptvsd_launcher.py") + "\" " +
                "\"" + dir + "\" " +
                " " + DebugConnectionListener.ListenerPort + " " +
                " " + _processGuid + " " +
                "\"" + options + "\" " +
                args;

            if (env != null) {
                string[] envValues = env.Split('\0');
                foreach (var curValue in envValues) {
                    string[] nameValue = curValue.Split(new[] { '=' }, 2);
                    if (nameValue.Length == 2 && !String.IsNullOrWhiteSpace(nameValue[0])) {
                        processInfo.EnvironmentVariables[nameValue[0]] = nameValue[1];
                    }
                }
            }

            Debug.WriteLine(String.Format("Launching: {0} {1}", processInfo.FileName, processInfo.Arguments));
            _process = new Process();
            _process.StartInfo = processInfo;
            _process.EnableRaisingEvents = true;
            _process.Exited += new EventHandler(_process_Exited);
        }

        public static PythonProcess Attach(int pid, PythonDebugOptions debugOptions = PythonDebugOptions.None) {
            return new PythonProcess(pid, debugOptions);
        }

        #region Public Process API

        public int Id => _pid;

        public Guid ProcessGuid => _processGuid;

        public bool StoppedForException => _stoppedForException;

        public PythonLanguageVersion LanguageVersion => _langVersion;

        public bool HasExited => _process?.HasExited == true;

        public async Task StartAsync(bool startListening = true) {
            _process.Start();
            _startedProcess = true;
            _pid = _process.Id;
            if (startListening) {
                await StartListeningAsync();
            }
        }

        private void ListenForConnection() {
            DebugConnectionListener.RegisterProcess(_processGuid, this);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal void AddDirMapping(string[] mapping) {
            if (mapping != null) {
                _dirMapping.Add(mapping);
            }
        }

        protected virtual void Dispose(bool disposing) {
            if (_isDisposed) {
                return;
            }
            _isDisposed = true;

            DebugConnectionListener.UnregisterProcess(_processGuid);

            if (disposing) {
                lock (_connectionLock) {
                    if (_connection != null) {
                        _connection.ProcessingMessagesEnded -= OnProcessingMessagesEnded;
                        _connection.LegacyAsyncBreak -= OnLegacyAsyncBreak;
                        _connection.LegacyBreakpointFailed -= OnLegacyBreakpointFailed;
                        _connection.LegacyBreakpointHit -= OnLegacyBreakpointHit;
                        _connection.LegacyBreakpointSet -= OnLegacyBreakpointSet;
                        _connection.LegacyDebuggerOutput -= OnLegacyDebuggerOutput;
                        _connection.LegacyDetach -= OnLegacyDetach;
                        _connection.LegacyEnumChildren -= OnLegacyEnumChildren;
                        _connection.LegacyException -= OnLegacyException;
                        _connection.LegacyExecutionException -= OnLegacyExecutionException;
                        _connection.LegacyExecutionResult -= OnLegacyExecutionResult;
                        _connection.LegacyLast -= OnLegacyLast;
                        _connection.LegacyModuleLoad -= OnLegacyModuleLoad;
                        _connection.LegacyProcessLoad -= OnLegacyProcessLoad;
                        _connection.LegacyRequestHandlers -= OnLegacyRequestHandlers;
                        _connection.LegacyStepDone -= OnLegacyStepDone;
                        _connection.LegacyThreadCreate -= OnLegacyThreadCreate;
                        _connection.LegacyThreadExit -= OnLegacyThreadExit;
                        _connection.LegacyThreadFrameList -= OnLegacyThreadFrameList;
                        _connection.LegacyModulesChanged -= OnLegacyModulesChanged;
                        _connection.Dispose();
                        _connection = null;
                    }
                    // Avoiding ?. syntax because FxCop doesn't understand it
                    if (_process != null) {
                        _process.Dispose();
                    }
                }

                _connectedEvent.Dispose();
            }
        }

        ~PythonProcess() {
            Dispose(false);
        }

        void _process_Exited(object sender, EventArgs e) {
            // TODO: Abort all pending operations
            if (!_sentExited) {
                _sentExited = true;
                var exited = ProcessExited;
                if (exited != null) {
                    int exitCode;
                    try {
                        exitCode = (_process != null && _process.HasExited) ? _process.ExitCode : -1;
                    } catch (InvalidOperationException) {
                        // debug attach, we didn't start the process...
                        exitCode = -1;
                    }
                    exited(this, new ProcessExitedEventArgs(exitCode));
                }
            }
        }

        public void WaitForExit() {
            if (_process == null) {
                throw new InvalidOperationException();
            }
            _process.WaitForExit();
        }

        public bool WaitForExit(int milliseconds) {
            if (_process == null) {
                throw new InvalidOperationException();
            }
            return _process.WaitForExit(milliseconds);
        }

        public void Terminate() {
            if (_process != null && !_process.HasExited) {
                _process.Kill();
            }

            _connection?.Dispose();
        }

        /// <summary>
        /// Breaks into the process.
        /// </summary>
        public async Task BreakAsync(CancellationToken ct) {
            await SendDebugRequestAsync(new LDP.BreakAllRequest(), ct);
        }

        public async Task ResumeAsync(CancellationToken ct) {
            // Resume must be from entry point or past
            _handleEntryPointHit = false;

            _stoppedForException = false;

            await SendDebugRequestAsync(new LDP.ResumeAllRequest(), ct);
        }

        public async Task AutoResumeThread(long threadId, CancellationToken ct) {
            if (_handleEntryPointHit) {
                // Handle entrypoint breakpoint/tracepoint
                var thread = _threads[threadId];
                if (_handleEntryPointBreakpoint) {
                    _handleEntryPointBreakpoint = false;
                    var frames = thread.Frames;
                    if (frames != null && frames.Count() > 0) {
                        var frame = frames[0];
                        if (frame != null) {
                            foreach (var breakpoint in _breakpoints.Values) {
                                // UNDONE Fuzzy filename matching
                                if (breakpoint.LineNo == frame.StartLine && breakpoint.Filename.Equals(frame.FileName, StringComparison.OrdinalIgnoreCase)) {
                                    // UNDONE: Conditional breakpoint/tracepoint
                                    var breakpointHit = BreakpointHit;
                                    if (breakpointHit != null) {
                                        breakpointHit(this, new BreakpointHitEventArgs(breakpoint, thread));
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }

                _handleEntryPointHit = false;
                var entryPointHit = EntryPointHit;
                if (entryPointHit != null) {
                    entryPointHit(this, new ThreadEventArgs(thread));
                    return;
                }
            }

            await SendAutoResumeThreadAsync(threadId, ct);
        }

        public PythonBreakpoint AddBreakpoint(
            string filename,
            int lineNo,
            PythonBreakpointConditionKind conditionKind = PythonBreakpointConditionKind.Always,
            string condition = "",
            PythonBreakpointPassCountKind passCountKind = PythonBreakpointPassCountKind.Always,
            int passCount = 0
        ) {
            int id = _breakpointCounter++;
            var res = new PythonBreakpoint(this, filename, lineNo, conditionKind, condition, passCountKind, passCount, id);
            _breakpoints[id] = res;
            return res;
        }

        public PythonBreakpoint AddDjangoBreakpoint(string filename, int lineNo) {
            int id = _breakpointCounter++;
            var res = new PythonBreakpoint(this, filename, lineNo, PythonBreakpointConditionKind.Always, "", PythonBreakpointPassCountKind.Always, 0 , id, true);
            _breakpoints[id] = res;
            return res;
        }

        public async Task SetExceptionInfoAsync(int defaultBreakOnMode, IEnumerable<KeyValuePair<string, int>> breakOn, CancellationToken ct) {
            Task task = null;

            lock (_connectionLock) {
                if (_connection != null) {
                    task = SendExceptionInfoAsync(defaultBreakOnMode, breakOn, ct);
                } else {
                    // We'll send it when we start listening
                    _breakOn = breakOn.ToArray();
                    _defaultBreakMode = defaultBreakOnMode;
                }
            }

            if (task != null) {
                await task;
            }
        }

        private async Task SendExceptionInfoAsync(int defaultBreakOnMode, IEnumerable<KeyValuePair<string, int>> breakOn, CancellationToken ct) {
            await SendDebugRequestAsync(new LDP.SetExceptionInfoRequest() {
                defaultBreakOnMode = defaultBreakOnMode,
                breakOn = breakOn.Select(pair => new LDP.ExceptionInfo() {
                    name = pair.Key,
                    mode = pair.Value,
                }).ToArray(),
            }, ct);
        }

        #endregion

        #region Debuggee Communication

        internal void Connect(DebugConnection connection) {
            Debug.WriteLine("Process Connected: " + _processGuid);

            EventHandler connected;
            lock (_connectionLock) {
                _connection = connection;
                _connection.SetProcess(_processGuid);
                _connection.ProcessingMessagesEnded += OnProcessingMessagesEnded;
                _connection.LegacyAsyncBreak += OnLegacyAsyncBreak;
                _connection.LegacyBreakpointFailed += OnLegacyBreakpointFailed;
                _connection.LegacyBreakpointHit += OnLegacyBreakpointHit;
                _connection.LegacyBreakpointSet += OnLegacyBreakpointSet;
                _connection.LegacyDebuggerOutput += OnLegacyDebuggerOutput;
                _connection.LegacyDetach += OnLegacyDetach;
                _connection.LegacyEnumChildren += OnLegacyEnumChildren;
                _connection.LegacyException += OnLegacyException;
                _connection.LegacyExecutionException += OnLegacyExecutionException;
                _connection.LegacyExecutionResult += OnLegacyExecutionResult;
                _connection.LegacyLast += OnLegacyLast;
                _connection.LegacyModuleLoad += OnLegacyModuleLoad;
                _connection.LegacyProcessLoad += OnLegacyProcessLoad;
                _connection.LegacyRequestHandlers += OnLegacyRequestHandlers;
                _connection.LegacyStepDone += OnLegacyStepDone;
                _connection.LegacyThreadCreate += OnLegacyThreadCreate;
                _connection.LegacyThreadExit += OnLegacyThreadExit;
                _connection.LegacyThreadFrameList += OnLegacyThreadFrameList;
                _connection.LegacyModulesChanged += OnLegacyModulesChanged;

                // This must be done under the lock so that any handlers that are added after we assigned _connection
                // above won't get called twice, once from Connected add handler, and the second time below. 
                connected = _connected;
            }

            Unregister();

            _connectedEvent.Set();

            connected?.Invoke(this, EventArgs.Empty);
        }

        internal void Unregister() {
            DebugConnectionListener.UnregisterProcess(_processGuid);
        }

        /// <summary>
        /// Starts listening for debugger communication.  Can be called after Start
        /// to give time to attach to debugger events.  This waits for the debuggee
        /// to connect to the socket.
        /// </summary>
        public async Task StartListeningAsync(int timeOutMs = 20000) {
            if (!_connectedEvent.WaitOne(timeOutMs)) {
                throw new ConnectionException(ConnErrorMessages.TimeOut);
            }

            _connection?.WaitForAuthentication();

            if (_breakOn != null) {
                await SendExceptionInfoAsync(_defaultBreakMode, _breakOn, default(CancellationToken));
                _breakOn = null;
            }
        }

        private static string ToDottedNameString(Expression expr, PythonAst ast) {
            NameExpression name;
            MemberExpression member;
            ParenthesisExpression paren;
            if ((name = expr as NameExpression) != null) {
                return name.Name;
            } else if ((member = expr as MemberExpression) != null) {
                while (member.Target is MemberExpression) {
                    member = (MemberExpression)member.Target;
                }
                if (member.Target is NameExpression) {
                    return expr.ToCodeString(ast);
                }
            } else if ((paren = expr as ParenthesisExpression) != null) {
                return ToDottedNameString(paren.Expression, ast);
            }
            return null;
        }

        internal IList<PythonThread> GetThreads() {
            return _threads.Values.ToList();
        }

        internal PythonAst GetAst(string filename) {
            PythonAst ast;
            lock (_astCacheLock) {
                if (_astCache.TryGetValue(filename, out ast)) {
                    return ast;
                }
            }

            try {
                using (var source = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    ast = Parser.CreateParser(source, LanguageVersion).ParseFile();
                }
            } catch (ArgumentException) {
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            } catch (NotSupportedException) {
            } catch (System.Security.SecurityException) {
            }

            lock (_astCacheLock) {
                _astCache[filename] = ast;
            }

            return ast;
        }

        internal IList<Tuple<int, int, IList<string>>> GetHandledExceptionRanges(string filename) {
            PythonAst ast;
            TryHandlerWalker walker = new TryHandlerWalker();
            var statements = new List<Tuple<int, int, IList<string>>>();

            try {
                ast = GetAst(filename);
                if (ast == null) {
                    return statements;
                }
                ast.Walk(walker);
            } catch (Exception ex) {
                Debug.WriteLine("Exception in GetHandledExceptionRanges:");
                Debug.WriteLine(string.Format("Filename: {0}", filename));
                Debug.WriteLine(ex);
                return statements;
            }

            foreach (var statement in walker.Statements) {
                int start = statement.GetStart(ast).Line;
                int end = statement.Body.GetEnd(ast).Line + 1;
                var expressions = new List<string>();

                if (statement.Handlers == null) {
                    if (statement.Finally != null) {
                        // This is a try/finally block without an except, which
                        // means that no exceptions are handled.
                        continue;
                    } else {
                        // If Handlers and Finally are null, there was probably
                        // a parser error. We assume all exceptions are handled
                        // by default, to avoid bothering the user too much, so
                        // handle everything here since we can't be more
                        // accurate.
                        expressions.Add("*");
                    }
                } else {
                    foreach (var handler in statement.Handlers) {
                        Expression expr = handler.Test;
                        TupleExpression tuple;
                        if (expr == null) {
                            expressions.Clear();
                            expressions.Add("*");
                            break;
                        } else if ((tuple = handler.Test as TupleExpression) != null) {
                            foreach (var e in tuple.Items) {
                                var text = ToDottedNameString(e, ast);
                                if (text != null) {
                                    expressions.Add(text);
                                }
                            }
                        
                        } else {
                            var text = ToDottedNameString(expr, ast);
                            if (text != null) {
                                expressions.Add(text);
                            }
                        }
                    }
                }

                if (expressions.Count > 0) {
                    statements.Add(new Tuple<int, int, IList<string>>(start, end, expressions));
                }
            }


            return statements;
        }

        private void OnProcessingMessagesEnded(object sender, EventArgs e) {
            // If the event thread ends, and the process is not controlled by the debugger (e.g. in remote scenarios, where there's no process),
            // make sure that we report ProcessExited event. If the thread has ended gracefully, we have already done this when handling DETC,
            // so it's a no-op. But if connection broke down unexpectedly, no-one knows yet, so we need to tell them.
            if (!_startedProcess) {
                _process_Exited(this, EventArgs.Empty);
            }
        }

        private void OnLegacyRequestHandlers(object sender, LDP.RequestHandlersEvent e) {
            Debug.WriteLine("Exception handlers requested for: " + e.fileName);
            var statements = GetHandledExceptionRanges(e.fileName);

            SendDebugRequestAsync(new LDP.SetExceptionHandlerInfoRequest() {
                fileName = e.fileName,
                statements = statements.Select(s => new LDP.ExceptionHandlerStatement() {
                    lineStart = s.Item1,
                    lineEnd = s.Item2,
                    expressions = s.Item3.ToArray(),
                }).ToArray(),
            }).WaitAndUnwrapExceptions();
        }

        private void OnLegacyDebuggerOutput(object sender, LDP.DebuggerOutputEvent e) {
            PythonThread thread;
            if (!_threads.TryGetValue(e.threadId, out thread)) {
                // Not finding the thread is okay in the case where output
                // comes in as result of code executing in debug REPL at
                // module scope rather than at thread/frame scope.
                // This is because code executed at module scope is done
                // on the debugger event handling thread.
                thread = null;
            }

            DebuggerOutput?.Invoke(this, new OutputEventArgs(thread, e.output, e.isStdOut));
        }

        private void OnLegacyAsyncBreak(object sender, LDP.AsyncBreakEvent e) {
            var thread = _threads[e.threadId];
            Debug.WriteLine("Received async break command from thread {0}", e.threadId);
            AsyncBreakComplete?.Invoke(this, new ThreadEventArgs(thread));
        }

        private void OnLegacyExecutionException(object sender, LDP.ExecutionExceptionEvent e) {
            CompletionInfo completion;
            lock (_pendingExecutes) {
                if (_pendingExecutes.TryGetValue(e.executionId, out completion)) {
                    _pendingExecutes.Remove(e.executionId);
                    _ids.Free(e.executionId);
                } else {
                    Debug.Fail("Received execution result with unknown execution ID " + e.executionId);
                }
            }

            completion?.Completion(new PythonEvaluationResult(this, e.exceptionText, completion.Text, completion.Frame));
        }

        private void OnLegacyExecutionResult(object sender, LDP.ExecutionResultEvent e) {
            CompletionInfo completion;
            lock (_pendingExecutes) {
                if (_pendingExecutes.TryGetValue(e.executionId, out completion)) {
                    _pendingExecutes.Remove(e.executionId);
                    _ids.Free(e.executionId);
                } else {
                    Debug.Fail("Received execution result with unknown execution ID " + e.executionId);
                }
            }

            Debug.WriteLine("Received execution request {0}", e.executionId);
            if (completion != null) {
                var evalResult = ReadPythonObject(e.obj, completion.Text, null, completion.Frame);
                completion.Completion(evalResult);
            } else {
                // Passing null for parameters other than stream is okay as long
                // as we drop the result.
                ReadPythonObject(e.obj, null, null, null);
            }
        }

        private PythonEvaluationResult ReadPythonObject(LDP.PythonObject obj, string expr, string childName, PythonStackFrame frame) {
            var flags = FromLDPEvaluationResultFlags(obj.flags);
            var objRepr = obj.objRepr;
            var hexRepr = obj.hexRepr;

            if (!flags.HasFlag(PythonEvaluationResultFlags.Raw) && ((obj.typeName == "unicode" && LanguageVersion.Is2x()) || (obj.typeName == "str" && LanguageVersion.Is3x()))) {
                objRepr = objRepr.FixupEscapedUnicodeChars();
            }

            if (obj.typeName == "bool") {
                hexRepr = null;
            }

            return new PythonEvaluationResult(this, objRepr, hexRepr, obj.typeName, obj.length, expr, childName, frame, flags);
        }

        private void OnLegacyProcessLoad(object sender, LDP.ProcessLoadEvent e) {
            Debug.WriteLine("Process loaded " + _processGuid);

            // process is loaded, no user code has run
            var thread = _threads[e.threadId];

            ProcessLoaded?.Invoke(this, new ThreadEventArgs(thread));
        }

        private void OnLegacyBreakpointFailed(object sender, LDP.BreakpointFailedEvent e) {
            // break point failed to set
            PythonBreakpoint breakpoint;
            if (_breakpoints.TryGetValue(e.breakpointId, out breakpoint)) {
                BreakpointBindFailed?.Invoke(this, new BreakpointEventArgs(breakpoint));
            }
        }

        private void OnLegacyBreakpointSet(object sender, LDP.BreakpointSetEvent e) {
            // break point successfully set
            PythonBreakpoint unbound;
            if (_breakpoints.TryGetValue(e.breakpointId, out unbound)) {
                BreakpointBindSucceeded?.Invoke(this, new BreakpointEventArgs(unbound));
            }
        }

        private void OnLegacyStepDone(object sender, LDP.StepDoneEvent e) {
            // stepping done
            StepComplete?.Invoke(this, new ThreadEventArgs(_threads[e.threadId]));
        }

        private void OnLegacyModuleLoad(object sender, LDP.ModuleLoadEvent e) {
            // module load
            if (e.moduleFileName != null) {
                Debug.WriteLine(String.Format("Module Loaded ({0}): {1}", e.moduleId, e.moduleFileName));
                var module = new PythonModule(e.moduleId, e.moduleFileName);

                ModuleLoaded?.Invoke(this, new ModuleLoadedEventArgs(module));
            }
        }

        private void OnLegacyThreadExit(object sender, LDP.ThreadExitEvent e) {
            PythonThread thread;
            if (_threads.TryRemove(e.threadId, out thread)) {
                ThreadExited?.Invoke(this, new ThreadEventArgs(thread));
                Debug.WriteLine("Thread exited, {0} active threads", _threads.Count);
            }
        }

        private void OnLegacyThreadCreate(object sender, LDP.ThreadCreateEvent e) {
            // new thread
            var thread = _threads[e.threadId] = new PythonThread(this, e.threadId, _createdFirstThread);
            _createdFirstThread = true;

            ThreadCreated?.Invoke(this, new ThreadEventArgs(thread));
        }

        private void OnLegacyBreakpointHit(object sender, LDP.BreakpointHitEvent e) {
            PythonBreakpoint unboundBreakpoint;
            if (_breakpoints.TryGetValue(e.breakpointId, out unboundBreakpoint)) {
                BreakpointHit?.Invoke(this, new BreakpointHitEventArgs(unboundBreakpoint, _threads[e.threadId]));
            } else {
                SendResumeThreadAsync(e.threadId, default(CancellationToken)).WaitAndUnwrapExceptions();
            }
        }

        private void OnLegacyException(object sender, LDP.ExceptionEvent e) {
            var exc = new PythonException();
            foreach (var item in e.data) {
                exc.SetValue(this, item.Key, item.Value);
            }

            if (e.threadId != 0) {
                Debug.WriteLine("Exception: " + (exc.FormattedDescription ?? exc.ExceptionMessage ?? exc.TypeName));
                ExceptionRaised?.Invoke(this, new ExceptionRaisedEventArgs(_threads[e.threadId], exc));
                _stoppedForException = true;
            }
        }

        private void OnLegacyDetach(object sender, LDP.DetachEvent e) {
            _process_Exited(this, EventArgs.Empty);
        }

        private void OnLegacyLast(object sender, LDP.LastEvent e) {
            try {
                SendDebugRequestAsync(new LDP.LastAckRequest()).WaitAndUnwrapExceptions();
            } catch (OperationCanceledException) {
                // The process waits for this request with short timeout before terminating
                // If the process has terminated, we expect an exception
            }
        }

        internal async Task SendStepOutAsync(long threadId, CancellationToken ct) {
            await SendDebugRequestAsync(new LDP.StepOutRequest() {
                threadId = threadId,
            }, ct);
        }

        internal async Task SendStepOverAsync(long threadId, CancellationToken ct) {
            await SendDebugRequestAsync(new LDP.StepOverRequest() {
                threadId = threadId,
            }, ct);
        }

        internal async Task SendStepIntoAsync(long threadId, CancellationToken ct) {
            await SendDebugRequestAsync(new LDP.StepIntoRequest() {
                threadId = threadId,
            }, ct);
        }

        public async Task SendResumeThreadAsync(long threadId, CancellationToken ct) {
            _stoppedForException = false;

            // race w/ removing the breakpoint, let the thread continue
            await SendDebugRequestAsync(new LDP.ResumeThreadRequest() {
                threadId = threadId,
            }, ct);
        }

        public async Task SendAutoResumeThreadAsync(long threadId, CancellationToken ct) {
            _stoppedForException = false;

            await SendDebugRequestAsync(new LDP.AutoResumeThreadRequest() {
                threadId = threadId,
            }, ct);
        }

        public async Task SendClearSteppingAsync(long threadId, CancellationToken ct) {
            // race w/ removing the breakpoint, let the thread continue
            await SendDebugRequestAsync(new LDP.ClearSteppingRequest() {
                threadId = threadId,
            }, ct);
        }

        public Task RefreshThreadFramesAsync(long threadId, CancellationToken ct) {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_pendingGetThreadFramesRequestsLock) {
                _pendingGetThreadFramesRequests.Add(tcs);
            }

            var requestTask = SendDebugRequestAsync(new LDP.EnumThreadFramesRequest() {
                threadId = threadId,
            }, ct);

            return Task.WhenAll(requestTask, tcs.Task);
        }

        private void OnLegacyThreadFrameList(object sender, LDP.ThreadFrameListEvent e) {
            var frames = new List<PythonStackFrame>();
            PythonThread thread;
            _threads.TryGetValue(e.threadId, out thread);

            for (int i = 0; i < e.threadFrames.Length; i++) {
                var item = e.threadFrames[i];
                PythonStackFrame frame = null;
                switch (item.frameKind) {
                    case LDP.FrameKind.Django:
                        frame = new DjangoStackFrame(
                            thread,
                            item.frameName,
                            item.fileName,
                            item.startLine,
                            item.endLine,
                            item.lineNo,
                            item.argCount,
                            i,
                            item.djangoSourceFile,
                            item.djangoSourceLine
                        );
                        break;
                    default:
                        frame = new PythonStackFrame(
                            thread,
                            item.frameName,
                            item.fileName,
                            item.startLine,
                            item.endLine,
                            item.lineNo,
                            item.argCount,
                            i,
                            FromLDPFrameKind(item.frameKind)
                        );
                        break;
                }

                var variables = item.variables.Select(v => ReadPythonObject(v.obj, v.name, v.name, frame)).ToArray();
                frame.SetVariables(variables);
                frames.Add(frame);
            }

            Debug.WriteLine("Received frames for thread {0}", e.threadId);
            if (thread != null) {
                thread.Frames = frames;
                if (e.threadName != null) {
                    thread.Name = e.threadName;
                }
            }

            TaskCompletionSource<int>[] completions;
            lock (_pendingGetThreadFramesRequestsLock) {
                completions = _pendingGetThreadFramesRequests.ToArray();
                _pendingGetThreadFramesRequests.Clear();
            }

            foreach (TaskCompletionSource<int> tcs in completions) {
                tcs.TrySetResult(0);
            }
        }

        public async Task DetachAsync(CancellationToken ct) {
            try {
                await SendDebugRequestAsync(new LDP.DetachRequest(), ct);
            } catch (OperationCanceledException) {
                // socket is closed after we send detach
            }
        }

        internal async Task BindBreakpointAsync(PythonBreakpoint breakpoint, CancellationToken ct) {
            var request = new LDP.SetBreakpointRequest() {
                language = breakpoint.IsDjangoBreakpoint ? LDP.LanguageKind.Django : LDP.LanguageKind.Python,
                breakpointId = breakpoint.Id,
                breakpointFileName = MapFile(breakpoint.Filename),
                breakpointLineNo = breakpoint.LineNo,
            };

            if (!breakpoint.IsDjangoBreakpoint) {
                request.conditionKind = ToLDPBreakpointConditionKind(breakpoint.ConditionKind);
                request.condition = breakpoint.Condition;
                request.passCountKind = ToLDPBreakpointPassCountKind(breakpoint.PassCountKind);
                request.passCount = breakpoint.PassCount;
            }

            await SendDebugRequestAsync(request, ct);
        }

        /// <summary>
        /// Maps a filename from the debugger machine to the debugge machine to vice versa.
        /// 
        /// The file mapping information is provided by our options when the debugger is started.  
        /// 
        /// This is used so that we can use the files local on the developers machine which have
        /// for setting breakpoints and viewing source code even though the files have been
        /// deployed to a remote machine.  For example the user may have:
        /// 
        /// C:\Users\Me\Documents\MyProject\Fob.py
        /// 
        /// which is deployed to
        /// 
        /// \\mycluster\deploydir\MyProject\Fob.py
        /// 
        /// We want the user to be working w/ the local project files during development but
        /// want to set break points in the cluster deployment share.
        /// </summary>
        internal string MapFile(string file, bool toDebuggee = true) {
            if (_dirMapping != null) {
                foreach (var mappingInfo in _dirMapping) {
                    string mapFrom = mappingInfo[toDebuggee ? 0 : 1];
                    string mapTo = mappingInfo[toDebuggee ? 1 : 0];

                    if (file.StartsWith(mapFrom, StringComparison.OrdinalIgnoreCase)) {
                        int len = mapFrom.Length;
                        if (!mappingInfo[0].EndsWith("\\")) {
                            len++;
                        }

                        string newFile = Path.Combine(mapTo, file.Substring(len));
                        Debug.WriteLine("Filename mapped from {0} to {1}", file, newFile);
                        return newFile;
                    }
                }
            }
            return file;
        }

        internal async Task SetBreakpointConditionAsync(PythonBreakpoint breakpoint, CancellationToken ct) {
            await SendDebugRequestAsync(new LDP.SetBreakpointConditionRequest() {
                breakpointId = breakpoint.Id,
                conditionKind = ToLDPBreakpointConditionKind(breakpoint.ConditionKind),
                condition = breakpoint.Condition ?? "",
            }, ct);
        }

        internal async Task SetBreakpointPassCountAsync(PythonBreakpoint breakpoint, CancellationToken ct) {
            await SendDebugRequestAsync(new LDP.SetBreakpointPassCountRequest() {
                breakpointId = breakpoint.Id,
                passCountKind = ToLDPBreakpointPassCountKind(breakpoint.PassCountKind),
                passCount = breakpoint.PassCount,
            }, ct);
        }

        internal async Task SetBreakpointHitCountAsync(PythonBreakpoint breakpoint, int count, CancellationToken ct) {
            await SendDebugRequestAsync(new LDP.SetBreakpointHitCountRequest() {
                breakpointId = breakpoint.Id,
                hitCount = count,
            }, ct);
        }

        internal async Task<int> GetBreakpointHitCountAsync(PythonBreakpoint breakpoint, CancellationToken ct) {
            var task = SendDebugRequestAsync(new LDP.GetBreakpointHitCountRequest() {
                breakpointId = breakpoint.Id,
            }, ct);

            var response = await task;
            return response.hitCount;
        }

        internal async Task ExecuteTextAsync(string text, PythonEvaluationResultReprKind reprKind, PythonStackFrame pythonStackFrame, bool printResult, Action<PythonEvaluationResult> completion, CancellationToken ct) {
            int executeId = _ids.Allocate();
            lock (_pendingExecutes) {
                _pendingExecutes[executeId] = new CompletionInfo(completion, text, pythonStackFrame);
            }

            await SendDebugRequestAsync(new LDP.ExecuteTextRequest() {
                text = text,
                executionId = executeId,
                threadId = pythonStackFrame.Thread.Id,
                frameId = pythonStackFrame.FrameId,
                frameKind = ToLDPFrameKind(pythonStackFrame.Kind),
                reprKind = ToLDPReprKind(reprKind),
                printResult = printResult
            }, ct);
        }

        internal async Task ExecuteTextAsync(string text, PythonEvaluationResultReprKind reprKind, string moduleName, bool printResult, Action<PythonEvaluationResult> completion, CancellationToken ct) {
            int executeId = _ids.Allocate();
            lock (_pendingExecutes) {
                _pendingExecutes[executeId] = new CompletionInfo(completion, text, null);
            }

            await SendDebugRequestAsync(new LDP.ExecuteTextRequest() {
                text = text,
                executionId = executeId,
                moduleName = moduleName,
                reprKind = ToLDPReprKind(reprKind),
                printResult = printResult
            }, ct);
        }

        private void OnLegacyModulesChanged(object sender, LDP.ModulesChangedEvent e) {
            ModulesChanged?.Invoke(this, EventArgs.Empty);
        }

        internal async Task<KeyValuePair<string, string>[]> GetModuleNamesAndPaths() {
            var response = await SendDebugRequestAsync(new LDP.ListReplModulesRequest(), new CancellationToken());
            return response.modules.Select(m => new KeyValuePair<string, string>(m.name, m.fileName)).ToArray();
        }

        internal async Task<PythonEvaluationResult[]> GetChildrenAsync(string text, PythonStackFrame pythonStackFrame, CancellationToken ct) {
            AutoResetEvent childrenEnumed = new AutoResetEvent(false);
            PythonEvaluationResult[] res = null;

            await EnumChildrenAsync(text, pythonStackFrame, (children) => {
                res = children;
                childrenEnumed.Set();
            }, ct).ConfigureAwait(false);

            while (!HasExited && !childrenEnumed.WaitOne(100)) {
                ct.ThrowIfCancellationRequested();
            }

            return res;
        }

        private async Task EnumChildrenAsync(string text, PythonStackFrame pythonStackFrame, Action<PythonEvaluationResult[]> completion, CancellationToken ct) {
            int executeId = _ids.Allocate();
            lock (_pendingChildEnums) {
                _pendingChildEnums[executeId] = new ChildrenInfo(completion, text, pythonStackFrame);
            }

            await SendDebugRequestAsync(new LDP.EnumChildrenRequest() {
                text = text,
                threadId = pythonStackFrame.Thread.Id,
                frameId = pythonStackFrame.FrameId,
                frameKind = ToLDPFrameKind(pythonStackFrame.Kind),
            }, ct);
        }

        private void OnLegacyEnumChildren(object sender, LDP.EnumChildrenEvent e) {
            int execId = e.executionId;

            ChildrenInfo completion;
            lock (_pendingChildEnums) {
                if (_pendingChildEnums.TryGetValue(execId, out completion)) {
                    _pendingChildEnums.Remove(execId);
                    _ids.Free(execId);
                } else {
                    Debug.Fail("Received enum children result with unknown execution ID " + execId);
                }
            }

            if (completion != null) {
                var children = e.children.Select(child => ReadPythonObject(
                    child.obj,
                    child.expression,
                    child.name,
                    completion?.Frame
                )).ToArray();

                completion.Completion(children);
            }
        }

        internal async Task RemoveBreakpointAsync(PythonBreakpoint unboundBreakpoint, CancellationToken ct) {
            PythonBreakpoint bp;
            _breakpoints.TryRemove(unboundBreakpoint.Id, out bp);
            await DisableBreakpointAsync(unboundBreakpoint, ct);
        }

        internal async Task DisableBreakpointAsync(PythonBreakpoint unboundBreakpoint, CancellationToken ct) {
            if (HasExited) {
                return;
            }

            await SendDebugRequestAsync(new LDP.RemoveBreakpointRequest() {
                language = unboundBreakpoint.IsDjangoBreakpoint ? LDP.LanguageKind.Django : LDP.LanguageKind.Python,
                breakpointId = unboundBreakpoint.Id,
                breakpointFileName = unboundBreakpoint.Filename,
                breakpointLineNo = unboundBreakpoint.LineNo,
            }, ct);
        }

        internal async Task<bool> SetLineNumberAsync(PythonStackFrame pythonStackFrame, int lineNo, CancellationToken ct) {
            if (_stoppedForException) {
                return false;
            }

            try {
                var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(ct, CancellationTokens.After2s);

                var response = await SendDebugRequestAsync(new LDP.SetLineNumberRequest() {
                    threadId = pythonStackFrame.Thread.Id,
                    frameId = pythonStackFrame.FrameId,
                    lineNo = lineNo,
                }, linkedSource.Token);

                if (response.result == 0) {
                    return false;
                }

                var frame = _threads[response.threadId].Frames.FirstOrDefault();
                if (frame != null) {
                    frame.LineNo = response.newLineNo;
                } else {
                    Debug.Fail("SetLineNumber result received, but there is no frame to update");
                }

                return true;
            } catch (OperationCanceledException) {
                return false;
            }
        }

        internal void SendStringToStdInput(string text) {
            if (_process == null) {
                throw new InvalidOperationException();
            }
            _process.StandardInput.Write(text);
        }

        private async Task<T> SendDebugRequestAsync<T>(Request<T> request, CancellationToken cancellationToken = default(CancellationToken))
            where T : Response, new() {
            Debug.WriteLine("PythonDebugger " + _processGuid + " Sending Command " + request.command);

            DebugConnection connection = null;
            lock (_connectionLock) {
                connection = _connection;
            }

            if (connection == null) {
                throw new OperationCanceledException();
            }

            try {
                using (new DebugTimer(string.Format("DebuggerRequest ({0})", request.command), 100)) {
                    return await connection.SendRequestAsync(request, cancellationToken);
                }
            } catch (IOException ex) {
                throw new OperationCanceledException(ex.Message, ex);
            } catch (ObjectDisposedException ex) {
                throw new OperationCanceledException(ex.Message, ex);
            }
        }

        #endregion

        #region Debugging Events

        private EventHandler _connected;

        public event EventHandler Connected {
            add {
                lock (_connectionLock) {
                    _connected += value;

                    // If a subscriber adds the handler after the process is connected, fire the event immediately.
                    // Since connection happens on a background thread, subscribers are racing against that thread
                    // when registering handlers, so we need to notify them even if they're too late.
                    if (_connection != null) {
                        value(this, EventArgs.Empty);
                    }
                }
            }
            remove {
                lock (_connectionLock) {
                    _connected -= value;
                }
            }
        }

        /// <summary>
        /// Fired when the process has started and is broken into the debugger, but before any user code is run.
        /// </summary>
        public event EventHandler<ThreadEventArgs> ProcessLoaded;
        public event EventHandler<ThreadEventArgs> ThreadCreated;
        public event EventHandler<ThreadEventArgs> ThreadExited;
        public event EventHandler<ThreadEventArgs> StepComplete;
        public event EventHandler<ThreadEventArgs> AsyncBreakComplete;
        public event EventHandler<ProcessExitedEventArgs> ProcessExited;
        public event EventHandler<ModuleLoadedEventArgs> ModuleLoaded;
        public event EventHandler<ExceptionRaisedEventArgs> ExceptionRaised;
        public event EventHandler<ThreadEventArgs> EntryPointHit;
        public event EventHandler<BreakpointHitEventArgs> BreakpointHit;
        public event EventHandler<BreakpointEventArgs> BreakpointBindSucceeded;
        public event EventHandler<BreakpointEventArgs> BreakpointBindFailed;
        public event EventHandler<OutputEventArgs> DebuggerOutput;
        public event EventHandler<EventArgs> ModulesChanged;

        #endregion

        private PythonEvaluationResultFlags FromLDPEvaluationResultFlags(LDP.EvaluationResultFlags flags) {
            // All fields of both enums match
            return (PythonEvaluationResultFlags)flags;
        }

        private FrameKind FromLDPFrameKind(LDP.FrameKind kind) {
            switch (kind) {
                case LDP.FrameKind.None:
                    return FrameKind.None;
                case LDP.FrameKind.Python:
                    return FrameKind.Python;
                case LDP.FrameKind.Django:
                    return FrameKind.Django;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Value is not supported.");
            }
        }

        private LDP.BreakpointConditionKind ToLDPBreakpointConditionKind(PythonBreakpointConditionKind kind) {
            switch (kind) {
                case PythonBreakpointConditionKind.Always:
                    return LDP.BreakpointConditionKind.Always;
                case PythonBreakpointConditionKind.WhenTrue:
                    return LDP.BreakpointConditionKind.WhenTrue;
                case PythonBreakpointConditionKind.WhenChanged:
                    return LDP.BreakpointConditionKind.WhenChanged;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Value is not supported.");
            }
        }

        private LDP.BreakpointPassCountKind ToLDPBreakpointPassCountKind(PythonBreakpointPassCountKind kind) {
            switch (kind) {
                case PythonBreakpointPassCountKind.Always:
                    return LDP.BreakpointPassCountKind.Always;
                case PythonBreakpointPassCountKind.Every:
                    return LDP.BreakpointPassCountKind.Every;
                case PythonBreakpointPassCountKind.WhenEqual:
                    return LDP.BreakpointPassCountKind.WhenEqual;
                case PythonBreakpointPassCountKind.WhenEqualOrGreater:
                    return LDP.BreakpointPassCountKind.WhenEqualOrGreater;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Value is not supported.");
            }
        }

        private LDP.ReprKind ToLDPReprKind(PythonEvaluationResultReprKind kind) {
            switch (kind) {
                case PythonEvaluationResultReprKind.Normal:
                    return LDP.ReprKind.Normal;
                case PythonEvaluationResultReprKind.Raw:
                    return LDP.ReprKind.Raw;
                case PythonEvaluationResultReprKind.RawLen:
                    return LDP.ReprKind.RawLen;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Value is not supported.");
            }
        }

        private LDP.FrameKind ToLDPFrameKind(FrameKind kind) {
            switch (kind) {
                case FrameKind.None:
                    return LDP.FrameKind.None;
                case FrameKind.Python:
                    return LDP.FrameKind.Python;
                case FrameKind.Django:
                    return LDP.FrameKind.Django;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Value is not supported.");
            }
        }

        class CompletionInfo {
            public readonly Action<PythonEvaluationResult> Completion;
            public readonly string Text;
            public readonly PythonStackFrame Frame;

            public CompletionInfo(Action<PythonEvaluationResult> completion, string text, PythonStackFrame frame) {
                Completion = completion;
                Text = text;
                Frame = frame;
            }
        }

        class ChildrenInfo {
            public readonly Action<PythonEvaluationResult[]> Completion;
            public readonly string Text;
            public readonly PythonStackFrame Frame;

            public ChildrenInfo(Action<PythonEvaluationResult[]> completion, string text, PythonStackFrame frame) {
                Completion = completion;
                Text = text;
                Frame = frame;
            }
        }
    }
}
