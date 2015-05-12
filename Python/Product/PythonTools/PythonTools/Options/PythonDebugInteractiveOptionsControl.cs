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

#if DEV14_OR_LATER
            _inlinePrompts.Visible = false;
#endif
        }

        internal void EnableUserDefinedPrompts(bool enable) {
            _priPromptLabel.Enabled = _secPromptLabel.Enabled = _secPrompt.Enabled = _priPrompt.Enabled = _useUserDefinedPrompts.Checked = enable;
        }

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
                    case ReplIntellisenseMode.AlwaysEvaluate: _evalAlways.Checked = true; break;
                    case ReplIntellisenseMode.DontEvaluateCalls: _evalNoCalls.Checked = true; break;
                    case ReplIntellisenseMode.NeverEvaluate: _evalNever.Checked = true; break;
                }
            }
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

        /// <summary>
        /// Disable/Enable other buttons related to interpreter prompts. 
        /// </summary>
        private void _useInterpreterPrompts_CheckedChanged(object sender, EventArgs e) {
            _priPromptLabel.Enabled = _secPromptLabel.Enabled = _secPrompt.Enabled = _priPrompt.Enabled = _useUserDefinedPrompts.Checked;
        }

        internal void SyncControlWithPageSettings(PythonToolsService pyService) {
            _smartReplHistory.Checked = pyService.DebugInteractiveOptions.ReplSmartHistory;
            ReplIntellisenseMode = pyService.DebugInteractiveOptions.ReplIntellisenseMode;
            _inlinePrompts.Checked = pyService.DebugInteractiveOptions.InlinePrompts;
            _priPrompt.Text = pyService.DebugInteractiveOptions.PrimaryPrompt;
            _secPrompt.Text = pyService.DebugInteractiveOptions.SecondaryPrompt;
            EnableUserDefinedPrompts(!pyService.DebugInteractiveOptions.UseInterpreterPrompts);
            _liveCompletionsOnly.Checked = pyService.DebugInteractiveOptions.LiveCompletionsOnly;
        }

        internal void SyncPageWithControlSettings(PythonToolsService pyService) {
            pyService.DebugInteractiveOptions.ReplSmartHistory = _smartReplHistory.Checked;
            pyService.DebugInteractiveOptions.InlinePrompts = _inlinePrompts.Checked;
            pyService.DebugInteractiveOptions.PrimaryPrompt = _priPrompt.Text;
            pyService.DebugInteractiveOptions.SecondaryPrompt = _secPrompt.Text;
            pyService.DebugInteractiveOptions.LiveCompletionsOnly = _liveCompletionsOnly.Checked;
            pyService.DebugInteractiveOptions.UseInterpreterPrompts = !_useUserDefinedPrompts.Checked;
        }
    }
}
