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
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Repl {
    [InteractiveWindowRole("Execution")]
    [InteractiveWindowRole("Reset")]
    [ContentType(PythonCoreConstants.ContentType)]
    [ContentType(PredefinedInteractiveCommandsContentTypes.InteractiveCommandContentTypeName)]
    sealed class SelectableReplEvaluator : IPythonInteractiveEvaluator, IMultipleScopeEvaluator, IDisposable {
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
                // TODO: Keep the evaluator around for a while?
                // In case the user wants it back
                oldEval.Dispose();
                oldEval.CurrentWindow = null;
            }
            DetachMultipleScopeHandling(oldEval);

            if (eval != null) {
                eval.CurrentWindow = CurrentWindow;
                if (eval.CurrentWindow != null) {
                    // Otherwise, we'll initialize when the window is set
                    eval.InitializeAsync().DoNotWait();
                }
            }
            _evaluator = eval;
            _evaluatorId = id;

            UpdateCaption();

            EvaluatorChanged?.Invoke(this, EventArgs.Empty);
            AttachMultipleScopeHandling(eval);
        }

        public string CurrentEvaluator => _evaluatorId;

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
                twp.Caption = SR.GetString(SR.ReplCaption, display);
            } else {
                twp.Caption = SR.GetString(SR.ReplCaptionNoEvaluator);
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
                    _evaluator.CurrentWindow = value;
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
        public bool IsDisconnected => (_evaluator as IPythonInteractiveEvaluator)?.IsDisconnected ?? true;
        public bool IsExecuting => (_evaluator as IPythonInteractiveEvaluator)?.IsExecuting ?? false;
        public string DisplayName => (_evaluator as IPythonInteractiveEvaluator)?.DisplayName;

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
            throw new NotImplementedException();
        }

        public struct AvailableEvaluator {
            public readonly string DisplayName;
            private readonly IInteractiveEvaluatorProvider _provider;
            private readonly string _id;

            public AvailableEvaluator(string displayName, IInteractiveEvaluatorProvider provider, string id) {
                DisplayName = displayName;
                _provider = provider;
                _id = id;
            }

            public IInteractiveEvaluator Create() {
                return _provider.GetEvaluator(_id);
            }

            public override bool Equals(object obj) {
                if (!(obj is AvailableEvaluator)) {
                    return false;
                }
                return _id == ((AvailableEvaluator)obj)._id;
            }

            public override int GetHashCode() {
                return _id.GetHashCode();
            }
        }
    }
}
