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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Repl {
    partial class PythonInteractiveEvaluator {
        private static readonly byte[] RunCommandBytes = MakeCommand("run ");
        private static readonly byte[] AbortCommandBytes = MakeCommand("abrt");
        private static readonly byte[] SetThreadAndFrameCommandBytes = MakeCommand("sett");
        private static readonly byte[] ExitCommandBytes = MakeCommand("exit");
        private static readonly byte[] GetSignaturesCommandBytes = MakeCommand("sigs");
        private static readonly byte[] GetMembersCommandBytes = MakeCommand("mems");
        private static readonly byte[] GetModulesListCommandBytes = MakeCommand("mods");
        private static readonly byte[] SetModuleCommandBytes = MakeCommand("setm");
        private static readonly byte[] InputLineCommandBytes = MakeCommand("inpl");
        private static readonly byte[] ExecuteFileCommandBytes = MakeCommand("excx");

        private static byte[] MakeCommand(string cmd) {
            var b = Encoding.ASCII.GetBytes(cmd);
            if (b.Length != 4) {
                throw new InvalidOperationException("Expected four bytes");
            }
            return b;
        }

        private Task<CommandProcessorThread> ConnectAsync(CancellationToken ct) {
            _serviceProvider.GetUIThread().MustBeCalledFromUIThreadOrThrow();

            var interpreterPath = Configuration?.GetInterpreterPath();
            if (string.IsNullOrWhiteSpace(interpreterPath)) {
                WriteError(Strings.ReplEvaluatorInterpreterNotConfigured.FormatUI(DisplayName));
                return Task.FromResult<CommandProcessorThread>(null);
            } else if (!File.Exists(interpreterPath)) {
                WriteError(Strings.ReplEvaluatorInterpreterNotFound);
                return Task.FromResult<CommandProcessorThread>(null);
            }

            var processInfo = new ProcessStartInfo(interpreterPath);
            processInfo.UseShellExecute = false;

#if DEBUG
            bool debugMode = Environment.GetEnvironmentVariable("_PTVS_DEBUG_REPL") != null;
            processInfo.CreateNoWindow = !debugMode;
            processInfo.RedirectStandardOutput = !debugMode;
            processInfo.RedirectStandardError = !debugMode;
            processInfo.RedirectStandardInput = !debugMode;
#else
            processInfo.CreateNoWindow = true;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardInput = true;
#endif

            var conn = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            conn.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            conn.Listen(0);
            var portNum = ((IPEndPoint)conn.LocalEndPoint).Port;

            var workingDirectory = Configuration.WorkingDirectory;
            if (!string.IsNullOrEmpty(workingDirectory)) {
                processInfo.WorkingDirectory = workingDirectory;
            } else {
                processInfo.WorkingDirectory = CommonUtils.GetParent(processInfo.FileName);
            }

            // Ensure that pydoc doesn't use redirection through an external process to display help
            processInfo.Environment["TERM"] = "dumb";

#if DEBUG
            if (!debugMode) {
#endif
                var env = processInfo.Environment;
                foreach (var kv in _serviceProvider.GetPythonToolsService().GetFullEnvironment(Configuration)) {
                    env[kv.Key] = kv.Value;
                }
#if DEBUG
            }
#endif

            var args = new List<string>();
            var interpreterArguments = Configuration.InterpreterArguments;
            if (!string.IsNullOrWhiteSpace(interpreterArguments)) {
                args.Add(interpreterArguments);
            }

            args.Add(ProcessOutput.QuoteSingleArgument(PythonToolsInstallPath.GetFile("ptvsd_repl_launcher.py")));
            args.Add("--port");
            args.Add(portNum.ToString());

            args.Add("--execution-mode");
            args.Add(string.IsNullOrEmpty(BackendName) ? "standard" : BackendName);

            processInfo.Arguments = string.Join(" ", args);

            Process process;
            try {
                if (!File.Exists(processInfo.FileName)) {
                    throw new Win32Exception(Microsoft.VisualStudioTools.Project.NativeMethods.ERROR_FILE_NOT_FOUND);
                }
                process = Process.Start(processInfo);
                if (process.WaitForExit(100)) {
                    throw new Win32Exception(process.ExitCode);
                }
            } catch (Win32Exception e) {
                if (e.NativeErrorCode == Microsoft.VisualStudioTools.Project.NativeMethods.ERROR_FILE_NOT_FOUND) {
                    WriteError(Strings.ReplEvaluatorInterpreterNotFound);
                } else {
                    WriteError(Strings.ErrorStartingInteractiveProcess.FormatUI(e.ToString()));
                }
                return null;
            } catch (Exception e) when(!e.IsCriticalException()) {
                return null;
            }

            return Task.FromResult(CommandProcessorThread.Create(this, conn, process));
        }



        protected class CommandProcessorThread : IDisposable {
            private readonly PythonInteractiveEvaluator _eval;

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

            private readonly TaskCompletionSource<object> _receivedPrompts = new TaskCompletionSource<object>();
            private bool _supportsMultipleStatements;

            private Action _deferredExecute;

            private readonly Process _process;
            private AutoResetEvent _completionResultEvent = new AutoResetEvent(false);
            private OverloadDoc[] _overloads;
            private Dictionary<string, string> _fileToModuleName;
            private Dictionary<string, string> _moduleToFileName;
            private StringBuilder _preConnectionOutput;
            private string _currentScope = "__main__";
            private string _currentScopeFileName;
            private string _currentWorkingDirectory;
            private MemberResults _memberResults;

            private CommandProcessorThread(PythonInteractiveEvaluator evaluator, Process process) {
                _process = process;
                _eval = evaluator;
                _preConnectionOutput = new StringBuilder();
            }

            public static CommandProcessorThread Create(
                PythonInteractiveEvaluator evaluator,
                Socket listenerSocket,
                Process process
            ) {
                var thread = new CommandProcessorThread(evaluator, process);
                thread._listenerSocket = listenerSocket;
                thread._currentWorkingDirectory = process.StartInfo.WorkingDirectory;
                thread.StartOutputThread(process.StartInfo.RedirectStandardOutput);
                return thread;
            }

            public bool IsConnected {
                get {
                    using (new StreamLock(this, throwIfDisconnected: false)) {
                        return _stream != null;
                    }
                }
            }

            public string CurrentScope => _currentScope;
            public string CurrentScopeFileName => _currentScopeFileName;
            public string CurrentWorkingDirectory => _currentWorkingDirectory;

            public bool IsProcessExpectedToExit { get; set; }

            private void StartOutputThread(bool redirectOutput) {
                var outputThread = new Thread(OutputThread);
                outputThread.Name = "PythonReplEvaluator: " + _eval.DisplayName;
                outputThread.Start();

                if (redirectOutput) {
                    Utilities.CheckNotNull(_process);
                    _process.OutputDataReceived += StdOutReceived;
                    _process.ErrorDataReceived += StdErrReceived;
                    _process.Exited += ProcessExited;
                    _process.EnableRaisingEvents = true;

                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                }
            }

            private static string FixNewLines(string input) {
                return input.Replace("\r\n", "\n").Replace('\r', '\n');
            }

            private static string UnfixNewLines(string input) {
                return input.Replace("\r\n", "\n");
            }

            private void ProcessExited(object sender, EventArgs e) {
                using (new StreamLock(this, throwIfDisconnected: false)) {
                    var stream = _stream;
                    _stream = null;
                    if (stream != null) {
                        stream.Dispose();
                    }
                }

                var pco = Interlocked.Exchange(ref _preConnectionOutput, null);
                if (pco != null) {
                    lock (pco) {
                        try {
                            _eval.WriteError(pco.ToString(), addNewline: false);
                        } catch (Exception ex) when (!ex.IsCriticalException()) {
                        }
                    }
                }

                if (!IsProcessExpectedToExit) {
                    try {
                        _eval.WriteError(Strings.ReplExited);
                    } catch (Exception ex) when(!ex.IsCriticalException()) {
                    }
                }
                IsProcessExpectedToExit = false;
            }

            private void StdErrReceived(object sender, DataReceivedEventArgs e) {
                if (e.Data != null) {
                    if (!AppendPreConnectionOutput(e)) {
                        _eval.WriteError(FixNewLines(e.Data));
                    }
                }
            }

            private void StdOutReceived(object sender, DataReceivedEventArgs e) {
                if (e.Data != null) {
                    if (!AppendPreConnectionOutput(e)) {
                        _eval.WriteOutput(FixNewLines(e.Data));
                    }
                }
            }

            private bool AppendPreConnectionOutput(DataReceivedEventArgs e) {
                var pco = Volatile.Read(ref _preConnectionOutput);
                if (pco != null) {
                    lock (pco) {
                        pco.Append(FixNewLines(e.Data) + Environment.NewLine);
                        return true;
                    }
                }
                return false;
            }

            private void OutputThread() {
                try {
                    Socket listenerSocket = null;
                    using (new StreamLock(this, throwIfDisconnected: false)) {
                        if (_stream == null) {
                            listenerSocket = _listenerSocket;
                        }
                    }
                    if (listenerSocket != null) {
                        var socket = listenerSocket.Accept();
                        using (new StreamLock(this, throwIfDisconnected: false)) {
                            _listenerSocket = null;
                            _stream = new NetworkStream(socket, ownsSocket: true);
                        }
                    }

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
                                case "DPNG": HandleDisplayPng(); break;
                                case "DXAM": HandleDisplayXaml(); break;
                                case "CHWD": HandleWorkingDirectoryChanged(); break;
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

                TaskCompletionSource<ExecutionResult> completion;
                lock (_completionLock) {
                    completion = _completion;
                    _completion = null;
                }

                if (completion != null) {
                    bool success = completion.TrySetCanceled();
                    Debug.Assert(success);
                    completion = null;
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
                    throw new DisconnectedException(Strings.ReplDisconnectedFromRemoteProcessError);
                }
            }

            private void HandleReadLine() {
                // perform the input on a new thread so that we don't block
                // additional commands (such as output) from being processed by
                // us (this is called on the output thread)
                var window = _eval.CurrentWindow;
                ThreadPool.QueueUserWorkItem(x => {
                    string input = window?.ReadStandardInput()?.ReadToEnd();
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

            private void HandleDisplayPng() {
                Debug.Assert(Monitor.IsEntered(_streamLock));

                int len = _stream.ReadInt32();
                byte[] buffer = new byte[len];
                _stream.ReadToFill(buffer);
                DisplayImage(buffer);
            }

            private void HandleDisplayXaml() {
                Debug.Assert(Monitor.IsEntered(_streamLock));

                int len = _stream.ReadInt32();
                byte[] buffer = new byte[len];
                _stream.ReadToFill(buffer);

                _eval.InvokeAsync(() => {
                    try {
                        var fe = XamlReader.Load(new MemoryStream(buffer)) as FrameworkElement;
                        if (fe != null) {
                            _eval.WriteFrameworkElement(fe, fe.DesiredSize);
                        }
                    } catch (Exception ex) when (!ex.IsCriticalException()) {
                        _eval.WriteError(ex.ToString());
                        return;
                    }
                }).DoNotWait();
            }

            private void HandlePromptChanged() {
                Debug.Assert(Monitor.IsEntered(_streamLock));

                var prompt1 = _stream.ReadString();
                var prompt2 = _stream.ReadString();
                var supportMultipleStatements = _stream.ReadInt32() != 0;
                Trace.TraceInformation("New prompts: \"{0}\" \"{1}\"", prompt1, prompt2);
                Trace.TraceInformation("  Support multiple statements: {0}", supportMultipleStatements);

                PrimaryPrompt = prompt1;
                SecondaryPrompt = prompt2;
                _supportsMultipleStatements = supportMultipleStatements;
                _receivedPrompts.TrySetResult(null);
            }

            private void HandleWorkingDirectoryChanged() {
                Debug.Assert(Monitor.IsEntered(_streamLock));

                _currentWorkingDirectory = _stream.ReadString();
            }

            public async Task<bool> GetSupportsMultipleStatementsAsync() {
                await _receivedPrompts.Task;
                return _supportsMultipleStatements;
            }

            public event EventHandler AvailableScopesChanged;

            private void HandleModulesChanged() {
                // modules changed
                using (new StreamUnlock(this)) {
                    _eval.EnableMultipleScopes = true;
                    AvailableScopesChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            private void HandleImageDisplay() {
                Debug.Assert(Monitor.IsEntered(_streamLock));

                string filename = _stream.ReadString();
                try {
                    DisplayImage(File.ReadAllBytes(filename));
                } catch (IOException) {
                    // can't read the file
                    _eval.WriteError(Strings.ReplCannotReadFile.FormatUI(filename));
                }
            }

            private void DisplayImage(byte[] bytes) {
                _eval.InvokeAsync(() => {
                    var imageSrc = new BitmapImage();
                    try {
                        imageSrc.BeginInit();
                        imageSrc.StreamSource = new MemoryStream(bytes);
                        imageSrc.EndInit();
                    } catch (IOException) {
                        return;
                    }

                    var img = new Image {
                        Source = imageSrc,
                        Stretch = Stretch.Uniform,
                        StretchDirection = StretchDirection.Both
                    };
                    var control = new Border {
                        Child = img,
                        Background = Brushes.White
                    };

                    _eval.WriteFrameworkElement(control, new Size(imageSrc.PixelWidth, imageSrc.PixelHeight));
                });
            }

            private void HandleModuleList() {
                Debug.Assert(Monitor.IsEntered(_streamLock));

                int moduleCount = _stream.ReadInt32();
                var fileToModule = new Dictionary<string, string>();
                var moduleToFile = new Dictionary<string, string>();
                for (int i = 0; i < moduleCount; i++) {
                    string name = _stream.ReadString();
                    string filename = _stream.ReadString();
                    if (!string.IsNullOrEmpty(filename)) {
                        fileToModule[filename] = name;
                    }
                    moduleToFile[name] = filename;
                }

                _fileToModuleName = fileToModule;
                _moduleToFileName = moduleToFile;
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
                        _eval.WriteOutput(FixNewLines(data), addNewline: false);
                    }
                }
            }

            private void HandleError() {
                Debug.Assert(Monitor.IsEntered(_streamLock));

                string data = _stream.ReadString();
                Trace.TraceInformation("Data = \"{0}\"", FixNewLines(data).Replace("\r\n", "\\r\\n"));
                using (new StreamUnlock(this)) {
                    _eval.WriteError(FixNewLines(data), addNewline: false);
                }
            }

            private void HandleExecutionError() {
                // ERRE command
                TaskCompletionSource<ExecutionResult> completion;
                lock (_completionLock) {
                    completion = _completion;
                    _completion = null;
                }

                if (completion != null) {
                    completion.SetResult(ExecutionResult.Failure);
                } else {
                    Debug.Fail("No completion task");
                }
            }

            private void HandleExecutionDone() {
                // DONE command
                TaskCompletionSource<ExecutionResult> completion;
                lock (_completionLock) {
                    completion = _completion;
                    _completion = null;
                }

                if (completion != null) {
                    completion.SetResult(ExecutionResult.Success);
                } else {
                    Debug.Fail("No completion task");
                }
            }

            public Task<ExecutionResult> ExecuteText(string text) {
                if (text.StartsWith("$")) {
                    _eval.WriteError(Strings.ReplUnknownCommand.FormatUI(text.Trim()));
                    return ExecutionResult.Failed;
                }

                Action send = () => {
                    if (_process != null) {
                        Microsoft.VisualStudioTools.Project.NativeMethods.AllowSetForegroundWindow(_process.Id);
                    }

                    _stream.Write(RunCommandBytes);

                    // normalize line endings to \n which is all older versions of CPython can handle.
                    text = FixNewLines(text).TrimEnd(' ');
                    SendString(text);
                };

                var tcs = new TaskCompletionSource<ExecutionResult>();
                lock (_completionLock) {
                    _completion = tcs;
                }

                Trace.TraceInformation("Executing text: {0}", text);
                using (new StreamLock(this, throwIfDisconnected: false)) {
                    if (_stream == null) {
                        // If we're still waiting for debuggee to connect to us, postpone the actual execution until we have the command stream.
                        if (_listenerSocket != null) {
                            Trace.TraceInformation("Deferred executing text because connection is not fully established yet.");
                            _deferredExecute = send;
                        } else {
                            _eval.WriteError(Strings.ReplDisconnectedReset);
                            lock (_completionLock) {
                                _completion = null;
                            }
                            return ExecutionResult.Failed;
                        }
                    } else {
                        try {
                            send();
                        } catch (IOException) {
                            _eval.WriteError(Strings.ReplDisconnectedReset);
                            lock (_completionLock) {
                                _completion = null;
                            }
                            return ExecutionResult.Failed;
                        }
                    }
                }

                return tcs.Task;
            }

            public async Task<bool> ExecuteFile(string filename, string extraArgs, string fileType) {
                Action send = () => {
                    if (_process != null) {
                        Microsoft.VisualStudioTools.Project.NativeMethods.AllowSetForegroundWindow(_process.Id);
                    }

                    _stream.Write(ExecuteFileCommandBytes);
                    SendString(fileType ?? string.Empty);
                    SendString(filename ?? string.Empty);
                    SendString(extraArgs ?? string.Empty);
                };

                var tcs = new TaskCompletionSource<ExecutionResult>();
                lock (_completionLock) {
                    _completion = tcs;
                }

                using (new StreamLock(this, throwIfDisconnected: false)) {
                    if (_stream == null) {
                        // If we're still waiting for debuggee to connect to us, postpone the actual execution until we have the command stream.
                        if (_listenerSocket != null) {
                            _deferredExecute = send;
                        } else {
                            _eval.WriteError(Strings.ReplDisconnectedReset);
                            lock (_completionLock) {
                                _completion = null;
                            }
                            return false;
                        }
                    } else {
                        try {
                            send();
                        } catch (IOException) {
                            _eval.WriteError(Strings.ReplDisconnectedReset);
                            lock (_completionLock) {
                                _completion = null;
                            }
                            return false;
                        }
                    }
                }

                return (await tcs.Task).IsSuccessful;
            }

            public void AbortCommand() {
                using (new StreamLock(this, throwIfDisconnected: true)) {
                    _stream.Write(AbortCommandBytes);
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

            public CompletionResult[] GetMemberNames(string text) {
                _completionResultEvent.Reset();
                _memberResults = null;

                using (new StreamLock(this, throwIfDisconnected: false)) {
                    if (_stream == null) {
                        return new CompletionResult[0];
                    }
                    try {
                        _stream.Write(GetMembersCommandBytes);
                        SendString(text);
                    } catch (IOException) {
                        return new CompletionResult[0];
                    }
                }

                if (_completionResultEvent.WaitOne(1000) && _memberResults != null) {
                    var res = new CompletionResult[_memberResults.TypeMembers.Count + _memberResults.InstanceMembers.Count];
                    int i = 0;
                    foreach (var member in _memberResults.TypeMembers) {
                        res[i++] = CreateCompletionResult(member.Key, member.Value);
                    }
                    foreach (var member in _memberResults.InstanceMembers) {
                        res[i++] = CreateCompletionResult(member.Key, member.Value);
                    }

                    _memberResults = null;
                    return res;
                }
                return null;
            }

            private static CompletionResult CreateCompletionResult(string name, string typeName) {
                switch (typeName) {
                    case "__builtin__.method-wrapper":
                    case "__builtin__.builtin_function_or_method":
                    case "__builtin__.method_descriptor":
                    case "__builtin__.wrapper_descriptor":
                    case "__builtin__.instancemethod":
                        return new CompletionResult(name, PythonMemberType.Method);
                    case "__builtin__.getset_descriptor":
                        return new CompletionResult(name, PythonMemberType.Property);
                    case "__builtin__.namespace#":
                        return new CompletionResult(name, PythonMemberType.Namespace);
                    case "__builtin__.type":
                        return new CompletionResult(name, PythonMemberType.Class);
                    case "__builtin__.function":
                        return new CompletionResult(name, PythonMemberType.Function);
                    case "__builtin__.module":
                        return new CompletionResult(name, PythonMemberType.Module);
                }

                return new CompletionResult(name, PythonMemberType.Field);
            }

            public void SetScope(string scopeName) {
                try {
                    using (new StreamLock(this, throwIfDisconnected: true)) {
                        if (!string.IsNullOrWhiteSpace(scopeName)) {
                            _stream.Write(SetModuleCommandBytes);
                            SendString(scopeName);
                            _currentScope = scopeName;
                            if (!(_moduleToFileName?.TryGetValue(scopeName, out _currentScopeFileName) ?? false)) {
                                _currentScopeFileName = null;
                            }

                            _eval.WriteOutput(Strings.ReplModuleChanged.FormatUI(scopeName));
                        } else {
                            _eval.WriteOutput(_currentScope);
                        }
                    }
                } catch (DisconnectedException) {
                    _eval.WriteError(Strings.ReplModuleCannotChange);
                } catch (IOException) {
                }
            }

            public Task<IEnumerable<string>> GetAvailableUserScopesAsync(int timeout = -1) {
                return Task.Run(() => {
                    try {
                        AutoResetEvent evt;
                        using (new StreamLock(this, throwIfDisconnected: true)) {
                            _stream.Write(GetModulesListCommandBytes);
                            evt = _completionResultEvent;
                        }
                        evt.WaitOne(timeout);
                        return _moduleToFileName?.Keys.AsEnumerable();
                    } catch (IOException) {
                    }

                    return null;
                });
            }

            public Task<IEnumerable<KeyValuePair<string, string>>> GetAvailableScopesAndPathsAsync(int timeout = -1) {
                return Task.Run(() => {
                    try {
                        AutoResetEvent evt;
                        using (new StreamLock(this, throwIfDisconnected: true)) {
                            _stream.Write(GetModulesListCommandBytes);
                            evt = _completionResultEvent;
                        }
                        evt.WaitOne(timeout);
                        return _moduleToFileName.AsEnumerable();
                    } catch (IOException) {
                    }

                    return null;
                });
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

                TaskCompletionSource<ExecutionResult> completion;
                lock (_completionLock) {
                    completion = _completion;
                    _completion = null;

                    if (_completionResultEvent != null) {
                        _completionResultEvent.Dispose();
                        _completionResultEvent = null;
                    }
                }

                if (completion != null) {
                    bool success = completion.TrySetResult(ExecutionResult.Failure);
                    Debug.Assert(success);
                }
            }

            private void SendString(string text) {
                Debug.Assert(text != null, "text should not be null");
                byte[] bytes = Encoding.UTF8.GetBytes(text);
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

            public bool IsExecuting => _completion != null && !_completion.Task.IsCompleted;

            public string PrimaryPrompt { get; internal set; }

            public string SecondaryPrompt { get; internal set; }

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

            class MemberResults {
                public readonly string TypeName;
                public readonly Dictionary<string, string> InstanceMembers;
                public readonly Dictionary<string, string> TypeMembers;

                public MemberResults(
                    string typeName,
                    Dictionary<string, string> instMembers,
                    Dictionary<string, string> typeMembers
                ) {
                    TypeName = typeName;
                    InstanceMembers = instMembers;
                    TypeMembers = typeMembers;
                }
            }
        }
    }
}
