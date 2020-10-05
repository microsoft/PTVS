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

using System.Windows.Forms;
using Microsoft.PythonTools.LanguageServerClient;

namespace Microsoft.PythonTools.Options {
    public partial class PythonAnalysisOptionsControl : UserControl {
        public PythonAnalysisOptionsControl() {
            InitializeComponent();

            _diagnosticsModeCombo.Items.Add(Strings.DiagnosticModeOpenFiles);
            _diagnosticsModeCombo.Items.Add(Strings.DiagnosticModeWorkspace);

            _typeCheckingModeCombo.Items.Add(Strings.TypeCheckingModeBasic);
            _typeCheckingModeCombo.Items.Add(Strings.TypeCheckingModeStrict);

            _logLevelCombo.Items.Add(Strings.LogLevelError);
            _logLevelCombo.Items.Add(Strings.LogLevelWarning);
            _logLevelCombo.Items.Add(Strings.LogLevelInformation);
            _logLevelCombo.Items.Add(Strings.LogLevelTrace);
        }

        internal void SyncControlWithPageSettings(PythonToolsService pyService) {
            _stubsPath.Text = pyService.AnalysisOptions.StubPath;
            _autoSearchPathCheckbox.Checked = pyService.AnalysisOptions.AutoSearchPaths;
            _diagnosticsModeCombo.SelectedIndex =
                pyService.AnalysisOptions.DiagnosticMode == PythonLanguageClient.DiagnosticMode.OpenFilesOnly ? 0 : 1;
            _typeCheckingModeCombo.SelectedIndex =
                pyService.AnalysisOptions.TypeCheckingMode == PythonLanguageClient.TypeCheckingMode.Basic ? 0 : 1;

            if (pyService.AnalysisOptions.LogLevel == PythonLanguageClient.LogLevel.Error) {
                _logLevelCombo.SelectedIndex = 0;
            } else if (pyService.AnalysisOptions.LogLevel == PythonLanguageClient.LogLevel.Warning) {
                _logLevelCombo.SelectedIndex = 1;
            } else if (pyService.AnalysisOptions.LogLevel == PythonLanguageClient.LogLevel.Information) {
                _logLevelCombo.SelectedIndex = 2;
            } else if (pyService.AnalysisOptions.LogLevel == PythonLanguageClient.LogLevel.Trace) {
                _logLevelCombo.SelectedIndex = 3;
            } else {
                _logLevelCombo.SelectedIndex = 2;
            }
        }

        internal void SyncPageWithControlSettings(PythonToolsService pyService) {
            pyService.AnalysisOptions.StubPath = _stubsPath.Text;
            pyService.AnalysisOptions.AutoSearchPaths = _autoSearchPathCheckbox.Checked;
            pyService.AnalysisOptions.DiagnosticMode = _diagnosticsModeCombo.SelectedIndex == 0 ?
                PythonLanguageClient.DiagnosticMode.OpenFilesOnly : PythonLanguageClient.DiagnosticMode.Workspace;
            pyService.AnalysisOptions.DiagnosticMode = _diagnosticsModeCombo.SelectedIndex == 0 ?
                PythonLanguageClient.TypeCheckingMode.Basic : PythonLanguageClient.TypeCheckingMode.Strict;

            switch (_logLevelCombo.SelectedIndex) {
                case 0:
                    pyService.AnalysisOptions.LogLevel = PythonLanguageClient.LogLevel.Error;
                    break;
                case 1:
                    pyService.AnalysisOptions.LogLevel = PythonLanguageClient.LogLevel.Warning;
                    break;
                case 2:
                    pyService.AnalysisOptions.LogLevel = PythonLanguageClient.LogLevel.Information;
                    break;
                case 3:
                    pyService.AnalysisOptions.LogLevel = PythonLanguageClient.LogLevel.Trace;
                    break;
            }
       }
    }
}
