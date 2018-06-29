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

using System.Runtime.InteropServices;
using EO = Microsoft.PythonTools.Interpreter.ExperimentalOptions;

namespace Microsoft.PythonTools.Options {
    [ComVisible(true)]
    public class PythonExperimentalOptionsPage : PythonDialogPage {
        private PythonExperimentalOptionsControl _window;

        public PythonExperimentalOptionsPage() {
            EO.UseVsCodeDebuggerChanged += OnUseVsCodeDebuggerChanged;
        }

        private void OnUseVsCodeDebuggerChanged(object sender, System.EventArgs e) {
            // Synchronize UI with backing properties.
            _window?.SyncControlWithPageSettings(PyService);
        }

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                if (_window == null) {
                    _window = new PythonExperimentalOptionsControl();
                    LoadSettingsFromStorage();
                }
                return _window;
            }
        }

        /// <summary>
        /// Resets settings back to their defaults. This should be followed by
        /// a call to <see cref="SaveSettingsToStorage"/> to commit the new
        /// values.
        /// </summary>
        public override void ResetSettings() {
            PyService.ExperimentalOptions.Reset();
        }

        public override void LoadSettingsFromStorage() {
            PyService.ExperimentalOptions.Load();

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

            PyService.ExperimentalOptions.Save();
        }
    }
}
