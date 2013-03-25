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
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.PythonTools.Debugger {
    /// <summary>
    /// Handles all interactions with a Python process which is being debugged.
    /// </summary>
    class PythonProcess {
        private static Random _portGenerator = new Random();

        private readonly Process _process;
        private readonly Dictionary<long, PythonThread> _threads = new Dictionary<long, PythonThread>();
        private readonly Dictionary<int, PythonBreakpoint> _breakpoints = new Dictionary<int, PythonBreakpoint>();
        private readonly IdDispenser _ids = new IdDispenser();
        private readonly AutoResetEvent _lineEvent = new AutoResetEvent(false);         // set when result of setting current line returns
        private readonly Dictionary<int, CompletionInfo> _pendingExecutes = new Dictionary<int, CompletionInfo>();
        private readonly Dictionary<int, ChildrenInfo> _pendingChildEnums = new Dictionary<int, ChildrenInfo>();
        private readonly PythonLanguageVersion _langVersion;
        private readonly Guid _processGuid = Guid.NewGuid();
        private readonly List<string[]> _dirMapping;
        private readonly bool _delayUnregister;
        private readonly object _socketLock = new object();

        private int _pid;
        private bool _sentExited;
        private Socket _socket;
        private Stream _stream;
        private int _breakpointCounter;
        private bool _setLineResult;                    // contains result of attempting to set the current line of a frame
        private bool _createdFirstThread;
        private bool _stoppedForException;
        private int _defaultBreakMode;
        private ICollection<KeyValuePair<string, int>> _breakOn;

        protected PythonProcess(int pid, PythonLanguageVersion languageVersion) {
            _pid = pid;
            _langVersion = languageVersion;
        }

        private PythonProcess(int pid) {
            _pid = pid;
            _process = Process.GetProcessById(pid);
            _process.EnableRaisingEvents = true;
            _process.Exited += new EventHandler(_process_Exited);

            ListenForConnection();

            using (var result = DebugAttach.Attach(pid, DebugConnectionListener.ListenerPort, _processGuid)) {
                if (result.Error != ConnErrorMessages.None) {
                    throw new AttachException(result.Error);
                }

                _langVersion = (PythonLanguageVersion)result.LanguageVersion;
                if (!result.AttachDone.WaitOne(20000)) {
                    throw new AttachException(ConnErrorMessages.TimeOut);
                }
            }
        }

        private PythonProcess(Stream stream, int pid, PythonLanguageVersion version) {
            _pid = pid;
            _process = Process.GetProcessById(pid);
            _process.EnableRaisingEvents = true;
            _process.Exited += new EventHandler(_process_Exited);
            
            _delayUnregister = true;
            
            ListenForConnection();

            stream.WriteInt32(DebugConnectionListener.ListenerPort);
            stream.WriteString(_processGuid.ToString());
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
                "\"" + Path.Combine(GetPythonToolsInstallPath(), "visualstudio_py_launcher.py") + "\" " +
                "\"" + dir + "\" " +
                " " + DebugConnectionListener.ListenerPort + " " +
                " " + _processGuid + " " +
                (((options & PythonDebugOptions.WaitOnAbnormalExit) != 0) ? " --wait-on-exception " : "") +
                (((options & PythonDebugOptions.WaitOnNormalExit) != 0) ? " --wait-on-exit " : "") +
                (((options & PythonDebugOptions.RedirectOutput) != 0) ? " --redirect-output " : "") +
                (((options & PythonDebugOptions.BreakOnSystemExitZero) != 0) ? " --break-on-systemexit-zero " : "") +
                (((options & PythonDebugOptions.DebugStdLib) != 0) ? " --debug-stdlib " : "") +
                (((options & PythonDebugOptions.DjangoDebugging) != 0) ? " --django-debugging " : "") +
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

        public static ConnErrorMessages TryAttach(int pid, out PythonProcess process) {
            try {
                process = new PythonProcess(pid);
                return ConnErrorMessages.None;
            } catch (AttachException ex) {
                process = null;
                return ex.Error;
            }
        }

        public static PythonProcess AttachRepl(Stream stream, int pid, PythonLanguageVersion version) {
            return new PythonProcess(stream, pid, version);
        }

        class AttachException : Exception {
            private readonly ConnErrorMessages _error;

            public AttachException(ConnErrorMessages error) {
                _error = error;
            }

            public ConnErrorMessages Error {
                get {
                    return _error;
                }
            }
        }

        #region Public Process API

        public int Id {
            get {
                return _pid;
            }
        }

        public Guid ProcessGuid {
            get {
                return _processGuid;
            }
        }

        public void Start(bool startListening = true) {
            _process.Start();
            _pid = _process.Id;
            if (startListening) {
                StartListening();
            }
        }

        private void ListenForConnection() {
            DebugConnectionListener.RegisterProcess(_processGuid, this);
        }

        ~PythonProcess() {
            DebugConnectionListener.UnregisterProcess(_processGuid);
        }

        void _process_Exited(object sender, EventArgs e) {
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

            _stream = null;
            _socket = null;
        }

        public bool HasExited {
            get {
                return _process != null && _process.HasExited;
            }
        }

        /// <summary>
        /// Breaks into the process.
        /// </summary>
        public void Break() {
            DebugWriteCommand("BreakAll");
            lock(_socketLock) {
                _stream.Write(BreakAllCommandBytes);
            }
        }

        [Conditional("DEBUG")]
        private void DebugWriteCommand(string commandName) {
            Debug.WriteLine("PythonDebugger " + _processGuid + " Sending Command " + commandName);
        }

        public void Resume() {
            _stoppedForException = false;
            DebugWriteCommand("ResumeAll");
            lock (_socketLock) {
                _stream.Write(ResumeAllCommandBytes);
            }
        }

        public bool StoppedForException {
            get {
                return _stoppedForException;
            }
        }

        public void Continue() {
            Resume();
        }

        public PythonBreakpoint AddBreakPoint(string filename, int lineNo, string condition = "", bool breakWhenChanged = false) {
            int id = _breakpointCounter++;
            var res = new PythonBreakpoint(this, filename, lineNo, condition, breakWhenChanged, id);
            _breakpoints[id] = res;
            return res;
        }

        public PythonBreakpoint AddDjangoBreakPoint(string filename, int lineNo) {
            int id = _breakpointCounter++;
            var res = new PythonBreakpoint(this, filename, lineNo, null, false, id, true);
            _breakpoints[id] = res;
            return res;
        }

        public PythonLanguageVersion LanguageVersion {
            get {
                return _langVersion;
            }
        }

        public void SetExceptionInfo(int defaultBreakOnMode, ICollection<KeyValuePair<string, int>> breakOn) {
            lock (this) {
                if (_stream != null) {
                    SendExceptionInfo(defaultBreakOnMode, breakOn);
                } else {
                    _breakOn = breakOn.ToArray();
                    _defaultBreakMode = defaultBreakOnMode;
                }
            }
        }

        private void SendExceptionInfo(int defaultBreakOnMode, ICollection<KeyValuePair<string, int>> breakOn) {
            lock (_socketLock) {
                _stream.Write(SetExceptionInfoCommandBytes);
                _stream.WriteInt32(defaultBreakOnMode);
                _stream.WriteInt32(breakOn.Count);
                foreach (var item in breakOn) {
                    _stream.WriteInt32(item.Value);
                    _stream.WriteString(item.Key);
                }
            }
        }

        #endregion

        #region Debuggee Communcation

        internal void Connected(Socket socket, Stream stream) {
            Debug.WriteLine("Process Connected: " + _processGuid);

            lock (this) {
                _socket = socket;
                _stream = stream;
                if (_breakOn != null) {
                    SendExceptionInfo(_defaultBreakMode, _breakOn);
                }
            }

            if (!_delayUnregister) {
                Unregister();
            }
        }

        internal void Unregister() {
            DebugConnectionListener.UnregisterProcess(_processGuid);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Starts listening for debugger communication.  Can be called after Start
        /// to give time to attach to debugger events.
        /// </summary>
        public void StartListening() {
            var debuggerThread = new Thread(DebugEventThread);
            debuggerThread.Name = "Python Debugger Thread " + _processGuid;
            debuggerThread.Start();
        }

        private void DebugEventThread() {
            Debug.WriteLine("DebugEvent Thread Started " + _processGuid);
            while ((_process == null || !_process.HasExited) && _stream == null) {
                // wait for connection...
                System.Threading.Thread.Sleep(10);
            }

            try {
                while (true) {
                    Stream stream = _stream;
                    if (stream == null) {
                        break;
                    }

                    string cmd = stream.ReadAsciiString(4);
                    Debug.WriteLine(String.Format("Received Debugger command: {0} ({1})", cmd, _processGuid));

                    switch (cmd) {
                        case "EXCP": HandleException(stream); break;
                        case "BRKH": HandleBreakPointHit(stream); break;
                        case "NEWT": HandleThreadCreate(stream); break;
                        case "EXTT": HandleThreadExit(stream); break;
                        case "MODL": HandleModuleLoad(stream); break;
                        case "STPD": HandleStepDone(stream); break;
                        case "BRKS": HandleBreakPointSet(stream); break;
                        case "BRKF": HandleBreakPointFailed(stream); break;
                        case "LOAD": HandleProcessLoad(stream); break;
                        case "THRF": HandleThreadFrameList(stream); break;
                        case "EXCR": HandleExecutionResult(stream); break;
                        case "EXCE": HandleExecutionException(stream); break;
                        case "ASBR": HandleAsyncBreak(stream); break;
                        case "SETL": HandleSetLineResult(stream); break;
                        case "CHLD": HandleEnumChildren(stream); break;
                        case "OUTP": HandleDebuggerOutput(stream); break;
                        case "REQH": HandleRequestHandlers(stream); break;
                        case "DETC": _process_Exited(this, EventArgs.Empty); break; // detach, report process exit
                    }
                }
            } catch (IOException ioExc) {
                var sockExc = ioExc.InnerException as SocketException;
                if (sockExc != null) {
                    // Treat non-recoverable socket errors as an indication that the debuggee process has been terminated.
                    switch (sockExc.SocketErrorCode) {
                        case SocketError.ConnectionAborted:
                        case SocketError.ConnectionReset:
                            _process_Exited(this, EventArgs.Empty);
                            break;
                    }
                }
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
            List<PythonThread> threads = new List<PythonThread>();
            foreach (var thread in _threads.Values) {
                threads.Add(thread);
            }
            return threads;
        }

        internal IList<Tuple<int, int, IList<string>>> GetHandledExceptionRanges(string filename) {
            PythonAst ast;
            TryHandlerWalker walker = new TryHandlerWalker();
            var statements = new List<Tuple<int, int, IList<string>>>();

            try {
                using (var source = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    ast = Parser.CreateParser(source, LanguageVersion).ParseFile();
                    ast.Walk(walker);
                }
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
                    expressions.Add("*");
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

        private void HandleRequestHandlers(Stream stream) {
            string filename = stream.ReadString();

            Debug.WriteLine("Exception handlers requested for: " + filename);
            var statements = GetHandledExceptionRanges(filename);

            lock (_socketLock) {
                _stream.Write(SetExceptionHandlerInfoCommandBytes);
                _stream.WriteString(filename);

                _stream.WriteInt32(statements.Count);

                foreach (var t in statements) {
                    _stream.WriteInt32(t.Item1);
                    _stream.WriteInt32(t.Item2);

                    foreach (var expr in t.Item3) {
                        _stream.WriteString(expr);
                    }
                    _stream.WriteString("-");
                }
            }
        }

        private void HandleDebuggerOutput(Stream stream) {
            long tid = stream.ReadInt64();
            string output = stream.ReadString();

            PythonThread thread;
            if (_threads.TryGetValue(tid, out thread)) {
                var outputEvent = DebuggerOutput;
                if (outputEvent != null) {
                    outputEvent(this, new OutputEventArgs(thread, output));
                }
            }
        }

        private void HandleSetLineResult(Stream stream) {
            int res = stream.ReadInt32();
            long tid = stream.ReadInt64();
            int newLine = stream.ReadInt32();
            _setLineResult = res != 0;
            if (_setLineResult) {
                _threads[tid].Frames[0].LineNo = newLine;
            }
            _lineEvent.Set();
        }

        private void HandleAsyncBreak(Stream stream) {
            long tid = stream.ReadInt64();
            var thread = _threads[tid];
            var asyncBreak = AsyncBreakComplete;
            Debug.WriteLine("Received async break command from thread {0}", tid);
            if (asyncBreak != null) {
                asyncBreak(this, new ThreadEventArgs(thread));
            }
        }

        private void HandleExecutionException(Stream stream) {
            int execId = stream.ReadInt32();
            CompletionInfo completion;

            lock (_pendingExecutes) {
                completion = _pendingExecutes[execId];
                _pendingExecutes.Remove(execId);
            }

            string exceptionText = stream.ReadString();
            completion.Completion(new PythonEvaluationResult(this, exceptionText, completion.Text, completion.Frame));
        }

        private void HandleExecutionResult(Stream stream) {
            int execId = stream.ReadInt32();
            CompletionInfo completion;
            lock (_pendingExecutes) {
                completion = _pendingExecutes[execId];

                _pendingExecutes.Remove(execId);
                _ids.Free(execId);
            }
            Debug.WriteLine("Received execution request {0}", execId);
            completion.Completion(ReadPythonObject(stream, completion.Text, "", false, false, completion.Frame));
        }

        private void HandleEnumChildren(Stream stream) {
            int execId = stream.ReadInt32();
            ChildrenInfo completion;

            lock (_pendingChildEnums) {
                completion = _pendingChildEnums[execId];
                _pendingChildEnums.Remove(execId);
            }

            int attributesCount = stream.ReadInt32();
            int indicesCount = stream.ReadInt32();
            bool indicesAreIndex = stream.ReadInt32() == 1;
            bool indicesAreEnumerate = stream.ReadInt32() == 1;
            PythonEvaluationResult[] res = new PythonEvaluationResult[attributesCount + indicesCount];
            for (int i = 0; i < attributesCount; i++) {
                string expr = stream.ReadString();
                res[i] = ReadPythonObject(stream, completion.Text, expr, false, false, completion.Frame);
            }
            for (int i = attributesCount; i < res.Length; i++) {
                string expr = stream.ReadString();
                res[i] = ReadPythonObject(stream, completion.Text, expr, indicesAreIndex, indicesAreEnumerate, completion.Frame);
            }
            completion.Completion(res);
        }

        private PythonEvaluationResult ReadPythonObject(Stream stream, string text, string childText, bool childIsIndex, bool childIsEnumerate, PythonStackFrame frame) {
            string objRepr = stream.ReadString();
            string hexRepr = stream.ReadString();
            string typeName = stream.ReadString();
            bool isExpandable = stream.ReadInt32() == 1;

            if ((typeName == "unicode" && LanguageVersion.Is2x()) ||
                (typeName == "str" && LanguageVersion.Is3x())) {
                objRepr = objRepr.FixupEscapedUnicodeChars();
            }
            return new PythonEvaluationResult(this, objRepr, hexRepr, typeName, text, childText, childIsIndex, childIsEnumerate, frame, isExpandable);
        }

        private void HandleThreadFrameList(Stream stream) {
            // list of thread frames
            var frames = new List<PythonStackFrame>();
            long tid = stream.ReadInt64();
            PythonThread thread;
            _threads.TryGetValue(tid, out thread);
            var threadName = stream.ReadString();

            int frameCount = stream.ReadInt32();
            for (int i = 0; i < frameCount; i++) {
                int startLine = stream.ReadInt32();
                int endLine = stream.ReadInt32();
                int lineNo = stream.ReadInt32();
                string frameName = stream.ReadString();
                string filename = stream.ReadString();
                int argCount = stream.ReadInt32();
                var frameKind = (FrameKind)stream.ReadInt32();
                PythonStackFrame frame = null; 
                if (thread != null) {
                    switch (frameKind) {
                        case FrameKind.Django:
                            string sourceFile = stream.ReadString();
                            var sourceLine = stream.ReadInt32();
                            frame = new DjangoStackFrame(thread, frameName, filename, startLine, endLine, lineNo, argCount, i, sourceFile, sourceLine);
                            break;
                        default:
                            frame = new PythonStackFrame(thread, frameName, filename, startLine, endLine, lineNo, argCount, i, frameKind);
                            break;
                    }
                    
                }

                int varCount = stream.ReadInt32();
                PythonEvaluationResult[] variables = new PythonEvaluationResult[varCount];
                for (int j = 0; j < variables.Length; j++) {
                    string name = stream.ReadString();
                    if (frame != null) {
                        variables[j] = ReadPythonObject(stream, name, "", false, false, frame);
                    }
                }
                if (frame != null) {
                    frame.SetVariables(variables);
                    frames.Add(frame);
                }
            }

            Debug.WriteLine("Received frames for thread {0}", tid);
            if (thread != null) {
                thread.Frames = frames;
                if (threadName != null) {
                    thread.Name = threadName;
                }
            }
        }

        private void HandleProcessLoad(Stream stream) {
            Debug.WriteLine("Process loaded " + _processGuid);

            // process is loaded, no user code has run
            long threadId = stream.ReadInt64();
            var thread = _threads[threadId];

            var loaded = ProcessLoaded;
            if (loaded != null) {
                loaded(this, new ThreadEventArgs(thread));
            }
        }

        private void HandleBreakPointFailed(Stream stream) {
            // break point failed to set
            int id = stream.ReadInt32();
            var brkEvent = BreakpointBindFailed;
            PythonBreakpoint breakpoint;
            if (brkEvent != null && _breakpoints.TryGetValue(id, out breakpoint)) {
                brkEvent(this, new BreakpointEventArgs(breakpoint));
            }
        }

        private void HandleBreakPointSet(Stream stream) {
            // break point successfully set
            int id = stream.ReadInt32();
            PythonBreakpoint unbound;
            if (_breakpoints.TryGetValue(id, out unbound)) {
                var brkEvent = BreakpointBindSucceeded;
                if (brkEvent != null) {
                    brkEvent(this, new BreakpointEventArgs(unbound));
                }
            }
        }

        private void HandleStepDone(Stream stream) {
            // stepping done
            long threadId = stream.ReadInt64();
            var stepComp = StepComplete;
            if (stepComp != null) {
                stepComp(this, new ThreadEventArgs(_threads[threadId]));
            }
        }

        private void HandleModuleLoad(Stream stream) {
            // module load
            int moduleId = stream.ReadInt32();
            string filename = stream.ReadString();
            if (filename != null) {
                Debug.WriteLine(String.Format("Module Loaded ({0}): {1}", moduleId, filename));
                var module = new PythonModule(moduleId, filename);

                var loaded = ModuleLoaded;
                if (loaded != null) {
                    loaded(this, new ModuleLoadedEventArgs(module));
                }
            }
        }

        private void HandleThreadExit(Stream stream) {
            // thread exit
            long threadId = stream.ReadInt64();
            PythonThread thread;
            if (_threads.TryGetValue(threadId, out thread)) {
                var exited = ThreadExited;
                if (exited != null) {
                    exited(this, new ThreadEventArgs(thread));
                }

                _threads.Remove(threadId);
                Debug.WriteLine("Thread exited, {0} active threads", _threads.Count);
            }

        }

        private void HandleThreadCreate(Stream stream) {
            // new thread
            long threadId = stream.ReadInt64();
            var thread = _threads[threadId] = new PythonThread(this, threadId, _createdFirstThread);
            _createdFirstThread = true;

            var created = ThreadCreated;
            if (created != null) {
                created(this, new ThreadEventArgs(thread));
            }
        }

        private void HandleBreakPointHit(Stream stream) {
            int breakId = stream.ReadInt32();
            long threadId = stream.ReadInt64();
            var brkEvent = BreakpointHit;
            PythonBreakpoint unboundBreakpoint;
            if (brkEvent != null) {
                if (_breakpoints.TryGetValue(breakId, out unboundBreakpoint)) {
                    brkEvent(this, new BreakpointHitEventArgs(unboundBreakpoint, _threads[threadId]));
                } else {
                    SendResumeThread(threadId);
                }
            }
        }

        private void HandleException(Stream stream) {
            string typeName = stream.ReadString();
            long tid = stream.ReadInt64();
            int breakType = stream.ReadInt32();
            string desc = stream.ReadString();
            if (typeName != null && desc != null) {
                Debug.WriteLine("Exception: " + desc);
                var excepRaised = ExceptionRaised;
                if (excepRaised != null) {
                    excepRaised(this, new ExceptionRaisedEventArgs(_threads[tid], new PythonException(typeName, desc), breakType == 1 /* BREAK_TYPE_UNHANLDED */));
                }
            }
            _stoppedForException = true;
        }

        private static string CommandtoString(byte[] cmd_buffer) {
            return new string(new char[] { (char)cmd_buffer[0], (char)cmd_buffer[1], (char)cmd_buffer[2], (char)cmd_buffer[3] });
        }

        internal void SendStepOut(long threadId) {
            DebugWriteCommand("StepOut");
            lock (_socketLock) {
                _stream.Write(StepOutCommandBytes);
                _stream.WriteInt64(threadId);
            }
        }

        internal void SendStepOver(long threadId) {
            DebugWriteCommand("StepOver");
            lock (_socketLock) {
                _stream.Write(StepOverCommandBytes);
                _stream.WriteInt64(threadId);
            }
        }

        internal void SendStepInto(long threadId) {
            DebugWriteCommand("StepInto");
            lock (_socketLock) {
                _stream.Write(StepIntoCommandBytes);
                _stream.WriteInt64(threadId);
            }
        }

        public void SendResumeThread(long threadId) {
            _stoppedForException = false;
            DebugWriteCommand("ResumeThread");
            lock (_socketLock) {
                // race w/ removing the breakpoint, let the thread continue
                _stream.Write(ResumeThreadCommandBytes);
                _stream.WriteInt64(threadId);
            }
        }

        public void SendClearStepping(long threadId) {
            DebugWriteCommand("ClearStepping");
            lock (_socketLock) {
                // race w/ removing the breakpoint, let the thread continue
                _stream.Write(ClearSteppingCommandBytes);
                _stream.WriteInt64(threadId);
            }
        }

        public void Detach() {
            DebugWriteCommand("Detach");
            try {
                lock (_socketLock) {
                    _stream.Write(DetachCommandBytes);
                }
            } catch (IOException) {
                // socket is closed after we send detach
            }
        }

        [DllImport("kernel32", SetLastError = true, ExactSpelling = true)]
        public static extern Int32 WaitForSingleObject(SafeWaitHandle handle, Int32 milliseconds);

        internal void BindBreakpoint(PythonBreakpoint breakpoint) {
            DebugWriteCommand(String.Format("Bind Breakpoint IsDjango: {0}", breakpoint.IsDjangoBreakpoint));

            lock (_socketLock) {
                if (breakpoint.IsDjangoBreakpoint) {
                    _stream.Write(AddDjangoBreakPointCommandBytes);
                } else {
                    _stream.Write(SetBreakPointCommandBytes);
                }
                _stream.WriteInt32(breakpoint.Id);
                _stream.WriteInt32(breakpoint.LineNo);
                _stream.WriteString(MapFile(breakpoint.Filename));
                if (!breakpoint.IsDjangoBreakpoint) {
                    SendCondition(breakpoint);
                }
            }
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
        /// C:\Users\Me\Documents\MyProject\Foo.py
        /// 
        /// which is deployed to
        /// 
        /// \\mycluster\deploydir\MyProject\Foo.py
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
                        if (file.StartsWith(mapFrom, StringComparison.OrdinalIgnoreCase)) {
                            int len = mapFrom.Length;
                            if (!mappingInfo[0].EndsWith("\\")) {
                                len++;
                            }

                            string newFile = Path.Combine(mapTo, file.Substring(len));
                            Debug.WriteLine(String.Format("Filename mapped from {0} to {1}", file, newFile));
                            return newFile;
                        }
                    }
                }
            }
            return file;
        }

        private void SendCondition(PythonBreakpoint breakpoint) {
            DebugWriteCommand("Send BP Condition");
            _stream.WriteString(breakpoint.Condition ?? "");
            _stream.WriteInt32(breakpoint.BreakWhenChanged ? 1 : 0);
        }

        internal void SetBreakPointCondition(PythonBreakpoint breakpoint) {
            DebugWriteCommand("Set BP Condition");
            lock (_socketLock) {
                _stream.Write(SetBreakPointConditionCommandBytes);
                _stream.WriteInt32(breakpoint.Id);
                SendCondition(breakpoint);
            }
        }

        internal void ExecuteText(string text, PythonStackFrame pythonStackFrame, Action<PythonEvaluationResult> completion) {
            int executeId = _ids.Allocate();
            DebugWriteCommand("ExecuteText to thread " + pythonStackFrame.Thread.Id + " " + executeId);
            lock (_pendingExecutes) {
                _pendingExecutes[executeId] = new CompletionInfo(completion, text, pythonStackFrame);
            }

            lock (_socketLock) {
                _stream.Write(ExecuteTextCommandBytes);
                _stream.WriteString(text);
                _stream.WriteInt64(pythonStackFrame.Thread.Id);
                _stream.WriteInt32(pythonStackFrame.FrameId);
                _stream.WriteInt32(executeId);
                _stream.WriteInt32((int)pythonStackFrame.Kind);
            }
        }

        internal void EnumChildren(string text, PythonStackFrame pythonStackFrame, bool childIsEnumerate, Action<PythonEvaluationResult[]> completion) {
            DebugWriteCommand("Enum Children");
            int executeId = _ids.Allocate();
            lock (_pendingChildEnums) {
                _pendingChildEnums[executeId] = new ChildrenInfo(completion, text, pythonStackFrame);
            }

            lock (_socketLock) {
                _stream.Write(GetChildrenCommandBytes);
                _stream.WriteString(text);
                _stream.WriteInt64(pythonStackFrame.Thread.Id);
                _stream.WriteInt32(pythonStackFrame.FrameId);
                _stream.WriteInt32(executeId);
                _stream.WriteInt32((int)pythonStackFrame.Kind);
                _stream.WriteInt32(childIsEnumerate ? 1 : 0);
            }
        }

        internal void RemoveBreakPoint(PythonBreakpoint unboundBreakpoint) {
            DebugWriteCommand("Remove Breakpoint");
            _breakpoints.Remove(unboundBreakpoint.Id);

            DisableBreakPoint(unboundBreakpoint);
        }

        internal void DisableBreakPoint(PythonBreakpoint unboundBreakpoint) {
            if (_stream != null && _socket.Connected) {
                DebugWriteCommand("Disable Breakpoint");
                lock (_socketLock) {
                    if (unboundBreakpoint.IsDjangoBreakpoint) {
                        _stream.Write(RemoveDjangoBreakPointCommandBytes);
                    } else {
                        _stream.Write(RemoveBreakPointCommandBytes);
                    }
                    _stream.WriteInt32(unboundBreakpoint.LineNo);
                    _stream.WriteInt32(unboundBreakpoint.Id);
                    if (unboundBreakpoint.IsDjangoBreakpoint) {
                        _stream.WriteString(unboundBreakpoint.Filename);
                    }
                }
            }
        }

        internal void ConnectRepl(int portNum) {
            DebugWriteCommand("Connect Repl");
            lock (_socketLock) {
                _stream.Write(ConnectReplCommandBytes);
                _stream.WriteInt32(portNum);
            }
        }

        internal void DisconnectRepl() {
            DebugWriteCommand("Disconnect Repl");
            lock (_socketLock) {
                try {
                    _stream.Write(DisconnectReplCommandBytes);
                } catch (IOException) {
                    // If the process has terminated, we expect an exception
                }
            }
        }

        internal bool SetLineNumber(PythonStackFrame pythonStackFrame, int lineNo) {
            if (_stoppedForException) {
                return false;
            }

            DebugWriteCommand("Set Line Number");
            lock (_socketLock) {
                _setLineResult = false;
                _stream.Write(SetLineNumberCommand);
                _stream.WriteInt64(pythonStackFrame.Thread.Id);
                _stream.WriteInt32(pythonStackFrame.FrameId);
                _stream.WriteInt32(lineNo);
            }

            // wait up to 2 seconds for line event...
            for (int i = 0; i < 20 && _socket.Connected && WaitForSingleObject(_lineEvent.SafeWaitHandle, 100) != 0; i++) {
            }

            return _setLineResult;
        }

        private static byte[] ExitCommandBytes = MakeCommand("exit");
        private static byte[] StepIntoCommandBytes = MakeCommand("stpi");
        private static byte[] StepOutCommandBytes = MakeCommand("stpo");
        private static byte[] StepOverCommandBytes = MakeCommand("stpv");
        private static byte[] BreakAllCommandBytes = MakeCommand("brka");
        private static byte[] SetBreakPointCommandBytes = MakeCommand("brkp");
        private static byte[] SetBreakPointConditionCommandBytes = MakeCommand("brkc");
        private static byte[] RemoveBreakPointCommandBytes = MakeCommand("brkr");
        private static byte[] ResumeAllCommandBytes = MakeCommand("resa");
        private static byte[] GetThreadFramesCommandBytes = MakeCommand("thrf");
        private static byte[] ExecuteTextCommandBytes = MakeCommand("exec");
        private static byte[] ResumeThreadCommandBytes = MakeCommand("rest");
        private static byte[] ClearSteppingCommandBytes = MakeCommand("clst");
        private static byte[] SetLineNumberCommand = MakeCommand("setl");
        private static byte[] GetChildrenCommandBytes = MakeCommand("chld");
        private static byte[] DetachCommandBytes = MakeCommand("detc");
        private static byte[] SetExceptionInfoCommandBytes = MakeCommand("sexi");
        private static byte[] SetExceptionHandlerInfoCommandBytes = MakeCommand("sehi");
        private static byte[] RemoveDjangoBreakPointCommandBytes = MakeCommand("bkdr");
        private static byte[] AddDjangoBreakPointCommandBytes = MakeCommand("bkda");
        private static byte[] ConnectReplCommandBytes = MakeCommand("crep");
        private static byte[] DisconnectReplCommandBytes = MakeCommand("drep");

        private static byte[] MakeCommand(string command) {
            return new byte[] { (byte)command[0], (byte)command[1], (byte)command[2], (byte)command[3] };
        }

        internal void SendStringToStdInput(string text) {
            if (_process == null) {
                throw new InvalidOperationException();
            }
            _process.StandardInput.Write(text);
        }

        #endregion

        #region Debugging Events

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
        public event EventHandler<BreakpointHitEventArgs> BreakpointHit;
        public event EventHandler<BreakpointEventArgs> BreakpointBindSucceeded;
        public event EventHandler<BreakpointEventArgs> BreakpointBindFailed;
        public event EventHandler<OutputEventArgs> DebuggerOutput;

        #endregion

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

        internal void Close() {
        }

        // This is duplicated throughout different assemblies in PythonTools, so search for it if you update it.
        internal static string GetPythonToolsInstallPath() {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (File.Exists(Path.Combine(path, "PyDebugAttach.dll"))) {
                return path;
            }

            // running from the GAC in remote attach scenario.  Look to the VS install dir.
            using (var configKey = OpenVisualStudioKey()) {
                var installDir = configKey.GetValue("InstallDir") as string;
                if (installDir != null) {
                    var toolsPath = Path.Combine(installDir, "Extensions\\Microsoft\\Python Tools for Visual Studio\\2.0");
                    if (File.Exists(Path.Combine(toolsPath, "PyDebugAttach.dll"))) {
                        return toolsPath;
                    }
                }
            }

            Debug.Assert(false, "Unable to determine Python Tools installation path");
            return string.Empty;
        }

        private static Win32.RegistryKey OpenVisualStudioKey() {
            if (Environment.Is64BitOperatingSystem) {
#if DEV12
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\12.0");
#elif DEV11
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\11.0");
#else
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
#endif
            } else {
#if DEV12
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\12.0");
#elif DEV11
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\11.0");
#else
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
#endif
            }
        }
    }
}
