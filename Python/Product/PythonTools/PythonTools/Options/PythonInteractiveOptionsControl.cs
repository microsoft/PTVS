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
using System.Linq;
using System.Windows.Forms;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Options {
    public partial class PythonInteractiveOptionsControl : UserControl {
        internal const string PythonExecutionModeKey = PythonCoreConstants.BaseRegistryKey + "\\ReplExecutionModes";
        private readonly ExecutionMode[] _executionModes;
        private readonly IServiceProvider _serviceProvider;

        [Obsolete("An IServiceProvider should be provided")]
        public PythonInteractiveOptionsControl()
            : this(PythonToolsPackage.Instance) {
        }

        public PythonInteractiveOptionsControl(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            InitializeComponent();

            _executionModes = ExecutionMode.GetRegisteredModes(_serviceProvider.GetComponentModel().GetService<SVsServiceProvider>());

            foreach (var mode in _executionModes) {
                // TODO: Support localizing these names...
                _executionMode.Items.Add(mode.FriendlyName);
            }

#if DEV14_OR_LATER
            // This isn't supported in the Dev14 interactive window
            _inlinePrompts.Visible = false;
#endif
            UpdateInterpreters();

            AddToolTips();
        }

        internal void UpdateInterpreters() {
            if (InvokeRequired) {
                Invoke((Action)(() => UpdateInterpreters()));
                return;
            }

            var previousSelection = _showSettingsFor.SelectedItem;
            object currentSelection = null;

            _showSettingsFor.BeginUpdate();
            try {
                _showSettingsFor.Items.Clear();

                var interpreters = _serviceProvider.GetPythonToolsService()
                    .InterpreterOptions
                    .Select(x => x.Key)
                    .Where(f => f.IsUIVisible() && f.CanBeConfigured())
                    .OrderBy(f => f.Description);

                foreach (var factory in interpreters) {
                    _showSettingsFor.Items.Add(factory);
                    if (factory == previousSelection) {
                        currentSelection = factory;
                    }
                }

                if (currentSelection != null) {
                    _showSettingsFor.SelectedItem = currentSelection;
                } else if (_showSettingsFor.Items.Count > 0) {
                    _showSettingsFor.SelectedIndex = 0;
                }
            } finally {
                _showSettingsFor.EndUpdate();
            }

            if (_showSettingsFor.Items.Count > 0) {
                Enabled = true;
                RefreshOptions();
            } else {
                _showSettingsFor.Items.Add("No Python Environments Installed");
                _showSettingsFor.SelectedIndex = 0;
                Enabled = false;
            }
        }

        protected override void OnVisibleChanged(EventArgs e) {
            base.OnVisibleChanged(e);

            if (Visible) {
                var selectInterpreter = PythonInteractiveOptionsPage.NextOptionsSelection ??
                    _serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>().DefaultInterpreter;
                PythonInteractiveOptionsPage.NextOptionsSelection = null;

                _showSettingsFor.SelectedItem = selectInterpreter;
            }
        }

        private void AddToolTips() {
            const string inlinePromptsToolTip = "When checked the prompts are in the editor buffer.  When unchecked the prompts are on the side in a separate margin.";
            const string useInterpreterPromptsToolTip = "When checked the prompts are the ones configured here.  When unchecked the prompt strings are defined by sys.ps1 and sys.ps2.";
            const string backendToolTop = @"Specifies the mode to be used for the interactive window.

The standard mode talks to a local Python process.
The IPython mode talks to an IPython kernel which can control multiple remote machines.

You can also specify a custom type as modulename.typename.  The module will 
be imported and the custom backend will be used.
";
            const string interpreterOptionsToolTip = @"Specifies command line options for the interactive window such as -O or -B.  

When launching a project in the interactive window these are combined with any interpreter options listed in the project.";

            const string smartReplHistoryToolTip = "Causes the up/down arrow keys to navigate history when the cursor is at the end of an input.";
            const string enableAttachToolTip = @"Enable attaching to the Visual Studio debugger to the REPL process.  Attaching can be performed by entering:

import visualstudio_py_repl
visualstudio_py_repl.BACKEND.attach()";

            const string liveCompletionsToolTip = @"When offering completions don't use values that have come from analysis of the REPL buffer.  Instead, only use values from live objects.";


            _tooltips.SetToolTip(_inlinePrompts, inlinePromptsToolTip);
            _tooltips.SetToolTip(_useUserDefinedPrompts, useInterpreterPromptsToolTip);
            _tooltips.SetToolTip(_executionMode, backendToolTop);
            _tooltips.SetToolTip(_executionModeLabel, backendToolTop);
            _tooltips.SetToolTip(_interpOptionsLabel, interpreterOptionsToolTip);
            _tooltips.SetToolTip(_interpreterOptions, interpreterOptionsToolTip);
            _tooltips.SetToolTip(_smartReplHistory, smartReplHistoryToolTip);
            _tooltips.SetToolTip(_enableAttach, enableAttachToolTip);
            _tooltips.SetToolTip(_liveCompletionsOnly, liveCompletionsToolTip);
        }

        private void RefreshOptions() {
            var factory = _showSettingsFor.SelectedItem as IPythonInterpreterFactory;
            if (factory != null) {
                CurrentOptions = _serviceProvider.GetPythonToolsService().GetInteractiveOptions(factory);
            } else {
                CurrentOptions = null;
            }

            if (CurrentOptions != null) {
                _smartReplHistory.Checked = CurrentOptions.ReplSmartHistory;

                ReplIntellisenseMode = CurrentOptions.ReplIntellisenseMode;

                _inlinePrompts.Checked = CurrentOptions.InlinePrompts;
                _useUserDefinedPrompts.Checked = !CurrentOptions.UseInterpreterPrompts;
                _priPrompt.Text = CurrentOptions.PrimaryPrompt;
                _secPrompt.Text = CurrentOptions.SecondaryPrompt;
                _startupScript.Text = CurrentOptions.StartupScript;
                _interpreterOptions.Text = CurrentOptions.InterpreterOptions;
                _priPromptLabel.Enabled = _secPromptLabel.Enabled = _secPrompt.Enabled = _priPrompt.Enabled = _useUserDefinedPrompts.Checked;
                _enableAttach.Checked = CurrentOptions.EnableAttach;
                _liveCompletionsOnly.Checked = CurrentOptions.LiveCompletionsOnly;

                int selectedExecutionMode = -1;
                for (int i = 0; i < _executionModes.Length; i++) {
                    var mode = _executionModes[i];
                    if (_showSettingsFor.SelectedIndex != -1) {
                        if (CurrentOptions.ExecutionMode == mode.Id ||
                            (String.IsNullOrWhiteSpace(CurrentOptions.ExecutionMode) && mode.Id == ExecutionMode.StandardModeId)) {

                            selectedExecutionMode = i;
                        }
                    }
                }

                if (selectedExecutionMode != -1) {
                    _executionMode.SelectedIndex = selectedExecutionMode;
                } else if (CurrentOptions.ExecutionMode != null) {
                    _executionMode.Text = CurrentOptions.ExecutionMode;
                }
            }
        }

        internal PythonInteractiveOptions CurrentOptions { get; private set; }

        internal ReplIntellisenseMode ReplIntellisenseMode {
            get {
                if (_evalNever.Checked) {
                    return ReplIntellisenseMode.NeverEvaluate;
                } else if (_evalNoCalls.Checked) {
                    return ReplIntellisenseMode.DontEvaluateCalls;
                } else if (_evalAlways.Checked) {
                    return ReplIntellisenseMode.AlwaysEvaluate;
                } else {
                    return ReplIntellisenseMode.NeverEvaluate;
                }
            }
            set {
                switch (value) {
                    case ReplIntellisenseMode.AlwaysEvaluate: 
                        _evalAlways.Checked = true; 
                        break;
                    case ReplIntellisenseMode.DontEvaluateCalls: 
                        _evalNoCalls.Checked = true; 
                        break;
                    case ReplIntellisenseMode.NeverEvaluate: 
                        _evalNever.Checked = true; 
                        break;
                }
            }
        }

        private void _smartReplHistory_CheckedChanged(object sender, EventArgs e) {
            CurrentOptions.ReplSmartHistory = _smartReplHistory.Checked;
        }

        private void _useInterpreterPrompts_CheckedChanged(object sender, EventArgs e) {
            _priPromptLabel.Enabled = _secPromptLabel.Enabled = _secPrompt.Enabled = _priPrompt.Enabled = _useUserDefinedPrompts.Checked;
            CurrentOptions.UseInterpreterPrompts = !_useUserDefinedPrompts.Checked;
        }

        private void _inlinePrompts_CheckedChanged(object sender, EventArgs e) {
            CurrentOptions.InlinePrompts = _inlinePrompts.Checked;
        }

        private void _priPrompt_TextChanged(object sender, EventArgs e) {
            CurrentOptions.PrimaryPrompt = _priPrompt.Text;
        }

        private void _secPrompt_TextChanged(object sender, EventArgs e) {
            CurrentOptions.SecondaryPrompt = _secPrompt.Text;
        }

        private void _showSettingsFor_SelectedIndexChanged(object sender, EventArgs e) {
            RefreshOptions();
        }

        private void _startupScriptButton_Click(object sender, EventArgs e) {
            var dialog = new OpenFileDialog();
            dialog.CheckFileExists = true;
            if (dialog.ShowDialog() == DialogResult.OK) {
                _startupScript.Text = dialog.FileName;
            }
        }

        private void _startupScript_TextChanged(object sender, EventArgs e) {
            CurrentOptions.StartupScript = _startupScript.Text;
        }

        private void _executionMode_SelectedIndexChanged(object sender, EventArgs e) {
            if (_executionMode.SelectedIndex != -1) {
                var mode = _executionModes[_executionMode.SelectedIndex];
                CurrentOptions.ExecutionMode = mode.Id;
            }
        }

        private void _executionMode_TextChanged(object sender, EventArgs e) {
            CurrentOptions.ExecutionMode = _executionMode.Text;
        }

        private void InterpreterOptionsTextChanged(object sender, EventArgs e) {
            CurrentOptions.InterpreterOptions = _interpreterOptions.Text;
        }

        private void EnableAttachCheckedChanged(object sender, EventArgs e) {
            CurrentOptions.EnableAttach = _enableAttach.Checked;
        }

        private void _liveCompletionsOnly_CheckedChanged(object sender, EventArgs e) {
            CurrentOptions.LiveCompletionsOnly = _liveCompletionsOnly.Checked;
        }

        private void Interpreter_Format(object sender, ListControlConvertEventArgs e) {
            var factory = e.ListItem as IPythonInterpreterFactory;
            if (factory != null) {
                e.Value = factory.Description;
            } else {
                e.Value = e.ListItem.ToString();
            }
        }
    }
}
