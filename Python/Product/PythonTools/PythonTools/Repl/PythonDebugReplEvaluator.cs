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
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Debugger.Remote;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
#else
using Microsoft.VisualStudio.Repl;
#endif
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;
#if DEV14_OR_LATER
using IReplEvaluator = Microsoft.VisualStudio.InteractiveWindow.IInteractiveEvaluator;
using IReplWindow = Microsoft.VisualStudio.InteractiveWindow.IInteractiveWindow;
using ReplRoleAttribute = Microsoft.PythonTools.Repl.InteractiveWindowRoleAttribute;
#endif

namespace Microsoft.PythonTools.Repl {
    [ReplRole("Debug")]
    internal class PythonDebugReplEvaluator : IReplEvaluator/*, IMultipleScopeEvaluator*/, IPythonReplIntellisense {
        private IReplWindow _window;
        private PythonDebugProcessReplEvaluator _activeEvaluator;
        private readonly Dictionary<int, PythonDebugProcessReplEvaluator> _evaluators = new Dictionary<int, PythonDebugProcessReplEvaluator>(); // process id to evaluator
        private EnvDTE.DebuggerEvents _debuggerEvents;
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;
#if DEV14_OR_LATER
        private IInteractiveWindowCommands _commands;
#endif

        private const string currentPrefix = "=> ";
        private const string notCurrentPrefix = "   ";

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

        public string PrimaryPrompt {
            get {
                if (_activeEvaluator != null) {
                    return _activeEvaluator.PrimaryPrompt;
                }
                return ">>> ";
            }
        }

        public string SecondaryPrompt {
            get {
                if (_activeEvaluator != null) {
                    return _activeEvaluator.SecondaryPrompt;
                }
                return "... ";
            }
        }

        internal PythonInteractiveCommonOptions CurrentOptions {
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

        #region IReplEvaluator Members

        public Task<ExecutionResult> Initialize(IReplWindow window) {
            _window = window;
            _window.SetSmartUpDown(CurrentOptions.ReplSmartHistory);
#if !DEV14_OR_LATER
            _window.SetOptionValue(ReplOptions.CommandPrefix, "$");
            _window.SetOptionValue(ReplOptions.PrimaryPrompt, CurrentOptions.PrimaryPrompt);
            _window.SetOptionValue(ReplOptions.SecondaryPrompt, CurrentOptions.SecondaryPrompt);
            _window.SetOptionValue(ReplOptions.DisplayPromptInMargin, !CurrentOptions.InlinePrompts);
            _window.SetOptionValue(ReplOptions.SupportAnsiColors, true);
            _window.SetOptionValue(ReplOptions.FormattedPrompts, true);
#endif
            _window.WriteLine("Python debug interactive window.  Type $help for a list of commands.");

            _window.TextView.BufferGraph.GraphBuffersChanged += BufferGraphGraphBuffersChanged;
            _window.ReadyForInput += new Action(OnReadyForInput);
            return ExecutionResult.Succeeded;
        }

        private void OnReadyForInput() {
            if (IsInDebugBreakMode()) {
                foreach (var engine in AD7Engine.GetEngines()) {
                    if (engine.Process != null) {
                        if (!_evaluators.ContainsKey(engine.Process.Id)) {
                            AttachProcess(engine.Process, engine);
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
                    AttachProcess(engine.Process, engine);
                    ChangeActiveThread(activeThreadId.Value, false);
                }
            }
        }

        private void BufferGraphGraphBuffersChanged(object sender, GraphBuffersChangedEventArgs e) {
            foreach (var removed in e.RemovedBuffers) {
                BufferParser parser;
                if (removed.Properties.TryGetProperty(typeof(BufferParser), out parser)) {
                    parser.RemoveBuffer(removed);
                }
            }
        }

        public void ActiveLanguageBufferChanged(ITextBuffer currentBuffer, ITextBuffer previousBuffer) {
        }

#if DEV14_OR_LATER
        public bool CanExecuteCode(string text) {
            if (_commands.InCommand) {
                return true;
            }
#else
        public bool CanExecuteText(string text) {
#endif
            if (_activeEvaluator != null) {
#if DEV14_OR_LATER
                return _activeEvaluator.CanExecuteCode(text);
#else
                return _activeEvaluator.CanExecuteText(text);
#endif
            }
            return true;
        }

        public Task<ExecutionResult> ExecuteText(string text) {
#if DEV14_OR_LATER
            var res = _commands.TryExecuteCommand();
            if (res != null) {
                return res;
            }
#endif

            if (!IsInDebugBreakMode()) {
                NoExecutionIfNotStoppedInDebuggerError();
                return ExecutionResult.Succeeded;
            }

            if (_activeEvaluator != null) {
                return _activeEvaluator.ExecuteText(text);
            }
            return ExecutionResult.Succeeded;
        }

        public void ExecuteFile(string filename) {
            if (!IsInDebugBreakMode()) {
                NoExecutionIfNotStoppedInDebuggerError();
                return;
            }

            if (_activeEvaluator != null) {
                _activeEvaluator.ExecuteFile(filename);
            }
        }

#if DEV14_OR_LATER
        public void AbortExecution() {
#else
        public void AbortCommand() {
#endif
            _window.WriteError("Abort is not supported." + Environment.NewLine);
        }

        public Task<ExecutionResult> Reset() {
            throw new NotSupportedException();
        }

        public string FormatClipboard() {
            if (_activeEvaluator != null) {
                return _activeEvaluator.FormatClipboard();
            }
            return String.Empty;
        }

#endregion

#region IDisposable Members

        public void Dispose() {
        }

#endregion

#region IMultipleScopeEvaluator Members

        public IEnumerable<string> GetAvailableScopes() {
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

        public string CurrentScopeName {
            get {
                if (_activeEvaluator != null) {
                    return _activeEvaluator.CurrentScopeName;
                } else {
                    return string.Empty;
                }
            }
        }

        public bool EnableMultipleScopes {
            get {
                if (_activeEvaluator != null) {
                    return _activeEvaluator.EnableMultipleScopes;
                } else {
                    return false;
                }
            }
        }

#endregion

#region IPythonReplIntellisense Members

        public bool LiveCompletionsOnly {
            get { return CurrentOptions.LiveCompletionsOnly; }
        }

#if DEV14_OR_LATER
        public IReplWindow CurrentWindow {
            get {
                return _window;
            }
            set {
                _window = value;
            }
        }
#endif

        public IEnumerable<KeyValuePair<string, bool>> GetAvailableScopesAndKind() {
            if (_activeEvaluator != null) {
                return _activeEvaluator.GetAvailableScopesAndKind();
            }

            return new KeyValuePair<string, bool>[0];
        }

        public Analysis.MemberResult[] GetMemberNames(string text) {
            if (_activeEvaluator != null) {
                return _activeEvaluator.GetMemberNames(text);
            }

            return new Analysis.MemberResult[0];
        }

        public OverloadDoc[] GetSignatureDocumentation(string text) {
            if (_activeEvaluator != null) {
                return _activeEvaluator.GetSignatureDocumentation(text);
            }

            return new OverloadDoc[0];
        }

#endregion

        internal void StepOut() {
            if (_activeEvaluator != null) {
                _activeEvaluator.StepOut();
                _window.TextView.VisualElement.Focus();
            } else {
                NoProcessError();
            }
        }

        internal void StepInto() {
            if (_activeEvaluator != null) {
                _activeEvaluator.StepInto();
                _window.TextView.VisualElement.Focus();
            } else {
                NoProcessError();
            }
        }

        internal void StepOver() {
            if (_activeEvaluator != null) {
                _activeEvaluator.StepOver();
                _window.TextView.VisualElement.Focus();
            } else {
                NoProcessError();
            }
        }

        internal void Resume() {
            if (_activeEvaluator != null) {
                _activeEvaluator.Resume();
                _window.TextView.VisualElement.Focus();
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
                _window.WriteLine(_activeEvaluator.ProcessId.ToString());
            } else {
                _window.WriteLine("None");
            }
        }

        internal void DisplayActiveThread() {
            if (_activeEvaluator != null) {
                _window.WriteLine(_activeEvaluator.ThreadId.ToString());
            } else {
                NoProcessError();
            }
        }

        internal void DisplayActiveFrame() {
            if (_activeEvaluator != null) {
                _window.WriteLine(_activeEvaluator.FrameId.ToString());
            } else {
                NoProcessError();
            }
        }

        internal void ChangeActiveProcess(int id, bool verbose) {
            if (_evaluators.Keys.Contains(id)) {
                SwitchProcess(_evaluators[id].Process, verbose);
            } else {
                _window.WriteError(String.Format("Invalid process id '{0}'.", id));
            }
        }

        internal void ChangeActiveThread(long id, bool verbose) {
            if (_activeEvaluator != null) {
                PythonThread thread = _activeEvaluator.GetThreads().SingleOrDefault(target => target.Id == id);
                if (thread != null) {
                    _activeEvaluator.SwitchThread(thread, verbose);
                } else {
                    _window.WriteError(String.Format("Invalid thread id '{0}'.", id));
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
                    _window.WriteError(String.Format("Invalid frame id '{0}'.", id));
                }
            } else {
                NoProcessError();
            }
        }

        internal void DisplayProcesses() {
            foreach (var target in _evaluators.Values) {
                if (target.Process != null) {
                    _window.WriteLine(String.Format("{2}Process id={0}, Language version={1}", target.Process.Id, target.Process.LanguageVersion, target.Process.Id == _activeEvaluator.ProcessId ? currentPrefix : notCurrentPrefix));
                }
            }
        }

        internal void DisplayThreads() {
            if (_activeEvaluator != null) {
                foreach (var target in _activeEvaluator.GetThreads()) {
                    _window.WriteLine(String.Format("{2}Thread id={0}, name={1}", target.Id, target.Name, target.Id == _activeEvaluator.ThreadId ? currentPrefix : notCurrentPrefix));
                }
            } else {
                NoProcessError();
            }
        }

        internal void DisplayFrames() {
            if (_activeEvaluator != null) {
                foreach (var target in _activeEvaluator.GetFrames()) {
                    _window.WriteLine(String.Format("{2}Frame id={0}, function={1}", target.FrameId, target.FunctionName, target.FrameId == _activeEvaluator.FrameId ? currentPrefix : notCurrentPrefix));
                }
            } else {
                NoProcessError();
            }
        }

        internal IEnumerable<string> SplitCode(string code) {
            if (_activeEvaluator != null) {
                return _activeEvaluator.SplitCode(code);
            } else {
                NoProcessError();
                return new string[0];
            }
        }

        private void OnEngineAttached(object sender, AD7EngineEventArgs e) {
            AttachProcess(e.Engine.Process, e.Engine);
        }

        private void OnEngineDetaching(object sender, AD7EngineEventArgs e) {
            DetachProcess(e.Engine.Process);
        }

        private void OnProcessExited(object sender, ProcessExitedEventArgs e) {
            DetachProcess((PythonProcess)sender);
        }

        internal void SwitchProcess(PythonProcess process, bool verbose) {
            var newEvaluator = _evaluators[process.Id];
            if (newEvaluator != _activeEvaluator) {
                _activeEvaluator = newEvaluator;
                ActiveProcessChanged();
                if (verbose) {
                    _window.WriteLine(String.Format("Current process changed to {0}", process.Id));
                }
            }
        }

        internal void AttachProcess(PythonProcess process, IThreadIdMapper threadIdMapper) {
            if (_evaluators.ContainsKey(process.Id)) {
                // Process is already attached, so just switch to it if needed
                SwitchProcess(process, false);
                return;
            }

            process.ProcessExited += new EventHandler<ProcessExitedEventArgs>(OnProcessExited);
            var evaluator = new PythonDebugProcessReplEvaluator(_serviceProvider, process, _pyService, threadIdMapper);
            evaluator.Window = _window;
            evaluator.AvailableScopesChanged += new EventHandler<EventArgs>(evaluator_AvailableScopesChanged);
            evaluator.MultipleScopeSupportChanged += new EventHandler<EventArgs>(evaluator_MultipleScopeSupportChanged);
            _evaluators.Add(process.Id, evaluator);

            _activeEvaluator = evaluator;
        }

        internal void DetachProcess(PythonProcess process) {
            int id = process.Id;
            PythonDebugProcessReplEvaluator evaluator;
            if (_evaluators.TryGetValue(id, out evaluator)) {
                evaluator.AvailableScopesChanged -= new EventHandler<EventArgs>(evaluator_AvailableScopesChanged);
                evaluator.MultipleScopeSupportChanged -= new EventHandler<EventArgs>(evaluator_MultipleScopeSupportChanged);
                process.DisconnectRepl();
                _evaluators.Remove(id);
                if (_activeEvaluator == evaluator) {
                    _activeEvaluator = null;
                }

                ActiveProcessChanged();
            }
        }

        private void evaluator_MultipleScopeSupportChanged(object sender, EventArgs e) {
            var supportChanged = MultipleScopeSupportChanged;
            if (supportChanged != null) {
                supportChanged(this, EventArgs.Empty);
            }
        }

        private void evaluator_AvailableScopesChanged(object sender, EventArgs e) {
            var curScopesChanged = AvailableScopesChanged;
            if (curScopesChanged != null) {
                curScopesChanged(this, EventArgs.Empty);
            }
        }

        private void ActiveProcessChanged() {
            var supportChanged = MultipleScopeSupportChanged;
            if (supportChanged != null) {
                supportChanged(this, EventArgs.Empty);
            }

            var curScopesChanged = AvailableScopesChanged;
            if (curScopesChanged != null) {
                curScopesChanged(this, EventArgs.Empty);
            }
        }

        private void NoProcessError() {
            _window.WriteError("Command only available when a process is being debugged.");
        }

        private void NoExecutionIfNotStoppedInDebuggerError() {
            _window.WriteError("Code can only be executed while stopped in debugger.");
        }

#if DEV14_OR_LATER
        public Task<ExecutionResult> InitializeAsync() {
            _commands = BasePythonReplEvaluator.GetInteractiveCommands(_serviceProvider, _window, this);

            return Initialize(CurrentWindow);
        }

        public Task<ExecutionResult> ResetAsync(bool initialize = true) {
            return Reset();
        }

        public Task<ExecutionResult> ExecuteCodeAsync(string text) {
            return ExecuteText(text);
        }

        public string GetPrompt() {
            if (_window != null &&
                _window.CurrentLanguageBuffer != null &&
                _window.CurrentLanguageBuffer.CurrentSnapshot != null &&
                _window.CurrentLanguageBuffer.CurrentSnapshot.LineCount > 1) {
                return SecondaryPrompt;
            }

            return PrimaryPrompt;
        }
#endif
    }

    internal class PythonDebugProcessReplEvaluator : BasePythonReplEvaluator {
        private readonly PythonProcess _process;
        private readonly IThreadIdMapper _threadIdMapper;
        private long _threadId;
        private int _frameId;
        private PythonLanguageVersion _languageVersion;

        public PythonDebugProcessReplEvaluator(IServiceProvider serviceProvider, PythonProcess process, PythonToolsService pyService, IThreadIdMapper threadIdMapper)
            : base(serviceProvider, pyService, GetOptions(serviceProvider, pyService)) {
            _process = process;
            _threadIdMapper = threadIdMapper;
            _threadId = process.GetThreads()[0].Id;
            _languageVersion = process.LanguageVersion;

            EnsureConnected();
        }

        private static PythonReplEvaluatorOptions GetOptions(IServiceProvider serviceProvider, PythonToolsService pyService) {
            return new DefaultPythonReplEvaluatorOptions(
                serviceProvider,
                () => pyService.DebugInteractiveOptions
            );
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

        protected override PythonLanguageVersion AnalyzerProjectLanguageVersion {
            get {
                return _languageVersion;
            }
        }

        protected override PythonLanguageVersion LanguageVersion {
            get {
                return _languageVersion;
            }
        }

        internal override string DisplayName {
            get {
                return "Debug" + _languageVersion.ToString();
            }
        }

        protected override void Connect() {
            var remoteProcess = _process as PythonRemoteProcess;
            if (remoteProcess == null) {
                Socket listenerSocket;
                int portNum;
                CreateConnection(out listenerSocket, out portNum);
                Process proc = System.Diagnostics.Process.GetProcessById(_process.Id);
                CreateCommandProcessor(listenerSocket, false, proc);
                _process.ConnectRepl(portNum);
            } else {
                // Ignore SSL errors, since user was already prompted about them and chose to ignore them when he attached to this process.
                var stream = remoteProcess.Connect(false);
                bool connected = false;
                try {
                    stream.Write(PythonRemoteProcess.ReplCommandBytes);

                    string attachResp = stream.ReadAsciiString(PythonRemoteProcess.Accepted.Length);
                    if (attachResp != PythonRemoteProcess.Accepted) {
                        throw new ConnectionException(ConnErrorMessages.RemoteAttachRejected);
                    }

                    connected = true;
                } finally {
                    if (!connected) {
                        if (stream != null) {
                            stream.Close();
                        }
                        stream = null;
                    }
                }

                CreateCommandProcessor(stream, false, null);
            }
        }

        protected override void OnConnected() {
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

            OnMultipleScopeSupportChanged();
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
                Window.WriteError(string.Format("Cannot change current thread to {0}, because it does not have any visible frames.", thread.Id));
                return;
            }

            _threadId = thread.Id;
            _frameId = frame.FrameId;
            SetThreadAndFrameCommand(thread.Id, _frameId, frame.Kind);
            if (verbose) {
                Window.WriteLine(String.Format("Current thread changed to {0}, frame {1}", _threadId, _frameId));
            }
        }

        internal void SwitchFrame(PythonStackFrame frame) {
            _frameId = frame.FrameId;
            SetThreadAndFrameCommand(frame.Thread.Id, frame.FrameId, frame.Kind);
            Window.WriteLine(String.Format("Current frame changed to {0}", frame.FrameId));
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
}
