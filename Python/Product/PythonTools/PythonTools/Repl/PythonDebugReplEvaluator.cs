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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.Repl {
    [InteractiveWindowRole("Debug")]
    [ContentType(PythonCoreConstants.ContentType)]
    [ContentType(PredefinedInteractiveCommandsContentTypes.InteractiveCommandContentTypeName)]
    internal class PythonDebugReplEvaluator :
        IInteractiveEvaluator,
        IPythonInteractiveEvaluator,
        IPythonInteractiveIntellisense {

        private readonly Dictionary<int, Task> _attachingTasks = new Dictionary<int, Task>();
        private EnvDTE.DebuggerEvents _debuggerEvents;
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;
        private IInteractiveWindowCommands _commands;
        private Uri _documentUri;

        private static readonly string currentPrefix = Strings.DebugReplCurrentIndicator;
        private static readonly string notCurrentPrefix = Strings.DebugReplNotCurrentIndicator;

        public PythonDebugReplEvaluator(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _pyService = serviceProvider.GetPythonToolsService();

            var dte = _serviceProvider.GetDTE();
            if (dte != null) {
                // running outside of VS, make this work for tests.
                _debuggerEvents = dte.Events.DebuggerEvents;
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

        public void ActiveLanguageBufferChanged(ITextBuffer currentBuffer, ITextBuffer previousBuffer) {
        }

        public bool CanExecuteCode(string text) {
            var pr = ParseResult.Complete;
            if (string.IsNullOrEmpty(text)) {
                return true;
            }
            if (string.IsNullOrWhiteSpace(text) && text.EndsWithOrdinal("\n")) {
                //pr = ParseResult.Empty;
                return true;
            }

            var parser = Parser.CreateParser(new StringReader(text), PythonLanguageVersion.None);
            parser.ParseInteractiveCode(null, out pr);
            if (pr == ParseResult.IncompleteStatement || pr == ParseResult.Empty) {
                return text.EndsWithOrdinal("\n");
            }
            if (pr == ParseResult.IncompleteToken) {
                return false;
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

            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                var tid = _serviceProvider.GetDTE().Debugger.CurrentThread.ID;
                (bool isSuccessful, string message) result = CustomDebugAdapterProtocolExtension.EvaluateReplRequest(text, tid);

                if (!result.isSuccessful) {
                    CurrentWindow.WriteError(result.message);
                    return ExecutionResult.Failed;
                }

                CurrentWindow.Write(result.message);
            }

            return ExecutionResult.Succeeded;
        }

        public async Task<bool> ExecuteFileAsync(string filename, string extraArgs) {
            NotSupported();
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
            return PythonCommonInteractiveEvaluator.FormatClipboard(_serviceProvider, CurrentWindow);
        }

        public void Dispose() {
        }

        public IEnumerable<string> GetAvailableScopes() {
            return new string[0];
        }

        public void SetScope(string scopeName) {
        }

        public string CurrentScopeName => "";
        public string CurrentScopePath => "";
        public bool EnableMultipleScopes => false;

        public bool LiveCompletionsOnly {
            get { return CurrentOptions.LiveCompletionsOnly; }
        }

        public IInteractiveWindow CurrentWindow { get; set; }

        public Uri DocumentUri {
            get {
                if (_documentUri != null) {
                    return _documentUri;
                } else {
                    _documentUri = new Uri($"repl://{Guid.NewGuid()}/repl.py");
                    return _documentUri;
                }
            }
        }

        public Uri NextDocumentUri() => null;

        public bool IsDisconnected => true;

        public bool IsExecuting => false;

        public string DisplayName => Strings.DebugReplDisplayName;

        public PythonLanguageVersion LanguageVersion => PythonLanguageVersion.None;

        public IEnumerable<KeyValuePair<string, string>> GetAvailableScopesAndPaths() {
            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        public async Task<CompletionResult[]> GetMemberNamesAsync(string text, CancellationToken ct) {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                var expression = string.Format(CultureInfo.InvariantCulture, "':'.join(dir({0}))", text ?? "");
                var tid = _serviceProvider.GetDTE().Debugger.CurrentThread.ID;
                (bool isSuccessful, string message) result = CustomDebugAdapterProtocolExtension.EvaluateReplRequest(text, tid);

                if (result.isSuccessful) {
                    var completionResults = result.message
                                    .Split(':')
                                    .Where(r => !string.IsNullOrEmpty(r))
                                    .Select(r => new CompletionResult(r, PythonMemberType.Generic))
                                    .ToArray();
                    return completionResults;
                }
            }

            return new CompletionResult[0];
        }

        public async Task<OverloadDoc[]> GetSignatureDocumentationAsync(string text, CancellationToken ct) {
            return new OverloadDoc[0];
        }

        internal void StepOut() {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                _serviceProvider.GetDTE().Debugger.CurrentThread.Parent.StepOut();
                CurrentWindow.TextView.VisualElement.Focus();
            } else {
                NoProcessError();
            }
        }

        internal void StepInto() {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                _serviceProvider.GetDTE().Debugger.CurrentThread.Parent.StepInto();
                CurrentWindow.TextView.VisualElement.Focus();
            } else {
                NoProcessError();
            }
        }

        internal void StepOver() {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                _serviceProvider.GetDTE().Debugger.CurrentThread.Parent.StepOver();
                CurrentWindow.TextView.VisualElement.Focus();
            } else {
                NoProcessError();
            }
        }

        internal void Resume() {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                _serviceProvider.GetDTE().Debugger.CurrentThread.Parent.Go();
                CurrentWindow.TextView.VisualElement.Focus();
            } else {
                NoProcessError();
            }
        }

        internal void FrameUp() {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                NotSupported();
            } else {
                NoProcessError();
            }
        }

        internal void FrameDown() {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                NotSupported();
            } else {
                NoProcessError();
            }
        }

        internal void DisplayActiveProcess() {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                NotSupported();
            } else {
                CurrentWindow.WriteLine("None" + Environment.NewLine);
            }
        }

        internal void DisplayActiveThread() {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                NotSupported();
            } else {
                NoProcessError();
            }
        }

        internal void DisplayActiveFrame() {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                NotSupported();
            } else {
                NoProcessError();
            }
        }

        internal void ChangeActiveProcess(int id, bool verbose) {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                NotSupported();
            } else {
                CurrentWindow.WriteErrorLine(Strings.DebugReplInvalidProcessId.FormatUI(id));
            }
        }

        internal void ChangeActiveThread(long id, bool verbose) {
           if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                NotSupported();
            } else {
                NoProcessError();
            }
        }

        internal void ChangeActiveFrame(int id) {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                NotSupported();
            } else {
                NoProcessError();
            }
        }

        internal void DisplayProcesses() {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                NotSupported();
            }
        }

        internal void DisplayThreads() {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                NotSupported();
            } else {
                NoProcessError();
            }
        }

        internal void DisplayFrames() {
            if (CustomDebugAdapterProtocolExtension.IsAvailable()) {
                NotSupported();
            } else {
                NoProcessError();
            }
        }

        private void NoProcessError() {
            CurrentWindow.WriteErrorLine(Strings.DebugReplNoProcessError);
        }

        private void NotSupported() {
            CurrentWindow.WriteError(Strings.DebugReplFeatureNotSupported);
        }

        private void NoExecutionIfNotStoppedInDebuggerError() {
            CurrentWindow.WriteErrorLine(Strings.DebugReplNoExecutionIfNotStoppedInDebuggerError);
        }

        public Task<ExecutionResult> InitializeAsync() {
            _commands = PythonCommonInteractiveEvaluator.GetInteractiveCommands(_serviceProvider, CurrentWindow, this);

            CurrentWindow.TextView.Options.SetOptionValue(InteractiveWindowOptions.SmartUpDown, CurrentOptions.UseSmartHistory);
            CurrentWindow.WriteLine(Strings.DebugReplHelpMessage);

            return ExecutionResult.Succeeded;
        }

        public Task<ExecutionResult> ResetAsync(bool initialize = true) {
            return Reset();
        }

        public string GetPrompt() => null;

        public Task<object> GetAnalysisCompletions(LSP.Position triggerPoint, LSP.CompletionContext context, CancellationToken token) {
            // No analysis completions for debug repl
            return Task.FromResult<object>(null);
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
