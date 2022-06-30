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
using Microsoft.PythonTools.Debugger;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Microsoft.PythonTools.Options {
    public partial class PythonDebuggingOptionsControl : UserControl {
        public PythonDebuggingOptionsControl() {
            InitializeComponent();

            // set allowed values for the variable presentation options
            var varPresComboBoxes = new List<ComboBox> { _showVariablesClassComboBox, _showVariablesFunctionComboBox, _showVariablesProtectedComboBox, _showVariablesSpecialComboBox };
            foreach (var varPresComboBox in varPresComboBoxes) {
                varPresComboBox.Items.Add(Strings.VariablePresentation_Group);
                varPresComboBox.Items.Add(Strings.VariablePresentation_Hide);
                varPresComboBox.Items.Add(Strings.VariablePresentation_Inline);
            }
        }

        internal void SyncControlWithPageSettings(PythonToolsService pyService) {
            _promptOnBuildError.Checked = pyService.DebuggerOptions.PromptBeforeRunningWithBuildError;
            _waitOnAbnormalExit.Checked = pyService.DebuggerOptions.WaitOnAbnormalExit;
            _waitOnNormalExit.Checked = pyService.DebuggerOptions.WaitOnNormalExit;
            _teeStdOut.Checked = pyService.DebuggerOptions.TeeStandardOutput;
            _breakOnSystemExitZero.Checked = pyService.DebuggerOptions.BreakOnSystemExitZero;
            _debugStdLib.Checked = pyService.DebuggerOptions.DebugStdLib;
            _showFunctionReturnValue.Checked = pyService.DebuggerOptions.ShowFunctionReturnValue;

            // variable presentation
            if (pyService.DebuggerOptions.VariablePresentationForClasses == PresentationMode.Group) {
                _showVariablesClassComboBox.SelectedIndex = 0;
            } else if (pyService.DebuggerOptions.VariablePresentationForClasses == PresentationMode.Hide) {
                _showVariablesClassComboBox.SelectedIndex = 1;
            } else if (pyService.DebuggerOptions.VariablePresentationForClasses == PresentationMode.Inline) {
                _showVariablesClassComboBox.SelectedIndex = 2;
            } else {
                _showVariablesClassComboBox.SelectedIndex = 0;
            }

            if (pyService.DebuggerOptions.VariablePresentationForFunctions == PresentationMode.Group) {
                _showVariablesFunctionComboBox.SelectedIndex = 0;
            } else if (pyService.DebuggerOptions.VariablePresentationForFunctions == PresentationMode.Hide) {
                _showVariablesFunctionComboBox.SelectedIndex = 1;
            } else if (pyService.DebuggerOptions.VariablePresentationForFunctions == PresentationMode.Inline) {
                _showVariablesFunctionComboBox.SelectedIndex = 2;
            } else {
                _showVariablesFunctionComboBox.SelectedIndex = 0;
            }

            if (pyService.DebuggerOptions.VariablePresentationForProtected == PresentationMode.Group) {
                _showVariablesProtectedComboBox.SelectedIndex = 0;
            } else if (pyService.DebuggerOptions.VariablePresentationForProtected == PresentationMode.Hide) {
                _showVariablesProtectedComboBox.SelectedIndex = 1;
            } else if (pyService.DebuggerOptions.VariablePresentationForProtected == PresentationMode.Inline) {
                _showVariablesProtectedComboBox.SelectedIndex = 2;
            } else {
                _showVariablesProtectedComboBox.SelectedIndex = 0;
            }

            if (pyService.DebuggerOptions.VariablePresentationForSpecial == PresentationMode.Group) {
                _showVariablesSpecialComboBox.SelectedIndex = 0;
            } else if (pyService.DebuggerOptions.VariablePresentationForSpecial == PresentationMode.Hide) {
                _showVariablesSpecialComboBox.SelectedIndex = 1;
            } else if (pyService.DebuggerOptions.VariablePresentationForSpecial == PresentationMode.Inline) {
                _showVariablesSpecialComboBox.SelectedIndex = 2;
            } else {
                _showVariablesSpecialComboBox.SelectedIndex = 0;
            }


        }

        internal void SyncPageWithControlSettings(PythonToolsService pyService) {
            pyService.DebuggerOptions.PromptBeforeRunningWithBuildError = _promptOnBuildError.Checked;
            pyService.DebuggerOptions.WaitOnAbnormalExit = _waitOnAbnormalExit.Checked;
            pyService.DebuggerOptions.WaitOnNormalExit = _waitOnNormalExit.Checked;
            pyService.DebuggerOptions.TeeStandardOutput = _teeStdOut.Checked;
            pyService.DebuggerOptions.BreakOnSystemExitZero = _breakOnSystemExitZero.Checked;
            pyService.DebuggerOptions.DebugStdLib = _debugStdLib.Checked;
            pyService.DebuggerOptions.ShowFunctionReturnValue = _showFunctionReturnValue.Checked;

            // variable presentation
            switch (_showVariablesClassComboBox.SelectedIndex) {
                case 0:
                    pyService.DebuggerOptions.VariablePresentationForClasses = PresentationMode.Group;
                    break;
                case 1:
                    pyService.DebuggerOptions.VariablePresentationForClasses = PresentationMode.Hide;
                    break;
                case 2:
                    pyService.DebuggerOptions.VariablePresentationForClasses = PresentationMode.Inline;
                    break;
            }

            switch (_showVariablesFunctionComboBox.SelectedIndex) {
                case 0:
                    pyService.DebuggerOptions.VariablePresentationForFunctions = PresentationMode.Group;
                    break;
                case 1:
                    pyService.DebuggerOptions.VariablePresentationForFunctions = PresentationMode.Hide;
                    break;
                case 2:
                    pyService.DebuggerOptions.VariablePresentationForFunctions = PresentationMode.Inline;
                    break;
            }

            switch (_showVariablesProtectedComboBox.SelectedIndex) {
                case 0:
                    pyService.DebuggerOptions.VariablePresentationForProtected = PresentationMode.Group;
                    break;
                case 1:
                    pyService.DebuggerOptions.VariablePresentationForProtected = PresentationMode.Hide;
                    break;
                case 2:
                    pyService.DebuggerOptions.VariablePresentationForProtected = PresentationMode.Inline;
                    break;
            }

            switch (_showVariablesSpecialComboBox.SelectedIndex) {
                case 0:
                    pyService.DebuggerOptions.VariablePresentationForSpecial = PresentationMode.Group;
                    break;
                case 1:
                    pyService.DebuggerOptions.VariablePresentationForSpecial = PresentationMode.Hide;
                    break;
                case 2:
                    pyService.DebuggerOptions.VariablePresentationForSpecial = PresentationMode.Inline;
                    break;
            }
        }
    }
}
