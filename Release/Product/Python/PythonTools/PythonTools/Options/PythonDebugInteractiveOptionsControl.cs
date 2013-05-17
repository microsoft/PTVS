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
using System.Windows.Forms;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;

namespace Microsoft.PythonTools.Options {
    public partial class PythonDebugInteractiveOptionsControl : UserControl {

        public PythonDebugInteractiveOptionsControl() {
            InitializeComponent();

            AddToolTips();
            RefreshOptions();
        }

        private void EnableOrDisableOptions(bool enable) {
            _priPrompt.Enabled = _priPromptLabel.Enabled = _secPrompt.Enabled = _secPromptLabel.Enabled = _useUserDefinedPrompts.Enabled = _smartReplHistory.Enabled = _inlinePrompts.Enabled = enable;
        }

        private void AddToolTips() {
            const string inlinePromptsToolTip = "When checked the prompts are in the editor buffer.  When unchecked the prompts are on the side in a separate margin.";
            const string useInterpreterPromptsToolTip = "When checked the prompts are the ones configured here.  When unchecked the prompt strings are defined by sys.ps1 and sys.ps2.";
            const string smartReplHistoryToolTip = "Causes the up/down arrow keys to navigate history when the cursor is at the end of an input.";
            const string liveCompletionsToolTip = @"When offering completions don't use values that have come from analysis of the REPL buffer.  Instead, only use values from live objects.";

            _tooltips.SetToolTip(_inlinePrompts, inlinePromptsToolTip);
            _tooltips.SetToolTip(_useUserDefinedPrompts, useInterpreterPromptsToolTip);
            _tooltips.SetToolTip(_smartReplHistory, smartReplHistoryToolTip);
            _tooltips.SetToolTip(_liveCompletionsOnly, liveCompletionsToolTip);
        }

        private void RefreshOptions() {
            _smartReplHistory.Checked = CurrentOptions.ReplSmartHistory;

            switch (CurrentOptions.ReplIntellisenseMode) {
                case ReplIntellisenseMode.AlwaysEvaluate: _evalAlways.Checked = true; break;
                case ReplIntellisenseMode.DontEvaluateCalls: _evalNoCalls.Checked = true; break;
                case ReplIntellisenseMode.NeverEvaluate: _evalNever.Checked = true; break;
            }

            _inlinePrompts.Checked = CurrentOptions.InlinePrompts;
            _useUserDefinedPrompts.Checked = !CurrentOptions.UseInterpreterPrompts;
            _priPrompt.Text = CurrentOptions.PrimaryPrompt;
            _secPrompt.Text = CurrentOptions.SecondaryPrompt;
            _priPromptLabel.Enabled = _secPromptLabel.Enabled = _secPrompt.Enabled = _priPrompt.Enabled = _useUserDefinedPrompts.Checked;
            _liveCompletionsOnly.Checked = CurrentOptions.LiveCompletionsOnly;
        }

        private PythonDebugInteractiveOptionsPage OptionsPage {
            get {
                return PythonToolsPackage.Instance.InteractiveDebugOptionsPage;
            }
        }

        private PythonInteractiveCommonOptions CurrentOptions {
            get {
                return OptionsPage.Options;
            }
        }

        private void _smartReplHistory_CheckedChanged(object sender, EventArgs e) {
            CurrentOptions.ReplSmartHistory = _smartReplHistory.Checked;
        }

        private void _evalNever_CheckedChanged(object sender, EventArgs e) {
            if (_evalNever.Checked) {
                CurrentOptions.ReplIntellisenseMode = ReplIntellisenseMode.NeverEvaluate;
            }
        }

        private void _evalNoCalls_CheckedChanged(object sender, EventArgs e) {
            if (_evalNoCalls.Checked) {
                CurrentOptions.ReplIntellisenseMode = ReplIntellisenseMode.DontEvaluateCalls;
            }
        }

        private void _evalAlways_CheckedChanged(object sender, EventArgs e) {
            if (_evalAlways.Checked) {
                CurrentOptions.ReplIntellisenseMode = ReplIntellisenseMode.AlwaysEvaluate;
            }
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

        private void _liveCompletionsOnly_CheckedChanged(object sender, EventArgs e) {
            CurrentOptions.LiveCompletionsOnly = _liveCompletionsOnly.Checked;
        }
    }
}
