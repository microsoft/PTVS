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
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project.Web {
    public partial class PythonWebPropertyPageControl : UserControl {
        private readonly PythonWebPropertyPage _properties;
        private readonly Timer _validateStaticPatternTimer;

        private PythonWebPropertyPageControl() {
            InitializeComponent();

            _validateStaticPatternTimer = new Timer();
            _validateStaticPatternTimer.Tick += ValidateStaticPattern;
            _validateStaticPatternTimer.Interval = 500;

            _toolTip.SetToolTip(_staticPattern, SR.GetString(SR.StaticPatternHelp));
            _toolTip.SetToolTip(_staticPatternLabel, SR.GetString(SR.StaticPatternHelp));

            _toolTip.SetToolTip(_staticRewrite, SR.GetString(SR.StaticRewriteHelp));
            _toolTip.SetToolTip(_staticRewriteLabel, SR.GetString(SR.StaticRewriteHelp));

            _toolTip.SetToolTip(_wsgiHandler, SR.GetString(SR.WsgiHandlerHelp));
            _toolTip.SetToolTip(_wsgiHandlerLabel, SR.GetString(SR.WsgiHandlerHelp));
        }

        private void ValidateStaticPattern(object sender, EventArgs e) {
            _validateStaticPatternTimer.Enabled = false;

            try {
                new Regex(_staticPattern.Text);
                _errorProvider.SetError(_staticPattern, null);
            } catch (ArgumentException) {
                _errorProvider.SetError(_staticPattern, SR.GetString(SR.StaticPatternError));
            }
        }

        internal PythonWebPropertyPageControl(PythonWebPropertyPage properties)
            : this() {
            _properties = properties;
        }

        public string StaticUriPattern {
            get { return _staticPattern.Text; }
            set { _staticPattern.Text = value; }
        }

        public string StaticUriRewrite {
            get { return _staticRewrite.Text; }
            set { _staticRewrite.Text = value; }
        }

        public string WsgiHandler {
            get { return _wsgiHandler.Text; }
            set { _wsgiHandler.Text = value; }
        }

        private void Setting_TextChanged(object sender, EventArgs e) {
            if (_properties != null) {
                _properties.IsDirty = true;
            }
            if (sender == _staticPattern) {
                _validateStaticPatternTimer.Enabled = false;
                _validateStaticPatternTimer.Enabled = true;
            }
        }
    }
}
