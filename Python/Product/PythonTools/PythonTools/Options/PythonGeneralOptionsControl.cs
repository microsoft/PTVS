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

        internal void SyncControlWithPageSettings(PythonGeneralOptionsPage page) {
            _showOutputWindowForVirtualEnvCreate.Checked = page.ShowOutputWindowForVirtualEnvCreate;
            _showOutputWindowForPackageInstallation.Checked = page.ShowOutputWindowForPackageInstallation;
            _elevatePip.Checked = page.ElevatePip;
            _elevateEasyInstall.Checked = page.ElevateEasyInstall;
            _autoAnalysis.Checked = page.AutoAnalyzeStandardLibrary;
            _updateSearchPathsForLinkedFiles.Checked = page.UpdateSearchPathsWhenAddingLinkedFiles;
            _unresolvedImportWarning.Checked = page.UnresolvedImportWarning;
            _clearGlobalPythonPath.Checked = page.ClearGlobalPythonPath;
            IndentationInconsistencySeverity = page.IndentationInconsistencySeverity;
            SurveyNewsCheck = page.SurveyNewsCheck;
        }

        internal void SyncPageWithControlSettings(PythonGeneralOptionsPage page) {
            page.ShowOutputWindowForVirtualEnvCreate = _showOutputWindowForVirtualEnvCreate.Checked;
            page.ShowOutputWindowForPackageInstallation = _showOutputWindowForPackageInstallation.Checked;
            page.ElevatePip = _elevatePip.Checked;
            page.ElevateEasyInstall = _elevateEasyInstall.Checked;
            page.AutoAnalyzeStandardLibrary = _autoAnalysis.Checked;
            page.UpdateSearchPathsWhenAddingLinkedFiles = _updateSearchPathsForLinkedFiles.Checked;
            page.IndentationInconsistencySeverity = IndentationInconsistencySeverity;
            page.SurveyNewsCheck = SurveyNewsCheck;
            page.UnresolvedImportWarning = _unresolvedImportWarning.Checked;
            page.ClearGlobalPythonPath = _clearGlobalPythonPath.Checked;
        }
    }
}
