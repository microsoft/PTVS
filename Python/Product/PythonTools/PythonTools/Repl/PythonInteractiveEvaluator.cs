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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Repl {
    [InteractiveWindowRole("Execution")]
    [InteractiveWindowRole("Reset")]
    [ContentType(PythonCoreConstants.ContentType)]
    [ContentType(PredefinedInteractiveCommandsContentTypes.InteractiveCommandContentTypeName)]
    partial class PythonInteractiveEvaluator :
        PythonCommonInteractiveEvaluator
    {
        protected CommandProcessorThread _thread;
        private bool _isDisposed;

        public PythonInteractiveEvaluator(IServiceProvider serviceProvider) :
            base(serviceProvider) {
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_analyzer")]
        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            if (_isDisposed) {
                return;
            }
            _isDisposed = true;

            if (disposing) {
                var thread = Interlocked.Exchange(ref _thread, null);
                if (thread != null) {
                    thread.Dispose();
                    WriteError(Strings.ReplExited);
                }
            }
        }

        public override bool IsDisconnected => !(_thread?.IsConnected ?? false);

        public override bool IsExecuting => (_thread?.IsExecuting ?? false);

        public override string CurrentScopeName {
            get {
                return (_thread?.IsConnected ?? false) ? _thread.CurrentScope : "<disconnected>";
            }
        }

        public override string CurrentScopePath {
            get {
                return (_thread?.IsConnected ?? false) ? _thread.CurrentScopeFileName : null;
            }
        }

        public override string CurrentWorkingDirectory {
            get {
                return (_thread?.IsConnected ?? false) ? _thread.CurrentWorkingDirectory : null;
            }
        }

        internal override string PrimaryPrompt => _thread?.PrimaryPrompt ?? ">>> ";
        internal override string SecondaryPrompt => _thread?.SecondaryPrompt ?? "... ";

        public override async Task<bool> GetSupportsMultipleStatementsAsync() {
            var thread = await EnsureConnectedAsync();
            if (thread == null) {
                return false;
            }

            return await thread.GetSupportsMultipleStatementsAsync();
        }

        private async void Thread_AvailableScopesChanged(object sender, EventArgs e) {
            var availableScopes = (await ((CommandProcessorThread)sender).GetAvailableUserScopesAsync(10000))?.ToArray();
            SetAvailableScopes(availableScopes);
        }

        public override IEnumerable<KeyValuePair<string, string>> GetAvailableScopesAndPaths() {
            var t = _thread?.GetAvailableScopesAndPathsAsync(1000);
            if (t != null && t.Wait(1000) && t.Result != null) {
                return t.Result;
            }
            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        public override Task<CompletionResult[]> GetMemberNamesAsync(string text, CancellationToken ct) {
            return Task.FromResult(_thread?.GetMemberNames(text) ?? new CompletionResult[0]);
        }

        public override Task<OverloadDoc[]> GetSignatureDocumentationAsync(string text, CancellationToken ct) {
            return Task.FromResult(_thread?.GetSignatureDocumentation(text) ?? new OverloadDoc[0]);
        }

        public override void AbortExecution() {
            _thread?.AbortCommand();
        }

        private async Task<CommandProcessorThread> EnsureConnectedAsync() {
            var thread = Volatile.Read(ref _thread);
            if (thread != null) {
                return thread;
            }

            return await _serviceProvider.GetUIThread().InvokeTask(async () => {
                if (!UpdatePropertiesFromProjectMoniker().IsSuccessful) {
                    return null;
                }

                if (!UpdatePropertiesFromWorkspaceMoniker().IsSuccessful) {
                    return null;
                }

                var scriptsPath = ScriptsPath;
                if (!Directory.Exists(scriptsPath) && Configuration?.Interpreter != null) {
                    try {
                        scriptsPath = GetScriptsPath(_serviceProvider, Configuration.Interpreter.Description, Configuration.Interpreter);
                    } catch (Exception ex) when (!ex.IsCriticalException()) {
                        scriptsPath = null;
                    }
                }

                // Allow tests to control the backend without relying on the mode.txt file
                string backendOverride = _serviceProvider.GetPythonToolsService().InteractiveBackendOverride;
                if (!string.IsNullOrEmpty(backendOverride)) {
                    BackendName = backendOverride;
                } else if (string.IsNullOrEmpty(BackendName) && !string.IsNullOrEmpty(scriptsPath)) {
                    // If BackendName is already set, don't use the value in mode.txt
                    var modeFile = PathUtils.GetAbsoluteFilePath(scriptsPath, "mode.txt");
                    if (File.Exists(modeFile)) {
                        try {
                            BackendName = File.ReadAllLines(modeFile).FirstOrDefault(line =>
                                !string.IsNullOrEmpty(line) && !line.TrimStart().StartsWithOrdinal("#")
                            );
                        } catch (Exception ex) when (!ex.IsCriticalException()) {
                            WriteError(Strings.ReplCannotReadFile.FormatUI(modeFile));
                        }

                        // Translate legacy backend names.
                        switch (BackendName) {
                            case "visualstudio_ipython_repl.IPythonBackend":
                                BackendName = "ptvsd.repl.ipython.IPythonBackend";
                                break;
                            case "visualstudio_ipython_repl.IPythonBackendWithoutPyLab":
                                BackendName = "ptvsd.repl.ipython.IPythonBackendWithoutPyLab";
                                break;
                        }
                    } else {
                        BackendName = null;
                    }
                }

                try {
                    thread = await ConnectAsync(default(CancellationToken));
                } catch (OperationCanceledException) {
                    thread = null;
                }

                var newerThread = Interlocked.CompareExchange(ref _thread, thread, null);
                if (newerThread != null) {
                    thread.Dispose();
                    return newerThread;
                }

                if (thread != null) {
                    await ExecuteStartupScripts(scriptsPath);

                    thread.AvailableScopesChanged += Thread_AvailableScopesChanged;
                }

                return thread;
            });
        }

        protected override async Task ExecuteStartupScripts(string scriptsPath) {
            if (File.Exists(scriptsPath)) {
                if (!(await ExecuteFileAsync(scriptsPath, null))) {
                    WriteError("Error executing " + scriptsPath);
                }
            } else if (Directory.Exists(scriptsPath)) {
                foreach (var file in PathUtils.EnumerateFiles(scriptsPath, "*.py", recurse: false)) {
                    if (!(await ExecuteFileAsync(file, null))) {
                        WriteError("Error executing " + file);
                    }
                }
            }
        }

        public override async Task<ExecutionResult> ExecuteCodeAsync(string text) {
            var cmds = _commands;
            if (cmds == null) {
                WriteError(Strings.ReplDisconnected);
                return ExecutionResult.Failure;
            }

            var cmdRes = cmds.TryExecuteCommand();
            if (cmdRes != null) {
                return await cmdRes;
            }

            ParseResult pr;
            if (CanExecuteCode(text, out pr)) {
                if (pr == ParseResult.Empty) {
                    // Actually execute "pass", so that we launch the
                    // interpreter but do not cause any other errors.
                    text = "pass";
                }
            }

            var thread = await EnsureConnectedAsync();
            if (thread != null) {
                return await thread.ExecuteText(text);
            }

            WriteError(Strings.ReplDisconnected);
            return ExecutionResult.Failure;
        }

        public override async Task<bool> ExecuteFileAsync(string filename, string extraArgs) {
            var thread = await EnsureConnectedAsync();
            if (thread != null) {
                return await thread.ExecuteFile(filename, extraArgs, "script");
            }

            WriteError(Strings.ReplDisconnected);
            return false;
        }

        public override async Task<bool> ExecuteModuleAsync(string name, string extraArgs) {
            var thread = await EnsureConnectedAsync();
            if (thread != null) {
                return await thread.ExecuteFile(name, extraArgs, "module");
            }

            WriteError(Strings.ReplDisconnected);
            return false;
        }

        public override async Task<bool> ExecuteProcessAsync(string filename, string extraArgs) {
            var thread = await EnsureConnectedAsync();
            if (thread != null) {
                return await thread.ExecuteFile(filename, extraArgs, "process");
            }

            WriteError(Strings.ReplDisconnected);
            return false;
        }

        public override void SetScope(string scopeName) {
            _thread?.SetScope(scopeName);
        }

        protected override async Task<ExecutionResult> ResetWorkerAsync(bool initialize, bool quiet) {
            // suppress reporting "failed to launch repl" process
            var thread = Interlocked.Exchange(ref _thread, null);
            if (thread == null) {
                if (!quiet) {
                    WriteError(Strings.ReplNotStarted);
                }
                return ExecutionResult.Success;
            }

            if (!quiet) {
                WriteOutput(Strings.ReplReset);
            }

            thread.IsProcessExpectedToExit = quiet;
            thread.Dispose();

            var options = _serviceProvider.GetPythonToolsService().InteractiveOptions;
            UseSmartHistoryKeys = options.UseSmartHistory;
            LiveCompletionsOnly = options.LiveCompletionsOnly;

            EnableMultipleScopes = false;

            return ExecutionResult.Success;
        }
    }
}
