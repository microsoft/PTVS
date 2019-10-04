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
using Microsoft.Python.Parsing;

namespace Microsoft.PythonTools.Options {
    public partial class PythonGeneralOptionsControl : UserControl {
        private const int ErrorIndex = 0;
        private const int WarningIndex = 1;
        private const int DontIndex = 2;

        public PythonGeneralOptionsControl() {
            InitializeComponent();
        }

        internal Severity IndentationInconsistencySeverity {
            get {
                switch (_indentationInconsistentCombo.SelectedIndex) {
                    case ErrorIndex: 
                        return Severity.Error;
                    case WarningIndex: 
                        return Severity.Warning;
                    case DontIndex: 
                        return Severity.Suppressed;
                    default:
                        return Severity.Suppressed;
                }
            }
            set {
                switch (value) {
                    case Severity.Error: 
                        _indentationInconsistentCombo.SelectedIndex = ErrorIndex; 
                        break;
                    case Severity.Warning: 
                        _indentationInconsistentCombo.SelectedIndex = WarningIndex; 
                        break;
                    default: 
                        _indentationInconsistentCombo.SelectedIndex = DontIndex; 
                        break;
                }
            }
        }

        internal void SyncControlWithPageSettings(PythonToolsService pyService) {
            _showOutputWindowForVirtualEnvCreate.Checked = pyService.GeneralOptions.ShowOutputWindowForVirtualEnvCreate;
            _showOutputWindowForPackageInstallation.Checked = pyService.GeneralOptions.ShowOutputWindowForPackageInstallation;
            _promptForEnvCreate.Checked = pyService.GeneralOptions.PromptForEnvCreate;
            _promptForPackageInstallation.Checked = pyService.GeneralOptions.PromptForPackageInstallation;
            _promptForPytestEnableAndInstall.Checked = pyService.GeneralOptions.PromptForTestFrameWorkInfoBar;
            _elevatePip.Checked = pyService.GeneralOptions.ElevatePip;
            _updateSearchPathsForLinkedFiles.Checked = pyService.GeneralOptions.UpdateSearchPathsWhenAddingLinkedFiles;
            _unresolvedImportWarning.Checked = pyService.GeneralOptions.UnresolvedImportWarning;
            _invalidEncodingWarning.Checked = pyService.GeneralOptions.InvalidEncodingWarning;
            _clearGlobalPythonPath.Checked = pyService.GeneralOptions.ClearGlobalPythonPath;
            IndentationInconsistencySeverity = pyService.GeneralOptions.IndentationInconsistencySeverity;
        }

        internal void SyncPageWithControlSettings(PythonToolsService pyService) {
            pyService.GeneralOptions.ShowOutputWindowForVirtualEnvCreate = _showOutputWindowForVirtualEnvCreate.Checked;
            pyService.GeneralOptions.ShowOutputWindowForPackageInstallation = _showOutputWindowForPackageInstallation.Checked;
            pyService.GeneralOptions.PromptForEnvCreate = _promptForEnvCreate.Checked;
            pyService.GeneralOptions.PromptForPackageInstallation = _promptForPackageInstallation.Checked;
            pyService.GeneralOptions.PromptForTestFrameWorkInfoBar = _promptForPytestEnableAndInstall.Checked;
            pyService.GeneralOptions.ElevatePip = _elevatePip.Checked;
            pyService.GeneralOptions.UpdateSearchPathsWhenAddingLinkedFiles = _updateSearchPathsForLinkedFiles.Checked;
            pyService.GeneralOptions.IndentationInconsistencySeverity = IndentationInconsistencySeverity;
            pyService.GeneralOptions.UnresolvedImportWarning = _unresolvedImportWarning.Checked;
            pyService.GeneralOptions.InvalidEncodingWarning = _invalidEncodingWarning.Checked;
            pyService.GeneralOptions.ClearGlobalPythonPath = _clearGlobalPythonPath.Checked;
        }

        private void _resetSuppressDialog_Click(object sender, EventArgs e) {
            System.Diagnostics.Debug.Assert(ResetSuppressDialog != null, "No listener for ResetSuppressDialog event");
            ResetSuppressDialog?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler ResetSuppressDialog;
    }
}
