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

namespace Microsoft.PythonTools.Project.Web {
    public partial class PythonWebPropertyPageControl : UserControl {
        private readonly PythonWebPropertyPage _properties;

        private PythonWebPropertyPageControl() {
            InitializeComponent();

            _toolTip.SetToolTip(_staticUri, SR.GetString(SR.StaticUriHelp));
            _toolTip.SetToolTip(_staticUriLabel, SR.GetString(SR.StaticUriHelp));

            _toolTip.SetToolTip(_wsgiHandler, SR.GetString(SR.WsgiHandlerHelp));
            _toolTip.SetToolTip(_wsgiHandlerLabel, SR.GetString(SR.WsgiHandlerHelp));
        }

        internal PythonWebPropertyPageControl(PythonWebPropertyPage properties)
            : this() {
            _properties = properties;
        }

        public string StaticUriPattern {
            get { return _staticUri.Text; }
            set { _staticUri.Text = value; }
        }

        public string WsgiHandler {
            get { return _wsgiHandler.Text; }
            set { _wsgiHandler.Text = value; }
        }

        private void Setting_TextChanged(object sender, EventArgs e) {
            if (_properties != null) {
                _properties.IsDirty = true;
            }
        }
    }
}
