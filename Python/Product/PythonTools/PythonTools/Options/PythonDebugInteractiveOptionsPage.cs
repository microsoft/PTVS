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

using Microsoft.PythonTools.Repl;
#if DEV14_OR_LATER
using IReplWindowProvider = Microsoft.PythonTools.Repl.InteractiveWindowProvider;
#else
using Microsoft.VisualStudio.Repl;
#endif

namespace Microsoft.PythonTools.Options {
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
                        replWindow.SetPrompts(pyEval.PrimaryPrompt, pyEval.SecondaryPrompt);
                    } else {
                        replWindow.SetPrompts(Options.PrimaryPrompt, Options.SecondaryPrompt);
                    }
#if !DEV14_OR_LATER
                    replWindow.SetOptionValue(ReplOptions.DisplayPromptInMargin, !Options.InlinePrompts);
#endif
                    replWindow.SetSmartUpDown(Options.ReplSmartHistory);
                }
            }
        }
    }
}
