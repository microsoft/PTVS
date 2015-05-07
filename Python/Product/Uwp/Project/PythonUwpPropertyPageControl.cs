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
