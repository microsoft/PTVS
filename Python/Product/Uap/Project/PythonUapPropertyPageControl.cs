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
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Uap.Project {
    public partial class PythonUapPropertyPageControl : UserControl {
        private readonly PythonUapPropertyPage _properties;

        private PythonUapPropertyPageControl() {
            InitializeComponent();

            _toolTip.SetToolTip(_remoteMachine, Resources.UapRemoteMachineHelp);
            _toolTip.SetToolTip(_remoteMachineLabel, Resources.UapRemoteMachineHelp);
        }

        internal PythonUapPropertyPageControl(PythonUapPropertyPage properties)
            : this() {
            _properties = properties;
        }

        public string RemoteDebugMachine {
            get { return _remoteMachine.Text; }
            set { _remoteMachine.Text = value; }
        }

        private void Setting_TextChanged(object sender, EventArgs e) {
            if (_properties != null) {
                _properties.IsDirty = true;
            }
        }
    }
}
