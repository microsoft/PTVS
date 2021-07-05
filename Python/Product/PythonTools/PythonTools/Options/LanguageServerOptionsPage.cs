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

namespace Microsoft.PythonTools.Options {
    class LanguageServerOptionsPage : PythonDialogPage {
        private LanguageServerOptionsControl _window;

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                if (_window == null) {
                    _window = new LanguageServerOptionsControl(Site);
                }
                return _window;
            }
        }

        public override void ResetSettings() {
            var opts = PyService.LanguageServerOptions;
            opts.Reset();
            _window?.UpdateSettings();
        }

        public override void LoadSettingsFromStorage() {
            var opts = PyService.LanguageServerOptions;
            opts.Load();
            _window?.UpdateSettings();
        }

        public override void SaveSettingsToStorage() {
            var opts = PyService.LanguageServerOptions;
            opts.Save();
            _window?.UpdateSettings();
        }
    }
}
