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
    public partial class LanguageServerOptionsControl : UserControl {
        private readonly IServiceProvider _serviceProvider;
        private readonly LanguageServerOptions _options;
        private bool _changing;

        private LanguageServerOptionsControl() : this(null) {
        }

        public LanguageServerOptionsControl(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _options = _serviceProvider?.GetPythonToolsService().LanguageServerOptions;
            InitializeComponent();
            UpdateSettings();
        }

        internal async void UpdateSettings() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _changing = true;
            try {
                typeShedPathTextBox.Text = _options.TypeShedPath;
                suppressTypeShedCheckbox.Checked = _options.SuppressTypeShed;
                disableLanguageServerCheckbox.Checked = _options.ServerDisabled;
            } finally {
                _changing = false;
            }
        }

        private void TypeShedPath_TextChanged(object sender, EventArgs e) {
            if (!_changing) {
                _options.TypeShedPath = ((TextBox)sender).Text;
            }
        }

        private void browseTypeShedPathButton_Click(object sender, EventArgs e) {
            var newPath = _serviceProvider.BrowseForDirectory(Handle, _options.TypeShedPath);
            if (!string.IsNullOrEmpty(newPath)) {
                typeShedPathTextBox.Text = newPath;
            }
        }

        private void suppressTypeShedCheckbox_CheckedChanged(object sender, EventArgs e) {
            if (!_changing) {
                _options.SuppressTypeShed = suppressTypeShedCheckbox.Checked;
            }
        }

        private void _enableLanguageServer_CheckedChanged(object sender, EventArgs e) {
            if (!_changing) {
                _options.ServerDisabled = disableLanguageServerCheckbox.Checked;
            }
        }
    }
}