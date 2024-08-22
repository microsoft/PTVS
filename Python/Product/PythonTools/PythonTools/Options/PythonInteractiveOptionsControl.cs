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

using System;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Options {
    public partial class PythonInteractiveOptionsControl : UserControl {
        private readonly IServiceProvider _serviceProvider;
        private readonly PythonInteractiveOptions _options;
        private bool _changing;

        private PythonInteractiveOptionsControl() : this(null) {
        }

        public PythonInteractiveOptionsControl(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _options = _serviceProvider?.GetPythonToolsService().InteractiveOptions;
            InitializeComponent();
            UpdateSettings();
        }

        internal async void UpdateSettings() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _changing = true;
            try {
                scriptsTextBox.Text = _options.Scripts;
                useSmartHistoryCheckBox.Checked = _options.UseSmartHistory;
            } finally {
                _changing = false;
            }
        }

        private void Scripts_TextChanged(object sender, EventArgs e) {
            if (!_changing) {
                _options.Scripts = ((TextBox)sender).Text;
            }
        }

        private void UseSmartHistory_CheckedChanged(object sender, EventArgs e) {
            if (!_changing) {
                _options.UseSmartHistory = ((CheckBox)sender).Checked;
            }
        }

        private void browseScriptsButton_Click(object sender, EventArgs e) {
            var newPath = _serviceProvider.BrowseForDirectory(Handle, _options.Scripts);
            if (!string.IsNullOrEmpty(newPath)) {
                scriptsTextBox.Text = newPath;
            }
        }
    }
}
