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

        internal void SyncControlWithPageSettings(PythonDebuggingOptionsPage page) {
            _promptOnBuildError.Checked = page.PromptBeforeRunningWithBuildError;
            _waitOnAbnormalExit.Checked = page.WaitOnAbnormalExit;
            _waitOnNormalExit.Checked = page.WaitOnNormalExit;
            _teeStdOut.Checked = page.TeeStandardOutput;
            _breakOnSystemExitZero.Checked = page.BreakOnSystemExitZero;
            _debugStdLib.Checked = page.DebugStdLib;
        }

        internal void SyncPageWithControlSettings(PythonDebuggingOptionsPage page) {
            page.PromptBeforeRunningWithBuildError = _promptOnBuildError.Checked;
            page.WaitOnAbnormalExit = _waitOnAbnormalExit.Checked;
            page.WaitOnNormalExit = _waitOnNormalExit.Checked;
            page.TeeStandardOutput = _teeStdOut.Checked;
            page.BreakOnSystemExitZero = _breakOnSystemExitZero.Checked;
            page.DebugStdLib = _debugStdLib.Checked;
        }
    }
}
