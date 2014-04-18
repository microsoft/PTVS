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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Language;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using SR = Microsoft.PythonTools.Project.SR;

namespace Microsoft.PythonTools.Repl {
#if INTERACTIVE_WINDOW
    using IReplWindow = IInteractiveWindow;
    using IReplEvaluator = IInteractiveEngine;
#endif

    internal abstract class BasePythonReplEvaluator : IPythonReplEvaluator, IReplEvaluator, IMultipleScopeEvaluator, IPythonReplIntellisense {
        private CommandProcessorThread _curListener;
        private IReplWindow _window;
        private bool _multipleScopes = true, _attached;
        internal Task<ExecutionResult> _lastExecutionResult;
        private readonly PythonReplEvaluatorOptions _options;

        internal static readonly object InputBeforeReset = new object();    // used to mark buffers which are no longer valid because we've done a reset

        private static readonly byte[] RunCommandBytes = MakeCommand("run ");
        private static readonly byte[] AbortCommandBytes = MakeCommand("abrt");
        private static readonly byte[] SetThreadAndFrameCommandBytes = MakeCommand("sett");
        private static readonly byte[] ExitCommandBytes = MakeCommand("exit");
        private static readonly byte[] GetSignaturesCommandBytes = MakeCommand("sigs");
        private static readonly byte[] GetMembersCommandBytes = MakeCommand("mems");
        private static readonly byte[] GetModulesListCommandBytes = MakeCommand("mods");
        private static readonly byte[] SetModuleCommandBytes = MakeCommand("setm");
        private static readonly byte[] InputLineCommandBytes = MakeCommand("inpl");
        private static readonly byte[] ExecuteFileCommandBytes = MakeCommand("excf");
        private static readonly byte[] ExecuteFileExCommandBytes = MakeCommand("excx");
        private static readonly byte[] DebugAttachCommandBytes = MakeCommand("dbga");

        const string ExecuteFileEx_Script = "script";
        const string ExecuteFileEx_Module = "module";
        const string ExecuteFileEx_Process = "process";

        protected BasePythonReplEvaluator(PythonReplEvaluatorOptions options) {
            _options = options;
        }

        protected abstract PythonLanguageVersion AnalyzerProjectLanguageVersion { get; }

        protected abstract PythonLanguageVersion LanguageVersion { get; }

        internal abstract string DisplayName { get; }

        internal PythonReplEvaluatorOptions CurrentOptions {
            get {
                return _options;
            }
        }

        protected virtual void OnConnected() {
        }

        protected void SetMultipleScopes(bool multipleScopes) {
            if (multipleScopes != _multipleScopes) {
                OnMultipleScopeSupportChanged();
                _multipleScopes = multipleScopes;
            }
        }

        protected void OnMultipleScopeSupportChanged() {
            var multiScopeSupportChanged = MultipleScopeSupportChanged;
            if (multiScopeSupportChanged != null) {
                multiScopeSupportChanged(this, EventArgs.Empty);
            }
        }

        internal void EnsureConnected() {
            if (_curListener == null) {
                UIThread.Invoke(() => {
                    if (_curListener == null) {
                        Connect();
                    }
                });
            }
        }

        #region IReplEvaluator Members

        protected virtual void WriteInitializationMessage() {
            Window.WriteLine(SR.GetString(SR.ReplInitializationMessage));
        }

        public Task<ExecutionResult> Initialize(IReplWindow window) {
            _window = window;
            _window.SetOptionValue(ReplOptions.CommandPrefix, "$");

            window.SetOptionValue(ReplOptions.UseSmartUpDown, CurrentOptions.ReplSmartHistory);
            UpdatePrompts(true);
            window.SetOptionValue(ReplOptions.DisplayPromptInMargin, !CurrentOptions.InlinePrompts);
            window.SetOptionValue(ReplOptions.SupportAnsiColors, true);
            window.SetOptionValue(ReplOptions.FormattedPrompts, true);

            WriteInitializationMessage();

            _window.TextView.BufferGraph.GraphBuffersChanged += BufferGraphGraphBuffersChanged;
            return ExecutionResult.Succeeded;
        }

        public void ActiveLanguageBufferChanged(ITextBuffer currentBuffer, ITextBuffer previousBuffer) {
        }

        private void BufferGraphGraphBuffersChanged(object sender, GraphBuffersChangedEventArgs e) {
            foreach (var removed in e.RemovedBuffers) {
                BufferParser parser;
                if (removed.Properties.TryGetProperty(typeof(BufferParser), out parser)) {
                    parser.RemoveBuffer(removed);
                }
            }
        }

        protected abstract void Connect();

        protected static void CreateConnection(out Socket socket, out int portNum) {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            socket.Listen(0);
            portNum = ((IPEndPoint)socket.LocalEndPoint).Port;
        }

        // If stream is null, creates a command processor that will listen for incoming connections on the socket (used for regular REPL and for local debugging REPL).
        // If stream is not null, creates a command processor that will use that stream as the REPL connection (used for remote debugging REPL).
        protected void CreateCommandProcessor(Socket socket, Stream stream, bool redirectStdOutput, Process process) {
            _curListener = new CommandProcessorThread(this, socket, stream, redirectStdOutput, process);
        }

        class CommandProcessorThread {
            private readonly BasePythonReplEvaluator _eval;
            private readonly object _socketLock = new object();
            private readonly Process _process;
            internal bool _connected;
            private Socket _socket;
            private Stream _stream;
            private TaskCompletionSource<ExecutionResult> _completion;
            private string _executionText, _executionFile, _executionExtraArgs, _executionFileType;
            private AutoResetEvent _completionResultEvent = new AutoResetEvent(false);
            private OverloadDoc[] _overloads;
            private Dictionary<string, string> _fileToModuleName;
            private Dictionary<string, bool> _allModules;
            internal bool _exitedIsExpected;
            private StringBuilder _preConnectionOutput;
            internal string _currentScope = "__main__";
            private MemberResults _memberResults;
            internal string _prompt1 = ">>> ", _prompt2 = "... ";
#if DEBUG
            private Thread _socketLockedThread;
#endif

            public CommandProcessorThread(BasePythonReplEvaluator evaluator, Socket socket, Stream stream, bool redirectOutput, Process process) {
                _eval = evaluator;
                _socket = socket;
                _stream = stream;
                _process = process;

                var outputThread = new Thread(OutputThread);
                outputThread.Name = "PythonReplEvaluator: " + evaluator.DisplayName;
                outputThread.Start();

                if (redirectOutput) {
                    Utilities.CheckNotNull(_process);
                    _process.OutputDataReceived += new DataReceivedEventHandler(StdOutReceived);
                    _process.ErrorDataReceived += new DataReceivedEventHandler(StdErrReceived);
                    _process.EnableRaisingEvents = true;
                    _process.Exited += new EventHandler(ProcessExited);

                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                }
            }

            private void ProcessExited(object sender, EventArgs e) {
                _connected = false;
                if (_preConnectionOutput != null) {
                    lock (_preConnectionOutput) {
                        Window.WriteError(FixNewLines(_preConnectionOutput.ToString()));
                    }
                }
                if (!_exitedIsExpected) {
                    Window.WriteError("The Python REPL process has exited\r\n");
                }
                _exitedIsExpected = false;
            }

            private void StdErrReceived(object sender, DataReceivedEventArgs e) {
                if (e.Data != null) {
                    if (!_connected) {
                        AppendPreConnectionOutput(e);
                    } else {
                        Window.WriteError(e.Data + Environment.NewLine);
                    }
                }
            }

            private void StdOutReceived(object sender, DataReceivedEventArgs e) {
                if (e.Data != null) {
                    if (!_connected) {
                        AppendPreConnectionOutput(e);
                    } else {
                        Window.WriteOutput(FixNewLines(e.Data) + Environment.NewLine);
                    }
                }
            }

            private void AppendPreConnectionOutput(DataReceivedEventArgs e) {
                if (_preConnectionOutput == null) {
                    Interlocked.CompareExchange(ref _preConnectionOutput, new StringBuilder(), null);
                }

                lock (_preConnectionOutput) {
                    _preConnectionOutput.Append(FixNewLines(e.Data) + Environment.NewLine);
                }
            }

            public void OutputThread() {
                try {
                    if (_stream == null) {
                        _socket = _socket.Accept();
                        _stream = new NetworkStream(_socket, ownsSocket: true);
                    }
                    using (new SocketLock(this)) {
                        _connected = true;
                    }

                    _eval.OnConnected();

                    using (new SocketLock(this)) {
                        if (_executionFile != null) {
                            SendExecuteFile(_executionFile, _executionExtraArgs, _executionFileType);
                            _executionFile = null;
                            _executionExtraArgs = null;
                        }

                        if (_executionText != null) {
                            Trace.TraceInformation("Executing delayed text: " + _executionText);
                            SendExecuteText(_executionText);
                            _executionText = null;
                        }
                    }

                    while (true) {
                        Stream stream = _stream;
                        if (stream == null) {
                            break;
                        }

                        string cmd = stream.ReadAsciiString(4);

                        using (new SocketLock(this)) {
                            Trace.TraceInformation("Repl {0} received command: {1}", _eval.DisplayName, cmd);
                            switch (cmd) {
                                case "DONE": HandleExecutionDone(); break;
                                case "ERRE": HandleExecutionError(); break;
                                case "STDO": HandleOutput(); break;
                                case "STDE": HandleError(); break;
                                case "MERR": HandleMemberResultError(); break;
                                case "MRES": HandleMemberResult(); break;
                                case "SRES": HandleSigResult(); break;
                                case "SERR": HandleSigError(); break;
                                case "MODS": HandleModuleList(); break;
                                case "IMGD": HandleImageDisplay(); break;
                                case "MODC": HandleModulesChanged(); break;
                                case "PRPC": HandlePromptChanged(); break;
                                case "RDLN": HandleReadLine(); break;
                                case "DETC": HandleDebuggerDetach(); break;
                                case "DPNG": DisplayPng(); break;
                                case "EXIT":
                                    // REPL has exited
                                    Stream.Write(ExitCommandBytes);
                                    return;
                            }
                        }
                    }
                } catch (IOException) {
                    _stream = null;
                } catch (SocketException) {
                    _stream = null;
                } catch (DisconnectedException) {
                } catch (NullReferenceException) {
                }

                lock (this) {
                    if (_completion != null) {
                        bool success = _completion.TrySetCanceled();
                        Debug.Assert(success);
                        _completion = null;
                    }
                }
            }

            private Stream Stream {
                get {
                    var socket = Socket; // for Socket.get state checks
                    var res = _stream;
                    if (res == null) {
                        throw new DisconnectedException("The interactive window has become disconnected from the remote process.  Please reset the window.");
                    }
                    return res;
                }
            }

            private Socket Socket {
                get {
#if DEBUG
                    Debug.Assert(_socketLockedThread == Thread.CurrentThread);
#endif
                    for (int i = 0; i < 40 && !_connected; i++) {
                        // wait for connection...
                        System.Threading.Thread.Sleep(100);
                    }

                    var res = _socket;
                    if (res == null) {
                        throw new DisconnectedException("The interactive window has become disconnected from the remote process.  Please reset the window.");
                    }

                    return res;
                }
            }

            class DisconnectedException : Exception {
                public DisconnectedException(string message)
                    : base(message) {
                }
            }

            private void HandleReadLine() {
                // perform the input on a new thread so that we don't block additional commands (such as output) from being processed by us
                // (this is called on the output thread)
                ThreadPool.QueueUserWorkItem(x => {
                    string input = Window.ReadStandardInput();
                    input = input != null ? UnfixNewLines(input) : "\n";
                    try {
                        using (new SocketLock(this)) {
                            if (Stream != null) {
                                Stream.Write(InputLineCommandBytes);
                                SendString(input);
                            }
                        }
                    } catch (IOException) {
                    } catch (SocketException) {
                    } catch (DisconnectedException) {
                    } catch (NullReferenceException) {
                    }
                });
            }

            private void HandleDebuggerDetach() {
                _eval._attached = false;
            }

            private void DisplayPng() {
                int len = Stream.ReadInt32();
                byte[] buffer = new byte[len];
                if (len != 0) {
                    int bytesRead = 0;
                    do {
                        bytesRead += Stream.Read(buffer, bytesRead, len - bytesRead);
                    } while (bytesRead != len);
                }

                DisplayImage(buffer);
            }

            internal string DoDebugAttach() {
                if (_eval._attached) {
                    return "Cannot attach to debugger when already attached.";
                }

                PythonProcess debugProcess;
                using (new SocketLock(this)) {
                    Stream.Write(DebugAttachCommandBytes);
                    debugProcess = PythonProcess.AttachRepl(Stream, _process.Id, _eval.AnalyzerProjectLanguageVersion);
                }

                // TODO: Surround in SocketUnlock
                var debugTarget = new VsDebugTargetInfo2();
                IntPtr pDebugInfo = IntPtr.Zero, pDebugEngines = IntPtr.Zero;
                try {
                    debugTarget.guidLaunchDebugEngine = AD7Engine.DebugEngineGuid;
                    debugTarget.dwDebugEngineCount = 1;

                    debugTarget.dlo = (uint)DEBUG_LAUNCH_OPERATION.DLO_Custom;
                    debugTarget.bstrExe = debugProcess.ProcessGuid.ToString();
                    debugTarget.cbSize = (uint)Marshal.SizeOf(typeof(VsDebugTargetInfo2));
                    debugTarget.bstrCurDir = "";
                    debugTarget.guidPortSupplier = new Guid("{708C1ECA-FF48-11D2-904F-00C04FA302A1}");     // local port supplier
                    debugTarget.LaunchFlags = (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_WaitForAttachComplete | (uint)__VSDBGLAUNCHFLAGS5.DBGLAUNCH_BreakOneProcess;
                    debugTarget.bstrOptions = AD7Engine.AttachRunning + "=True";
                    debugTarget.pDebugEngines = pDebugEngines = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(Guid)));
                    Marshal.StructureToPtr(AD7Engine.DebugEngineGuid, debugTarget.pDebugEngines, false);
                    pDebugInfo = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(VsDebugTargetInfo2)));
                    Marshal.StructureToPtr(debugTarget, pDebugInfo, false);
                    
                    var debugger = (IVsDebugger2)PythonToolsPackage.GetGlobalService(typeof(SVsShellDebugger));
                    int hr = debugger.LaunchDebugTargets2(1, pDebugInfo);

                    if (ErrorHandler.Failed(hr)) {
                        var uiShell = (IVsUIShell)PythonToolsPackage.GetGlobalService(typeof(SVsUIShell));
                        string errorText;
                        uiShell.GetErrorInfo(out errorText);
                        if (String.IsNullOrWhiteSpace(errorText)) {
                            errorText = "Unknown Error: " + hr;
                        }
                        return errorText;
                    } else {
                        _eval._attached = true;
                    }
                } finally {
                    Marshal.FreeCoTaskMem(pDebugInfo);
                    Marshal.FreeCoTaskMem(pDebugEngines);
                }

                GC.KeepAlive(debugProcess);
                return null;
            }

            /// <summary>
            /// Replaces \r\n with \n
            /// </summary>
            private string UnfixNewLines(string input) {
                StringBuilder res = new StringBuilder();
                for (int i = 0; i < input.Length; i++) {
                    if (input[i] == '\r' && i != input.Length - 1 && input[i + 1] == '\n') {
                        res.Append('\n');
                        i++;
                    } else {
                        res.Append(input[i]);
                    }
                }
                return res.ToString();
            }

            private void HandlePromptChanged() {
                // prompt change
                Trace.TraceInformation("Old prompts: \"{0}\" \"{1}\"", _prompt1, _prompt2);
                _prompt1 = Stream.ReadString();
                _prompt2 = Stream.ReadString();
                bool updateAll = Stream.ReadInt32() == 1;
                Trace.TraceInformation("New prompts: \"{0}\" \"{1}\" updateAll={2}", _prompt1, _prompt2, updateAll);
                if (Window != null) {
                    using (new SocketUnlock(this)) {
                        _eval.UpdatePrompts(updateAll);
                    }
                }
            }

            private void HandleModulesChanged() {
                // modules changed
                using (new SocketUnlock(this)) {
                    var curScopesChanged = _eval.AvailableScopesChanged;
                    if (curScopesChanged != null) {
                        curScopesChanged(this, EventArgs.Empty);
                    }
                }
            }

            private void HandleImageDisplay() {
                string filename = Stream.ReadString();
                try {
                    DisplayImage(File.ReadAllBytes(filename));
                } catch (IOException) {
                    // can't read the file
                    Window.WriteError("Unable to read image file " + filename);
                }

            }

            private void DisplayImage(byte[] bytes) {
                using (new SocketUnlock(this)) {
                    ((System.Windows.UIElement)Window.TextView).Dispatcher.BeginInvoke((Action)(() => {
                        try {
                            var imageSrc = new BitmapImage();
                            imageSrc.BeginInit();
                            imageSrc.StreamSource = new MemoryStream(bytes);
                            imageSrc.EndInit();

                            Window.WriteOutput(new Image() { Source = imageSrc });
                        } catch (IOException) {
                        }
                    }));
                }
            }

            private void HandleModuleList() {
                int moduleCount = Stream.ReadInt32();
                Dictionary<string, string> moduleNames = new Dictionary<string, string>();
                Dictionary<string, bool> allModules = new Dictionary<string, bool>();
                for (int i = 0; i < moduleCount; i++) {
                    string name = Stream.ReadString();
                    string filename = Stream.ReadString();
                    if (!String.IsNullOrWhiteSpace(filename)) {
                        moduleNames[filename] = name;
                        allModules[name] = true;
                    } else {
                        allModules[name] = false;
                    }
                }

                _fileToModuleName = moduleNames;
                _allModules = allModules;
                _completionResultEvent.Set();
            }

            private void HandleSigError() {
                _completionResultEvent.Set();
            }

            private void HandleSigResult() {
                int overloadCount = Stream.ReadInt32();
                OverloadDoc[] docs = new OverloadDoc[overloadCount];
                for (int i = 0; i < overloadCount; i++) {
                    string doc = Stream.ReadString();
                    int paramCount = Stream.ReadInt32();

                    ParameterResult[] parameters = new ParameterResult[paramCount];
                    for (int curParam = 0; curParam < paramCount; curParam++) {
                        string name = Stream.ReadString();
                        int equals = name.IndexOf('=');
                        if (equals < 0) {
                            parameters[curParam] = new ParameterResult(name);
                        } else {
                            parameters[curParam] = new ParameterResult(
                                name.Remove(equals),
                                null,
                                null,
                                // Even though it has a default, don't mark the
                                // parameter as optional (for consistency with
                                // signature help from the database)
                                false,
                                null,
                                name.Substring(equals + 1)
                            );
                        }
                    }

                    docs[i] = new OverloadDoc(doc, parameters);
                }
                _overloads = docs;
                _completionResultEvent.Set();
            }

            private void HandleMemberResult() {
                string typeName = Stream.ReadString();

                var instDict = ReadMemberDict();
                var typeDict = ReadMemberDict();
                _memberResults = new MemberResults(typeName, instDict, typeDict);

                _completionResultEvent.Set();
            }

            private void HandleMemberResultError() {
                _memberResults = null;
                _completionResultEvent.Set();
            }

            private void HandleOutput() {
                string data = Stream.ReadString();
                if (data != null) {
                    Trace.TraceInformation("Data = \"{0}\"", FixNewLines(data).Replace("\r\n", "\\r\\n"));
                    using (new SocketUnlock(this)) {
                        Window.WriteOutput(FixNewLines(data));
                    }
                }
            }

            private void HandleError() {
                string data = Stream.ReadString();
                Trace.TraceInformation("Data = \"{0}\"", FixNewLines(data).Replace("\r\n", "\\r\\n"));
                using (new SocketUnlock(this)) {
                    Window.WriteError(FixNewLines(data));
                }
            }

            private void HandleExecutionError() {
                // DONE command
                lock (this) {
                    if (_completion != null) {
                        _completion.SetResult(ExecutionResult.Failure);
                        _completion = null;
                    }
                }
            }

            private void HandleExecutionDone() {
                // DONE command
                lock (this) {
                    if (_completion != null) {
                        _completion.SetResult(ExecutionResult.Success);
                        _completion = null;
                    }
                }
            }

            static readonly string _noReplProcess = "Current interactive window is disconnected - please reset the process." + Environment.NewLine;
            public Task<ExecutionResult> ExecuteText(string text) {
                if (text.StartsWith("$")) {
                    _eval._window.WriteError(String.Format("Unknown command '{0}', use \"$help\" for help" + Environment.NewLine, text.Substring(1).Trim()));
                    return ExecutionResult.Failed;
                }

                Trace.TraceInformation("Executing text: {0}", text);
                using (new SocketLock(this)) {
                    if (!_connected) {
                        // delay executing the text until we're connected
                        Trace.TraceInformation("Delayed executing text");
                        _completion = new TaskCompletionSource<ExecutionResult>();
                        _executionText = text;
                        return _completion.Task;
                    }

                    try {
                        if (!Socket.Connected) {
                            _eval._window.WriteError(_noReplProcess);
                            return ExecutionResult.Failed;
                        }

                        _completion = new TaskCompletionSource<ExecutionResult>();

                        SendExecuteText(text);
                    } catch (DisconnectedException) {
                        _eval._window.WriteError(_noReplProcess);
                        return ExecutionResult.Failed;
                    } catch (IOException) {
                        _eval._window.WriteError(_noReplProcess);
                        return ExecutionResult.Failed;
                    } catch (SocketException) {
                        _eval._window.WriteError(_noReplProcess);
                        return ExecutionResult.Failed;
                    }

                    return _completion.Task;
                }
            }

            [DllImport("user32", CallingConvention = CallingConvention.Winapi)]
            static extern bool AllowSetForegroundWindow(int dwProcessId);

            private void SendExecuteText(string text) {
                if (_process != null) {
                    AllowSetForegroundWindow(_process.Id);
                }

                Stream.Write(RunCommandBytes);

                // normalize line endings to \n which is all older versions of CPython can handle.
                text = text.Replace("\r\n", "\n");
                text = text.Replace("\r", "\n");
                text = text.TrimEnd(' ');
                SendString(text);
            }

            public bool IsConnected {
                get {
                    using (new SocketLock(this)) {
                        return _connected && _socket != null && _socket.Connected && _stream != null;
                    }
                }
            }

            public Task<ExecutionResult> ExecuteFile(string filename, string extraArgs, string fileType) {
                using (new SocketLock(this)) {
                    if (!_connected) {
                        // delay executing the text until we're connected
                        _executionFile = filename;
                        _executionExtraArgs = extraArgs;
                        _executionFileType = fileType;
                        _completion = new TaskCompletionSource<ExecutionResult>();
                        return _completion.Task;
                    } else if (!Socket.Connected) {
                        _eval._window.WriteError(_noReplProcess);
                        return ExecutionResult.Failed;
                    }

                    SendExecuteFile(filename, extraArgs, fileType);
                    _completion = new TaskCompletionSource<ExecutionResult>();
                    return _completion.Task;
                }
            }

            private void SendExecuteFile(string filename, string extraArgs, string fileType) {
                if (_process != null) {
                    AllowSetForegroundWindow(_process.Id);
                }

                if (fileType == ExecuteFileEx_Script) {
                    Stream.Write(ExecuteFileCommandBytes);
                } else {
                    Stream.Write(ExecuteFileExCommandBytes);
                    SendString(fileType);
                }
                SendString(filename);
                SendString(extraArgs ?? String.Empty);
            }

            public void AbortCommand() {
                using (new SocketLock(this)) {
                    Stream.Write(AbortCommandBytes);
                }
            }

            public void SetThreadAndFrameCommand(long thread, int frame, FrameKind frameKind) {
                using (new SocketLock(this)) {
                    Stream.Write(SetThreadAndFrameCommandBytes);
                    Stream.WriteInt64(thread);
                    Stream.WriteInt32(frame);
                    Stream.WriteInt32((int)frameKind);
                    _currentScope = "<CurrentFrame>";
                }
            }

            public OverloadDoc[] GetSignatureDocumentation(string text) {
                using (new SocketLock(this)) {
                    if (!Socket.Connected || !_connected) {
                        return new OverloadDoc[0];
                    }
                    try {
                        Stream.Write(GetSignaturesCommandBytes);
                        SendString(text);
                    } catch (IOException) {
                        return new OverloadDoc[0];
                    } catch (SocketException) {
                        return new OverloadDoc[0];
                    }
                }

                if (_completionResultEvent.WaitOne(1000)) {
                    var res = _overloads;
                    _overloads = null;
                    return res;
                }
                return null;
            }

            public MemberResult[] GetMemberNames(string text) {
                _completionResultEvent.Reset();
                _memberResults = null;

                using (new SocketLock(this)) {
                    if (!Socket.Connected || !_connected) {
                        return new MemberResult[0];
                    }
                    try {
                        Stream.Write(GetMembersCommandBytes);
                        SendString(text);
                    } catch (IOException) {
                        return new MemberResult[0];
                    } catch (SocketException) {
                        return new MemberResult[0];
                    }
                }

                if (_completionResultEvent.WaitOne(1000) && _memberResults != null) {
                    MemberResult[] res = new MemberResult[_memberResults.TypeMembers.Count + _memberResults.InstanceMembers.Count];
                    int i = 0;
                    foreach (var member in _memberResults.TypeMembers) {
                        res[i++] = CreateMemberResult(member.Key, member.Value);
                    }
                    foreach (var member in _memberResults.InstanceMembers) {
                        res[i++] = CreateMemberResult(member.Key, member.Value);
                    }

                    _memberResults = null;
                    return res;
                }
                return null;
            }

            public string GetScopeByFilename(string path) {
                GetAvailableScopesAndKind();

                string res;
                if (_fileToModuleName.TryGetValue(path, out res)) {
                    return res;
                }
                return null;
            }

            public void SetScope(string scopeName) {
                try {
                    using (new SocketLock(this)) {
                        if (!String.IsNullOrWhiteSpace(scopeName)) {
                            Stream.Write(SetModuleCommandBytes);
                            SendString(scopeName);
                            _currentScope = scopeName;

                            _eval._window.WriteLine(String.Format("Current module changed to {0}", scopeName));
                        } else {
                            _eval._window.WriteLine(_currentScope);
                        }
                    }
                } catch (IOException) {
                    _eval._window.WriteError("Cannot change module, interactive window is disconnected.");
                } catch (SocketException) {
                    _eval._window.WriteError("Cannot change module, interactive window is disconnected.");
                }
            }

            public IEnumerable<string> GetAvailableUserScopes() {
                if (_connected) {   // if startup's taking a long time we won't be connected yet
                    try {
                        using (new SocketLock(this)) {
                            Stream.Write(GetModulesListCommandBytes);
                        }

                        _completionResultEvent.WaitOne(1000);

                        if (_fileToModuleName != null) {
                            return _fileToModuleName.Values;
                        }
                    } catch (DisconnectedException) {
                    }
                }
                return new string[0];
            }

            public IEnumerable<KeyValuePair<string, bool>> GetAvailableScopesAndKind() {
                if (_connected) {   // if startup's taking a long time we won't be connected yet
                    using (new SocketLock(this)) {
                        Stream.Write(GetModulesListCommandBytes);
                    }

                    _completionResultEvent.WaitOne(1000);

                    if (_allModules != null) {
                        return _allModules;
                    }
                }
                return new KeyValuePair<string, bool>[0];
            }

            public void Close() {
                // try and exit gracefully first, but if we're wedged don't both...
                if (Monitor.TryEnter(_socketLock, 200)) {
                    try {
                        using (new SocketLock(this)) {
                            if (_stream != null && _socket.Connected) {
                                var stream = Stream;
                                _stream = null;

                                try {
                                    stream.Write(ExitCommandBytes);
                                    stream.Close();
                                } catch (IOException) {
                                } catch (SocketException) {
                                }
                            }
                        }
                    } finally {
                        Monitor.Exit(_socketLock);
                    }
                }

                if (_process != null && !_process.HasExited) {
                    try {
                        _process.Kill();
                    } catch (InvalidOperationException) {
                    } catch (Win32Exception) {
                        // race w/ killing the process
                    }
                }

                lock (this) {
                    if (_completion != null) {
                        bool success = _completion.TrySetResult(ExecutionResult.Failure);
                        Debug.Assert(success);
                        _completion = null;
                    }
                }
            }

            private void SendString(string text) {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
                Stream.WriteInt32(bytes.Length);
                Stream.Write(bytes);
            }

            private Dictionary<string, string> ReadMemberDict() {
                int memCount = Stream.ReadInt32();
                var dict = new Dictionary<string, string>(memCount);
                for (int i = 0; i < memCount; i++) {
                    string memName = Stream.ReadString();
                    string typeName = Stream.ReadString();
                    dict[memName] = typeName;
                }

                return dict;
            }

            private IReplWindow Window {
                get {
                    return _eval._window;
                }
            }

            /// <summary>
            /// Helper struct for locking and tracking the current holding thread.  This allows
            /// us to assert that our socket is always accessed while the lock is held.  The lock
            /// needs to be held so that requests from the UI (switching scopes, getting module lists,
            /// executing text, etc...) won't become interleaved with interactions from the repl process 
            /// (output, execution completing, etc...).
            /// </summary>
            struct SocketLock : IDisposable {
                private readonly CommandProcessorThread _evaluator;

                public SocketLock(CommandProcessorThread evaluator) {
                    Monitor.Enter(evaluator._socketLock);
#if DEBUG
                    Debug.Assert(evaluator._socketLockedThread == null);
                    evaluator._socketLockedThread = Thread.CurrentThread;
#endif
                    _evaluator = evaluator;
                }

                public void Dispose() {
#if DEBUG
                    _evaluator._socketLockedThread = null;
#endif
                    Monitor.Exit(_evaluator._socketLock);
                }
            }

            /// <summary>
            /// Releases the socket lock and re-acquires it when finished.  This enables
            /// calling back into the repl window which could potentially call back to do
            /// work w/ the evaluator that we don't want to deadlock.
            /// </summary>
            struct SocketUnlock : IDisposable {
                private readonly CommandProcessorThread _evaluator;

                public SocketUnlock(CommandProcessorThread evaluator) {
#if DEBUG
                    Debug.Assert(evaluator._socketLockedThread == Thread.CurrentThread);
                    evaluator._socketLockedThread = null;
#endif
                    _evaluator = evaluator;
                    Monitor.Exit(evaluator._socketLock);
                }

                public void Dispose() {
                    Monitor.Enter(_evaluator._socketLock);
#if DEBUG
                    _evaluator._socketLockedThread = Thread.CurrentThread;
#endif
                }
            }

        }

        private static string CommandtoString(byte[] cmd_buffer) {
            return new string(new char[] { (char)cmd_buffer[0], (char)cmd_buffer[1], (char)cmd_buffer[2], (char)cmd_buffer[3] });
        }


        private void UpdatePrompts(bool updateAll) {
            if (CurrentOptions.UseInterpreterPrompts && _curListener != null) {
                _window.SetOptionValue(updateAll ? ReplOptions.PrimaryPrompt : ReplOptions.CurrentPrimaryPrompt, _curListener._prompt1);
                _window.SetOptionValue(updateAll ? ReplOptions.SecondaryPrompt : ReplOptions.CurrentSecondaryPrompt, _curListener._prompt2);
            } else {
                _window.SetOptionValue(updateAll ? ReplOptions.PrimaryPrompt : ReplOptions.CurrentPrimaryPrompt, CurrentOptions.PrimaryPrompt);
                _window.SetOptionValue(updateAll ? ReplOptions.SecondaryPrompt : ReplOptions.CurrentSecondaryPrompt, CurrentOptions.SecondaryPrompt);
            }
        }

        /// <summary>
        /// Transforms lone \r or \n into \r\n.
        /// </summary>
        private static string FixNewLines(string output) {
            StringBuilder fixedOutput = new StringBuilder();
            for (int i = 0; i < output.Length; i++) {
                if (output[i] == '\r' && i < output.Length - 1) {
                    if (output[i + 1] == '\n') {
                        i++;
                        if (fixedOutput != null) {
                            fixedOutput.Append("\r\n");
                        }
                        continue;
                    } else {
                        // single \r, change to \r\n
                        if (fixedOutput == null) {
                            fixedOutput = new StringBuilder(output, 0, i, output.Length + 128);
                        }
                        fixedOutput.Append("\r\n");
                    }
                } else if (output[i] == '\n') {
                    // single \n, change to \r\n
                    if (fixedOutput == null) {
                        fixedOutput = new StringBuilder(output, 0, i, output.Length + 128);
                    }
                    fixedOutput.Append("\r\n");
                } else if (fixedOutput != null) {
                    // normal char, and we've already transformed a \n or \r to \r\n
                    fixedOutput.Append(output[i]);
                }
            }
            return fixedOutput.ToString();
        }

        class MemberResults {
            public readonly string TypeName;
            public readonly Dictionary<string, string> InstanceMembers;
            public readonly Dictionary<string, string> TypeMembers;

            public MemberResults(string typeName, Dictionary<string, string> instMembers, Dictionary<string, string> typeMembers) {
                TypeName = typeName;
                InstanceMembers = instMembers;
                TypeMembers = typeMembers;
            }

        }
        private static bool IsCommand(byte[] cmd_buffer, string command) {
            return cmd_buffer[0] == command[0] && cmd_buffer[1] == command[1] && cmd_buffer[2] == command[2] && cmd_buffer[3] == command[3];
        }

        public virtual bool SupportsMultipleCompleteStatementInputs {
            get {
                return false;
            }
        }

        public bool CanExecuteText(string text) {
            int newLines = 0;
            for (int i = text.Length - 1; i >= 0; i--) {
                if (text[i] == '\n') {
                    if (++newLines == 1) {
                        return true;
                    }
                } else if (Char.IsWhiteSpace(text[i])) {
                    continue;
                } else {
                    break;
                }
            }

            var parser = Parser.CreateParser(new StringReader(text), LanguageVersion);
            ParseResult result;
            parser.ParseInteractiveCode(out result);
            if (result == ParseResult.Empty) {
                return false;
            } else if (!(result == ParseResult.Complete || result == ParseResult.Invalid)) {
                return false;
            }

            // Single-line: if it's executable, then execute
            if (text.IndexOf('\n') == -1) {
                return true;
            }

            return false;
        }

        private static byte[] MakeCommand(string command) {
            return new byte[] { (byte)command[0], (byte)command[1], (byte)command[2], (byte)command[3] };
        }

        public Task<ExecutionResult> ExecuteText(string text) {
            var res = _lastExecutionResult = ExecuteTextWorker(text);
            return res;
        }

        private Task<ExecutionResult> ExecuteTextWorker(string text) {
            var parser = Parser.CreateParser(new StringReader(text), LanguageVersion);
            ParseResult parseResult;
            parser.ParseInteractiveCode(out parseResult);
            if (parseResult == ParseResult.Empty) {
                return ExecutionResult.Succeeded;
            }

            EnsureConnected();
            if (_curListener != null) {
                return _curListener.ExecuteText(text);
            } else {
                _window.WriteError("Current interactive window is disconnected." + Environment.NewLine);
            }
            return ExecutionResult.Failed;
        }

        public bool IsDisconnected {
            get {
                var curListener = _curListener;
                if (curListener != null) {
                    return !curListener.IsConnected;
                }

                return false;
            }
        }

        public bool IsExecuting {
            get {
                return _lastExecutionResult != null && !_lastExecutionResult.IsCompleted;
            }
        }

        public Task<ExecutionResult> ExecuteFile(string filename, string extraArgs) {
            EnsureConnected();

            if (_curListener != null) {
                return _curListener.ExecuteFile(filename, extraArgs, ExecuteFileEx_Script);
            } else {
                _window.WriteError("Current interactive window is disconnected." + Environment.NewLine);
                return ExecutionResult.Failed;
            }
        }

        public void ExecuteFile(string filename) {
            EnsureConnected();

            string startupFilename, startupDir, extraArgs = null;
            UIThread.Invoke(() => {
                VsProjectAnalyzer analyzer;
                if (PythonToolsPackage.TryGetStartupFileAndDirectory(out startupFilename, out startupDir, out analyzer)) {
                    var startupProj = PythonToolsPackage.GetStartupProject();
                    if (startupProj != null) {
                        extraArgs = startupProj.GetProjectProperty(CommonConstants.CommandLineArguments, true);
                    }
                }
            });

            if (_curListener != null) {
                _curListener.ExecuteFile(filename, extraArgs, ExecuteFileEx_Script);
            } else {
                _window.WriteError("Current interactive window is disconnected." + Environment.NewLine);
            }
        }

        public Task<ExecutionResult> ExecuteModule(string moduleName, string arguments) {
            EnsureConnected();

            if (_curListener != null) {
                return _curListener.ExecuteFile(moduleName, arguments, ExecuteFileEx_Module);
            } else {
                _window.WriteError("Current interactive window is disconnected." + Environment.NewLine);
                return ExecutionResult.Failed;
            }
        }

        public Task<ExecutionResult> ExecuteProcess(string filename, string arguments) {
            EnsureConnected();

            if (_curListener != null) {
                return _curListener.ExecuteFile(filename, arguments, ExecuteFileEx_Process);
            } else {
                _window.WriteError("Current interactive window is disconnected." + Environment.NewLine);
                return ExecutionResult.Failed;
            }
        }

        public void AbortCommand() {
            if (_curListener != null) {
                _curListener.AbortCommand();
            }
        }

        public void SetThreadAndFrameCommand(long thread, int frame, FrameKind frameKind) {
            if (_curListener != null) {
                _curListener.SetThreadAndFrameCommand(thread, frame, frameKind);
            }
        }

        public Task<ExecutionResult> Reset() {
            return Reset(false);
        }

        public async Task<ExecutionResult> Reset(bool quiet) {
            // suppress reporting "failed to launch repl" process
            if (_curListener == null) {
                if (!quiet) {
                    _window.WriteError("Interactive window is not yet started." + Environment.NewLine);
                }
                return ExecutionResult.Success;
            }

            if (!quiet) {
                _window.WriteLine("Resetting execution engine");
            }

            _curListener._connected = true;
            _curListener._exitedIsExpected = quiet;

            Close();
            await UIThread.InvokeAsync(Connect).ConfigureAwait(false);

            BufferParser parser = null;

            var buffersBeforeReset = _window.TextView.BufferGraph.GetTextBuffers(TruePredicate);
            for (int i = 0; i < buffersBeforeReset.Count - 1; i++) {
                var buffer = buffersBeforeReset[i];

                if (!buffer.Properties.ContainsProperty(InputBeforeReset)) {
                    buffer.Properties.AddProperty(InputBeforeReset, InputBeforeReset);
                }

                if (parser == null) {
                    buffer.Properties.TryGetProperty<BufferParser>(typeof(BufferParser), out parser);
                }
            }
            if (parser != null) {
                parser.Requeue();
            }

            return ExecutionResult.Success;
        }

        private static bool TruePredicate(ITextBuffer buffer) {
            return true;
        }

        const string _splitRegexPattern = @"(?x)\s*,\s*(?=(?:[^""]*""[^""]*"")*[^""]*$)"; // http://regexhero.net/library/52/
        private static Regex _splitLineRegex = new Regex(_splitRegexPattern);

        public string FormatClipboard() {
            if (Clipboard.ContainsData(DataFormats.CommaSeparatedValue)) {
                string data = Clipboard.GetData(DataFormats.CommaSeparatedValue) as string;
                if (data != null) {
                    string[] lines = data.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    StringBuilder res = new StringBuilder();
                    res.AppendLine("[");
                    foreach (var line in lines) {
                        string[] items = _splitLineRegex.Split(line);

                        res.Append("  [");
                        for (int i = 0; i < items.Length; i++) {
                            res.Append(FormatItem(items[i]));

                            if (i != items.Length - 1) {
                                res.Append(", ");
                            }
                        }
                        res.AppendLine("],");
                    }
                    res.AppendLine("]");
                    return res.ToString();
                }
            }
            return EditFilter.RemoveReplPrompts(_window.TextView.Options.GetNewLineCharacter());
        }

        private static string FormatItem(string item) {
            if (String.IsNullOrWhiteSpace(item)) {
                return "None";
            }
            double doubleVal;
            int intVal;
            if (Double.TryParse(item, out doubleVal) ||
                Int32.TryParse(item, out intVal)) {
                return item;
            }

            if (item[0] == '"' && item[item.Length - 1] == '"' && item.IndexOf(',') != -1) {
                // remove outer quotes, remove "" escaping
                item = item.Substring(1, item.Length - 2).Replace("\"\"", "\"");
            }

            // put in single quotes and escape single quotes and backslashes
            return "'" + item.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
        }

        #endregion

        #region IDisposable Members

        public virtual void Dispose() {
            if (_window != null) {
                _window.TextView.BufferGraph.GraphBuffersChanged -= BufferGraphGraphBuffersChanged;
            }
            try {
                Close();
            } catch {
            }
        }

        public virtual void Close() {
            if (_curListener != null) {
                _curListener.Close();
                _curListener = null;
            }
            _attached = false;
        }

        #endregion

        #region IPythonReplIntellisense Members

        public bool LiveCompletionsOnly {
            get {
                return CurrentOptions.LiveCompletionsOnly;
            }
        }

        public MemberResult[] GetMemberNames(string text) {
            EnsureConnected();

            return _curListener.GetMemberNames(text);
        }

        public OverloadDoc[] GetSignatureDocumentation(string text) {
            EnsureConnected();

            return _curListener.GetSignatureDocumentation(text);
        }

        public IEnumerable<KeyValuePair<string, bool>> GetAvailableScopesAndKind() {
            if (_curListener != null) {
                return _curListener.GetAvailableScopesAndKind();
            }

            return new KeyValuePair<string, bool>[0];
        }

        #endregion

        private static MemberResult CreateMemberResult(string name, string typeName) {
            switch (typeName) {
                case "__builtin__.method-wrapper":
                case "__builtin__.builtin_function_or_method":
                case "__builtin__.method_descriptor":
                case "__builtin__.wrapper_descriptor":
                case "__builtin__.instancemethod":
                    return new MemberResult(name, PythonMemberType.Method);
                case "__builtin__.getset_descriptor":
                    return new MemberResult(name, PythonMemberType.Property);
                case "__builtin__.namespace#":
                    return new MemberResult(name, PythonMemberType.Namespace);
                case "__builtin__.type":
                    return new MemberResult(name, PythonMemberType.Class);
                case "__builtin__.function":
                    return new MemberResult(name, PythonMemberType.Function);
                case "__builtin__.module":
                    return new MemberResult(name, PythonMemberType.Module);
            }

            return new MemberResult(name, PythonMemberType.Field);
        }

        #region IMultipleScopeEvaluator Members

        public IEnumerable<string> GetAvailableScopes() {
            if (_curListener != null) {
                return _curListener.GetAvailableUserScopes();
            }
            return new string[0];
        }

        public event EventHandler<EventArgs> AvailableScopesChanged;
        public event EventHandler<EventArgs> MultipleScopeSupportChanged;

        public void SetScope(string scopeName) {
            EnsureConnected();

            _curListener.SetScope(scopeName);
        }

        public string CurrentScopeName {
            get {
                if (_curListener != null) {
                    return _curListener._currentScope;
                }
                return "<disconnected>";
            }
        }

        public bool EnableMultipleScopes {
            get { return _multipleScopes; }
        }

        #endregion

        internal string GetScopeByFilename(string path) {
            if (_curListener != null) {
                return _curListener.GetScopeByFilename(path);
            }
            return null;
        }

        public string PrimaryPrompt {
            get {
                if (_curListener != null) {
                    return _curListener._prompt1;
                }
                return ">>> ";
            }
        }

        public string SecondaryPrompt {
            get {
                if (_curListener != null) {
                    return _curListener._prompt2;
                }
                return "... ";
            }
        }

        internal string AttachDebugger() {
            EnsureConnected();

            return _curListener.DoDebugAttach();
        }

        internal IEnumerable<string> SplitCode(IEnumerable<string> lines) {
            if (SupportsMultipleCompleteStatementInputs) {
                yield return string.Join(Environment.NewLine, lines);
                yield break;
            }
            
            StringBuilder temp = new StringBuilder();
            string prevText = null;
            ParseResult? prevParseResult = null;

            using (var e = new PeekableEnumerator<string>(lines)) {
                bool skipNextMoveNext = false;
                while (skipNextMoveNext || e.MoveNext()) {
                    skipNextMoveNext = false;
                    var line = e.Current;

                    if (e.HasNext) {
                        temp.AppendLine(line);
                    } else {
                        temp.Append(line);
                    }
                    string newCode = temp.ToString();

                    var parser = Parser.CreateParser(new StringReader(newCode), LanguageVersion);
                    ParseResult result;
                    parser.ParseInteractiveCode(out result);

                    // if this parse is invalid then we need more text to be valid.
                    // But if this text is invalid and the previous parse was incomplete
                    // then appending more text won't fix things - the code in invalid, the user
                    // needs to fix it, so let's not break it up which would prevent that from happening.
                    if (result == ParseResult.Empty) {
                        if (!String.IsNullOrWhiteSpace(newCode)) {
                            // comment line, include w/ following code.
                            prevText = newCode;
                            prevParseResult = result;
                        } else {
                            temp.Clear();
                        }
                    } else if (result == ParseResult.Complete) {
                        yield return FixEndingNewLine(newCode);
                        temp.Clear();

                        prevParseResult = null;
                        prevText = null;
                    } else if (ShouldAppendCode(prevParseResult, result)) {
                        prevText = newCode;
                        prevParseResult = result;
                    } else if (prevText != null) {
                        // we have a complete input
                        yield return FixEndingNewLine(prevText);
                        temp.Clear();

                        // reparse this line so our state remains consistent as if we just started out.
                        skipNextMoveNext = true;
                        prevParseResult = null;
                        prevText = null;
                    } else {
                        prevParseResult = result;
                    }
                }
            }

            if (temp.Length > 0) {
                yield return FixEndingNewLine(temp.ToString());
            }
        }

        internal IEnumerable<string> SplitCode(string code) {
            if (SupportsMultipleCompleteStatementInputs) {
                return Enumerable.Repeat(code, 1);
            } else {
                return SplitCode(code.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None));
            }
        }

        private static bool ShouldAppendCode(ParseResult? prevParseResult, ParseResult result) {
            if (result == ParseResult.Invalid) {
                if (prevParseResult == ParseResult.IncompleteStatement || prevParseResult == ParseResult.Invalid) {
                    return false;
                }
            }
            return true;
        }

        public IReplWindow Window {
            get {
                return _window;
            }
            set {
                _window = value;
            }
        }

        private static string FixEndingNewLine(string prevText) {
            if ((prevText.IndexOf('\n') == prevText.LastIndexOf('\n')) &&
                (prevText.IndexOf('\r') == prevText.LastIndexOf('\r'))) {
                prevText = prevText.TrimEnd();
            } else if (prevText.EndsWith("\r\n\r\n")) {
                prevText = prevText.Substring(0, prevText.Length - 2);
            } else if (prevText.EndsWith("\n\n") || prevText.EndsWith("\r\r")) {
                prevText = prevText.Substring(0, prevText.Length - 1);
            }
            return prevText;
        }

        internal static void CloseReplWindow(object key) {
            var window = key as IReplWindow;
            Debug.Assert(window != null);
            if (window == null) {
                return;
            }

            // Close backends when the project closes so we don't
            // leave Python processes hanging around.
            var evaluator = window.Evaluator as BasePythonReplEvaluator;
            if (evaluator != null) {
                evaluator.Close();
            }

            // Close project-specific REPL windows when the project
            // closes.
            var pane = window as ToolWindowPane;
            var frame = pane != null ? pane.Frame as IVsWindowFrame : null;
            if (frame != null) {
                frame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
            }
        }
    }
}
