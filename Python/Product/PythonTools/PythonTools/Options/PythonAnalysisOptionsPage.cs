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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Microsoft.PythonTools.Options {
    [ComVisible(true)]
    public class PythonAnalysisOptionsPage : PythonDialogPage {
        private PythonAnalysisOptionsControl _window;

        // replace the default UI of the dialog page w/ our own UI.
        protected override IWin32Window Window {
            get {
                if (_window == null) {
                    _window = new PythonAnalysisOptionsControl();
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
        public override void ResetSettings() => PyService.AnalysisOptions.Reset();

        public override void LoadSettingsFromStorage() {
            PyService.AnalysisOptions.Load();
            // Synchronize UI with backing properties.
            _window?.SyncControlWithPageSettings(PyService);
        }

        public override void SaveSettingsToStorage() {
            // Synchronize backing properties with UI.
            _window?.SyncPageWithControlSettings(PyService);
            PyService.AnalysisOptions.Save();
        }
    }
}
