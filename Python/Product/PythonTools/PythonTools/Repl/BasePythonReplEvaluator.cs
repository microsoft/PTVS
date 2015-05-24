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
using System.Diagnostics.CodeAnalysis;
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
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
#else
using Microsoft.VisualStudio.Repl;
#endif
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using SR = Microsoft.PythonTools.Project.SR;

#if DEV14_OR_LATER
using IReplEvaluator = Microsoft.VisualStudio.InteractiveWindow.IInteractiveEvaluator;
using IReplWindow = Microsoft.VisualStudio.InteractiveWindow.IInteractiveWindow;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
#endif

namespace Microsoft.PythonTools.Repl {
    internal abstract class BasePythonReplEvaluator : IPythonReplEvaluator, IPythonReplIntellisense, IReplEvaluator, IMultipleScopeEvaluator {
        private CommandProcessorThread _curListener;
        private IReplWindow _window;
        private bool _multipleScopes = true, _attached;
        internal Task<ExecutionResult> _lastExecutionResult;
        private readonly PythonReplEvaluatorOptions _options;
        internal readonly PythonToolsService _pyService;
        internal readonly IServiceProvider _serviceProvider;
#if DEV14_OR_LATER
        private IInteractiveWindowCommands _commands;
#endif

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

        protected BasePythonReplEvaluator(IServiceProvider serviceProvider, PythonToolsService pyService, PythonReplEvaluatorOptions options) {
            _pyService = pyService;
            _options = options;
            _serviceProvider = serviceProvider;
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
                _serviceProvider.GetUIThread().Invoke(() => {
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

#if !DEV14_OR_LATER
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
#endif

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

        // Creates a command processor that will listen for incoming connections on the socket (used for regular REPL and for local debugging REPL).
        protected void CreateCommandProcessor(Socket listenerSocket, bool redirectStdOutput, Process process) {
            _curListener = new CommandProcessorThread(this, listenerSocket, redirectStdOutput, process);
        }

        // Creates a command processor that will use the provided stream as the REPL connection (used for remote debugging REPL).
        protected void CreateCommandProcessor(Stream stream, bool redirectStdOutput, Process process) {
            _curListener = new CommandProcessorThread(this, stream, redirectStdOutput, process);
        }

        class CommandProcessorThread : IDisposable {
            private readonly BasePythonReplEvaluator _eval;

            // Of the following two, only one is non-null at any given time.
            // If _listenerSocket is not null, then the thread is waiting for an incoming debugger connection.
            // If _stream is not null, then it is attached to the debuggee.
            // If both are null, the thread is shutting down after detaching.
            private Socket _listenerSocket;
            private Stream _stream;

            // All access to _stream should be done while holding this lock. Don't lock this directly - use StreamLock instead.
            private readonly object _streamLock = new object();

            private TaskCompletionSource<ExecutionResult> _completion;
            private readonly object _completionLock = new object();

            private Action _deferredExecute;

            private readonly Process _process;
            private AutoResetEvent _completionResultEvent = new AutoResetEvent(false);
            private OverloadDoc[] _overloads;
            private Dictionary<string, string> _fileToModuleName;
            private Dictionary<string, bool> _allModules;
            private StringBuilder _preConnectionOutput;
            internal string _currentScope = "__main__";
            private MemberResults _memberResults;
            internal string _prompt1 = ">>> ", _prompt2 = "... ";
#if DEV14_OR_LATER
            internal string _userPrompt1, _userPrompt2;
#endif

            public CommandProcessorThread(BasePythonReplEvaluator evaluator, Stream stream, bool redirectOutput, Process process) {
                _eval = evaluator;
                _stream = stream;
                _process = process;
                StartOutputThread(redirectOutput);
            }

            public CommandProcessorThread(BasePythonReplEvaluator evaluator, Socket listenerSocket, bool redirectOutput, Process process) {
                _eval = evaluator;
                _listenerSocket = listenerSocket;
                _process = process;
                StartOutputThread(redirectOutput);
            }

            public bool IsConnected {
                get {
                    using (new StreamLock(this, throwIfDisconnected: false)) {
                        return _stream == null;
                    }
                }
            }

            public bool IsProcessExpectedToExit { get; set; }

            private void StartOutputThread(bool redirectOutput) {
                var outputThread = new Thread(OutputThread);
                outputThread.Name = "PythonReplEvaluator: " + _eval.DisplayName;
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
                using (new StreamLock(this, throwIfDisconnected: false)) {
                    var stream = _stream;
                    _stream = null;
                    if (stream != null) {
                        stream.Dispose();
                    }
                }

                if (_preConnectionOutput != null) {
                    lock (_preConnectionOutput) {
                        Window.WriteError(FixNewLines(_preConnectionOutput.ToString()));
                    }
                }

                if (!IsProcessExpectedToExit) {
                    Window.WriteError("The Python REPL process has exited\r\n");
                }
                IsProcessExpectedToExit = false;
            }

            private void StdErrReceived(object sender, DataReceivedEventArgs e) {
                if (e.Data != null) {
                    if (_stream == null) {
                        AppendPreConnectionOutput(e);
                    } else {
                        Window.WriteError(e.Data + Environment.NewLine);
                    }
                }
            }

            private void StdOutReceived(object sender, DataReceivedEventArgs e) {
                if (e.Data != null) {
                    if (_stream == null) {
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

            private void OutputThread() {
                try {
                    using (new StreamLock(this, throwIfDisconnected: false)) {
                        if (_stream == null) {
                            var socket = _listenerSocket.Accept();
                            _listenerSocket = null;
                            _stream = new NetworkStream(socket, ownsSocket: true);
                        }
                    }

                    _eval.OnConnected();

                    using (new StreamLock(this, throwIfDisconnected: true)) {
                        if (_deferredExecute != null) {
                            _deferredExecute();
                            _deferredExecute = null;
                        }
                    }

                    while (true) {
                        Stream stream;
                        using (new StreamLock(this, throwIfDisconnected: true)) {
                            stream = _stream;
                        }

                        // Release the lock while waiting for response, so that the main thread can issue commands etc.
                        // If the stream goes away, we'll get an IOException from this which we will catch below.
                        string cmd = stream.ReadAsciiString(4);

                        using (new StreamLock(this, throwIfDisconnected: true)) {
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
                                case "DPNG": HandleDisplayPng(); break;
                                case "EXIT":
                                    // REPL has exited
                                    _stream.Write(ExitCommandBytes);
                                    return;
                            }
                        }
                    }
                } catch (IOException) {
                } catch (SocketException) {
                } catch (ObjectDisposedException) {
                } finally {
                    using (new StreamLock(this, throwIfDisconnected: false)) {
                        _stream = null;
                    }
                }

                lock (_completionLock) {
                    if (_completion != null) {
                        bool success = _completion.TrySetCanceled();
                        Debug.Assert(success);
                        _completion = null;
                    }
                }
            }

            [Serializable]
            private class DisconnectedException : IOException {
                public DisconnectedException(string message)
                    : base(message) {
                }
            }

            private void ThrowIfDisconnected() {
                if (_stream == null) {
                    throw new DisconnectedException("The interactive window has become disconnected from the remote process. Please reset the window.");
                }
            }

            private void HandleReadLine() {
                // perform the input on a new thread so that we don't block additional commands (such as output) from being processed by us
                // (this is called on the output thread)
                ThreadPool.QueueUserWorkItem(x => {
#if DEV14_OR_LATER
                    string input = Window.ReadStandardInput().ReadToEnd();
#else
                    string input = Window.ReadStandardInput();
#endif
                    input = input != null ? UnfixNewLines(input) : "\n";
                    try {
                        using (new StreamLock(this, throwIfDisconnected: true)) {
                            _stream.Write(InputLineCommandBytes);
                            SendString(input);
                        }
                    } catch (IOException) {
                    }
                });
            }

            private void HandleDebuggerDetach() {
                _eval._attached = false;
            }

            private void HandleDisplayPng() {
                Debug.Assert(Monitor.IsEntered(_streamLock));

                int len = _stream.ReadInt32();
                byte[] buffer = new byte[len];
                if (len != 0) {
                    int bytesRead = 0;
                    do {
                        bytesRead += _stream.Read(buffer, bytesRead, len - bytesRead);
                    } while (bytesRead != len);
                }

                DisplayImage(buffer);
            }

            internal string DoDebugAttach() {
                if (_eval._attached) {
                    return "Cannot attach to debugger when already attached.";
                }

                PythonProcess debugProcess;
                using (new StreamLock(this, throwIfDisconnected: true)) {
                    _stream.Write(DebugAttachCommandBytes);
                    debugProcess = PythonProcess.AttachRepl(_stream, _process.Id, _eval.AnalyzerProjectLanguageVersion);
                }

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

                    var debugger = (IVsDebugger2)_eval._serviceProvider.GetService(typeof(SVsShellDebugger));
                    int hr = debugger.LaunchDebugTargets2(1, pDebugInfo);

                    if (ErrorHandler.Failed(hr)) {
                        var uiShell = (IVsUIShell)_eval._serviceProvider.GetService(typeof(SVsUIShell));
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
                Debug.Assert(Monitor.IsEntered(_streamLock));

                // prompt change
                Trace.TraceInformation("Old prompts: \"{0}\" \"{1}\"", _prompt1, _prompt2);
                _prompt1 = _stream.ReadString();
                _prompt2 = _stream.ReadString();
                bool updateAll = _stream.ReadInt32() == 1;
                Trace.TraceInformation("New prompts: \"{0}\" \"{1}\" updateAll={2}", _prompt1, _prompt2, updateAll);
#if !DEV14_OR_LATER
                if (Window != null) {
                    using (new StreamUnlock(this)) {
                        _eval.UpdatePrompts(updateAll);
                    }
                }
#endif
            }

            private void HandleModulesChanged() {
                // modules changed
                using (new StreamUnlock(this)) {
                    var curScopesChanged = _eval.AvailableScopesChanged;
                    if (curScopesChanged != null) {
                        curScopesChanged(this, EventArgs.Empty);
                    }
                }
            }

            private void HandleImageDisplay() {
                Debug.Assert(Monitor.IsEntered(_streamLock));

                string filename = _stream.ReadString();
                try {
                    DisplayImage(File.ReadAllBytes(filename));
                } catch (IOException) {
                    // can't read the file
                    Window.WriteError("Unable to read image file " + filename);
                }

            }

            private void DisplayImage(byte[] bytes) {
                using (new StreamUnlock(this)) {
                    ((System.Windows.UIElement)Window.TextView).Dispatcher.BeginInvoke((Action)(() => {
                        try {
                            var imageSrc = new BitmapImage();
                            imageSrc.BeginInit();
                            imageSrc.StreamSource = new MemoryStream(bytes);
                            imageSrc.EndInit();
#if DEV14_OR_LATER
                            Window.Write(new Image() { Source = imageSrc });
#else
                            Window.WriteOutput(new Image() { Source = imageSrc });
#endif
                        } catch (IOException) {
                        }
                    }));
                }
            }

            private void HandleModuleList() {
                Debug.Assert(Monitor.IsEntered(_streamLock));

                int moduleCount = _stream.ReadInt32();
                Dictionary<string, string> moduleNames = new Dictionary<string, string>();
                Dictionary<string, bool> allModules = new Dictionary<string, bool>();
                for (int i = 0; i < moduleCount; i++) {
                    string name = _stream.ReadString();
                    string filename = _stream.ReadString();
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
                Debug.Assert(Monitor.IsEntered(_streamLock));

                int overloadCount = _stream.ReadInt32();
                OverloadDoc[] docs = new OverloadDoc[overloadCount];
                for (int i = 0; i < overloadCount; i++) {
                    string doc = _stream.ReadString();
                    int paramCount = _stream.ReadInt32();

                    ParameterResult[] parameters = new ParameterResult[paramCount];
                    for (int curParam = 0; curParam < paramCount; curParam++) {
                        string name = _stream.ReadString();
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
                Debug.Assert(Monitor.IsEntered(_streamLock));

                string typeName = _stream.ReadString();
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
                Debug.Assert(Monitor.IsEntered(_streamLock));

                string data = _stream.ReadString();
                if (data != null) {
                    Trace.TraceInformation("Data = \"{0}\"", FixNewLines(data).Replace("\r\n", "\\r\\n"));
                    using (new StreamUnlock(this)) {
                        Window.WriteOutput(FixNewLines(data));
                    }
                }
            }

            private void HandleError() {
                Debug.Assert(Monitor.IsEntered(_streamLock));

                string data = _stream.ReadString();
                Trace.TraceInformation("Data = \"{0}\"", FixNewLines(data).Replace("\r\n", "\\r\\n"));
                using (new StreamUnlock(this)) {
                    Window.WriteError(FixNewLines(data));
                }
            }

            private void HandleExecutionError() {
                // ERRE command
                lock (_completionLock) {
                    if (_completion != null) {
                        _completion.SetResult(ExecutionResult.Failure);
                        _completion = null;
                    }
                }
            }

            private void HandleExecutionDone() {
                // DONE command
                lock (_completionLock) {
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

                Action send = () => {
                    if (_process != null) {
                        Microsoft.VisualStudioTools.Project.NativeMethods.AllowSetForegroundWindow(_process.Id);
                    }

                    _stream.Write(RunCommandBytes);

                    // normalize line endings to \n which is all older versions of CPython can handle.
                    text = text.Replace("\r\n", "\n");
                    text = text.Replace("\r", "\n");
                    text = text.TrimEnd(' ');
                    SendString(text);
                };

                Trace.TraceInformation("Executing text: {0}", text);
                using (new StreamLock(this, throwIfDisconnected: false)) {
                    if (_stream == null) {
                        // If we're still waiting for debuggee to connect to us, postpone the actual execution until we have the command stream.
                        if (_listenerSocket != null) {
                            Trace.TraceInformation("Deferred executing text because connection is not fully established yet.");
                            _deferredExecute = send;
                            _completion = new TaskCompletionSource<ExecutionResult>();
                            return _completion.Task;
                        } else {
                            _eval._window.WriteError(_noReplProcess);
                            return ExecutionResult.Failed;
                        }
                    }

                    try {
                        send();
                    } catch (IOException) {
                        _eval._window.WriteError(_noReplProcess);
                        return ExecutionResult.Failed;
                    }
                }

                lock (_completionLock) {
                    _completion = new TaskCompletionSource<ExecutionResult>();
                    return _completion.Task;
                }
            }

            public Task<ExecutionResult> ExecuteFile(string filename, string extraArgs, string fileType) {
                Action send = () => {
                    if (_process != null) {
                        Microsoft.VisualStudioTools.Project.NativeMethods.AllowSetForegroundWindow(_process.Id);
                    }

                    if (fileType == ExecuteFileEx_Script) {
                        _stream.Write(ExecuteFileCommandBytes);
                    } else {
                        _stream.Write(ExecuteFileExCommandBytes);
                        SendString(fileType);
                    }
                    SendString(filename);
                    SendString(extraArgs ?? String.Empty);
                };

                using (new StreamLock(this, throwIfDisconnected: false)) {
                    if (_stream == null) {
                        // If we're still waiting for debuggee to connect to us, postpone the actual execution until we have the command stream.
                        if (_listenerSocket != null) {
                            _deferredExecute = send;
                            _completion = new TaskCompletionSource<ExecutionResult>();
                            return _completion.Task;
                        } else {
                            _eval._window.WriteError(_noReplProcess);
                            return ExecutionResult.Failed;
                        }
                    }

                    try {
                        send();
                    } catch (IOException) {
                        _eval._window.WriteError(_noReplProcess);
                        return ExecutionResult.Failed;
                    }
                }

                lock (_completionLock) {
                    _completion = new TaskCompletionSource<ExecutionResult>();
                    return _completion.Task;
                }

            }

            public void AbortCommand() {
                using (new StreamLock(this, throwIfDisconnected: true)) {
                    _stream.Write(AbortCommandBytes);
                }
            }

            public void SetThreadAndFrameCommand(long thread, int frame, FrameKind frameKind) {
                using (new StreamLock(this, throwIfDisconnected: true)) {
                    _stream.Write(SetThreadAndFrameCommandBytes);
                    _stream.WriteInt64(thread);
                    _stream.WriteInt32(frame);
                    _stream.WriteInt32((int)frameKind);
                    _currentScope = "<CurrentFrame>";
                }
            }

            public OverloadDoc[] GetSignatureDocumentation(string text) {
                using (new StreamLock(this, throwIfDisconnected: false)) {
                    if (_stream == null) {
                        return new OverloadDoc[0];
                    }
                    try {
                        _stream.Write(GetSignaturesCommandBytes);
                        SendString(text);
                    } catch (IOException) {
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

                using (new StreamLock(this, throwIfDisconnected: false)) {
                    if (_stream == null) {
                        return new MemberResult[0];
                    }
                    try {
                        _stream.Write(GetMembersCommandBytes);
                        SendString(text);
                    } catch (IOException) {
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
                    using (new StreamLock(this, throwIfDisconnected: true)) {
                        if (!String.IsNullOrWhiteSpace(scopeName)) {
                            _stream.Write(SetModuleCommandBytes);
                            SendString(scopeName);
                            _currentScope = scopeName;

                            _eval._window.WriteLine(String.Format("Current module changed to {0}", scopeName));
                        } else {
                            _eval._window.WriteLine(_currentScope);
                        }
                    }
                } catch (DisconnectedException) {
                    _eval._window.WriteError("Cannot change module, interactive window is disconnected.");
                } catch (IOException) {
                }
            }

            public IEnumerable<string> GetAvailableUserScopes() {
                try {
                    using (new StreamLock(this, throwIfDisconnected: true)) {
                        _stream.Write(GetModulesListCommandBytes);
                        _completionResultEvent.WaitOne(1000);
                        if (_fileToModuleName != null) {
                            return _fileToModuleName.Values;
                        }
                    }
                } catch (IOException) {
                }

                return new string[0];
            }

            public IEnumerable<KeyValuePair<string, bool>> GetAvailableScopesAndKind() {
                try {
                    using (new StreamLock(this, throwIfDisconnected: true)) {
                        _stream.Write(GetModulesListCommandBytes);
                        _completionResultEvent.WaitOne(1000);
                        if (_allModules != null) {
                            return _allModules;
                        }
                    }
                } catch (IOException) {
                }

                return new KeyValuePair<string, bool>[0];
            }

            [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_stream", Justification = "false positive")]
            public void Dispose() {
                var stream = _stream;

                // There is a potential race with another thread doing _stream = null, but even if that happens
                // we do have our own reference and can dispose the object.

                if (stream != null) {
                    try {
                        // Try and close the connection gracefully, but don't
                        // block on it indefinitely - if we're stuck waiting for
                        // too long, just drop it.
                        if (Monitor.TryEnter(_streamLock, 200)) {
                            // Set _stream to null inside the lock. We handle
                            // ObjectDisposed but not NullReferencExceptions in
                            // places where we access _stream inside the lock.
                            _stream = null;
                            try {
                                try {
                                    var ar = stream.BeginWrite(ExitCommandBytes, 0, ExitCommandBytes.Length, null, null);
                                    using (ar.AsyncWaitHandle) {
                                        ar.AsyncWaitHandle.WaitOne(200);
                                    }
                                    stream.EndWrite(ar);
                                } catch (IOException) {
                                }
                            } finally {
                                Monitor.Exit(_streamLock);
                            }
                        }
                    } finally {
                        stream.Dispose();
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

                lock (_completionLock) {
                    if (_completion != null) {
                        bool success = _completion.TrySetResult(ExecutionResult.Failure);
                        Debug.Assert(success);
                        _completion = null;
                    }
                    if (_completionResultEvent != null) {
                        _completionResultEvent.Dispose();
                        _completionResultEvent = null;
                    }
                }
            }

            private void SendString(string text) {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
                _stream.WriteInt32(bytes.Length);
                _stream.Write(bytes);
            }

            private Dictionary<string, string> ReadMemberDict() {
                int memCount = _stream.ReadInt32();
                var dict = new Dictionary<string, string>(memCount);
                for (int i = 0; i < memCount; i++) {
                    string memName = _stream.ReadString();
                    string typeName = _stream.ReadString();
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
            /// us to assert that our stream is always accessed while the lock is held.  The lock
            /// needs to be held so that requests from the UI (switching scopes, getting module lists,
            /// executing text, etc...) won't become interleaved with interactions from the repl process 
            /// (output, execution completing, etc...).
            /// </summary>
            struct StreamLock : IDisposable {
                private readonly CommandProcessorThread _evaluator;

                public StreamLock(CommandProcessorThread evaluator, bool throwIfDisconnected) {
                    Monitor.Enter(evaluator._streamLock);
                    try {
                        if (throwIfDisconnected) {
                            evaluator.ThrowIfDisconnected();
                        }
                        _evaluator = evaluator;

                    } catch {
                        // If any exceptions are thrown in the constructor, we
                        // must exit the lock to avoid a deadlock.
                        Monitor.Exit(evaluator._streamLock);
                        throw;
                    }
                }

                public void Dispose() {
                    Monitor.Exit(_evaluator._streamLock);
                }
            }

            /// <summary>
            /// Releases the stream lock and re-acquires it when finished.  This enables
            /// calling back into the repl window which could potentially call back to do
            /// work w/ the evaluator that we don't want to deadlock.
            /// </summary>
            struct StreamUnlock : IDisposable {
                private readonly CommandProcessorThread _evaluator;

                public StreamUnlock(CommandProcessorThread evaluator) {
                    Debug.Assert(Monitor.IsEntered(evaluator._streamLock));
                    _evaluator = evaluator;
                    Monitor.Exit(evaluator._streamLock);
                }

                public void Dispose() {
                    Monitor.Enter(_evaluator._streamLock);
                }
            }

        }

        private static string CommandtoString(byte[] cmd_buffer) {
            return new string(new char[] { (char)cmd_buffer[0], (char)cmd_buffer[1], (char)cmd_buffer[2], (char)cmd_buffer[3] });
        }

#if !DEV14_OR_LATER
        private void UpdatePrompts(bool updateAll) {
            if (CurrentOptions.UseInterpreterPrompts && _curListener != null) {
                _window.SetOptionValue(updateAll ? ReplOptions.PrimaryPrompt : ReplOptions.CurrentPrimaryPrompt, _curListener._prompt1);
                _window.SetOptionValue(updateAll ? ReplOptions.SecondaryPrompt : ReplOptions.CurrentSecondaryPrompt, _curListener._prompt2);
            } else {
                _window.SetOptionValue(updateAll ? ReplOptions.PrimaryPrompt : ReplOptions.CurrentPrimaryPrompt, CurrentOptions.PrimaryPrompt);
                _window.SetOptionValue(updateAll ? ReplOptions.SecondaryPrompt : ReplOptions.CurrentSecondaryPrompt, CurrentOptions.SecondaryPrompt);
            }
        }
#endif

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

#if DEV14_OR_LATER
        public bool CanExecuteCode(string text) {
            if (_commands.InCommand) {
                return true;
            }
#else
        public bool CanExecuteText(string text) {
#endif
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
#if DEV14_OR_LATER
            var res = _commands.TryExecuteCommand();
            if (res != null) {
                return res;
            }
#endif

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
            _serviceProvider.GetUIThread().Invoke(() => {
                VsProjectAnalyzer analyzer;
                if (PythonToolsPackage.TryGetStartupFileAndDirectory(_serviceProvider, out startupFilename, out startupDir, out analyzer)) {
                    var startupProj = PythonToolsPackage.GetStartupProject(_serviceProvider);
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

#if DEV14_OR_LATER
        public void AbortExecution() {
#else
        public void AbortCommand() {
#endif
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

            _curListener.IsProcessExpectedToExit = quiet;

            Close();
            await _serviceProvider.GetUIThread().InvokeAsync(Connect).ConfigureAwait(false);

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
            return EditFilter.RemoveReplPrompts(_pyService, _window.TextView.Options.GetNewLineCharacter());
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
                _curListener.Dispose();
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
                    return
#if DEV14_OR_LATER
                        _curListener._userPrompt1 ??
#endif
                        _curListener._prompt1;
                }
                return ">>> ";
            }
#if DEV14_OR_LATER
            set {
                if (_curListener != null) {
                    _curListener._userPrompt1 = value;
                }
            }
#endif
        }

        public string SecondaryPrompt {
            get {
                if (_curListener != null) {
                    return
#if DEV14_OR_LATER
                        _curListener._userPrompt2 ??
#endif
                        _curListener._prompt2;
                }
                return "... ";
            }
#if DEV14_OR_LATER
            set {
                if (_curListener != null) {
                    _curListener._userPrompt2 = value;
                }
            }
#endif
        }

        internal string AttachDebugger() {
            EnsureConnected();
            return _curListener.DoDebugAttach();
        }

        internal IEnumerable<string> TrimIndent(IEnumerable<string> lines) {
            string indent = null;
            foreach (var line in lines) {
                if (indent == null) {
                    indent = line.Substring(0, line.TakeWhile(char.IsWhiteSpace).Count());
                }

                if (line.StartsWith(indent)) {
                    yield return line.Substring(indent.Length);
                } else {
                    yield return line.TrimStart();
                }
            }
        }

        internal static IEnumerable<string> JoinCodeLines(IEnumerable<string> lines, PythonLanguageVersion version) {
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

                    var parser = Parser.CreateParser(new StringReader(newCode), version);
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
            return code.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        }

        internal IEnumerable<string> JoinCode(IEnumerable<string> code) {
            var split = JoinCodeLines(TrimIndent(code), LanguageVersion);
            if (SupportsMultipleCompleteStatementInputs) {
                return Enumerable.Repeat(string.Join(Environment.NewLine, split), 1);
            } else {
                return split;
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

#if DEV14_OR_LATER
        public IContentType ContentType {
            get {
                return _serviceProvider.GetPythonContentType();
            }
        }

        public IReplWindow CurrentWindow {
            get {
                return _window;
            }

            set {
                _window = value;
            }
        }

        public Task<ExecutionResult> InitializeAsync() {
            WriteInitializationMessage();
            _window.TextView.BufferGraph.GraphBuffersChanged += BufferGraphGraphBuffersChanged;

            _window.SetSmartUpDown(CurrentOptions.ReplSmartHistory);

            _commands = GetInteractiveCommands(_serviceProvider, _window, this);

            return ExecutionResult.Succeeded;
        }

        internal static IInteractiveWindowCommands GetInteractiveCommands(IServiceProvider serviceProvider, IInteractiveWindow window, IReplEvaluator eval) {
            var cmdFactory = serviceProvider.GetComponentModel().GetService<IInteractiveWindowCommandsFactory>();
            var cmds = serviceProvider.GetComponentModel().GetExtensions<IInteractiveWindowCommand>();
            string[] roles = eval.GetType().GetCustomAttributes(typeof(InteractiveWindowRoleAttribute), true).Select(r => ((InteractiveWindowRoleAttribute)r).Name).ToArray();

            return cmdFactory.CreateInteractiveCommands(window, "$", cmds.Where(x => IsCommandApplicable(x, roles)));
        }

        private static bool IsCommandApplicable(IInteractiveWindowCommand command, string[] supportedRoles) {
            bool applicable = true;
            string[] commandRoles = command.GetType().GetCustomAttributes(typeof(InteractiveWindowRoleAttribute), true).Select(r => ((InteractiveWindowRoleAttribute)r).Name).ToArray();
            if (supportedRoles.Length > 0) {
                // The window has one or more roles, so the following commands will be applicable:
                // - commands that don't specify a role
                // - commands that specify a role that matches one of the window roles
                if (commandRoles.Length > 0) {
                    applicable = supportedRoles.Intersect(commandRoles).Count() > 0;
                }
            } else {
                // The window doesn't have any role, so the following commands will be applicable:
                // - commands that don't specify any role
                applicable = commandRoles.Length == 0;
            }

            return applicable;
        }

        public Task<ExecutionResult> ResetAsync(bool initialize = true) {
            return Reset(false);
        }

        public Task<ExecutionResult> ExecuteCodeAsync(string text) {
            return ExecuteText(text);
        }

        public string GetPrompt() {
            if (_window != null &&
                _window.CurrentLanguageBuffer != null &&
                _window.CurrentLanguageBuffer.CurrentSnapshot.LineCount > 1) {
                return SecondaryPrompt;
            } else {
                return PrimaryPrompt;
            }
        }
#endif
    }

    static class ReplWindowExtensions {
#if DEV14_OR_LATER
        public static void WriteError(this IReplWindow window, string message) {
            window.ErrorOutputWriter.Write(message);
        }

        public static void WriteOutput(this IReplWindow window, string message) {
            AppendEscapedText(window, message);
        }

        public static void WriteLine(this IVsInteractiveWindow window, string message) {
            window.InteractiveWindow.WriteLine(message);
        }

        public static void Focus(this IVsInteractiveWindow window) {
            window.Show(true);
        }

        public static void Submit(this IInteractiveWindow window, IEnumerable<string> inputs) {
            window.SubmitAsync(inputs);
        }

        public static IReplEvaluator GetReplEvaluator(this ITextBuffer buffer) {
            var res = buffer.GetInteractiveWindow();
            if (res != null) {
                return res.Evaluator;
            }
            return null;
        }

        public static void SetSmartUpDown(this IVsInteractiveWindow window, bool setting) {
            window.InteractiveWindow.SetSmartUpDown(setting);
        }

        public static void SetSmartUpDown(this IReplWindow window, bool setting) {
            window.TextView.Options.SetOptionValue(InteractiveWindowOptions.SmartUpDown, setting);
        }

        public static bool CanExecuteText(this IReplEvaluator window, string text) {
            return window.CanExecuteCode(text);
        }

        public static void AbortCommand(this IReplEvaluator window) {
            window.AbortExecution();
        }

        public static void SetPrompts(this IInteractiveWindow window, string primary, string secondary) {
            var eval = window.Evaluator as BasePythonReplEvaluator;
            eval.PrimaryPrompt = primary;
            eval.SecondaryPrompt = secondary;
        }

        public static void UseInterpreterPrompts(this IInteractiveWindow window) {
            var eval = window.Evaluator as BasePythonReplEvaluator;
            eval.PrimaryPrompt = null;
            eval.SecondaryPrompt = null;
        }

        private static void AppendEscapedText(IInteractiveWindow window, string text, bool isError = false) {
            // http://en.wikipedia.org/wiki/ANSI_escape_code
            // process any ansi color sequences...
            ConsoleColor? color = null;
            List<ColoredSpan> colors;
            if (!window.OutputBuffer.Properties.TryGetProperty(ReplOutputClassifier.ColorKey, out colors)) {
                window.OutputBuffer.Properties[ReplOutputClassifier.ColorKey] = colors = new List<ColoredSpan>();
            }
            int start = 0, escape = text.IndexOf('\x1b');

            List<int> codes = new List<int>();
            while (escape != -1) {
                if (escape != start) {
                    // add unescaped text
                    if (isError) {
                        window.ErrorOutputWriter.Write(text.Substring(start, escape - start));
                    } else {
                        var span = window.Write(text.Substring(start, escape - start));
                        colors.Add(new ColoredSpan(span, color));
                    }
                }

                // process the escape sequence                
                if (escape < text.Length - 1 && text[escape + 1] == '[') {
                    // We have the Control Sequence Introducer (CSI) - ESC [

                    codes.Clear();
                    int? value = 0;

                    for (int i = escape + 2; i < text.Length; i++) { // skip esc + [
                        if (text[i] >= '0' && text[i] <= '9') {
                            // continue parsing the integer...
                            if (value == null) {
                                value = 0;
                            }
                            value = 10 * value.Value + (text[i] - '0');
                        } else if (text[i] == ';') {
                            if (value != null) {
                                codes.Add(value.Value);
                                value = null;
                            } else {
                                // CSI ; - invalid or CSI ### ;;, both invalid
                                break;
                            }
                        } else if (text[i] == 'm') {
                            if (value != null) {
                                codes.Add(value.Value);
                            }

                            // parsed a valid code
                            start = i + 1;
                            if (codes.Count == 0) {
                                // reset
                                color = null;
                            } else {
                                for (int j = 0; j < codes.Count; j++) {
                                    switch (codes[j]) {
                                        case 0: color = ConsoleColor.White; break;
                                        case 1: // bright/bold
                                            color |= ConsoleColor.DarkGray;
                                            break;
                                        case 2: // faint

                                        case 3: // italic
                                        case 4: // single underline
                                            break;
                                        case 5: // blink slow
                                        case 6: // blink fast
                                            break;
                                        case 7: // negative
                                        case 8: // conceal
                                        case 9: // crossed out
                                        case 10: // primary font
                                        case 11: // 11-19, n-th alternate font
                                            break;
                                        case 21: // bright/bold off 
                                            color &= ~ConsoleColor.DarkGray;
                                            break;
                                        case 22: // normal intensity
                                        case 24: // underline off
                                            break;
                                        case 25: // blink off
                                            break;
                                        case 27: // image - postive
                                        case 28: // reveal
                                        case 29: // not crossed out
                                        case 30: color = ConsoleColor.Black | ((color ?? ConsoleColor.Black) & ConsoleColor.DarkGray); break;
                                        case 31: color = ConsoleColor.DarkRed | ((color ?? ConsoleColor.Black) & ConsoleColor.DarkGray); break;
                                        case 32: color = ConsoleColor.DarkGreen | ((color ?? ConsoleColor.Black) & ConsoleColor.DarkGray); break;
                                        case 33: color = ConsoleColor.DarkYellow | ((color ?? ConsoleColor.Black) & ConsoleColor.DarkGray); break;
                                        case 34: color = ConsoleColor.DarkBlue | ((color ?? ConsoleColor.Black) & ConsoleColor.DarkGray); break;
                                        case 35: color = ConsoleColor.DarkMagenta | ((color ?? ConsoleColor.Black) & ConsoleColor.DarkGray); break;
                                        case 36: color = ConsoleColor.DarkCyan | ((color ?? ConsoleColor.Black) & ConsoleColor.DarkGray); break;
                                        case 37: color = ConsoleColor.Gray | ((color ?? ConsoleColor.Black) & ConsoleColor.DarkGray); break;
                                        case 38: // xterm 286 background color
                                        case 39: // default text color
                                            color = null;
                                            break;
                                        case 40: // background colors
                                        case 41:
                                        case 42:
                                        case 43:
                                        case 44:
                                        case 45:
                                        case 46:
                                        case 47: break;
                                        case 90: color = ConsoleColor.DarkGray; break;
                                        case 91: color = ConsoleColor.Red; break;
                                        case 92: color = ConsoleColor.Green; break;
                                        case 93: color = ConsoleColor.Yellow; break;
                                        case 94: color = ConsoleColor.Blue; break;
                                        case 95: color = ConsoleColor.Magenta; break;
                                        case 96: color = ConsoleColor.Cyan; break;
                                        case 97: color = ConsoleColor.White; break;
                                    }
                                }
                            }
                            break;
                        } else {
                            // unknown char, invalid escape
                            break;
                        }
                    }

                    escape = text.IndexOf('\x1b', escape + 1);
                }// else not an escape sequence, process as text
            }

            if (start != text.Length) {
                if (isError) {
                    window.ErrorOutputWriter.Write(text.Substring(start, escape - start));
                } else {
                    var span = window.Write(text.Substring(start));
                    colors.Add(new ColoredSpan(span, color));
                }
            }
        }
#else
        public static void SetPrompts(this IReplWindow window, string primary, string secondary) {
            window.SetOptionValue(ReplOptions.PrimaryPrompt, primary);
            window.SetOptionValue(ReplOptions.SecondaryPrompt, secondary);
        }

        public static void UseInterpreterPrompts(this IReplWindow window) {
            var eval = window.Evaluator as BasePythonReplEvaluator;
            window.SetPrompts(eval.PrimaryPrompt, eval.SecondaryPrompt);
        }

        public static void SetSmartUpDown(this IReplWindow window, bool setting) {
            window.SetOptionValue(ReplOptions.UseSmartUpDown, setting);
        }
#endif

    }

}
