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

using System.Collections.Generic;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Repl;

namespace Microsoft.PythonTools.Options {
#if INTERACTIVE_WINDOW
    using IReplWindowProvider = IInteractiveWindowProvider;
#endif

    class PythonDebugInteractiveOptionsPage : PythonDialogPage {
        internal PythonInteractiveCommonOptions _options = new PythonInteractiveCommonOptions();

        private PythonDebugInteractiveOptionsControl _window;

        private const string DefaultPrompt = ">>> ";
        private const string DefaultSecondaryPrompt = "... ";

        private const string PrimaryPromptSetting = "PrimaryPrompt";
        private const string SecondaryPromptSetting = "SecondaryPrompt";
        private const string InlinePromptsSetting = "InlinePrompts";
        private const string UseInterpreterPromptsSetting = "UseInterpreterPrompts";
        private const string ReplIntellisenseModeSetting = "InteractiveIntellisenseMode";
        private const string SmartHistorySetting = "InteractiveSmartHistory";
        private const string LiveCompletionsOnlySetting = "LiveCompletionsOnly";

        public PythonDebugInteractiveOptionsPage()
            : base("Debug Interactive Window") {
        }

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                if (_window == null) {
                    _window = new PythonDebugInteractiveOptionsControl();
                    LoadSettingsFromStorage();
                }
                return _window;
            }
        }

        internal PythonInteractiveCommonOptions Options {
            get {
                return _options;
            }
        }

        public override void ResetSettings() {
            _options.PrimaryPrompt = DefaultPrompt;
            _options.SecondaryPrompt = DefaultSecondaryPrompt;
            _options.InlinePrompts = true;
            _options.UseInterpreterPrompts = true;
            _options.ReplIntellisenseMode = ReplIntellisenseMode.DontEvaluateCalls;
            _options.ReplSmartHistory = true;
            _options.LiveCompletionsOnly = false;
        }

        public override void LoadSettingsFromStorage() {
            // Load settings from storage.
            _options.PrimaryPrompt = LoadString(PrimaryPromptSetting) ?? DefaultPrompt;
            _options.SecondaryPrompt = LoadString(SecondaryPromptSetting) ?? DefaultSecondaryPrompt;
            _options.InlinePrompts = LoadBool(InlinePromptsSetting) ?? true;
            _options.UseInterpreterPrompts = LoadBool(UseInterpreterPromptsSetting) ?? true;
            _options.ReplIntellisenseMode = LoadEnum<ReplIntellisenseMode>(ReplIntellisenseModeSetting) ?? ReplIntellisenseMode.DontEvaluateCalls;
            _options.ReplSmartHistory = LoadBool(SmartHistorySetting) ?? true;
            _options.LiveCompletionsOnly = LoadBool(LiveCompletionsOnlySetting) ?? false;

            // Synchronize UI with backing properties.
            if (_window != null) {
                _window.SyncControlWithPageSettings(this);
            }
        }

        public override void SaveSettingsToStorage() {
            // Synchronize backing properties with UI.
            if (_window != null) {
                _window.SyncPageWithControlSettings(this);
            }
            
            // Save settings.
            SaveString(PrimaryPromptSetting, _options.PrimaryPrompt);
            SaveString(SecondaryPromptSetting, _options.SecondaryPrompt);
            SaveBool(InlinePromptsSetting, _options.InlinePrompts);
            SaveBool(UseInterpreterPromptsSetting, _options.UseInterpreterPrompts);
            SaveEnum<ReplIntellisenseMode>(ReplIntellisenseModeSetting, _options.ReplIntellisenseMode);
            SaveBool(SmartHistorySetting, _options.ReplSmartHistory);
            SaveBool(LiveCompletionsOnlySetting, _options.LiveCompletionsOnly);

            // propagate changed settings to existing REPL windows
            var model = (IComponentModel)PythonToolsPackage.GetGlobalService(typeof(SComponentModel));
            var replProvider = model.GetService<IReplWindowProvider>();
            
            foreach (var replWindow in replProvider.GetReplWindows()) {
                PythonDebugReplEvaluator pyEval = replWindow.Evaluator as PythonDebugReplEvaluator;
                if (pyEval != null){
                    if (_options.UseInterpreterPrompts) {
                        replWindow.SetOptionValue(ReplOptions.PrimaryPrompt, pyEval.PrimaryPrompt);
                        replWindow.SetOptionValue(ReplOptions.SecondaryPrompt, pyEval.SecondaryPrompt);
                    } else {
                        replWindow.SetOptionValue(ReplOptions.PrimaryPrompt, _options.PrimaryPrompt);
                        replWindow.SetOptionValue(ReplOptions.SecondaryPrompt, _options.SecondaryPrompt);
                    }
                    replWindow.SetOptionValue(ReplOptions.DisplayPromptInMargin, !_options.InlinePrompts);
                    replWindow.SetOptionValue(ReplOptions.UseSmartUpDown, _options.ReplSmartHistory);
                }
            }
        }
    }
}
