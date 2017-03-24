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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Debugger.Remote;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Repl {
    using LDP = Microsoft.PythonTools.Debugger.LegacyDebuggerProtocol;

    [InteractiveWindowRole("Debug")]
    [ContentType(PythonCoreConstants.ContentType)]
    [ContentType(PredefinedInteractiveCommandsContentTypes.InteractiveCommandContentTypeName)]
    internal class PythonDebugReplEvaluator :
        IInteractiveEvaluator,
        IPythonInteractiveEvaluator,
        IMultipleScopeEvaluator,
        IPythonInteractiveIntellisense
    {
        private PythonDebugProcessReplEvaluator _activeEvaluator;
        private readonly Dictionary<int, PythonDebugProcessReplEvaluator> _evaluators = new Dictionary<int, PythonDebugProcessReplEvaluator>(); // process id to evaluator
        private EnvDTE.DebuggerEvents _debuggerEvents;
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;
        private IInteractiveWindowCommands _commands;

        private static readonly string currentPrefix = Strings.DebugReplCurrentIndicator;
        private static readonly string notCurrentPrefix = Strings.DebugReplNotCurrentIndicator;

        public PythonDebugReplEvaluator(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _pyService = serviceProvider.GetPythonToolsService();
            AD7Engine.EngineAttached += new EventHandler<AD7EngineEventArgs>(OnEngineAttached);
            AD7Engine.EngineDetaching += new EventHandler<AD7EngineEventArgs>(OnEngineDetaching);

            var dte = _serviceProvider.GetDTE();
            if (dte != null) {
                // running outside of VS, make this work for tests.
                _debuggerEvents = dte.Events.DebuggerEvents;
                _debuggerEvents.OnEnterBreakMode += new EnvDTE._dispDebuggerEvents_OnEnterBreakModeEventHandler(OnEnterBreakMode);
            }
        }

        internal PythonInteractiveOptions CurrentOptions {
            get {
                return _pyService.DebugInteractiveOptions;
            }
        }

        private bool IsInDebugBreakMode() {
            var dte = _serviceProvider.GetDTE();
            if (dte == null) {
                // running outside of VS, make this work for tests.
                return true;
            }
            return dte.Debugger.CurrentMode == EnvDTE.dbgDebugMode.dbgBreakMode;
        }

        private void OnReadyForInput() {
            OnReadyForInputAsync().HandleAllExceptions(_serviceProvider, GetType()).DoNotWait();
        }

        private async Task OnReadyForInputAsync() {
            if (IsInDebugBreakMode()) {
                foreach (var engine in AD7Engine.GetEngines()) {
                    if (engine.Process != null) {
                        if (!_evaluators.ContainsKey(engine.Process.Id)) {
                            await AttachProcessAsync(engine.Process, engine);
                        }
                    }
                }
            }
        }

        private void OnEnterBreakMode(EnvDTE.dbgEventReason Reason, ref EnvDTE.dbgExecutionAction ExecutionAction) {
            int activeProcessId = _serviceProvider.GetDTE().Debugger.CurrentProcess.ProcessID;
            AD7Engine engine = AD7Engine.GetEngines().SingleOrDefault(target => target.Process != null && target.Process.Id == activeProcessId);
            if (engine != null) {
                long? activeThreadId = ((IThreadIdMapper)engine).GetPythonThreadId((uint)_serviceProvider.GetDTE().Debugger.CurrentThread.ID);
                if (activeThreadId != null) {
                    AttachProcessAsync(engine.Process, engine).ContinueWith(t => {
                        ChangeActiveThread(activeThreadId.Value, false);
                    });
                }
            }
        }

        public void ActiveLanguageBufferChanged(ITextBuffer currentBuffer, ITextBuffer previousBuffer) {
        }

        public bool CanExecuteCode(string text) {
            if (_commands.InCommand) {
                return true;
            }
            if (_activeEvaluator != null) {
                return _activeEvaluator.CanExecuteCode(text);
            }
            return true;
        }

        public Task<ExecutionResult> ExecuteCodeAsync(string text) {
            var res = _commands.TryExecuteCommand();
            if (res != null) {
                return res;
            }

            if (!IsInDebugBreakMode()) {
                NoExecutionIfNotStoppedInDebuggerError();
                return ExecutionResult.Succeeded;
            }

            if (_activeEvaluator != null) {
                return _activeEvaluator.ExecuteCodeAsync(text);
            }
            return ExecutionResult.Succeeded;
        }

        public async Task<bool> ExecuteFileAsync(string filename, string extraArgs) {
            if (!IsInDebugBreakMode()) {
                NoExecutionIfNotStoppedInDebuggerError();
                return true;
            }

            var t = _activeEvaluator?.ExecuteFileAsync(filename, extraArgs);
            if (t != null) {
                return await t;
            }
            // No evaluator, so say we succeeded
            return true;
        }

        public void AbortExecution() {
            CurrentWindow.WriteErrorLine(Strings.DebugReplAbortNotSupported);
        }

        public Task<ExecutionResult> Reset() {
            CurrentWindow.WriteErrorLine(Strings.DebugReplResetNotSupported);
            return ExecutionResult.Succeeded;
        }

        public string FormatClipboard() {
            if (_activeEvaluator != null) {
                return _activeEvaluator.FormatClipboard();
            }
            return String.Empty;
        }

        public void Dispose() {
        }

        public IEnumerable<string> GetAvailableScopes() {
            // TODO: Localization: we may need to do something with <CurrentFrame> string. Is it displayed?
            // It also appears visualstudio_py_repl.py so it probably needs to be in sync with it.
            string[] fixedScopes = new string[] { "<CurrentFrame>" };
            if (_activeEvaluator != null) {
                return fixedScopes.Concat(_activeEvaluator.GetAvailableScopes());
            } else {
                return new string[0];
            }
        }

        public event EventHandler<EventArgs> AvailableScopesChanged;
        public event EventHandler<EventArgs> MultipleScopeSupportChanged;

        public void SetScope(string scopeName) {
            if (_activeEvaluator != null) {
                _activeEvaluator.SetScope(scopeName);
            } else {
            }
        }

        public string CurrentScopeName => _activeEvaluator?.CurrentScopeName ?? "";
        public string CurrentScopePath => _activeEvaluator?.CurrentScopePath ?? "";
        public bool EnableMultipleScopes => _activeEvaluator?.EnableMultipleScopes ?? false;

        public bool LiveCompletionsOnly {
            get { return CurrentOptions.LiveCompletionsOnly; }
        }

        public IInteractiveWindow CurrentWindow { get; set; }

        public VsProjectAnalyzer Analyzer => _activeEvaluator?.Analyzer;
        public string AnalysisFilename => _activeEvaluator?.AnalysisFilename;

        public bool IsDisconnected => _activeEvaluator?.IsDisconnected ?? true;

        public bool IsExecuting => _activeEvaluator?.IsExecuting ?? false;

        public string DisplayName => Strings.DebugReplDisplayName;

        public IEnumerable<KeyValuePair<string, string>> GetAvailableScopesAndPaths() {
            if (_activeEvaluator != null) {
                return _activeEvaluator.GetAvailableScopesAndPaths();
            }

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        public CompletionResult[] GetMemberNames(string text) {
            if (_activeEvaluator != null) {
                return _activeEvaluator.GetMemberNames(text);
            }

            return new CompletionResult[0];
        }

        public OverloadDoc[] GetSignatureDocumentation(string text) {
            if (_activeEvaluator != null) {
                return _activeEvaluator.GetSignatureDocumentation(text);
            }

            return new OverloadDoc[0];
        }

        internal void StepOut() {
            if (_activeEvaluator != null) {
                _activeEvaluator.StepOut();
                CurrentWindow.TextView.VisualElement.Focus();
            } else {
                NoProcessError();
            }
        }

        internal void StepInto() {
            if (_activeEvaluator != null) {
                _activeEvaluator.StepInto();
                CurrentWindow.TextView.VisualElement.Focus();
            } else {
                NoProcessError();
            }
        }

        internal void StepOver() {
            if (_activeEvaluator != null) {
                _activeEvaluator.StepOver();
                CurrentWindow.TextView.VisualElement.Focus();
            } else {
                NoProcessError();
            }
        }

        internal void Resume() {
            if (_activeEvaluator != null) {
                _activeEvaluator.Resume();
                CurrentWindow.TextView.VisualElement.Focus();
            } else {
                NoProcessError();
            }
        }

        internal void FrameUp() {
            if (_activeEvaluator != null) {
                _activeEvaluator.FrameUp();
            } else {
                NoProcessError();
            }
        }

        internal void FrameDown() {
            if (_activeEvaluator != null) {
                _activeEvaluator.FrameDown();
            } else {
                NoProcessError();
            }
        }

        internal void DisplayActiveProcess() {
            if (_activeEvaluator != null) {
                _activeEvaluator.WriteOutput(_activeEvaluator.ProcessId.ToString());
            } else {
                CurrentWindow.WriteLine("None" + Environment.NewLine);
            }
        }

        internal void DisplayActiveThread() {
            if (_activeEvaluator != null) {
                _activeEvaluator.WriteOutput(_activeEvaluator.ThreadId.ToString());
            } else {
                NoProcessError();
            }
        }

        internal void DisplayActiveFrame() {
            if (_activeEvaluator != null) {
                _activeEvaluator.WriteOutput(_activeEvaluator.FrameId.ToString());
            } else {
                NoProcessError();
            }
        }

        internal void ChangeActiveProcess(int id, bool verbose) {
            if (_evaluators.Keys.Contains(id)) {
                SwitchProcess(_evaluators[id].Process, verbose);
            } else {
                CurrentWindow.WriteError(Strings.DebugReplInvalidProcessId.FormatUI(id));
            }
        }

        internal void ChangeActiveThread(long id, bool verbose) {
            if (_activeEvaluator != null) {
                PythonThread thread = _activeEvaluator.GetThreads().SingleOrDefault(target => target.Id == id);
                if (thread != null) {
                    _activeEvaluator.SwitchThread(thread, verbose);
                } else {
                    CurrentWindow.WriteError(Strings.DebugReplInvalidThreadId.FormatUI(id));
                }
            } else {
                NoProcessError();
            }
        }

        internal void ChangeActiveFrame(int id) {
            if (_activeEvaluator != null) {
                PythonStackFrame frame = _activeEvaluator.GetFrames().SingleOrDefault(target => target.FrameId == id);
                if (frame != null) {
                    _activeEvaluator.SwitchFrame(frame);
                } else {
                    CurrentWindow.WriteError(Strings.DebugReplInvalidFrameId.FormatUI(id));
                }
            } else {
                NoProcessError();
            }
        }

        internal void DisplayProcesses() {
            if (_activeEvaluator != null) {
                foreach (var target in _evaluators.Values) {
                    if (target.Process != null) {
                        _activeEvaluator.WriteOutput(Strings.DebugReplProcessesOutput.FormatUI(target.Process.Id, target.Process.LanguageVersion, target.Process.Id == _activeEvaluator.ProcessId ? currentPrefix : notCurrentPrefix));
                    }
                }
            }
        }

        internal void DisplayThreads() {
            if (_activeEvaluator != null) {
                foreach (var target in _activeEvaluator.GetThreads()) {
                    _activeEvaluator.WriteOutput(Strings.DebugReplThreadsOutput.FormatUI(target.Id, target.Name, target.Id == _activeEvaluator.ThreadId ? currentPrefix : notCurrentPrefix));
                }
            } else {
                NoProcessError();
            }
        }

        internal void DisplayFrames() {
            if (_activeEvaluator != null) {
                foreach (var target in _activeEvaluator.GetFrames()) {
                    _activeEvaluator.WriteOutput(Strings.DebugReplFramesOutput.FormatUI(target.FrameId, target.FunctionName, target.FrameId == _activeEvaluator.FrameId ? currentPrefix : notCurrentPrefix));
                }
            } else {
                NoProcessError();
            }
        }

        private void OnEngineAttached(object sender, AD7EngineEventArgs e) {
            _serviceProvider.GetUIThread().InvokeAsync(async () => {
                await AttachProcessAsync(e.Engine.Process, e.Engine);
            }).HandleAllExceptions(_serviceProvider, GetType()).DoNotWait();
        }

        private void OnEngineDetaching(object sender, AD7EngineEventArgs e) {
            _serviceProvider.GetUIThread().InvokeAsync(async () => {
                await DetachProcessAsync(e.Engine.Process);
            }).HandleAllExceptions(_serviceProvider, GetType()).DoNotWait();
        }

        private void OnProcessExited(object sender, ProcessExitedEventArgs e) {
            _serviceProvider.GetUIThread().InvokeAsync(async () => {
                await DetachProcessAsync((PythonProcess)sender);
            }).HandleAllExceptions(_serviceProvider, GetType()).DoNotWait();
        }

        internal void SwitchProcess(PythonProcess process, bool verbose) {
            var newEvaluator = _evaluators[process.Id];
            if (newEvaluator != _activeEvaluator) {
                _activeEvaluator = newEvaluator;
                ActiveProcessChanged();
                if (verbose) {
                    CurrentWindow.WriteLine(Strings.DebugReplSwitchProcessOutput.FormatUI(process.Id));
                }
            }
        }

        internal async Task AttachProcessAsync(PythonProcess process, IThreadIdMapper threadIdMapper) {
            _serviceProvider.GetUIThread().MustBeCalledFromUIThread();

            if (_evaluators.ContainsKey(process.Id)) {
                // Process is already attached, so just switch to it if needed
                SwitchProcess(process, false);
                return;
            }

            process.ProcessExited += new EventHandler<ProcessExitedEventArgs>(OnProcessExited);
            var evaluator = new PythonDebugProcessReplEvaluator(_serviceProvider, process, threadIdMapper);
            evaluator.CurrentWindow = CurrentWindow;
            await evaluator.InitializeAsync();
            evaluator.AvailableScopesChanged += new EventHandler<EventArgs>(evaluator_AvailableScopesChanged);
            evaluator.MultipleScopeSupportChanged += new EventHandler<EventArgs>(evaluator_MultipleScopeSupportChanged);
            _evaluators.Add(process.Id, evaluator);

            _activeEvaluator = evaluator;
        }

        internal async Task DetachProcessAsync(PythonProcess process) {
            _serviceProvider.GetUIThread().MustBeCalledFromUIThread();

            int id = process.Id;
            PythonDebugProcessReplEvaluator evaluator;
            if (_evaluators.TryGetValue(id, out evaluator)) {
                evaluator.AvailableScopesChanged -= new EventHandler<EventArgs>(evaluator_AvailableScopesChanged);
                evaluator.MultipleScopeSupportChanged -= new EventHandler<EventArgs>(evaluator_MultipleScopeSupportChanged);
                await process.DisconnectReplAsync();
                _evaluators.Remove(id);
                if (_activeEvaluator == evaluator) {
                    _activeEvaluator = null;
                }

                ActiveProcessChanged();
            }
        }

        private void evaluator_MultipleScopeSupportChanged(object sender, EventArgs e) {
            MultipleScopeSupportChanged?.Invoke(this, EventArgs.Empty);
        }

        private void evaluator_AvailableScopesChanged(object sender, EventArgs e) {
            AvailableScopesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ActiveProcessChanged() {
            MultipleScopeSupportChanged?.Invoke(this, EventArgs.Empty);
            AvailableScopesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void NoProcessError() {
            CurrentWindow.WriteError(Strings.DebugReplNoProcessError);
        }

        private void NoExecutionIfNotStoppedInDebuggerError() {
            CurrentWindow.WriteError(Strings.DebugReplNoExecutionIfNotStoppedInDebuggerError);
        }

        public Task<ExecutionResult> InitializeAsync() {
            _commands = PythonInteractiveEvaluator.GetInteractiveCommands(_serviceProvider, CurrentWindow, this);

            CurrentWindow.TextView.Options.SetOptionValue(InteractiveWindowOptions.SmartUpDown, CurrentOptions.UseSmartHistory);
            CurrentWindow.WriteLine(Strings.DebugReplHelpMessage);

            CurrentWindow.ReadyForInput += OnReadyForInput;
            return ExecutionResult.Succeeded;
        }

        public Task<ExecutionResult> ResetAsync(bool initialize = true) {
            return Reset();
        }

        public string GetPrompt() {
            return _activeEvaluator?.GetPrompt();
        }
    }

    [InteractiveWindowRole("Debug")]
    [ContentType(PythonCoreConstants.ContentType)]
    [ContentType(PredefinedInteractiveCommandsContentTypes.InteractiveCommandContentTypeName)]
    internal class PythonDebugProcessReplEvaluator : PythonInteractiveEvaluator {
        private ExceptionDispatchInfo _connectFailure;
        private readonly PythonProcess _process;
        private readonly IThreadIdMapper _threadIdMapper;
        private long _threadId;
        private int _frameId;
        private PythonLanguageVersion _languageVersion;

        public PythonDebugProcessReplEvaluator(IServiceProvider serviceProvider, PythonProcess process, IThreadIdMapper threadIdMapper)
            : base(serviceProvider) {
            _process = process;
            _threadIdMapper = threadIdMapper;
            _threadId = process.GetThreads()[0].Id;
            _languageVersion = process.LanguageVersion;
            DisplayName = Strings.DebugReplDisplayName;

            EnsureConnectedOnCreate();
        }

        private async void EnsureConnectedOnCreate() {
            try {
                await EnsureConnectedAsync();
            } catch (Exception ex) {
                _connectFailure = ExceptionDispatchInfo.Capture(ex);
            }
        }

        protected override Task ExecuteStartupScripts(string scriptsPath) {
            // Do not execute scripts for debug evaluator
            return Task.FromResult<object>(null);
        }

        public PythonProcess Process {
            get { return _process; }

        }
        public int ProcessId {
            get { return _process.Id; }
        }

        public long ThreadId {
            get { return _threadId; }
        }

        public int FrameId {
            get { return _frameId; }
        }

        protected override async Task<CommandProcessorThread> ConnectAsync(CancellationToken ct) {
            var remoteProcess = _process as PythonRemoteProcess;
            if (remoteProcess == null) {
                var conn = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                conn.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                conn.Listen(0);
                var portNum = ((IPEndPoint)conn.LocalEndPoint).Port;
                var proc = System.Diagnostics.Process.GetProcessById(_process.Id);

                var thread = CommandProcessorThread.Create(this, conn, proc);
                await _process.ConnectReplAsync(portNum);
                return thread;
            }

            // Ignore SSL errors, since user was already prompted about them and chose to ignore them when he attached to this process.
            using (var debugConn = await remoteProcess.ConnectAsync(false, ct)) {
                // After the REPL attach response is received, we go from
                // using the debugger protocol to the REPL protocol.
                // It's important to have a clean break between the 2, and at the right time.
                // The server will send the debugger protocol response for attach
                // before sending anything else and as soon as we read that
                // response, we stop reading any more messages.
                // Then we give the stream to the REPL protocol handler.
                var response = await debugConn.SendRequestAsync(new LDP.RemoteReplAttachRequest(), ct, resp => {
                    Debug.WriteLine("Stopping debug connection message processing. Switching from debugger protocol to REPL protocol.");
                    // This causes the message handling loop to exit
                    throw new OperationCanceledException();
                });

                if (!response.accepted) {
                    throw new ConnectionException(ConnErrorMessages.RemoteAttachRejected);
                }

                // Get the stream out of the connection before Dispose is called,
                // so that the stream doesn't get closed.
                var stream = debugConn.DetachStream();
                return CommandProcessorThread.Create(this, stream);
            }
        }

        internal override void OnConnected() {
            // Finish initialization now that the socket connection has been established
            var threads = _process.GetThreads();
            PythonThread activeThread = null;

            var dte = _serviceProvider.GetDTE();
            if (dte != null) {
                // If we are broken into the debugger, let's set the debug REPL active thread
                // to be the one that is active in the debugger
                var dteDebugger = dte.Debugger;
                if (dteDebugger.CurrentMode == EnvDTE.dbgDebugMode.dbgBreakMode &&
                    dteDebugger.CurrentProcess != null &&
                    dteDebugger.CurrentThread != null) {
                    if (_process.Id == dteDebugger.CurrentProcess.ProcessID) {
                        var activeThreadId = _threadIdMapper.GetPythonThreadId((uint)dteDebugger.CurrentThread.ID);
                        activeThread = threads.SingleOrDefault(t => t.Id == activeThreadId);
                    }
                }
            }

            if (activeThread == null) {
                activeThread = threads.Count > 0 ? threads[0] : null;
            }

            if (activeThread != null) {
                SwitchThread(activeThread, false);
            }
        }

        internal IList<PythonThread> GetThreads() {
            return _process.GetThreads();
        }

        internal IList<PythonStackFrame> GetFrames() {
            PythonThread activeThread = _process.GetThreads().SingleOrDefault(t => t.Id == _threadId);
            return activeThread != null ? activeThread.Frames : new List<PythonStackFrame>();
        }

        internal void SwitchThread(PythonThread thread, bool verbose) {
            var frame = thread.Frames.FirstOrDefault();
            if (frame == null) {
                WriteError(Strings.DebugReplCannotChangeCurrentThreadNoFrame.FormatUI(thread.Id));
                return;
            }

            _threadId = thread.Id;
            _frameId = frame.FrameId;
            _thread?.SetThreadAndFrameCommand(thread.Id, _frameId, frame.Kind);
            if (verbose) {
                WriteOutput(Strings.DebugReplThreadChanged.FormatUI(_threadId, _frameId));
            }
        }

        internal void SwitchFrame(PythonStackFrame frame) {
            _frameId = frame.FrameId;
            _thread?.SetThreadAndFrameCommand(frame.Thread.Id, frame.FrameId, frame.Kind);
            WriteOutput(Strings.DebugReplFrameChanged.FormatUI(frame.FrameId));
        }

        internal void FrameUp() {
            var frames = GetFrames();
            var currentFrame = frames.SingleOrDefault(f => f.FrameId == _frameId);
            if (currentFrame != null) {
                int index = frames.IndexOf(currentFrame);
                if (index < (frames.Count - 1)) {
                    SwitchFrame(frames[index + 1]);
                }
            }
        }

        internal void FrameDown() {
            var frames = GetFrames();
            var currentFrame = frames.SingleOrDefault(f => f.FrameId == _frameId);
            if (currentFrame != null) {
                int index = frames.IndexOf(currentFrame);
                if (index > 0) {
                    SwitchFrame(frames[index - 1]);
                }
            }
        }

        internal void StepOut() {
            UpdateDTEDebuggerProcessAndThread();
            _serviceProvider.GetDTE().Debugger.CurrentThread.Parent.StepOut();
        }

        internal void StepInto() {
            UpdateDTEDebuggerProcessAndThread();
            _serviceProvider.GetDTE().Debugger.CurrentThread.Parent.StepInto();
        }

        internal void StepOver() {
            UpdateDTEDebuggerProcessAndThread();
            _serviceProvider.GetDTE().Debugger.CurrentThread.Parent.StepOver();
        }

        internal void Resume() {
            UpdateDTEDebuggerProcessAndThread();
            _serviceProvider.GetDTE().Debugger.CurrentThread.Parent.Go();
        }

        private void UpdateDTEDebuggerProcessAndThread() {
            EnvDTE.Process dteActiveProcess = null;
            foreach (EnvDTE.Process dteProcess in _serviceProvider.GetDTE().Debugger.DebuggedProcesses) {
                if (dteProcess.ProcessID == _process.Id) {
                    dteActiveProcess = dteProcess;
                    break;
                }
            }

            if (dteActiveProcess != _serviceProvider.GetDTE().Debugger.CurrentProcess) {
                _serviceProvider.GetDTE().Debugger.CurrentProcess = dteActiveProcess;
            }

            EnvDTE.Thread dteActiveThread = null;
            foreach (EnvDTE.Thread dteThread in _serviceProvider.GetDTE().Debugger.CurrentProgram.Threads) {
                if (_threadIdMapper.GetPythonThreadId((uint)dteThread.ID) == _threadId) {
                    dteActiveThread = dteThread;
                    break;
                }
            }

            if (dteActiveThread != _serviceProvider.GetDTE().Debugger.CurrentThread) {
                _serviceProvider.GetDTE().Debugger.CurrentThread = dteActiveThread;
            }
        }
    }

    internal static class PythonDebugReplEvaluatorExtensions {
        public static PythonDebugReplEvaluator GetPythonDebugReplEvaluator(this IInteractiveWindow window) {
            var eval = window?.Evaluator as PythonDebugReplEvaluator;
            if (eval != null) {
                return eval;
            }

            eval = (window?.Evaluator as SelectableReplEvaluator)?.Evaluator as PythonDebugReplEvaluator;
            return eval;
        }
    }
}
