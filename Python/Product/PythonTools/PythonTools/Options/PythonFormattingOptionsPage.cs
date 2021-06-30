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
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Options {
    [ComVisible(true)]
    public class PythonFormattingOptionsPage : PythonDialogPage {
        private PythonFormattingOptionsControl _window;
        private readonly OptionCategory[] _categories;

        internal PythonFormattingOptionsPage()
            : this(new OptionCategory(
                    Strings.FormattingOptionsClassDeclarations,
                    OptionCategory.GetOptions(CodeFormattingCategory.Classes)
                )) {
        }

        internal PythonFormattingOptionsPage(params OptionCategory[] categories) {
            _categories = categories;
        }

        protected override void OnActivate(System.ComponentModel.CancelEventArgs e) {
            base.OnActivate(e);
            _window.OnActivated();
        }

        protected override void OnDeactivate(System.ComponentModel.CancelEventArgs e) {
            _window.OnDeactivated();
        }

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                EnsureWindow();
                return _window;
            }
        }

        private void EnsureWindow() {
            if (_window == null) {
                _window = new PythonFormattingOptionsControl(Site, _categories);
            }
        }

        public override void ResetSettings() {
            EnsureWindow();

            foreach (var value in _categories) {
                foreach (var option in value.Options) {
                    _window.SetSetting(option.Key, option.DefaultValue);
                }
            }
            base.ResetSettings();
        }

        public override void LoadSettingsFromStorage() {
            EnsureWindow();

            foreach (var value in _categories) {
                foreach (var option in value.Options) {
                    _window.SetSetting(option.Key, PyService.GetFormattingOption(option.Key));
                }
            }
            base.LoadSettingsFromStorage();
        }

        public override void SaveSettingsToStorage() {
            EnsureWindow();

            foreach (var value in _categories) {
                foreach (var option in value.Options) {
                    PyService.SetFormattingOption(option.Key, _window.GetSetting(option.Key));
                }
            }
            base.SaveSettingsToStorage();
        }
    }
}