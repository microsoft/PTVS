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
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Options {
    public partial class PythonGeneralOptionsControl : UserControl {
        private const int ErrorIndex = 0;
        private const int WarningIndex = 1;
        private const int DontIndex = 2;

        private const int SurveyNewsNeverIndex = 0;
        private const int SurveyNewsOnceDayIndex = 1;
        private const int SurveyNewsOnceWeekIndex = 2;
        private const int SurveyNewsOnceMonthIndex = 3;

        public PythonGeneralOptionsControl() {
            InitializeComponent();

            _showOutputWindowForVirtualEnvCreate.Checked = PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForVirtualEnvCreate;
            _showOutputWindowForPackageInstallation.Checked = PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForPackageInstallation;
            _elevatePip.Checked = PythonToolsPackage.Instance.GeneralOptionsPage.ElevatePip;
            _elevateEasyInstall.Checked = PythonToolsPackage.Instance.GeneralOptionsPage.ElevateEasyInstall;
            _autoAnalysis.Checked = PythonToolsPackage.Instance.DebuggingOptionsPage.AutoAnalyzeStandardLibrary;
            _updateSearchPathsForLinkedFiles.Checked = PythonToolsPackage.Instance.DebuggingOptionsPage.UpdateSearchPathsWhenAddingLinkedFiles;
            _unresolvedImportWarning.Checked = PythonToolsPackage.Instance.GeneralOptionsPage.UnresolvedImportWarning;

            switch (PythonToolsPackage.Instance.DebuggingOptionsPage.IndentationInconsistencySeverity) {
                case Severity.Error: _indentationInconsistentCombo.SelectedIndex = ErrorIndex; break;
                case Severity.Warning: _indentationInconsistentCombo.SelectedIndex = WarningIndex; break;
                default: _indentationInconsistentCombo.SelectedIndex = DontIndex; break;
            }

            switch (PythonToolsPackage.Instance.GeneralOptionsPage.SurveyNewsCheck) {
                case SurveyNewsPolicy.Disabled: _surveyNewsCheckCombo.SelectedIndex = SurveyNewsNeverIndex; break;
                case SurveyNewsPolicy.CheckOnceDay: _surveyNewsCheckCombo.SelectedIndex = SurveyNewsOnceDayIndex; break;
                case SurveyNewsPolicy.CheckOnceWeek: _surveyNewsCheckCombo.SelectedIndex = SurveyNewsOnceWeekIndex; break;
                case SurveyNewsPolicy.CheckOnceMonth: _surveyNewsCheckCombo.SelectedIndex = SurveyNewsOnceMonthIndex; break;
            }
        }

        private void _showOutputWindowForVirtualEnvCreate_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForVirtualEnvCreate = _showOutputWindowForVirtualEnvCreate.Checked;
        }

        private void _showOutputWindowForPackageInstallation_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForPackageInstallation = _showOutputWindowForPackageInstallation.Checked;
        }

        private void _elevatePip_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.GeneralOptionsPage.ElevatePip = _elevatePip.Checked;
        }

        private void _elevateEasyInstall_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.GeneralOptionsPage.ElevateEasyInstall = _elevateEasyInstall.Checked;
        }

        private void _autoAnalysis_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.DebuggingOptionsPage.AutoAnalyzeStandardLibrary = _autoAnalysis.Checked;
        }

        private void _updateSearchPathsForLinkedFiles_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.DebuggingOptionsPage.UpdateSearchPathsWhenAddingLinkedFiles = _updateSearchPathsForLinkedFiles.Checked;
        }

        private void _indentationInconsistentCombo_SelectedIndexChanged(object sender, EventArgs e) {
            switch (_indentationInconsistentCombo.SelectedIndex) {
                case ErrorIndex: PythonToolsPackage.Instance.DebuggingOptionsPage.IndentationInconsistencySeverity = Severity.Error; break;
                case WarningIndex: PythonToolsPackage.Instance.DebuggingOptionsPage.IndentationInconsistencySeverity = Severity.Warning; break;
                case DontIndex: PythonToolsPackage.Instance.DebuggingOptionsPage.IndentationInconsistencySeverity = Severity.Ignore; break;
            }
        }

        private void _surveyNewsCheckCombo_SelectedIndexChanged(object sender, EventArgs e) {
            switch (_surveyNewsCheckCombo.SelectedIndex) {
                case SurveyNewsNeverIndex: PythonToolsPackage.Instance.GeneralOptionsPage.SurveyNewsCheck = SurveyNewsPolicy.Disabled; break;
                case SurveyNewsOnceDayIndex: PythonToolsPackage.Instance.GeneralOptionsPage.SurveyNewsCheck = SurveyNewsPolicy.CheckOnceDay; break;
                case SurveyNewsOnceWeekIndex: PythonToolsPackage.Instance.GeneralOptionsPage.SurveyNewsCheck = SurveyNewsPolicy.CheckOnceWeek; break;
                case SurveyNewsOnceMonthIndex: PythonToolsPackage.Instance.GeneralOptionsPage.SurveyNewsCheck = SurveyNewsPolicy.CheckOnceMonth; break;
            }
        }

        private void _unresolvedImportWarning_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.GeneralOptionsPage.UnresolvedImportWarning = _unresolvedImportWarning.Checked;
        }
    }
}
