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
        }

        internal void SyncControlWithPageSettings(PythonToolsService pyService) {
            _promptOnBuildError.Checked = pyService.DebuggerOptions.PromptBeforeRunningWithBuildError;
            _waitOnAbnormalExit.Checked = pyService.DebuggerOptions.WaitOnAbnormalExit;
            _waitOnNormalExit.Checked = pyService.DebuggerOptions.WaitOnNormalExit;
            _teeStdOut.Checked = pyService.DebuggerOptions.TeeStandardOutput;
            _breakOnSystemExitZero.Checked = pyService.DebuggerOptions.BreakOnSystemExitZero;
            _debugStdLib.Checked = pyService.DebuggerOptions.DebugStdLib;
        }

        internal void SyncPageWithControlSettings(PythonToolsService pyService) {
            pyService.DebuggerOptions.PromptBeforeRunningWithBuildError = _promptOnBuildError.Checked;
            pyService.DebuggerOptions.WaitOnAbnormalExit = _waitOnAbnormalExit.Checked;
            pyService.DebuggerOptions.WaitOnNormalExit = _waitOnNormalExit.Checked;
            pyService.DebuggerOptions.TeeStandardOutput = _teeStdOut.Checked;
            pyService.DebuggerOptions.BreakOnSystemExitZero = _breakOnSystemExitZero.Checked;
            pyService.DebuggerOptions.DebugStdLib = _debugStdLib.Checked;
        }
    }
}
