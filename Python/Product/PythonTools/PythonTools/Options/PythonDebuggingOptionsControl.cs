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
    public partial class PythonDebuggingOptionsControl : UserControl {
        public PythonDebuggingOptionsControl() {
            InitializeComponent();
            _promptOnBuildError.Checked = PythonToolsPackage.Instance.DebuggingOptionsPage.PromptBeforeRunningWithBuildError;

            _waitOnAbnormalExit.Checked = PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit;
            _waitOnNormalExit.Checked = PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit;
            _teeStdOut.Checked = PythonToolsPackage.Instance.DebuggingOptionsPage.TeeStandardOutput;
            _breakOnSystemExitZero.Checked = PythonToolsPackage.Instance.DebuggingOptionsPage.BreakOnSystemExitZero;
            _debugStdLib.Checked = PythonToolsPackage.Instance.DebuggingOptionsPage.DebugStdLib;
        }

        private void _promptOnBuildError_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.DebuggingOptionsPage.PromptBeforeRunningWithBuildError = _promptOnBuildError.Checked;
        }

        private void _waitOnExit_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit = _waitOnAbnormalExit.Checked;
        }

        private void _waitOnNormalExit_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit = _waitOnNormalExit.Checked;
        }

        private void _redirectOutputToVs_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.DebuggingOptionsPage.TeeStandardOutput = _teeStdOut.Checked;
        }

        private void _breakOnSystemExitZero_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.DebuggingOptionsPage.BreakOnSystemExitZero = _breakOnSystemExitZero.Checked;
        }

        private void _debugStdLib_CheckedChanged(object sender, EventArgs e) {
            PythonToolsPackage.Instance.DebuggingOptionsPage.DebugStdLib = _debugStdLib.Checked;
        }
    }
}
