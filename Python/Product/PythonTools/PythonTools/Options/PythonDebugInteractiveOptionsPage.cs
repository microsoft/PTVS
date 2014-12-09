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
        private PythonDebugInteractiveOptionsControl _window;

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
                return PyService.DebugInteractiveOptions;
            }
        }

        public override void ResetSettings() {
            PyService.DebugInteractiveOptions.Reset();
        }

        public override void LoadSettingsFromStorage() {
            // Load settings from storage.
            PyService.DebugInteractiveOptions.Load();

            // Synchronize UI with backing properties.
            if (_window != null) {
                _window.SyncControlWithPageSettings(PyService);
            }
        }

        public override void SaveSettingsToStorage() {
            // Synchronize backing properties with UI.
            if (_window != null) {
                _window.SyncPageWithControlSettings(PyService);
            }
            
            // Save settings.
            PyService.DebugInteractiveOptions.Save();

            // propagate changed settings to existing REPL windows
            var model = ComponentModel;
            var replProvider = model.GetService<IReplWindowProvider>();
            
            foreach (var replWindow in replProvider.GetReplWindows()) {
                PythonDebugReplEvaluator pyEval = replWindow.Evaluator as PythonDebugReplEvaluator;
                if (pyEval != null){
                    if (Options.UseInterpreterPrompts) {
                        replWindow.SetOptionValue(ReplOptions.PrimaryPrompt, pyEval.PrimaryPrompt);
                        replWindow.SetOptionValue(ReplOptions.SecondaryPrompt, pyEval.SecondaryPrompt);
                    } else {
                        replWindow.SetOptionValue(ReplOptions.PrimaryPrompt, Options.PrimaryPrompt);
                        replWindow.SetOptionValue(ReplOptions.SecondaryPrompt, Options.SecondaryPrompt);
                    }
                    replWindow.SetOptionValue(ReplOptions.DisplayPromptInMargin, !Options.InlinePrompts);
                    replWindow.SetOptionValue(ReplOptions.UseSmartUpDown, Options.ReplSmartHistory);
                }
            }
        }
    }
}
