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
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;

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

        //        private void AddToolTips() {
        //            const string useInterpreterPromptsToolTip = "When checked the prompts are the ones configured here.  When unchecked the prompt strings are defined by sys.ps1 and sys.ps2.";
        //            const string backendToolTop = @"Specifies the mode to be used for the interactive window.

        //The standard mode talks to a local Python process.
        //The IPython mode talks to an IPython kernel which can control multiple remote machines.

        //You can also specify a custom type as modulename.typename.  The module will 
        //be imported and the custom backend will be used.
        //";
        //            const string interpreterOptionsToolTip = @"Specifies command line options for the interactive window such as -O or -B.  

        //When launching a project in the interactive window these are combined with any interpreter options listed in the project.";

        //            const string smartReplHistoryToolTip = "Causes the up/down arrow keys to navigate history when the cursor is at the end of an input.";
        //            const string enableAttachToolTip = @"Enable attaching to the Visual Studio debugger to the REPL process.  Attaching can be performed by entering:

        //import visualstudio_py_repl
        //visualstudio_py_repl.BACKEND.attach()";

        //            const string liveCompletionsToolTip = @"When offering completions don't use values that have come from analysis of the REPL buffer.  Instead, only use values from live objects.";


        //            _tooltips.SetToolTip(_useUserDefinedPrompts, useInterpreterPromptsToolTip);
        //            _tooltips.SetToolTip(_executionMode, backendToolTop);
        //            _tooltips.SetToolTip(_executionModeLabel, backendToolTop);
        //            _tooltips.SetToolTip(_interpOptionsLabel, interpreterOptionsToolTip);
        //            _tooltips.SetToolTip(_interpreterOptions, interpreterOptionsToolTip);
        //            _tooltips.SetToolTip(_smartReplHistory, smartReplHistoryToolTip);
        //            _tooltips.SetToolTip(_enableAttach, enableAttachToolTip);
        //            _tooltips.SetToolTip(_liveCompletionsOnly, liveCompletionsToolTip);
        //        }

        //private void RefreshOptions() {
        //    var factory = _showSettingsFor.SelectedItem as IPythonInterpreterFactory;
        //    if (factory != null) {
        //        CurrentOptions = _serviceProvider.GetPythonToolsService().GetInteractiveOptions(factory);
        //    } else {
        //        CurrentOptions = null;
        //    }

        //    if (CurrentOptions != null) {
        //        _smartReplHistory.Checked = CurrentOptions.ReplSmartHistory;

        //        ReplIntellisenseMode = CurrentOptions.ReplIntellisenseMode;

        //        _useUserDefinedPrompts.Checked = !CurrentOptions.UseInterpreterPrompts;
        //        _priPrompt.Text = CurrentOptions.PrimaryPrompt;
        //        _secPrompt.Text = CurrentOptions.SecondaryPrompt;
        //        _startupScript.Text = CurrentOptions.StartupScript;
        //        _interpreterOptions.Text = CurrentOptions.InterpreterOptions;
        //        _priPromptLabel.Enabled = _secPromptLabel.Enabled = _secPrompt.Enabled = _priPrompt.Enabled = _useUserDefinedPrompts.Checked;
        //        _enableAttach.Checked = CurrentOptions.EnableAttach;
        //        _liveCompletionsOnly.Checked = CurrentOptions.LiveCompletionsOnly;

        //        int selectedExecutionMode = -1;
        //        for (int i = 0; i < _executionModes.Length; i++) {
        //            var mode = _executionModes[i];
        //            if (_showSettingsFor.SelectedIndex != -1) {
        //                if (CurrentOptions.ExecutionMode == mode.Id ||
        //                    (String.IsNullOrWhiteSpace(CurrentOptions.ExecutionMode) && mode.Id == ExecutionMode.StandardModeId)) {

        //                    selectedExecutionMode = i;
        //                }
        //            }
        //        }

        //        if (selectedExecutionMode != -1) {
        //            _executionMode.SelectedIndex = selectedExecutionMode;
        //        } else if (CurrentOptions.ExecutionMode != null) {
        //            _executionMode.Text = CurrentOptions.ExecutionMode;
        //        }
        //    }
        //}
    }
}
