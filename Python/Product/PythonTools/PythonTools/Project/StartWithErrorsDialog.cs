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

using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing;

namespace Microsoft.PythonTools.Project {
    public partial class StartWithErrorsDialog : Form {

        public StartWithErrorsDialog() {
            InitializeComponent();
            _icon.Image = SystemIcons.Warning.ToBitmap();
        }

        public static bool ShouldShow {
            get {
                return PythonToolsPackage.Instance.DebuggingOptionsPage.PromptBeforeRunningWithBuildError;
            }
        }

        protected override void OnClosing(CancelEventArgs e) {
            if (_dontShowAgainCheckbox.Checked) {
                PythonToolsPackage.Instance.DebuggingOptionsPage.PromptBeforeRunningWithBuildError = false;
                PythonToolsPackage.Instance.DebuggingOptionsPage.SaveSettingsToStorage();
            }
        }

        private void YesButtonClick(object sender, System.EventArgs e) {
            DialogResult = System.Windows.Forms.DialogResult.Yes;
            Close();
        }

        private void NoButtonClick(object sender, System.EventArgs e) {
            this.DialogResult = System.Windows.Forms.DialogResult.No;
            Close();
        }
    }
}
