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
    class PythonInteractiveOptionsPage : PythonDialogPage {
        internal PythonInteractiveOptionsControl _window;
        internal readonly Dictionary<IPythonInterpreterFactory, PythonInteractiveOptions> _options = new Dictionary<IPythonInterpreterFactory, PythonInteractiveOptions>();

        private const string DefaultPrompt = ">>> ";
        private const string DefaultSecondaryPrompt = "... ";

        private const string EnterOutlingModeOnOpenSetting = "EnterOutlingModeOnOpen";
        private const string FillParagraphColumnsSetting = "FillParagraphColumns";
        private const string PrimaryPromptSetting = "PrimaryPrompt";
        private const string SecondaryPromptSetting = "SecondaryPrompt";
        private const string InlinePromptsSetting = "InlinePrompts";
        private const string UseInterpreterPromptsSetting = "UseInterpreterPrompts";
        private const string ReplIntellisenseModeSetting = "InteractiveIntellisenseMode";
        private const string SmartHistorySetting = "InteractiveSmartHistory";
        private const string StartupScriptSetting = "StartupScript";
        private const string ExecutionModeSetting = "ExecutionMode";

        public PythonInteractiveOptionsPage()
            : base("Interactive Windows") {
        }

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                if (_window == null) {
                    _window = new PythonInteractiveOptionsControl();
                }
                return _window;
            }
        }

        public PythonInteractiveOptions GetOptions(IPythonInterpreterFactory interpreterFactory) {
            PythonInteractiveOptions options;
            if (!_options.TryGetValue(interpreterFactory, out options)) {
                _options[interpreterFactory] = options = ReadOptions(interpreterFactory.GetInterpreterPath() + "\\");
            }
            return options;
        }

        public override void ResetSettings() {
            foreach (var options in _options.Values) {
                options.PrimaryPrompt = DefaultPrompt;
                options.SecondaryPrompt = DefaultSecondaryPrompt;
                options.InlinePrompts = true;
                options.UseInterpreterPrompts = true;
                options.ReplIntellisenseMode = ReplIntellisenseMode.DontEvaluateCalls;
                options.ReplSmartHistory = true;
                options.StartupScript = "";
                options.ExecutionMode = "";
            }
        }

        public override void LoadSettingsFromStorage() {
            var model = (IComponentModel)PythonToolsPackage.GetGlobalService(typeof(SComponentModel));
            var interpreters = model.GetAllPythonInterpreterFactories();
            _options.Clear();
            foreach (var interpreter in interpreters) {
                string interpreterId = interpreter.GetInterpreterPath() + "\\";

                _options[interpreter] =
                    ReadOptions(interpreterId);

            }
        }

        private PythonInteractiveOptions ReadOptions(string interpreterId) {
            return new PythonInteractiveOptions() {
                PrimaryPrompt = LoadString(interpreterId + PrimaryPromptSetting) ?? DefaultPrompt,
                SecondaryPrompt = LoadString(interpreterId + SecondaryPromptSetting) ?? DefaultSecondaryPrompt,
                InlinePrompts = LoadBool(interpreterId + InlinePromptsSetting) ?? true,
                UseInterpreterPrompts = LoadBool(interpreterId + UseInterpreterPromptsSetting) ?? true,
                ReplIntellisenseMode = LoadEnum<ReplIntellisenseMode>(interpreterId + ReplIntellisenseModeSetting) ?? ReplIntellisenseMode.DontEvaluateCalls,
                ReplSmartHistory = LoadBool(interpreterId + SmartHistorySetting) ?? true,
                StartupScript = LoadString(interpreterId + StartupScriptSetting) ?? "",
                ExecutionMode = LoadString(interpreterId + ExecutionModeSetting) ?? ""
            };
        }

        public override void SaveSettingsToStorage() {
            var model = (IComponentModel)PythonToolsPackage.GetGlobalService(typeof(SComponentModel));
            var interpreters = model.GetAllPythonInterpreterFactories();
            var replProvider = model.GetService<IReplWindowProvider>();

            foreach (var interpreter in interpreters) {
                string interpreterId = interpreter.GetInterpreterPath() + "\\";

                PythonInteractiveOptions options;
                if (_options.TryGetValue(interpreter, out options)) {
                    SaveString(interpreterId + PrimaryPromptSetting, options.PrimaryPrompt);
                    SaveString(interpreterId + SecondaryPromptSetting, options.SecondaryPrompt);
                    SaveBool(interpreterId + InlinePromptsSetting, options.InlinePrompts);
                    SaveBool(interpreterId + UseInterpreterPromptsSetting, options.UseInterpreterPrompts);
                    SaveEnum<ReplIntellisenseMode>(interpreterId + ReplIntellisenseModeSetting, options.ReplIntellisenseMode);
                    SaveBool(interpreterId + SmartHistorySetting, options.ReplSmartHistory);
                    SaveString(interpreterId + StartupScriptSetting, options.StartupScript);
                    SaveString(interpreterId + ExecutionModeSetting, options.ExecutionMode ?? "");

                    // propagate changed settings to existing REPL windows
                    foreach (var replWindow in replProvider.GetReplWindows()) {
                        PythonReplEvaluator pyEval = replWindow.Evaluator as PythonReplEvaluator;
                        if (EvaluatorUsesThisInterpreter(pyEval, interpreter)) {
                            if (options.UseInterpreterPrompts) {
                                replWindow.SetOptionValue(ReplOptions.PrimaryPrompt, pyEval.PrimaryPrompt);
                                replWindow.SetOptionValue(ReplOptions.SecondaryPrompt, pyEval.SecondaryPrompt);
                            } else {
                                replWindow.SetOptionValue(ReplOptions.PrimaryPrompt, options.PrimaryPrompt);
                                replWindow.SetOptionValue(ReplOptions.SecondaryPrompt, options.SecondaryPrompt);
                            }
                            replWindow.SetOptionValue(ReplOptions.DisplayPromptInMargin, !options.InlinePrompts);
                            replWindow.SetOptionValue(ReplOptions.UseSmartUpDown, options.ReplSmartHistory);
                        }
                    }
                }
            }
        }

        private static bool EvaluatorUsesThisInterpreter(PythonReplEvaluator pyEval, IPythonInterpreterFactory interpreter) {
            return pyEval != null && pyEval.Interpreter.Id == interpreter.Id && pyEval.Interpreter.Configuration.Version == interpreter.Configuration.Version;
        }
    }
}
