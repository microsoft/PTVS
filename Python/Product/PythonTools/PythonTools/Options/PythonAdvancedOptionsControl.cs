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
    public partial class PythonAdvancedOptionsControl : UserControl {
        private const int ErrorIndex = 0;
        private const int WarningIndex = 1;
        private const int DontIndex = 2;

        public PythonAdvancedOptionsControl() {
            InitializeComponent();
            _promptOnBuildError.Checked = PythonToolsPackage.Instance.OptionsPage.PromptBeforeRunningWithBuildError;

            switch (PythonToolsPackage.Instance.OptionsPage.IndentationInconsistencySeverity) {
                case Severity.Error: _indentationInconsistentCombo.SelectedIndex = ErrorIndex; break;
                case Severity.Warning: _indentationInconsistentCombo.SelectedIndex = WarningIndex; break;
                default: _indentationInconsistentCombo.SelectedIndex = DontIndex; break;
            }

            _waitOnAbnormalExit.Checked = PythonToolsPackage.Instance.OptionsPage.WaitOnAbnormalExit;
            _waitOnNormalExit.Checked = PythonToolsPackage.Instance.OptionsPage.WaitOnNormalExit;
            _teeStdOut.Checked = PythonToolsPackage.Instance.OptionsPage.TeeStandardOutput;
            _autoAnalysis.Checked = PythonToolsPackage.Instance.OptionsPage.AutoAnalyzeStandardLibrary;
            _breakOnSystemExitZero.Checked = PythonToolsPackage.Instance.OptionsPage.BreakOnSystemExitZero;
            _updateSearchPathsForLinkedFiles.Checked = PythonToolsPackage.Instance.OptionsPage.UpdateSearchPathsWhenAddingLinkedFiles;
            _debugStdLib.Checked = PythonToolsPackage.Instance.OptionsPage.DebugStdLib;
        }

        private void _promptOnBuildError_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.OptionsPage.PromptBeforeRunningWithBuildError = _promptOnBuildError.Checked;
        }

        private void _indentationInconsistentCombo_SelectedIndexChanged(object sender, EventArgs e) {
            switch (_indentationInconsistentCombo.SelectedIndex) {
                case ErrorIndex: PythonToolsPackage.Instance.OptionsPage.IndentationInconsistencySeverity = Severity.Error; break;
                case WarningIndex: PythonToolsPackage.Instance.OptionsPage.IndentationInconsistencySeverity = Severity.Warning; break;
                case DontIndex: PythonToolsPackage.Instance.OptionsPage.IndentationInconsistencySeverity = Severity.Ignore; break;
            }
        }

        private void _waitOnExit_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.OptionsPage.WaitOnAbnormalExit = _waitOnAbnormalExit.Checked;
        }

        private void _autoAnalysis_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.OptionsPage.AutoAnalyzeStandardLibrary = _autoAnalysis.Checked;
        }

        private void _waitOnNormalExit_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.OptionsPage.WaitOnNormalExit = _waitOnNormalExit.Checked;
        }

        private void _redirectOutputToVs_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.OptionsPage.TeeStandardOutput = _teeStdOut.Checked;
        }

        private void _breakOnSystemExitZero_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.OptionsPage.BreakOnSystemExitZero = _breakOnSystemExitZero.Checked;
        }

        private void _updateSearchPathsForLinkedFiles_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.OptionsPage.UpdateSearchPathsWhenAddingLinkedFiles = _updateSearchPathsForLinkedFiles.Checked;
        }

        private void _debugStdLib_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.OptionsPage.DebugStdLib = _debugStdLib.Checked;
        }
    }
}
