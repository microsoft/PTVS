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

namespace Microsoft.PythonTools.Options
{
    public partial class PythonDebuggingOptionsControl : UserControl
    {
        public PythonDebuggingOptionsControl()
        {
            InitializeComponent();

            // set allowed values for the variable presentation options
            var varPresComboBoxes = new List<ComboBox> { _showVariablesClassComboBox, _showVariablesFunctionComboBox, _showVariablesProtectedComboBox, _showVariablesSpecialComboBox };
            foreach (var varPresComboBox in varPresComboBoxes)
            {
                varPresComboBox.DataSource = Enum.GetValues(typeof(PresentationMode));
            }
        }

        internal void SyncControlWithPageSettings(PythonToolsService pyService)
        {
            _promptOnBuildError.Checked = pyService.DebuggerOptions.PromptBeforeRunningWithBuildError;
            _waitOnAbnormalExit.Checked = pyService.DebuggerOptions.WaitOnAbnormalExit;
            _waitOnNormalExit.Checked = pyService.DebuggerOptions.WaitOnNormalExit;
            _teeStdOut.Checked = pyService.DebuggerOptions.TeeStandardOutput;
            _breakOnSystemExitZero.Checked = pyService.DebuggerOptions.BreakOnSystemExitZero;
            _debugStdLib.Checked = pyService.DebuggerOptions.DebugStdLib;
            _showFunctionReturnValue.Checked = pyService.DebuggerOptions.ShowFunctionReturnValue;
            _useLegacyDebugger.Checked = pyService.DebuggerOptions.UseLegacyDebugger;

            // variable presentation
            _showVariablesClassComboBox.SelectedItem = pyService.DebuggerOptions.VariablePresentationForClasses;
            _showVariablesFunctionComboBox.SelectedItem = pyService.DebuggerOptions.VariablePresentationForFunctions;
            _showVariablesProtectedComboBox.SelectedItem = pyService.DebuggerOptions.VariablePresentationForProtected;
            _showVariablesSpecialComboBox.SelectedItem = pyService.DebuggerOptions.VariablePresentationForSpecial;
        }

        internal void SyncPageWithControlSettings(PythonToolsService pyService)
        {
            pyService.DebuggerOptions.PromptBeforeRunningWithBuildError = _promptOnBuildError.Checked;
            pyService.DebuggerOptions.WaitOnAbnormalExit = _waitOnAbnormalExit.Checked;
            pyService.DebuggerOptions.WaitOnNormalExit = _waitOnNormalExit.Checked;
            pyService.DebuggerOptions.TeeStandardOutput = _teeStdOut.Checked;
            pyService.DebuggerOptions.BreakOnSystemExitZero = _breakOnSystemExitZero.Checked;
            pyService.DebuggerOptions.DebugStdLib = _debugStdLib.Checked;
            pyService.DebuggerOptions.ShowFunctionReturnValue = _showFunctionReturnValue.Checked;
            pyService.DebuggerOptions.UseLegacyDebugger = _useLegacyDebugger.Checked;

            // variable presentation
            pyService.DebuggerOptions.VariablePresentationForClasses = (PresentationMode)_showVariablesClassComboBox.SelectedItem;
            pyService.DebuggerOptions.VariablePresentationForFunctions = (PresentationMode)_showVariablesFunctionComboBox.SelectedItem;
            pyService.DebuggerOptions.VariablePresentationForProtected = (PresentationMode)_showVariablesProtectedComboBox.SelectedItem;
            pyService.DebuggerOptions.VariablePresentationForSpecial = (PresentationMode)_showVariablesSpecialComboBox.SelectedItem;
        }
    }
}
