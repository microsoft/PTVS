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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Language;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

namespace Microsoft.PythonTools.Repl {
    public class PythonReplEvaluator : IReplEvaluator, IMultipleScopeEvaluator {
        private readonly IPythonInterpreterFactory _interpreter;
        private ListenerThread _curListener;
        private IReplWindow _window;
        private bool _multipleScopes = true;

        private static readonly byte[] RunCommandBytes = MakeCommand("run ");
        private static readonly byte[] AbortCommandBytes = MakeCommand("abrt");
        private static readonly byte[] ExitCommandBytes = MakeCommand("exit");
        private static readonly byte[] GetSignaturesCommandBytes = MakeCommand("sigs");
        private static readonly byte[] GetMembersCommandBytes = MakeCommand("mems");
        private static readonly byte[] GetModulesListCommandBytes = MakeCommand("mods");
        private static readonly byte[] SetModuleCommandBytes = MakeCommand("setm");
        private static readonly byte[] InputLineCommandBytes = MakeCommand("inpl");
        private static readonly byte[] ExecuteFileCommandBytes = MakeCommand("excf");

        public PythonReplEvaluator(IPythonInterpreterFactory interpreter) {
            _interpreter = interpreter;
        }

        public IPythonInterpreterFactory Interpreter {
            get {
                return _interpreter;
            }
        }

        #region IReplEvaluator Members

        public void Start(IReplWindow window) {
            _window = window;
            _window.SetOptionValue(ReplOptions.CommandPrefix, "$");

            Connect();

            window.SetOptionValue(ReplOptions.UseSmartUpDown, CurrentOptions.ReplSmartHistory);
            UpdatePrompts();
            window.SetOptionValue(ReplOptions.DisplayPromptInMargin, !CurrentOptions.InlinePrompts);
            window.SetOptionValue(ReplOptions.SupportAnsiColors, true);
            window.SetOptionValue(ReplOptions.FormattedPrompts, true);
        }

        private void Connect() {
            var processInfo = new ProcessStartInfo(_interpreter.Configuration.InterpreterPath);

#if DEBUG
            bool debugMode = Environment.GetEnvironmentVariable("DEBUG_REPL") != null;
            processInfo.CreateNoWindow = !debugMode;
            processInfo.UseShellExecute = debugMode;
            processInfo.RedirectStandardOutput = !debugMode;
            processInfo.RedirectStandardError = !debugMode;
#else
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;
#endif

            var conn = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            conn.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            conn.Listen(0);
            int portNum = ((IPEndPoint)conn.LocalEndPoint).Port;

            string filename, dir;
            ProjectAnalyzer analyzer;
            if (PythonToolsPackage.TryGetStartupFileAndDirectory(out filename, out dir, out analyzer)) {
                processInfo.WorkingDirectory = dir;
                var startupProj = PythonToolsPackage.GetStartupProject();
                if (startupProj != null) {
                    string searchPath = startupProj.GetProjectProperty(CommonConstants.SearchPath, true);
                    if (!string.IsNullOrEmpty(searchPath)) {
                        processInfo.EnvironmentVariables[_interpreter.Configuration.PathEnvironmentVariable] = searchPath;
                    }
                }
            }

            List<string> args = new List<string>() { "\"" + Path.Combine(PythonToolsPackage.GetPythonToolsInstallPath(), "visualstudio_py_repl.py") + "\"" };
            args.Add("--port");
            args.Add(portNum.ToString());

            if (!String.IsNullOrWhiteSpace(CurrentOptions.StartupScript)) {
                args.Add("--launch_file");
                args.Add("\"" + CurrentOptions.StartupScript + "\"");
            }

            bool multipleScopes = true;
            if (!String.IsNullOrWhiteSpace(CurrentOptions.ExecutionMode)) {
                // change ID to module name if we have a registered mode
                var modes = ExecutionMode.GetRegisteredModes();
                string modeValue = CurrentOptions.ExecutionMode;
                foreach (var mode in modes) {
                    if (mode.Id == CurrentOptions.ExecutionMode) {
                        modeValue = mode.Type;
                        multipleScopes = mode.SupportsMultipleScopes;
                        break;
                    }
                }
                args.Add("--execution_mode");
                args.Add(modeValue);
            }

            if (multipleScopes != _multipleScopes) {
                var multiScopeSupportChanged = MultipleScopeSupportChanged;
                if (multiScopeSupportChanged != null) {
                    multiScopeSupportChanged(this, EventArgs.Empty);
                }
                _multipleScopes = multipleScopes;
            }

            processInfo.Arguments = String.Join(" ", args);

            var process = new Process();
            process.StartInfo = processInfo;
            try {
                process.Start();
            } catch(Exception e) {
                _window.WriteError(String.Format("Failed to start interactive process: {0}{1}{2}", Environment.NewLine, e.ToString(), Environment.NewLine));
                return;
            }

            _curListener = new ListenerThread(this, conn, processInfo.RedirectStandardOutput, process);
        }

        
        class ListenerThread {
            private readonly PythonReplEvaluator _eval;
            private readonly object _socketLock = new object();
            private readonly Process _process;
            private Socket _socket;
            private Action<ExecutionResult> _completion;
            private AutoResetEvent _completionResultEvent = new AutoResetEvent(false);
            private OverloadDoc[] _overloads;
            private Dictionary<string, string> _moduleNames;
            internal string _currentScope = "__main__";
            private MemberResults _memberResults;
            internal string _prompt1 = ">>> ", _prompt2 = "... ";
#if DEBUG
            private Thread _socketLockedThread;
#endif

            public ListenerThread(PythonReplEvaluator evaluator, Socket socket, bool redirectOutput, Process process) {
                _eval = evaluator;
                _socket = socket;
                _process = process;

                var outputThread = new Thread(OutputThread);
                outputThread.Name = "PythonReplEvaluator: " + evaluator._interpreter.GetInterpreterDisplay();
                outputThread.Start();

                if (redirectOutput) {
                    var readOutputThread = new Thread(ReadOutput);
                    readOutputThread.Start();
                }

                if (redirectOutput) {
                    var readErrorThread = new Thread(ReadError);
                    readErrorThread.Start();
                }
            }

            public void OutputThread() {
                byte[] cmd_buffer = new byte[4];
                try {
                    _socket = _socket.Accept();
                    Socket socket;
                    while ((socket = _socket) != null && socket.Receive(cmd_buffer) == 4) {
                        using (new SocketLock(this)) {
                            string cmd = CommandtoString(cmd_buffer);
                            Debug.WriteLine("Repl {0} received command: {1}", _eval._interpreter.GetInterpreterDisplay(), cmd);
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
                                case "EXIT":
                                    // REPL has exited
                                    return;
                            }
                        }
                    }
                } catch (SocketException) {
                    _socket = null;
                }
            }

            private void ReadOutput() {
                var buffer = new char[1024];
                try {
                    while (!_process.HasExited) {
                        _process.StandardOutput.Read(buffer, 0, buffer.Length);
                    }
                } catch {
                }
            }

            private void ReadError() {
                var buffer = new char[1024];
                try {
                    while (!_process.HasExited) {
                        _process.StandardError.Read(buffer, 0, buffer.Length);
                    }
                } catch {
                }
            }

            private Socket Socket {
                get {
#if DEBUG
                    Debug.Assert(_socketLockedThread == Thread.CurrentThread);
#endif
                    var res = _socket;
                    if (res == null) {
                        throw new SocketException();
                    }
                    return res;
                }
            }

            private void HandleReadLine() {
                // perform the input on a new thread so that we don't block additional commands (such as output) from being processed by us
                // (this is called on the output thread)
                ThreadPool.QueueUserWorkItem(x => {
                    string input = UnfixNewLines(Window.ReadStandardInput());
                    using (new SocketLock(this)) {
                        Socket.Send(InputLineCommandBytes);
                        SendString(input);
                    }
                });
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
                _prompt1 = Socket.ReadString();
                _prompt2 = Socket.ReadString();
                if (Window != null) {
                    using (new SocketUnlock(this)) {
                        _eval.UpdatePrompts();
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
                string filename = Socket.ReadString();

                using (new SocketUnlock(this)) {
                    ((System.Windows.UIElement)Window.TextView).Dispatcher.BeginInvoke((Action)(() => {
                        try {
                            var imageSrc = new BitmapImage();
                            imageSrc.BeginInit();
                            imageSrc.StreamSource = new MemoryStream(File.ReadAllBytes(filename));
                            imageSrc.EndInit();

                            Window.WriteOutput(new Image() { Source = imageSrc });
                        } catch (IOException) {
                            // can't read the file
                            Window.WriteError("Unable to read image file " + filename);
                        }
                    }));
                }
            }

            private void HandleModuleList() {
                int moduleCount = Socket.ReadInt();
                Dictionary<string, string> moduleNames = new Dictionary<string, string>();
                for (int i = 0; i < moduleCount; i++) {
                    string name = Socket.ReadString();
                    string filename = Socket.ReadString();
                    moduleNames[filename] = name;
                }
                _moduleNames = moduleNames;
                _completionResultEvent.Set();
            }

            private void HandleSigError() {
                _completionResultEvent.Set();
            }

            private void HandleSigResult() {
                int overloadCount = Socket.ReadInt();
                OverloadDoc[] docs = new OverloadDoc[overloadCount];
                for (int i = 0; i < overloadCount; i++) {
                    string doc = Socket.ReadString();
                    int paramCount = Socket.ReadInt();

                    ParameterResult[] parameters = new ParameterResult[paramCount];
                    for (int curParam = 0; curParam < paramCount; curParam++) {
                        string name = Socket.ReadString();
                        parameters[curParam] = new ParameterResult(name);
                    }
                    docs[i] = new OverloadDoc(doc, parameters);
                }
                _overloads = docs;
                _completionResultEvent.Set();
            }

            private void HandleMemberResult() {
                string typeName = Socket.ReadString();

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
                string data = Socket.ReadString();
                if (data != null) {
                    using (new SocketUnlock(this)) {
                        Window.WriteOutput(FixNewLines(data));
                    }
                }
            }

            private void HandleError() {
                string data = Socket.ReadString();
                using (new SocketUnlock(this)) {
                    Window.WriteError(FixNewLines(data));
                }
            }

            private void HandleExecutionError() {
                using (new SocketUnlock(this)) {
                    // DONE command
                    if (_completion != null) {
                        _completion(new ExecutionResult(false));
                    }
                }
            }

            private void HandleExecutionDone() {
                using (new SocketUnlock(this)) {
                    // DONE command
                    if (_completion != null) {
                        _completion(new ExecutionResult(true));
                    }
                }
            }

            public bool ExecuteText(string text, Action<ExecutionResult> completion) {
                using (new SocketLock(this)) {
                    if (Socket == null) {
                        return false;
                    }

                    Socket.Send(RunCommandBytes);

                    // normalize line endings to \n which is all older versions of CPython can handle.
                    text = text.Replace("\r\n", "\n");
                    text = text.Replace("\r", "\n");
                    text = text.TrimEnd(' ');


                    _completion = completion;

                    SendString(text);
                }

                return true;
            }

            public void ExecuteFile(string filename) {
                using (new SocketLock(this)) {
                    Socket.Send(ExecuteFileCommandBytes);
                    SendString(filename);
                }
            }

            public void AbortCommand() {
                using (new SocketLock(this)) {
                    Socket.Send(AbortCommandBytes);
                }
            }

            public OverloadDoc[] GetSignatureDocumentation(ProjectAnalyzer analyzer, string text) {
                using (new SocketLock(this)) {
                    Socket.Send(GetSignaturesCommandBytes);
                    SendString(text);
                }

                if (_completionResultEvent.WaitOne(1000)) {
                    var res = _overloads;
                    _overloads = null;
                    return res;
                }
                return null;
            }

            public MemberResult[] GetMemberNames(ProjectAnalyzer analyzer, string text) {
                _completionResultEvent.Reset();
                _memberResults = null;

                using (new SocketLock(this)) {
                    Socket.Send(GetMembersCommandBytes);
                    SendString(text);
                }

                if (_completionResultEvent.WaitOne(1000) && _memberResults != null) {
                    MemberResult[] res = new MemberResult[_memberResults.TypeMembers.Count + _memberResults.InstanceMembers.Count];
                    int i = 0;
                    foreach (var member in _memberResults.TypeMembers) {
                        res[i++] = CreateMemberResult(analyzer, member.Key, member.Value);
                    }
                    foreach (var member in _memberResults.InstanceMembers) {
                        res[i++] = CreateMemberResult(analyzer, member.Key, member.Value);
                    }

                    _memberResults = null;
                    return res;
                }
                return null;
            }

            public string GetScopeByFilename(string path) {
                GetAvailableScopes();

                string res;
                if (_moduleNames.TryGetValue(path, out res)) {
                    return res;
                }
                return null;
            }

            public void SetScope(string scopeName) {
                using (new SocketLock(this)) {
                    Socket.Send(SetModuleCommandBytes);
                    SendString(scopeName);
                    _currentScope = scopeName;
                }
            }

            public IEnumerable<string> GetAvailableScopes() {
                using (new SocketLock(this)) {
                    Socket.Send(GetModulesListCommandBytes);
                }

                _completionResultEvent.WaitOne(1000);

                if (_moduleNames != null) {
                    return _moduleNames.Values;
                }
                return new string[0];
            }

            public void Close() {
                using (new SocketLock(this)) {
                    if (_socket != null) {
                        var socket = Socket;
                        _socket = null;

                        socket.Send(ExitCommandBytes);
                        socket.Close();
                    }
                }

                if (!_process.HasExited) {
                    try {
                        _process.Kill();
                    } catch (InvalidOperationException) {
                        // race w/ killing the process
                    }
                }
            }

            private void SendString(string text) {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
                Socket.Send(BitConverter.GetBytes(bytes.Length));
                Socket.Send(bytes);
            }

            private Dictionary<string, string> ReadMemberDict() {
                int memCount = Socket.ReadInt();
                var dict = new Dictionary<string, string>(memCount);
                for (int i = 0; i < memCount; i++) {
                    string memName = Socket.ReadString();
                    string typeName = Socket.ReadString();
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
                private readonly ListenerThread _evaluator;

                public SocketLock(ListenerThread evaluator) {
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
                private readonly ListenerThread _evaluator;

                public SocketUnlock(ListenerThread evaluator) {
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

        internal PythonInteractiveOptions CurrentOptions {
            get {
                if (PythonToolsPackage.Instance == null) {
                    // running outside of VS, make this work for tests.
                    return new PythonInteractiveOptions();
                }
                return PythonToolsPackage.Instance.InteractiveOptionsPage.GetOptions(_interpreter);
            }
        }

        private void UpdatePrompts() {
            if (CurrentOptions.UseInterpreterPrompts && _curListener != null) {
                _window.SetOptionValue(ReplOptions.PrimaryPrompt, _curListener._prompt1);
                _window.SetOptionValue(ReplOptions.SecondaryPrompt, _curListener._prompt2);
            } else {
                _window.SetOptionValue(ReplOptions.PrimaryPrompt, CurrentOptions.PrimaryPrompt);
                _window.SetOptionValue(ReplOptions.SecondaryPrompt, CurrentOptions.SecondaryPrompt);
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

        public bool CanExecuteText(string text) {
            int newLines = 0;
            for (int i = text.Length - 1; i >= 0; i--) {
                if (text[i] == '\n') {
                    if (++newLines == 2) {
                        return true;
                    }
                } else if (Char.IsWhiteSpace(text[i])) {
                    continue;
                } else {
                    break;
                }
            }

            var parser = Parser.CreateParser(new StringReader(text), ErrorSink.Null, _interpreter.GetLanguageVersion());
            ParseResult result;
            parser.ParseInteractiveCode(out result);
            if (!(result == ParseResult.Empty || result == ParseResult.Complete || result == ParseResult.Invalid)) {
                return false;
            }

            // Single-line: if it's executable, then execute
            if (text.IndexOf('\n') == text.LastIndexOf('\n')) {
                return true;
            }

            return false;
        }

        private static byte[] MakeCommand(string command) {
            return new byte[] { (byte)command[0], (byte)command[1], (byte)command[2], (byte)command[3] };
        }

        public bool ExecuteText(string text, Action<ExecutionResult> completion) {
            if (_curListener != null) {
                for (int i = 0; i < 2; i++) {
                    if (!_curListener.ExecuteText(text, completion)) {
                        // we've become disconnected, try again 1 time
                        Reset();
                    } else {
                        break;
                    }
                }
                return true;
            } else {
                _window.WriteError("Current interactive window is disconnected." + Environment.NewLine);
            }
            return false;
        }

        public void ExecuteFile(string filename) {
            if (_curListener != null) {
                _curListener.ExecuteFile(filename);
            } else {
                _window.WriteError("Current interactive window is disconnected." + Environment.NewLine);
            }
        }

        public void AbortCommand() {
            _curListener.AbortCommand();
        }

        public void Reset() {
            Close();

            Connect();
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

        public void Dispose() {
            try {                
                Close();                
            } catch {
            }
        }

        private void Close() {
            if (_curListener != null) {
                _curListener.Close();
            }
        }

        #endregion

        internal MemberResult[] GetMemberNames(ProjectAnalyzer analyzer, string text) {
            return _curListener.GetMemberNames(analyzer, text);
        }

        private static MemberResult CreateMemberResult(ProjectAnalyzer analyzer, string name, string typeName) {
            switch (typeName) {
                case "__builtin__.method-wrapper":
                case "__builtin__.builtin_function_or_method":
                case "__builtin__.method_descriptor":
                case "__builtin__.wrapper_descriptor":
                case "__builtin__.instancemethod":
                    return new MemberResult(name, PythonMemberType.Method);
                case "__builtin__.getset_descriptor":
                    return new MemberResult(name, PythonMemberType.Property);
            }

            return new MemberResult(name, PythonMemberType.Field);
        }

        internal OverloadDoc[] GetSignatureDocumentation(ProjectAnalyzer analyzer, string text) {
            return _curListener.GetSignatureDocumentation(analyzer, text);
        }

        #region IMultipleScopeEvaluator Members

        public IEnumerable<string> GetAvailableScopes() {            
            return _curListener.GetAvailableScopes();
        }

        public event EventHandler<EventArgs> AvailableScopesChanged;
        public event EventHandler<EventArgs> MultipleScopeSupportChanged;

        public void SetScope(string scopeName) {
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
            return _curListener.GetScopeByFilename(path);
        }

        public string PrimaryPrompt {
            get {
                return _curListener._prompt1;
            }
        }

        public string SecondaryPrompt {
            get {
                return _curListener._prompt2;
            }
        }

    }
}
