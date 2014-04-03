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
        private readonly PythonGeneralOptionsPage _options;
        
        private const int ErrorIndex = 0;
        private const int WarningIndex = 1;
        private const int DontIndex = 2;

        private const int SurveyNewsNeverIndex = 0;
        private const int SurveyNewsOnceDayIndex = 1;
        private const int SurveyNewsOnceWeekIndex = 2;
        private const int SurveyNewsOnceMonthIndex = 3;

        public PythonGeneralOptionsControl() {
            _options = PythonToolsPackage.Instance.GeneralOptionsPage;

            InitializeComponent();

            _showOutputWindowForVirtualEnvCreate.Checked = _options.ShowOutputWindowForVirtualEnvCreate;
            _showOutputWindowForPackageInstallation.Checked = _options.ShowOutputWindowForPackageInstallation;
            _elevatePip.Checked = _options.ElevatePip;
            _elevateEasyInstall.Checked = _options.ElevateEasyInstall;
            _autoAnalysis.Checked = _options.AutoAnalyzeStandardLibrary;
            _updateSearchPathsForLinkedFiles.Checked = _options.UpdateSearchPathsWhenAddingLinkedFiles;
            _unresolvedImportWarning.Checked = _options.UnresolvedImportWarning;
            _clearGlobalPythonPath.Checked = _options.ClearGlobalPythonPath;

            switch (_options.IndentationInconsistencySeverity) {
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
            _options.ShowOutputWindowForVirtualEnvCreate = _showOutputWindowForVirtualEnvCreate.Checked;
        }

        private void _showOutputWindowForPackageInstallation_CheckedChanged(object sender, EventArgs e) {
            _options.ShowOutputWindowForPackageInstallation = _showOutputWindowForPackageInstallation.Checked;
        }

        private void _elevatePip_CheckedChanged(object sender, EventArgs e) {
            _options.ElevatePip = _elevatePip.Checked;
        }

        private void _elevateEasyInstall_CheckedChanged(object sender, EventArgs e) {
            _options.ElevateEasyInstall = _elevateEasyInstall.Checked;
        }

        private void _autoAnalysis_CheckedChanged(object sender, EventArgs e) {
            _options.AutoAnalyzeStandardLibrary = _autoAnalysis.Checked;
        }

        private void _updateSearchPathsForLinkedFiles_CheckedChanged(object sender, EventArgs e) {
            _options.UpdateSearchPathsWhenAddingLinkedFiles = _updateSearchPathsForLinkedFiles.Checked;
        }

        private void _indentationInconsistentCombo_SelectedIndexChanged(object sender, EventArgs e) {
            switch (_indentationInconsistentCombo.SelectedIndex) {
                case ErrorIndex: _options.IndentationInconsistencySeverity = Severity.Error; break;
                case WarningIndex: _options.IndentationInconsistencySeverity = Severity.Warning; break;
                case DontIndex: _options.IndentationInconsistencySeverity = Severity.Ignore; break;
            }
        }

        private void _surveyNewsCheckCombo_SelectedIndexChanged(object sender, EventArgs e) {
            switch (_surveyNewsCheckCombo.SelectedIndex) {
                case SurveyNewsNeverIndex: _options.SurveyNewsCheck = SurveyNewsPolicy.Disabled; break;
                case SurveyNewsOnceDayIndex: _options.SurveyNewsCheck = SurveyNewsPolicy.CheckOnceDay; break;
                case SurveyNewsOnceWeekIndex: _options.SurveyNewsCheck = SurveyNewsPolicy.CheckOnceWeek; break;
                case SurveyNewsOnceMonthIndex: _options.SurveyNewsCheck = SurveyNewsPolicy.CheckOnceMonth; break;
            }
        }

        private void _unresolvedImportWarning_CheckedChanged(object sender, EventArgs e) {
            _options.UnresolvedImportWarning = _unresolvedImportWarning.Checked;
        }

        private void _clearGlobalPythonPath_CheckedChanged(object sender, EventArgs e) {
            _options.ClearGlobalPythonPath = _clearGlobalPythonPath.Checked;
        }
    }
}
