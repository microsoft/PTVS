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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Microsoft.PythonTools.Commands {
    public partial class DiagnosticsForm : Form {
        public DiagnosticsForm(string content) {
            InitializeComponent();
            _textBox.Text = content;
        }

        public TextBox TextBox {
            get {
                return _textBox;
            }
        }

        private void _ok_Click(object sender, EventArgs e) {
            Close();
        }

        private void _copy_Click(object sender, EventArgs e) {
            _textBox.SelectAll();
            Clipboard.SetText(_textBox.SelectedText);
        }
    }
}
