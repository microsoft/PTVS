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

using Microsoft.PythonTools.Repl;

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
            Options.Reset();
        }

        public override void LoadSettingsFromStorage() {
            // Load settings from storage.
            Options.Load();

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
            Options.Save();

            // propagate changed settings to existing REPL windows
            var model = ComponentModel;
            if (model == null) {
                // Might be shutting down?
                return;
            }

            var replProvider = model.GetService<InteractiveWindowProvider>();
            
            foreach (var replWindow in replProvider.GetReplWindows()) {
                PythonDebugReplEvaluator pyEval = replWindow.Evaluator as PythonDebugReplEvaluator;
                if (pyEval != null) {
                    // TODO: Update REPL prompts
                    replWindow.SetSmartUpDown(Options.ReplSmartHistory);
                }
            }
        }
    }
}
