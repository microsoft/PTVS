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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Parsing;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.Remote;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Repl {
    [InteractiveWindowRole("Debug")]
    [ContentType(PythonCoreConstants.ContentType)]
    [ContentType(PredefinedInteractiveCommandsContentTypes.InteractiveCommandContentTypeName)]
    internal class PythonDebugProcessReplEvaluator : PythonInteractiveEvaluator {
        private readonly PythonProcess _process;
        private readonly IThreadIdMapper _threadIdMapper;
        private long _threadId;
        private int _frameId;
        private PythonLanguageVersion _languageVersion;
        private Dictionary<string, string> _moduleToFileName;
        private string _currentScopeName;
        private string _currentScopeFileName;

        private string _currentFrameFilename;
        private CompletionResult[] _currentFrameLocals;

        /// <summary>
        /// Backwards compatible and non-localized name for the scope
        /// that represents execution on the current frame.
        /// </summary>
        private const string CurrentFrameScopeFixedName = "<CurrentFrame>";

        public PythonDebugProcessReplEvaluator(IServiceProvider serviceProvider, PythonProcess process, IThreadIdMapper threadIdMapper)
            : base(serviceProvider) {
            _process = process;
            _threadIdMapper = threadIdMapper;
            _threadId = process.GetThreads()[0].Id;
            _languageVersion = process.LanguageVersion;
            _currentScopeName = CurrentFrameScopeFixedName;
            DisplayName = Strings.DebugReplDisplayName;
        }

        public override async Task<ExecutionResult> InitializeAsync() {
            var result = await base.InitializeAsync();
            if (!result.IsSuccessful) {
                return result;
            }

            result = await _serviceProvider.GetUIThread().InvokeTask(async () => {
                UpdatePropertiesFromProjectMoniker();

                var remoteProcess = _process as PythonRemoteProcess;

                try {
                    _serviceProvider.GetPythonToolsService().Logger?.LogEvent(Logging.PythonLogEvent.DebugRepl, new Logging.DebugReplInfo {
                        RemoteProcess = remoteProcess != null,
                        Version = _process.LanguageVersion.ToVersion().ToString()
                    });
                } catch (Exception ex) {
                    Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                }

                _process.ModulesChanged += OnModulesChanged;

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

                return ExecutionResult.Success;
            });

            return result;
        }


        private async void OnModulesChanged(object sender, EventArgs e) {
            await RefreshAvailableScopes();
        }

        internal async Task<KeyValuePair<string, string>[]> RefreshAvailableScopes() {
            var modules = await _process.GetModuleNamesAndPaths();

            var moduleToFile = new Dictionary<string, string>();
            foreach (var item in modules) {
                if (!string.IsNullOrEmpty(item.Value)) {
                    moduleToFile[item.Key] = item.Value;
                }
            }
            _moduleToFileName = moduleToFile;

            SetAvailableScopes(modules.Select(m => m.Key).ToArray());
            EnableMultipleScopes = true;

            return modules;
        }

        protected override Task ExecuteStartupScripts(string scriptsPath) {
            // Do not execute scripts for debug evaluator
            return Task.FromResult<object>(null);
        }

        public override async Task<ExecutionResult> ExecuteCodeAsync(string text) {
            var tcs = new TaskCompletionSource<ExecutionResult>();
            var ct = new CancellationToken();
            var cancellationRegistration = ct.Register(() => tcs.TrySetCanceled());

            EventHandler<ProcessExitedEventArgs> processExited = delegate {
                tcs.TrySetCanceled();
            };

            EventHandler<OutputEventArgs> debuggerOutput = (object sender, OutputEventArgs e) => {
                switch (e.Channel) {
                    case OutputChannel.StdOut:
                        WriteOutput(e.Output, addNewline: false);
                        break;
                    case OutputChannel.StdErr:
                        WriteError(e.Output, addNewline: false);
                        break;
                }
            };

            Action<PythonEvaluationResult> resultReceived = (PythonEvaluationResult result) => {
                if (!string.IsNullOrEmpty(result.ExceptionText)) {
                    tcs.TrySetResult(ExecutionResult.Failure);
                } else {
                    tcs.TrySetResult(ExecutionResult.Success);
                }
            };

            _process.ProcessExited += processExited;
            _process.DebuggerOutput += debuggerOutput;
            try {
                if (_currentScopeName == CurrentFrameScopeFixedName) {
                    var frame = GetFrames().SingleOrDefault(f => f.FrameId == _frameId);
                    if (frame != null) {
                        await _process.ExecuteTextAsync(text, PythonEvaluationResultReprKind.Normal, frame, true, resultReceived, ct);
                    } else {
                        WriteError(Strings.DebugReplCannotRetrieveFrameError);
                        tcs.TrySetResult(ExecutionResult.Failure);
                    }
                } else {
                    await _process.ExecuteTextAsync(text, PythonEvaluationResultReprKind.Normal, _currentScopeName, true, resultReceived, ct);
                }

                return await tcs.Task;
            } finally {
                _process.ProcessExited -= processExited;
                _process.DebuggerOutput -= debuggerOutput;
                cancellationRegistration.Dispose();
            }
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

        public override string CurrentScopeName {
            get {
                // Callers expect the localized name
                return _currentScopeName == CurrentFrameScopeFixedName
                    ? Strings.DebugReplCurrentFrameScope : _currentScopeName;
            }
        }

        public override string CurrentScopePath {
            get {
                return _currentScopeFileName;
            }
        }

        public override string CurrentWorkingDirectory {
            get {
                // This is never called
                return null;
            }
        }

        public override IEnumerable<KeyValuePair<string, string>> GetAvailableScopesAndPaths() {
            var t = RefreshAvailableScopes();
            if (t != null && t.Wait(1000) && t.Result != null) {
                return t.Result;
            }

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        public override async Task<CompletionResult[]> GetMemberNamesAsync(string text, CancellationToken ct) {
            if (_currentScopeName == CurrentFrameScopeFixedName && string.IsNullOrEmpty(text) && _currentFrameLocals != null) {
                return _currentFrameLocals.ToArray();
            };

            return await GetMemberNamesInternalAsync(text, ct);
        }

        public override Task<OverloadDoc[]> GetSignatureDocumentationAsync(string text, CancellationToken ct) {
            // TODO: implement this
            return Task.FromResult(new OverloadDoc[0]);
        }

        private async Task<CompletionResult[]> GetMemberNamesInternalAsync(string text, CancellationToken ct) {
            // TODO: implement support for getting members for module scope,
            // not just for current frame scope
            if (_currentScopeName == CurrentFrameScopeFixedName) {
                var frame = GetFrames().SingleOrDefault(f => f.FrameId == _frameId);
                if (frame != null) {
                    using (var completion = new AutoResetEvent(false)) {
                        PythonEvaluationResult result = null;

                        var expression = string.Format(CultureInfo.InvariantCulture, "':'.join(dir({0}))", text ?? "");
                        _serviceProvider.GetUIThread().InvokeTaskSync(() => frame.ExecuteTextAsync(expression, PythonEvaluationResultReprKind.Raw, (obj) => {
                            result = obj;
                            try {
                                completion.Set();
                            } catch (ObjectDisposedException) {
                            }
                        }, CancellationToken.None), CancellationToken.None);

                        if (completion.WaitOne(100) && !_process.HasExited && result?.StringRepr != null) {
                            // We don't really know if it's a field, function or else...
                            var completionResults = result.StringRepr
                                .Split(':')
                                .Where(r => !string.IsNullOrEmpty(r))
                                .Select(r => new CompletionResult(r, PythonMemberType.Generic))
                                .ToArray();

                            return completionResults;
                        }
                    }
                }
            }

            return Array.Empty<CompletionResult>();
        }

        public override void SetScope(string scopeName) {
            if (!string.IsNullOrWhiteSpace(scopeName)) {
                if (scopeName == Strings.DebugReplCurrentFrameScope) {
                    // Most callers will pass in the localized name, but the $mod command may have either.
                    // Always store the unlocalized name.
                    scopeName = CurrentFrameScopeFixedName;
                }
                _currentScopeName = scopeName;

                if (!(_moduleToFileName?.TryGetValue(scopeName, out _currentScopeFileName) ?? false)) {
                    _currentScopeFileName = null;
                }

                WriteOutput(Strings.ReplModuleChanged.FormatUI(CurrentScopeName));
            } else {
                WriteOutput(CurrentScopeName);
            }
        }

        internal IList<PythonThread> GetThreads() {
            return _process.GetThreads();
        }

        internal IList<PythonStackFrame> GetFrames() {
            var activeThread = _process.GetThreads().SingleOrDefault(t => t.Id == _threadId);
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
            _currentScopeName = CurrentFrameScopeFixedName;
            _currentScopeFileName = null;
            if (_currentFrameFilename != frame.FileName) {
                _currentFrameFilename = frame.FileName;
            }
            UpdateFrameLocals(frame);
            if (verbose) {
                WriteOutput(Strings.DebugReplThreadChanged.FormatUI(_threadId, _frameId));
            }
        }

        internal void SwitchFrame(PythonStackFrame frame) {
            _frameId = frame.FrameId;
            _currentScopeName = CurrentFrameScopeFixedName;
            _currentScopeFileName = null;
            UpdateFrameLocals(frame);
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

        private void UpdateFrameLocals(PythonStackFrame frame) {
            _currentFrameLocals = frame.Locals.Union(frame.Parameters)
                .Where(r => !string.IsNullOrEmpty(r.Expression))
                .Select(r => new CompletionResult(r.Expression, PythonMemberType.Generic))
                .ToArray();
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
