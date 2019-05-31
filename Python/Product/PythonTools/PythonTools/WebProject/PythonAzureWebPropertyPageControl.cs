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

namespace Microsoft.PythonTools.Project.Web {
    public partial class PythonAzureWebPropertyPageControl : UserControl {
        private readonly PythonAzureWebPropertyPage _properties;

        public PythonAzureWebPropertyPageControl() {
            InitializeComponent();

            // Web tools publish implementation from Web Tools takes the
            // framework value and prefixes it with "PYTHON|" to get final
            // setting ex. "PYTHON|2.7" that is expected by Azure.
            _frameworkComboBox.Items.AddRange(new [] {
                "2.7",
                "3.6",
                "3.7"
            });
        }

        internal PythonAzureWebPropertyPageControl(PythonAzureWebPropertyPage properties)
            : this() {
            _properties = properties;
        }

        public string FrameworkVersion {
            get { return _frameworkComboBox.Text; }
            set { _frameworkComboBox.Text = value; }
        }

        public string StartupCommand {
            get { return _startupCommandTextBox.Text; }
            set { _startupCommandTextBox.Text = value; }
        }

        private void Setting_TextChanged(object sender, EventArgs e) {
            if (_properties != null) {
                _properties.IsDirty = true;
            }
        }
    }
}
