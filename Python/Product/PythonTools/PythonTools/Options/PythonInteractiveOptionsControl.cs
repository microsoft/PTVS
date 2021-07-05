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

namespace Microsoft.PythonTools.Options {
    public partial class PythonInteractiveOptionsControl : UserControl {
        private readonly IServiceProvider _serviceProvider;
        private readonly PythonInteractiveOptions _options;
        private bool _changing;

        private PythonInteractiveOptionsControl() : this(null) {
        }

        public PythonInteractiveOptionsControl(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _options = _serviceProvider?.GetPythonToolsService().InteractiveOptions;
            InitializeComponent();
            UpdateSettings();
        }

        internal async void UpdateSettings() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _changing = true;
            try {
                scriptsTextBox.Text = _options.Scripts;
                useSmartHistoryCheckBox.Checked = _options.UseSmartHistory;
                neverEvaluateButton.Checked = _options.CompletionMode == Repl.ReplIntellisenseMode.NeverEvaluate;
                evaluateNoCallsButton.Checked = _options.CompletionMode == Repl.ReplIntellisenseMode.DontEvaluateCalls;
                alwaysEvaluateButton.Checked = _options.CompletionMode == Repl.ReplIntellisenseMode.AlwaysEvaluate;
                liveCompletionsOnlyCheckBox.Checked = _options.LiveCompletionsOnly;
            } finally {
                _changing = false;
            }
        }

        private void Scripts_TextChanged(object sender, EventArgs e) {
            if (!_changing) {
                _options.Scripts = ((TextBox)sender).Text;
            }
        }

        private void UseSmartHistory_CheckedChanged(object sender, EventArgs e) {
            if (!_changing) {
                _options.UseSmartHistory = ((CheckBox)sender).Checked;
            }
        }

        private void CompletionMode_CheckedChanged(object sender, EventArgs e) {
            if (!_changing) {
                _options.CompletionMode =
                    neverEvaluateButton.Checked ? Repl.ReplIntellisenseMode.NeverEvaluate :
                    evaluateNoCallsButton.Checked ? Repl.ReplIntellisenseMode.DontEvaluateCalls :
                    Repl.ReplIntellisenseMode.AlwaysEvaluate;
            }
        }

        private void LiveCompletionsOnly_CheckedChanged(object sender, EventArgs e) {
            if (!_changing) {
                _options.LiveCompletionsOnly = ((CheckBox)sender).Checked;
            }
        }

        private void browseScriptsButton_Click(object sender, EventArgs e) {
            var newPath = _serviceProvider.BrowseForDirectory(Handle, _options.Scripts);
            if (!string.IsNullOrEmpty(newPath)) {
                scriptsTextBox.Text = newPath;
            }
        }
    }
}
