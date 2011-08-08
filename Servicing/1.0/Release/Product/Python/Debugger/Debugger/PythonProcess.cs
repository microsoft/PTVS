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
using System.Text;
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
        private readonly Process _process;
        private readonly Dictionary<int, PythonThread> _threads = new Dictionary<int, PythonThread>();
        private readonly Dictionary<int, PythonBreakpoint> _breakpoints = new Dictionary<int, PythonBreakpoint>();
        private readonly IdDispenser _ids = new IdDispenser();
        private readonly AutoResetEvent _lineEvent = new AutoResetEvent(false);         // set when result of setting current line returns
        private readonly Dictionary<int, CompletionInfo> _pendingExecutes = new Dictionary<int, CompletionInfo>();
        private readonly Dictionary<int, ChildrenInfo> _pendingChildEnums = new Dictionary<int, ChildrenInfo>();
        private readonly PythonLanguageVersion _langVersion;
        private readonly Guid _processGuid = Guid.NewGuid();
        private readonly List<string[]> _dirMapping;
        private readonly bool _delayUnregister;

        private bool _sentExited;
        private Socket _socket;
        private int _breakpointCounter;
        private bool _setLineResult;                    // contains result of attempting to set the current line of a frame
        private bool _createdFirstThread;
        private bool _stoppedForException;
        private int _defaultBreakMode;
        private ICollection<KeyValuePair<string, int>> _breakOn;

        private static Random _portGenerator = new Random();

        private PythonProcess(PythonLanguageVersion languageVersion) {
            _langVersion = languageVersion;

            ListenForConnection();
        }

        private PythonProcess(int pid) {
            _process = Process.GetProcessById(pid);
            _process.Exited += new EventHandler(_process_Exited);

            ListenForConnection();

            using (var result = DebugAttach.Attach(pid, DebugConnectionListener.ListenerPort, _processGuid)) {
                if (result.Error != ConnErrorMessages.None) {
                    throw new AttachException(result.Error);
                }

                _langVersion = (PythonLanguageVersion)result.LanguageVersion;
                if (!result.AttachDone.WaitOne(5000)) {
                    throw new AttachException(ConnErrorMessages.TimeOut);
                }
            }
        }

        private PythonProcess(Socket socket, int pid, PythonLanguageVersion version) {
            _process = Process.GetProcessById(pid);
            _process.Exited += new EventHandler(_process_Exited);
            
            _delayUnregister = true;
            
            ListenForConnection();

            socket.Send(BitConverter.GetBytes(DebugConnectionListener.ListenerPort));
            SendString(socket, _processGuid.ToString());
        }

        public PythonProcess(PythonLanguageVersion languageVersion, string exe, string args, string dir, string env, string interpreterOptions, PythonDebugOptions options = PythonDebugOptions.None, List<string[]> dirMapping = null)
            : this(languageVersion) {
            if (dir.EndsWith("\\")) {
                dir = dir.Substring(0, dir.Length - 1);
            }
            _dirMapping = dirMapping;
            var processInfo = new ProcessStartInfo(exe);

            processInfo.CreateNoWindow = false;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = false;

            processInfo.Arguments = 
                (String.IsNullOrWhiteSpace(interpreterOptions) ? "" : (interpreterOptions + " ")) +
                "\"" + Path.Combine(GetPythonToolsInstallPath(), "visualstudio_py_launcher.py") + "\" " +
                "\"" + dir + "\" " +
                " " + DebugConnectionListener.ListenerPort + " " +
                " " + _processGuid + " " +
                (((options & PythonDebugOptions.WaitOnAbnormalExit) != 0) ? " --wait-on-exception " : "") +
                (((options & PythonDebugOptions.WaitOnNormalExit) != 0) ? " --wait-on-exit " : "") +
                (((options & PythonDebugOptions.RedirectOutput) != 0) ? " --redirect-output " : "") +
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

        public static PythonProcess AttachRepl(Socket socket, int pid, PythonLanguageVersion version) {
            return new PythonProcess(socket, pid, version);
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
                return _process.Id;
            }
        }

        public Guid ProcessGuid {
            get {
                return _processGuid;
            }
        }

        public void Start(bool startListening = true) {
            _process.Start();
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
                        exitCode = _process.HasExited ? _process.ExitCode : -1;
                    } catch (InvalidOperationException) {
                        // debug attach, we didn't start the process...
                        exitCode = -1;
                    }
                    exited(this, new ProcessExitedEventArgs(exitCode));
                }
            }
        }

        public void WaitForExit() {
            _process.WaitForExit();
        }

        public void Terminate() {
            if (!_process.HasExited) {
                _socket = null;
                _process.Kill();
            }
        }

        public bool HasExited {
            get {
                return _process.HasExited;
            }
        }

        /// <summary>
        /// Breaks into the process.
        /// </summary>
        public void Break() {
            DebugWriteCommand("BreakAll");
            _socket.Send(BreakAllCommandBytes);
        }

        [Conditional("DEBUG")]
        private void DebugWriteCommand(string commandName) {
            Debug.WriteLine("PythonDebugger " + _processGuid + " Sending Command " + commandName);
        }

        public void Resume() {
            _stoppedForException = false;
            DebugWriteCommand("ResumeAll");
            _socket.Send(ResumeAllCommandBytes);
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

        public PythonLanguageVersion LanguageVersion {
            get {
                return _langVersion;
            }
        }

        public void SetExceptionInfo(int defaultBreakOnMode, ICollection<KeyValuePair<string, int>> breakOn) {
            lock (this) {
                if (_socket != null) {
                    SendExceptionInfo(defaultBreakOnMode, breakOn);
                } else {
                    _breakOn = breakOn.ToArray();
                    _defaultBreakMode = defaultBreakOnMode;
                }
            }
        }

        private void SendExceptionInfo(int defaultBreakOnMode, ICollection<KeyValuePair<string, int>> breakOn) {
            _socket.Send(SetExceptionInfoCommandBytes);
            _socket.Send(BitConverter.GetBytes(defaultBreakOnMode));
            _socket.Send(BitConverter.GetBytes(breakOn.Count));
            foreach (var item in breakOn) {
                _socket.Send(BitConverter.GetBytes(item.Value));
                SendString(_socket, item.Key);
            }
        }

        #endregion

        #region Debuggee Communcation

        internal void Connected(Socket socket) {
            Debug.WriteLine("Process Connected: " + _processGuid);

            lock (this) {
                _socket = socket;
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
            while (!_process.HasExited && _socket == null) {
                // wait for connection...
                System.Threading.Thread.Sleep(10);
            }

            byte[] cmd_buffer = new byte[4];
            try {
                Socket socket;
                while ((socket = _socket) != null && socket.Receive(cmd_buffer) == 4) {
                    Debug.WriteLine(String.Format("Received Debugger command: {0} ({1})", CommandtoString(cmd_buffer), _processGuid));

                    switch (CommandtoString(cmd_buffer)) {
                        case "EXCP": HandleException(socket); break;
                        case "BRKH": HandleBreakPointHit(socket); break;
                        case "NEWT": HandleThreadCreate(socket); break;
                        case "EXTT": HandleThreadExit(socket); break;
                        case "MODL": HandleModuleLoad(socket); break;
                        case "STPD": HandleStepDone(socket); break;
                        case "EXIT": HandleProcessExit(socket); return;
                        case "BRKS": HandleBreakPointSet(socket); break;
                        case "BRKF": HandleBreakPointFailed(socket); break;
                        case "LOAD": HandleProcessLoad(socket); break;
                        case "THRF": HandleThreadFrameList(socket); break;
                        case "EXCR": HandleExecutionResult(socket); break;
                        case "EXCE": HandleExecutionException(socket); break;
                        case "ASBR": HandleAsyncBreak(socket); break;
                        case "SETL": HandleSetLineResult(socket); break;
                        case "CHLD": HandleEnumChildren(socket); break;
                        case "OUTP": HandleDebuggerOutput(socket); break;
                        case "REQH": HandleRequestHandlers(socket); break;
                        case "DETC": _process_Exited(this, EventArgs.Empty); break; // detach, report process exit
                    }
                }
            } catch (SocketException) {
                _process_Exited(this, EventArgs.Empty);
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

        private void HandleRequestHandlers(Socket socket) {
            string filename = socket.ReadString();

            Debug.WriteLine("Exception handlers requested for: " + filename);
            var statements = GetHandledExceptionRanges(filename);

            _socket.Send(SetExceptionHandlerInfoCommandBytes);
            SendString(_socket, filename);

            _socket.Send(BitConverter.GetBytes(statements.Count));

            foreach (var t in statements) {
                _socket.Send(BitConverter.GetBytes(t.Item1));
                _socket.Send(BitConverter.GetBytes(t.Item2));

                foreach (var expr in t.Item3) {
                    SendString(_socket, expr);
                }
                SendString(_socket, "-");
            }
        }

        private void HandleDebuggerOutput(Socket socket) {
            int tid = socket.ReadInt();
            string output = socket.ReadString();

            PythonThread thread;
            if (_threads.TryGetValue(tid, out thread)) {
                var outputEvent = DebuggerOutput;
                if (outputEvent != null) {
                    outputEvent(this, new OutputEventArgs(thread, output));
                }
            }
        }

        private void HandleSetLineResult(Socket socket) {
            int res = socket.ReadInt();
            int tid = socket.ReadInt();
            int newLine = socket.ReadInt();
            _setLineResult = res != 0;
            if (_setLineResult) {
                _threads[tid].Frames[0].LineNo = newLine;
            }
            _lineEvent.Set();
        }

        private void HandleAsyncBreak(Socket socket) {
            int tid = socket.ReadInt();
            var thread = _threads[tid];
            var asyncBreak = AsyncBreakComplete;
            Debug.WriteLine("Received async break command from thread {0}", tid);
            if (asyncBreak != null) {
                asyncBreak(this, new ThreadEventArgs(thread));
            }
        }

        private void HandleExecutionException(Socket socket) {
            int execId = socket.ReadInt();
            CompletionInfo completion;

            lock (_pendingExecutes) {
                completion = _pendingExecutes[execId];
                _pendingExecutes.Remove(execId);
            }

            string exceptionText = socket.ReadString();
            completion.Completion(new PythonEvaluationResult(this, exceptionText, completion.Text, completion.Frame));
        }

        private void HandleExecutionResult(Socket socket) {
            int execId = socket.ReadInt();
            CompletionInfo completion;
            lock (_pendingExecutes) {
                completion = _pendingExecutes[execId];

                _pendingExecutes.Remove(execId);
                _ids.Free(execId);
            }
            completion.Completion(ReadPythonObject(socket, completion.Text, "", false, false, completion.Frame));
        }

        private void HandleEnumChildren(Socket socket) {
            int execId = socket.ReadInt();
            ChildrenInfo completion;

            lock (_pendingChildEnums) {
                completion = _pendingChildEnums[execId];
                _pendingChildEnums.Remove(execId);
            }

            int childCount = socket.ReadInt();
            bool childIsIndex = socket.ReadInt() == 1;
            bool childIsEnumerate = socket.ReadInt() == 1;
            PythonEvaluationResult[] res = new PythonEvaluationResult[childCount];
            for (int i = 0; i < res.Length; i++) {
                string expr = socket.ReadString();
                res[i] = ReadPythonObject(socket, completion.Text, expr, childIsIndex, childIsEnumerate, completion.Frame);
            }
            completion.Completion(res);
        }

        private PythonEvaluationResult ReadPythonObject(Socket socket, string text, string childText, bool childIsIndex, bool childIsEnumerate, PythonStackFrame frame) {
            string objRepr = socket.ReadString();
            string hexRepr = socket.ReadString();
            string typeName = socket.ReadString();
            bool isExpandable = socket.ReadInt() == 1;

            if ((typeName == "unicode" && LanguageVersion.Is2x()) ||
                (typeName == "str" && LanguageVersion.Is3x())) {
                objRepr = objRepr.FixupEscapedUnicodeChars();
            }
            return new PythonEvaluationResult(this, objRepr, hexRepr, typeName, text, childText, childIsIndex, childIsEnumerate, frame, isExpandable);
        }

        private void HandleThreadFrameList(Socket socket) {
            // list of thread frames
            var frames = new List<PythonStackFrame>();
            int tid = socket.ReadInt();
            var thread = _threads[tid];

            int frameCount = socket.ReadInt();
            for (int i = 0; i < frameCount; i++) {
                int startLine = socket.ReadInt();
                int endLine = socket.ReadInt();
                int lineNo = socket.ReadInt();
                string frameName = socket.ReadString();
                string filename = socket.ReadString();
                int argCount = socket.ReadInt();
                int varCount = socket.ReadInt();
                PythonEvaluationResult[] variables = new PythonEvaluationResult[varCount];
                var frame = new PythonStackFrame(thread, frameName, filename, startLine, endLine, lineNo, argCount, i);
                for (int j = 0; j < variables.Length; j++) {
                    string name = socket.ReadString();
                    variables[j] = ReadPythonObject(socket, name, "", false, false, frame);
                }
                frame.SetVariables(variables);
                frames.Add(frame);
            }

            Debug.WriteLine("Received frames for thread {0}", tid);
            thread.Frames = frames;
        }

        private void HandleProcessLoad(Socket socket) {
            Debug.WriteLine("Process loaded " + _processGuid);

            // process is loaded, no user code has run
            int threadId = socket.ReadInt();
            var thread = _threads[threadId];

            var loaded = ProcessLoaded;
            if (loaded != null) {
                loaded(this, new ThreadEventArgs(thread));
            }
        }

        private void HandleBreakPointFailed(Socket socket) {
            // break point failed to set
            int id = socket.ReadInt();
            var brkEvent = BreakpointBindFailed;
            if (brkEvent != null) {
                brkEvent(this, new BreakpointEventArgs(_breakpoints[id]));
            }
        }

        private void HandleBreakPointSet(Socket socket) {
            // break point successfully set
            int id = socket.ReadInt();
            PythonBreakpoint unbound;
            if (_breakpoints.TryGetValue(id, out unbound)) {
                var brkEvent = BreakpointBindSucceeded;
                if (brkEvent != null) {
                    brkEvent(this, new BreakpointEventArgs(unbound));
                }
            }
        }

        private void HandleProcessExit(Socket socket) {
            // process exit
            int exitCode = socket.ReadInt();
            var processExited = ProcessExited;
            if (processExited != null) {
                _sentExited = true;
                processExited(this, new ProcessExitedEventArgs(exitCode));
            }
            _socket.Send(ExitCommandBytes);
            _socket.Close();
            _socket = null;
        }

        private void HandleStepDone(Socket socket) {
            // stepping done
            int threadId = socket.ReadInt();
            var stepComp = StepComplete;
            if (stepComp != null) {
                stepComp(this, new ThreadEventArgs(_threads[threadId]));
            }
        }

        private void HandleModuleLoad(Socket socket) {
            // module load
            int moduleId = socket.ReadInt();
            string filename = socket.ReadString();
            if (filename != null) {
                Debug.WriteLine(String.Format("Module Loaded ({0}): {1}", moduleId, filename));
                var module = new PythonModule(moduleId, filename);

                var loaded = ModuleLoaded;
                if (loaded != null) {
                    loaded(this, new ModuleLoadedEventArgs(module));
                }
            }
        }

        private void HandleThreadExit(Socket socket) {
            // thread exit
            int threadId = socket.ReadInt();
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

        private void HandleThreadCreate(Socket socket) {
            // new thread
            int threadId = socket.ReadInt();
            var thread = _threads[threadId] = new PythonThread(this, threadId, _createdFirstThread);
            _createdFirstThread = true;

            var created = ThreadCreated;
            if (created != null) {
                created(this, new ThreadEventArgs(thread));
            }
        }

        private void HandleBreakPointHit(Socket socket) {
            int breakId = socket.ReadInt();
            int threadId = socket.ReadInt();
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

        private void HandleException(Socket socket) {
            string typeName = socket.ReadString();
            int tid = socket.ReadInt();
            string desc = socket.ReadString();
            if (typeName != null && desc != null) {
                Debug.WriteLine("Exception: " + desc);
                var excepRaised = ExceptionRaised;
                if (excepRaised != null) {
                    excepRaised(this, new ExceptionRaisedEventArgs(_threads[tid], new PythonException(typeName, desc)));
                }
            }
            _stoppedForException = true;
        }

        private static string CommandtoString(byte[] cmd_buffer) {
            return new string(new char[] { (char)cmd_buffer[0], (char)cmd_buffer[1], (char)cmd_buffer[2], (char)cmd_buffer[3] });
        }

        internal void SendStepOut(int identity) {
            DebugWriteCommand("StepOut");
            _socket.Send(StepOutCommandBytes);
            _socket.Send(BitConverter.GetBytes(identity));
        }

        internal void SendStepOver(int identity) {
            DebugWriteCommand("StepOver");
            _socket.Send(StepOverCommandBytes);
            _socket.Send(BitConverter.GetBytes(identity));
        }

        internal void SendStepInto(int identity) {
            DebugWriteCommand("StepInto");
            _socket.Send(StepIntoCommandBytes);
            _socket.Send(BitConverter.GetBytes(identity));
        }

        public void SendResumeThread(int threadId) {
            _stoppedForException = false;
            DebugWriteCommand("ResumeThread");
            // race w/ removing the breakpoint, let the thread continue
            _socket.Send(ResumeThreadCommandBytes);
            _socket.Send(BitConverter.GetBytes(threadId));
        }

        public void SendClearStepping(int threadId) {
            DebugWriteCommand("ClearStepping");
            // race w/ removing the breakpoint, let the thread continue
            _socket.Send(ClearSteppingCommandBytes);
            _socket.Send(BitConverter.GetBytes(threadId));
        }

        public void Detach() {
            DebugWriteCommand("Detach");
            try {
                _socket.Send(DetachCommandBytes);
            } catch (SocketException) {
                // socket is closed after we send detach
            }
        }

        [DllImport("kernel32", SetLastError = true, ExactSpelling = true)]
        public static extern Int32 WaitForSingleObject(SafeWaitHandle handle, Int32 milliseconds);

        internal void BindBreakpoint(PythonBreakpoint breakpoint) {
            DebugWriteCommand("Bind Breakpoint");
            _socket.Send(SetBreakPointCommandBytes);
            _socket.Send(BitConverter.GetBytes(breakpoint.Id));
            _socket.Send(BitConverter.GetBytes(breakpoint.LineNo));
            SendString(_socket, MapFile(breakpoint.Filename));
            SendCondition(breakpoint);
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
            SendString(_socket, breakpoint.Condition ?? "");
            _socket.Send(BitConverter.GetBytes(breakpoint.BreakWhenChanged ? 1 : 0));
        }

        internal void SetBreakPointCondition(PythonBreakpoint breakpoint) {
            DebugWriteCommand("Set BP Condition");
            _socket.Send(SetBreakPointConditionCommandBytes);
            _socket.Send(BitConverter.GetBytes(breakpoint.Id));
            SendCondition(breakpoint);
        }

        internal void ExecuteText(string text, PythonStackFrame pythonStackFrame, Action<PythonEvaluationResult> completion) {
            DebugWriteCommand("ExecuteText");
            _socket.Send(ExecuteTextCommandBytes);
            SendString(_socket, text);
            int executeId = _ids.Allocate();
            lock (_pendingExecutes) {
                _socket.Send(BitConverter.GetBytes(pythonStackFrame.Thread.Id));
                _socket.Send(BitConverter.GetBytes(pythonStackFrame.FrameId));
                _socket.Send(BitConverter.GetBytes(executeId));
                _pendingExecutes[executeId] = new CompletionInfo(completion, text, pythonStackFrame);
            }
        }

        internal void EnumChildren(string text, PythonStackFrame pythonStackFrame, bool childIsEnumerate, Action<PythonEvaluationResult[]> completion) {
            DebugWriteCommand("Enum Children");
            _socket.Send(GetChildrenCommandBytes);
            SendString(_socket, text);
            int executeId = _ids.Allocate();
            lock (_pendingChildEnums) {
                _socket.Send(BitConverter.GetBytes(pythonStackFrame.Thread.Id));
                _socket.Send(BitConverter.GetBytes(pythonStackFrame.FrameId));
                _socket.Send(BitConverter.GetBytes(executeId));
                _socket.Send(BitConverter.GetBytes(childIsEnumerate ? 1 : 0));
                _pendingChildEnums[executeId] = new ChildrenInfo(completion, text, pythonStackFrame);
            }
        }

        internal void RemoveBreakPoint(PythonBreakpoint unboundBreakpoint) {
            DebugWriteCommand("Remove Breakpoint");
            _breakpoints.Remove(unboundBreakpoint.Id);

            DisableBreakPoint(unboundBreakpoint);
        }

        internal void DisableBreakPoint(PythonBreakpoint unboundBreakpoint) {
            if (_socket != null && _socket.Connected) {
                DebugWriteCommand("Disable Breakpoint");
                _socket.Send(RemoveBreakPointCommandBytes);
                _socket.Send(BitConverter.GetBytes(unboundBreakpoint.LineNo));
                _socket.Send(BitConverter.GetBytes(unboundBreakpoint.Id));
            }
        }

        internal bool SetLineNumber(PythonStackFrame pythonStackFrame, int lineNo) {
            if (_stoppedForException) {
                return false;
            }

            DebugWriteCommand("Set Line Number");
            _setLineResult = false;
            _socket.Send(SetLineNumberCommand);
            _socket.Send(BitConverter.GetBytes(pythonStackFrame.Thread.Id));
            _socket.Send(BitConverter.GetBytes(pythonStackFrame.FrameId));
            _socket.Send(BitConverter.GetBytes(lineNo));

            // wait up to 2 seconds for line event...
            for (int i = 0; i < 20 &&
                _socket.Connected &&
                WaitForSingleObject(_lineEvent.SafeWaitHandle, 100) != 0; i++) {
            }

            return _setLineResult;
        }

        private void SendString(Socket socket, string str) {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            socket.Send(BitConverter.GetBytes(bytes.Length));
            socket.Send(bytes);
        }

        private int? ReadInt(Socket socket) {
            byte[] cmd_buffer = new byte[4];
            if (socket.Receive(cmd_buffer) == 4) {
                return BitConverter.ToInt32(cmd_buffer, 0);
            }
            return null;
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

        private static byte[] MakeCommand(string command) {
            return new byte[] { (byte)command[0], (byte)command[1], (byte)command[2], (byte)command[3] };
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
                    var toolsPath = Path.Combine(installDir, "Extensions\\Microsoft\\Python Tools for Visual Studio\\1.0");
                    if (File.Exists(Path.Combine(toolsPath, "PyDebugAttach.dll"))) {
                        return toolsPath;
                    }
                }
            }

            return null;
        }

        private static Win32.RegistryKey OpenVisualStudioKey() {
            if (Environment.Is64BitOperatingSystem) {
#if DEV11
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\11.0");
#else
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
#endif
            } else {
#if DEV11
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\11.0");
#else
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
#endif
            }
        }
    }
}
