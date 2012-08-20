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
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;

namespace Microsoft.PythonTools.Repl {
#if INTERACTIVE_WINDOW
    using IReplWindow = IInteractiveWindow;
    using IReplEvaluator = IInteractiveEngine;
#endif

    internal class PythonDebugReplEvaluator : IReplEvaluator, IMultipleScopeEvaluator, IPythonReplIntellisense {
        private IReplWindow _window;
        private PythonDebugProcessReplEvaluator _activeEvaluator;
        private readonly Dictionary<int, PythonDebugProcessReplEvaluator> _evaluators = new Dictionary<int, PythonDebugProcessReplEvaluator>(); // process id to evaluator
        private EnvDTE.DebuggerEvents _debuggerEvents;
        private PythonInteractiveCommonOptions _options;

        private const string currentPrefix = "=> ";
        private const string notCurrentPrefix = "   ";

        public PythonDebugReplEvaluator() {
            AD7Engine.EngineAttached += new EventHandler<AD7EngineEventArgs>(OnEngineAttached);
            AD7Engine.EngineDetaching += new EventHandler<AD7EngineEventArgs>(OnEngineDetaching);
            AD7Engine.EngineBreakpointHit += new EventHandler<AD7EngineEventArgs>(OnEngineBreakpointHit);
            if (PythonToolsPackage.Instance != null) {
                // running outside of VS, make this work for tests.
                _debuggerEvents = PythonToolsPackage.Instance.DTE.Events.DebuggerEvents;
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
                if (PythonToolsPackage.Instance == null) {
                    // running outside of VS, make this work for tests.
                    if (_options == null) {
                        _options = CreatePackageOptions();
                    }
                    return _options;
                }
                return PythonToolsPackage.Instance.InteractiveDebugOptionsPage.Options;
            }
        }

        protected PythonInteractiveCommonOptions CreatePackageOptions() {
            PythonInteractiveCommonOptions options = new PythonInteractiveCommonOptions();
            options.PrimaryPrompt = ">>> ";
            options.SecondaryPrompt = "... ";
            options.InlinePrompts = true;
            options.UseInterpreterPrompts = true;
            options.ReplIntellisenseMode = ReplIntellisenseMode.DontEvaluateCalls;
            options.ReplSmartHistory = true;
            options.LiveCompletionsOnly = false;
            return options;
        }

        private static bool IsInDebugBreakMode() {
            if (PythonToolsPackage.Instance == null) {
                // running outside of VS, make this work for tests.
                return true;
            }
            return PythonToolsPackage.Instance.DTE.Debugger.CurrentMode == EnvDTE.dbgDebugMode.dbgBreakMode;
        }

        #region IReplEvaluator Members

        public Task<ExecutionResult> Initialize(IReplWindow window) {
            _window = window;
            _window.SetOptionValue(ReplOptions.CommandPrefix, "$");
            _window.SetOptionValue(ReplOptions.UseSmartUpDown, CurrentOptions.ReplSmartHistory);
            _window.SetOptionValue(ReplOptions.PrimaryPrompt, CurrentOptions.PrimaryPrompt);
            _window.SetOptionValue(ReplOptions.SecondaryPrompt, CurrentOptions.SecondaryPrompt);
            _window.SetOptionValue(ReplOptions.DisplayPromptInMargin, !CurrentOptions.InlinePrompts);
            _window.SetOptionValue(ReplOptions.SupportAnsiColors, true);
            _window.SetOptionValue(ReplOptions.FormattedPrompts, true);
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
                            AttachProcess(engine.Process);
                        }
                    }
                }
            }
        }

        private void OnEnterBreakMode(EnvDTE.dbgEventReason Reason, ref EnvDTE.dbgExecutionAction ExecutionAction) {
            int activeProcessId = PythonToolsPackage.Instance.DTE.Debugger.CurrentProcess.ProcessID;
            int activeThreadId = PythonToolsPackage.Instance.DTE.Debugger.CurrentThread.ID;

            AD7Engine engine = AD7Engine.GetEngines().SingleOrDefault(target => target.Process != null && target.Process.Id == activeProcessId);
            if (engine != null) {
                AttachProcess(engine.Process);
                ChangeActiveThread(activeThreadId, false);
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

        public bool CanExecuteText(string text) {
            if (_activeEvaluator != null) {
                return _activeEvaluator.CanExecuteText(text);
            }
            return true;
        }

        public Task<ExecutionResult> ExecuteText(string text) {
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

        public void AbortCommand() {
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

        internal void ChangeActiveThread(int id, bool verbose) {
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

        private void OnEngineBreakpointHit(object sender, AD7EngineEventArgs e) {
            AttachProcess(e.Engine.Process);
        }

        private void OnEngineAttached(object sender, AD7EngineEventArgs e) {
            AttachProcess(e.Engine.Process);
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

        internal void AttachProcess(PythonProcess process) {
            if (_evaluators.ContainsKey(process.Id)) {
                // Process is already attached, so just switch to it if needed
                SwitchProcess(process, false);
                return;
            }

            process.ProcessExited += new EventHandler<ProcessExitedEventArgs>(OnProcessExited);
            var evaluator = new PythonDebugProcessReplEvaluator(process);
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
    }

    internal class PythonDebugProcessReplEvaluator : BasePythonReplEvaluator {
        private PythonProcess _process;
        private int _threadId, _frameId;
        private PythonLanguageVersion _languageVersion;

        public PythonDebugProcessReplEvaluator(PythonProcess process) {
            _process = process;
            _threadId = process.GetThreads()[0].Id;
            _languageVersion = process.LanguageVersion;

            EnsureConnected();
        }

        public PythonProcess Process {
            get { return _process; }

        }
        public int ProcessId {
            get { return _process.Id; }
        }

        public int ThreadId {
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

        protected override PythonInteractiveCommonOptions CreatePackageOptions() {
            PythonInteractiveCommonOptions options = new PythonInteractiveCommonOptions();
            options.PrimaryPrompt = ">>> ";
            options.SecondaryPrompt = "... ";
            options.InlinePrompts = true;
            options.UseInterpreterPrompts = true;
            options.ReplIntellisenseMode = ReplIntellisenseMode.DontEvaluateCalls;
            options.ReplSmartHistory = true;
            options.LiveCompletionsOnly = false;
            return options;
        }

        protected override PythonInteractiveCommonOptions GetPackageOptions() {
            return PythonToolsPackage.Instance.InteractiveDebugOptionsPage.Options;
        }

        protected override void Close() {
            base.Close();
        }

        protected override void Connect() {
            Socket conn;
            int portNum;
            CreateConnection(out conn, out portNum);

            Process proc = System.Diagnostics.Process.GetProcessById(_process.Id);
            CreateListener(conn, false, proc);

            _process.ConnectRepl(portNum);
        }

        protected override void OnConnected() {
            // Finish initialization now that the socket connection has been established
            var threads = _process.GetThreads();
            PythonThread activeThread = null;

            if (PythonToolsPackage.Instance != null) {
                // If we are broken into the debugger, let's set the debug REPL active thread
                // to be the one that is active in the debugger
                var dteDebugger = PythonToolsPackage.Instance.DTE.Debugger;
                if (dteDebugger.CurrentMode == EnvDTE.dbgDebugMode.dbgBreakMode &&
                    dteDebugger.CurrentProcess != null &&
                    dteDebugger.CurrentThread != null) {
                    if (_process.Id == dteDebugger.CurrentProcess.ProcessID) {
                        activeThread = threads.SingleOrDefault(t => t.Id == dteDebugger.CurrentThread.ID);
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
            _threadId = thread.Id;
            _frameId = thread.Frames[0].FrameId;
            SetThreadAndFrameCommand(thread.Id, thread.Frames[0].FrameId, thread.Frames[0].Kind);
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
            PythonToolsPackage.Instance.DTE.Debugger.CurrentThread.Parent.StepOut();
        }

        internal void StepInto() {
            UpdateDTEDebuggerProcessAndThread();
            PythonToolsPackage.Instance.DTE.Debugger.CurrentThread.Parent.StepInto();
        }

        internal void StepOver() {
            UpdateDTEDebuggerProcessAndThread();
            PythonToolsPackage.Instance.DTE.Debugger.CurrentThread.Parent.StepOver();
        }

        internal void Resume() {
            UpdateDTEDebuggerProcessAndThread();
            PythonToolsPackage.Instance.DTE.Debugger.CurrentThread.Parent.Go();
        }

        private void UpdateDTEDebuggerProcessAndThread() {
            EnvDTE.Process dteActiveProcess = null;
            foreach (EnvDTE.Process dteProcess in PythonToolsPackage.Instance.DTE.Debugger.DebuggedProcesses) {
                if (dteProcess.ProcessID == _process.Id) {
                    dteActiveProcess = dteProcess;
                    break;
                }
            }

            if (dteActiveProcess != PythonToolsPackage.Instance.DTE.Debugger.CurrentProcess) {
                PythonToolsPackage.Instance.DTE.Debugger.CurrentProcess = dteActiveProcess;
            }

            EnvDTE.Thread dteActiveThread = null;
            foreach (EnvDTE.Thread dteThread in PythonToolsPackage.Instance.DTE.Debugger.CurrentProgram.Threads) {
                if (dteThread.ID == _threadId) {
                    dteActiveThread = dteThread;
                    break;
                }
            }

            if (dteActiveThread != PythonToolsPackage.Instance.DTE.Debugger.CurrentThread) {
                PythonToolsPackage.Instance.DTE.Debugger.CurrentThread = dteActiveThread;
            }
        }
    }
}
