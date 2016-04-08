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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Repl {
    [InteractiveWindowRole("Execution")]
    [InteractiveWindowRole("Reset")]
    [ContentType(PythonCoreConstants.ContentType)]
    [ContentType(PredefinedInteractiveCommandsContentTypes.InteractiveCommandContentTypeName)]
    sealed class SelectableReplEvaluator : 
        IPythonInteractiveEvaluator,
        IMultipleScopeEvaluator,
        IPythonInteractiveIntellisense,
        IDisposable
    {
        private readonly IReadOnlyList<IInteractiveEvaluatorProvider> _providers;

        private IInteractiveEvaluator _evaluator;
        private string _evaluatorId;
        private IInteractiveWindow _window;

        public event EventHandler EvaluatorChanged;
        public event EventHandler AvailableEvaluatorsChanged;

        public event EventHandler<EventArgs> AvailableScopesChanged;
        public event EventHandler<EventArgs> MultipleScopeSupportChanged;

        public SelectableReplEvaluator(
            IEnumerable<IInteractiveEvaluatorProvider> providers,
            string initialReplId
        ) {
            _providers = providers.ToArray();
            foreach (var provider in _providers) {
                provider.EvaluatorsChanged += Provider_EvaluatorsChanged;
            }
            if (!string.IsNullOrEmpty(initialReplId)) {
                _evaluatorId = initialReplId;
            }
        }

        private void Provider_EvaluatorsChanged(object sender, EventArgs e) {
            AvailableEvaluatorsChanged?.Invoke(this, EventArgs.Empty);
        }

        public IInteractiveEvaluator Evaluator => _evaluator;
        public string CurrentEvaluator => _evaluatorId;


        public bool IsDisconnected => (_evaluator as IPythonInteractiveEvaluator)?.IsDisconnected ?? true;
        public bool IsExecuting => (_evaluator as IPythonInteractiveEvaluator)?.IsExecuting ?? false;
        public string DisplayName => (_evaluator as IPythonInteractiveEvaluator)?.DisplayName;

        public bool LiveCompletionsOnly => (_evaluator as IPythonInteractiveIntellisense)?.LiveCompletionsOnly ?? false;

        public VsProjectAnalyzer Analyzer => (_evaluator as IPythonInteractiveIntellisense)?.Analyzer;

        // Test methods
        internal string PrimaryPrompt => ((dynamic)_evaluator)?.PrimaryPrompt ?? ">>> ";
        internal string SecondaryPrompt => ((dynamic)_evaluator)?.SecondaryPrompt ?? "... ";

        public void SetEvaluator(string id) {
            if (_evaluatorId == id && _evaluator != null) {
                return;
            }

            var eval = string.IsNullOrEmpty(id) ?
                null :
                _providers.Select(p => p.GetEvaluator(id)).FirstOrDefault(e => e != null);

            var oldEval = _evaluator;
            _evaluator = null;
            if (oldEval != null) {
                DetachWindow(oldEval);
                DetachMultipleScopeHandling(oldEval);
                // TODO: Keep the evaluator around for a while?
                // In case the user wants it back
                oldEval.Dispose();
                oldEval.CurrentWindow = null;
            }

            if (eval != null) {
                eval.CurrentWindow = CurrentWindow;
                if (eval.CurrentWindow != null) {
                    // Otherwise, we'll initialize when the window is set
                    AttachWindow(eval);
                    eval.InitializeAsync().DoNotWait();
                }
            }
            _evaluator = eval;
            _evaluatorId = id;

            UpdateCaption();

            EvaluatorChanged?.Invoke(this, EventArgs.Empty);
            AttachMultipleScopeHandling(eval);
        }

        private void DetachWindow(IInteractiveEvaluator oldEval) {
            var view = oldEval.CurrentWindow?.TextView;
            if (view == null) {
                return;
            }

            foreach (var buffer in view.BufferGraph.GetTextBuffers(EditorExtensions.IsPythonContent)) {
                if (oldEval.CurrentWindow.CurrentLanguageBuffer == buffer) {
                    continue;
                }
                buffer.Properties[BufferParser.DoNotParse] = BufferParser.DoNotParse;
            }
        }

        private void AttachWindow(IInteractiveEvaluator eval) {
            var view = eval.CurrentWindow?.TextView;
            if (view == null) {
                return;
            }

            VsProjectAnalyzer oldAnalyzer;
            if (view.TextBuffer.Properties.TryGetProperty(typeof(VsProjectAnalyzer), out oldAnalyzer)) {
                view.TextBuffer.Properties.RemoveProperty(typeof(VsProjectAnalyzer));
            }

            var newAnalyzer = (eval as IPythonInteractiveIntellisense)?.Analyzer;
            if (newAnalyzer != null) {
                view.TextBuffer.Properties[typeof(VsProjectAnalyzer)] = newAnalyzer;
                if (oldAnalyzer != null) {
                    newAnalyzer.SwitchAnalyzers(oldAnalyzer);
                }
            }
        }

        private void UpdateCaption() {
            var display = DisplayName;

            var window = CurrentWindow;
            IVsInteractiveWindow viw;
            if (window == null || !window.Properties.TryGetProperty(typeof(IVsInteractiveWindow), out viw)) {
                return;
            }

            var twp = viw as ToolWindowPane;
            if (twp == null) {
                return;
            }

            if (!string.IsNullOrEmpty(display)) {
                twp.Caption = Strings.ReplCaption.FormatUI(display);
            } else {
                twp.Caption = Strings.ReplCaptionNoEvaluator;
            }
        }

        public IEnumerable<KeyValuePair<string, string>> AvailableEvaluators {
            get {
                return _providers.SelectMany(e => e.GetEvaluators());
            }
        }

        public IInteractiveWindow CurrentWindow {
            get { return _window; }
            set {
                _window = value;
                if (_evaluator != null && _evaluator.CurrentWindow != value) {
                    DetachWindow(_evaluator);
                    _evaluator.CurrentWindow = value;
                    AttachWindow(_evaluator);
                    if (value != null) {
                        _evaluator.InitializeAsync().DoNotWait();
                    }
                }
                UpdateCaption();
            }
        }

        #region Multiple Scope Support

        private void AttachMultipleScopeHandling(IInteractiveEvaluator evaluator) {
            var mse = evaluator as IMultipleScopeEvaluator;
            if (mse == null) {
                return;
            }
            mse.AvailableScopesChanged += Evaluator_AvailableScopesChanged;
            mse.MultipleScopeSupportChanged += Evaluator_MultipleScopeSupportChanged;
            MultipleScopeSupportChanged?.Invoke(this, EventArgs.Empty);
            AvailableScopesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void DetachMultipleScopeHandling(IInteractiveEvaluator evaluator) {
            var mse = evaluator as IMultipleScopeEvaluator;
            if (mse == null) {
                return;
            }
            mse.AvailableScopesChanged -= Evaluator_AvailableScopesChanged;
            mse.MultipleScopeSupportChanged -= Evaluator_MultipleScopeSupportChanged;
            MultipleScopeSupportChanged?.Invoke(this, EventArgs.Empty);
            AvailableScopesChanged?.Invoke(this, EventArgs.Empty);
        }

        public string CurrentScopeName => (_evaluator as IMultipleScopeEvaluator)?.CurrentScopeName;
        public bool EnableMultipleScopes => (_evaluator as IMultipleScopeEvaluator)?.EnableMultipleScopes ?? false;

        private void Evaluator_MultipleScopeSupportChanged(object sender, EventArgs e) {
            MultipleScopeSupportChanged?.Invoke(this, e);
        }

        private void Evaluator_AvailableScopesChanged(object sender, EventArgs e) {
            AvailableScopesChanged?.Invoke(this, e);
        }

        public void SetScope(string scopeName) {
            (_evaluator as IMultipleScopeEvaluator)?.SetScope(scopeName);
        }

        public IEnumerable<string> GetAvailableScopes() {
            return (_evaluator as IMultipleScopeEvaluator)?.GetAvailableScopes() ?? Enumerable.Empty<string>();
        }

        #endregion

        public void AbortExecution() {
            _evaluator?.AbortExecution();
        }

        public bool CanExecuteCode(string text) {
            return _evaluator?.CanExecuteCode(text) ?? false;
        }

        public void Dispose() {
            _evaluator?.Dispose();
            _evaluator = null;
            _window = null;
        }

        public Task<ExecutionResult> ExecuteCodeAsync(string text) {
            return _evaluator?.ExecuteCodeAsync(text) ?? ExecutionResult.Failed;
        }

        public string FormatClipboard() {
            return _evaluator?.FormatClipboard();
        }

        public string GetPrompt() {
            return _evaluator?.GetPrompt() ?? ">>> ";
        }

        public Task<ExecutionResult> InitializeAsync() {
            if (_evaluator == null && !string.IsNullOrEmpty(_evaluatorId)) {
                SetEvaluator(_evaluatorId);
                return ExecutionResult.Succeeded;
            }

            return _evaluator?.InitializeAsync() ?? ExecutionResult.Succeeded;
        }

        public Task<ExecutionResult> ResetAsync(bool initialize = true) {
            return _evaluator?.ResetAsync(initialize) ?? ExecutionResult.Succeeded;
        }

        public Task<ExecutionResult> ExecuteFileAsync(string filename, string extraArgs) {
            return (_evaluator as IPythonInteractiveEvaluator)?.ExecuteFileAsync(filename, extraArgs)
                ?? ExecutionResult.Failed;
        }

        public IEnumerable<KeyValuePair<string, bool>> GetAvailableScopesAndKind() {
            return (_evaluator as IPythonInteractiveIntellisense)?.GetAvailableScopesAndKind()
                ?? Enumerable.Empty<KeyValuePair<string, bool>>();
        }

        public CompletionResult[] GetMemberNames(string text) {
            return (_evaluator as IPythonInteractiveIntellisense)?.GetMemberNames(text)
                ?? new CompletionResult[0];
        }

        public OverloadDoc[] GetSignatureDocumentation(string text) {
            return (_evaluator as IPythonInteractiveIntellisense)?.GetSignatureDocumentation(text)
                ?? new OverloadDoc[0];
        }
    }
}
