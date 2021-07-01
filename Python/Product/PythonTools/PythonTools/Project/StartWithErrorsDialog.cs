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

namespace Microsoft.PythonTools.Project
{
    public partial class StartWithErrorsDialog : Form
    {
        internal PythonToolsService PythonService { get; }

        public StartWithErrorsDialog(PythonToolsService pyService)
        {
            PythonService = pyService;
            InitializeComponent();
            _icon.Image = SystemIcons.Warning.ToBitmap();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_dontShowAgainCheckbox.Checked)
            {
                PythonService.DebuggerOptions.PromptBeforeRunningWithBuildError = false;
                PythonService.DebuggerOptions.Save();
            }
        }

        private void YesButtonClick(object sender, System.EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.Yes;
            Close();
        }

        private void NoButtonClick(object sender, System.EventArgs e)
        {
            this.DialogResult = System.Windows.Forms.DialogResult.No;
            Close();
        }
    }
}
