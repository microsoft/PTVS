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

using System;
using System.Windows.Forms;

namespace Microsoft.PythonTools.Uwp.Project {
    public partial class PythonUwpPropertyPageControl : UserControl {
        private readonly PythonUwpPropertyPage _properties;

        private PythonUwpPropertyPageControl() {
            InitializeComponent();

            _toolTip.SetToolTip(_remoteDevice, Resources.UwpRemoteDeviceHelp);
            _toolTip.SetToolTip(_remoteDeviceLabel, Resources.UwpRemoteDeviceHelp);
            _toolTip.SetToolTip(_remotePort, Resources.UwpRemotePortHelp);
            _toolTip.SetToolTip(_remotePortLabel, Resources.UwpRemotePortHelp);
        }

        internal PythonUwpPropertyPageControl(PythonUwpPropertyPage properties)
            : this() {
            _properties = properties;
        }

        public string RemoteDevice {
            get { return _remoteDevice.Text; }
            set { _remoteDevice.Text = value; }
        }

        public decimal RemotePort {
            get { return _remotePort.Value; }
            set { _remotePort.Value = value; }
        }

        private void Setting_TextChanged(object sender, EventArgs e) {
            if (_properties != null) {
                _properties.IsDirty = true;
            }
        }
    }
}
