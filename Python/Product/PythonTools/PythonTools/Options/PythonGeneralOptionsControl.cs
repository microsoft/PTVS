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

namespace Microsoft.PythonTools.Options {
    public partial class PythonGeneralOptionsControl : UserControl {

        public PythonGeneralOptionsControl() {
            InitializeComponent();
        }

        internal void SyncControlWithPageSettings(PythonToolsService pyService) {
            _showOutputWindowForVirtualEnvCreate.Checked = pyService.GeneralOptions.ShowOutputWindowForVirtualEnvCreate;
            _showOutputWindowForPackageInstallation.Checked = pyService.GeneralOptions.ShowOutputWindowForPackageInstallation;
            _promptForEnvCreate.Checked = pyService.GeneralOptions.PromptForEnvCreate;
            _promptForPackageInstallation.Checked = pyService.GeneralOptions.PromptForPackageInstallation;
            _promptForPytestEnableAndInstall.Checked = pyService.GeneralOptions.PromptForTestFrameWorkInfoBar;
            _elevatePip.Checked = pyService.GeneralOptions.ElevatePip;
            //_clearGlobalPythonPath.Checked = pyService.GeneralOptions.ClearGlobalPythonPath;
        }

        internal void SyncPageWithControlSettings(PythonToolsService pyService) {
            pyService.GeneralOptions.ShowOutputWindowForVirtualEnvCreate = _showOutputWindowForVirtualEnvCreate.Checked;
            pyService.GeneralOptions.ShowOutputWindowForPackageInstallation = _showOutputWindowForPackageInstallation.Checked;
            pyService.GeneralOptions.PromptForEnvCreate = _promptForEnvCreate.Checked;
            pyService.GeneralOptions.PromptForPackageInstallation = _promptForPackageInstallation.Checked;
            pyService.GeneralOptions.PromptForTestFrameWorkInfoBar = _promptForPytestEnableAndInstall.Checked;
            pyService.GeneralOptions.ElevatePip = _elevatePip.Checked;
            //pyService.GeneralOptions.ClearGlobalPythonPath = _clearGlobalPythonPath.Checked;
        }

        private void _resetSuppressDialog_Click(object sender, EventArgs e) {
            System.Diagnostics.Debug.Assert(ResetSuppressDialog != null, "No listener for ResetSuppressDialog event");
            ResetSuppressDialog?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler ResetSuppressDialog;
    }
}
