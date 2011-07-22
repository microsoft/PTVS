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

        private void _ok_Click(object sender, EventArgs e) {
            Close();
        }
    }
}
