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
        }

        internal Severity IndentationInconsistencySeverity {
            get {
                switch (_indentationInconsistentCombo.SelectedIndex) {
                    case ErrorIndex: 
                        return Severity.Error;
                    case WarningIndex: 
                        return Severity.Warning;
                    case DontIndex: 
                        return Severity.Ignore;
                    default:
                        return Severity.Ignore;
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

        internal SurveyNewsPolicy SurveyNewsCheck {
            get {
                switch (_surveyNewsCheckCombo.SelectedIndex) {
                    case SurveyNewsNeverIndex: 
                        return SurveyNewsPolicy.Disabled; 
                    case SurveyNewsOnceDayIndex: 
                        return SurveyNewsPolicy.CheckOnceDay; 
                    case SurveyNewsOnceWeekIndex: 
                        return SurveyNewsPolicy.CheckOnceWeek; 
                    case SurveyNewsOnceMonthIndex: 
                        return SurveyNewsPolicy.CheckOnceMonth; 
                    default:
                        return SurveyNewsPolicy.Disabled;
                }
            }
            set {
                switch (value) {
                    case SurveyNewsPolicy.Disabled: 
                        _surveyNewsCheckCombo.SelectedIndex = SurveyNewsNeverIndex; 
                        break;
                    case SurveyNewsPolicy.CheckOnceDay: 
                        _surveyNewsCheckCombo.SelectedIndex = SurveyNewsOnceDayIndex; 
                        break;
                    case SurveyNewsPolicy.CheckOnceWeek: 
                        _surveyNewsCheckCombo.SelectedIndex = SurveyNewsOnceWeekIndex; 
                        break;
                    case SurveyNewsPolicy.CheckOnceMonth: 
                        _surveyNewsCheckCombo.SelectedIndex = SurveyNewsOnceMonthIndex; 
                        break;
                }
            }
        }

        internal void SyncControlWithPageSettings(PythonToolsService pyService) {
            _showOutputWindowForVirtualEnvCreate.Checked = pyService.GeneralOptions.ShowOutputWindowForVirtualEnvCreate;
            _showOutputWindowForPackageInstallation.Checked = pyService.GeneralOptions.ShowOutputWindowForPackageInstallation;
            _elevatePip.Checked = pyService.GeneralOptions.ElevatePip;
            _elevateEasyInstall.Checked = pyService.GeneralOptions.ElevateEasyInstall;
            _autoAnalysis.Checked = pyService.GeneralOptions.AutoAnalyzeStandardLibrary;
            _updateSearchPathsForLinkedFiles.Checked = pyService.GeneralOptions.UpdateSearchPathsWhenAddingLinkedFiles;
            _unresolvedImportWarning.Checked = pyService.GeneralOptions.UnresolvedImportWarning;
            _clearGlobalPythonPath.Checked = pyService.GeneralOptions.ClearGlobalPythonPath;
            IndentationInconsistencySeverity = pyService.GeneralOptions.IndentationInconsistencySeverity;
            SurveyNewsCheck = pyService.GeneralOptions.SurveyNewsCheck;
        }

        internal void SyncPageWithControlSettings(PythonToolsService pyService) {
            pyService.GeneralOptions.ShowOutputWindowForVirtualEnvCreate = _showOutputWindowForVirtualEnvCreate.Checked;
            pyService.GeneralOptions.ShowOutputWindowForPackageInstallation = _showOutputWindowForPackageInstallation.Checked;
            pyService.GeneralOptions.ElevatePip = _elevatePip.Checked;
            pyService.GeneralOptions.ElevateEasyInstall = _elevateEasyInstall.Checked;
            pyService.GeneralOptions.AutoAnalyzeStandardLibrary = _autoAnalysis.Checked;
            pyService.GeneralOptions.UpdateSearchPathsWhenAddingLinkedFiles = _updateSearchPathsForLinkedFiles.Checked;
            pyService.GeneralOptions.IndentationInconsistencySeverity = IndentationInconsistencySeverity;
            pyService.GeneralOptions.SurveyNewsCheck = SurveyNewsCheck;
            pyService.GeneralOptions.UnresolvedImportWarning = _unresolvedImportWarning.Checked;
            pyService.GeneralOptions.ClearGlobalPythonPath = _clearGlobalPythonPath.Checked;
        }
    }
}
