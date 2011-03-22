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
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Options {
    [ComVisible(true)]
    public class PythonAdvancedOptionsPage : PythonDialogPage {
        private bool _promptBeforeRunningWithBuildError, _waitOnAbnormalExit, _autoAnalysis, _waitOnNormalExit, _teeStdOut;
        private Severity _indentationInconsistencySeverity;
        private PythonAdvancedOptionsControl _window;

        public PythonAdvancedOptionsPage()
            : base("Advanced") {
        }

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                if (_window == null) {
                    _window = new PythonAdvancedOptionsControl();
                }
                return _window;
            }
        }

        #region Editor Options

        public bool PromptBeforeRunningWithBuildError {
            get { return _promptBeforeRunningWithBuildError; }
            set { _promptBeforeRunningWithBuildError = value; }
        }

        public bool AutoAnalyzeStandardLibrary {
            get { return _autoAnalysis; }
            set { _autoAnalysis = value; }
        }

        public bool TeeStandardOutput {
            get { return _teeStdOut; }
            set { _teeStdOut = value; }
        }

        public Severity IndentationInconsistencySeverity {
            get { return _indentationInconsistencySeverity; }
            set { 
                _indentationInconsistencySeverity = value;
                var changed = IndentationInconsistencyChanged;
                if (changed != null) {
                    changed(this, EventArgs.Empty);
                }
            }
        }

        public bool WaitOnAbnormalExit {
            get { return _waitOnAbnormalExit; }
            set { _waitOnAbnormalExit = value; }
        }

        public bool WaitOnNormalExit {
            get { return _waitOnNormalExit; }
            set { _waitOnNormalExit = value; }
        }

        public event EventHandler IndentationInconsistencyChanged;

        #endregion

        public override void ResetSettings() {
            _promptBeforeRunningWithBuildError = false;
            _waitOnAbnormalExit = true;
            _indentationInconsistencySeverity = Severity.Warning;
            _waitOnNormalExit = false;
            _autoAnalysis = true;
            _teeStdOut = true;
        }

        private const string DontPromptBeforeRunningWithBuildErrorSetting = "DontPromptBeforeRunningWithBuildError";
        private const string IndentationInconsistencySeveritySetting = "IndentationInconsistencySeverity";
        private const string WaitOnAbnormalExitSetting = "WaitOnAbnormalExit";
        private const string WaitOnNormalExitSetting = "WaitOnNormalExit";
        private const string AutoAnalysisSetting = "AutoAnalysis";
        private const string TeeStandardOutSetting = "TeeStandardOut";

        public override void LoadSettingsFromStorage() {
            _promptBeforeRunningWithBuildError = !(LoadBool(DontPromptBeforeRunningWithBuildErrorSetting) ?? false);
            _waitOnAbnormalExit = LoadBool(WaitOnAbnormalExitSetting) ?? true;
            _waitOnNormalExit = LoadBool(WaitOnNormalExitSetting) ?? false;
            _autoAnalysis = LoadBool(AutoAnalysisSetting) ?? true;
            _teeStdOut = LoadBool(TeeStandardOutSetting) ?? true;
            _indentationInconsistencySeverity = LoadEnum<Severity>(IndentationInconsistencySeveritySetting) ?? Severity.Warning;
        }

        public override void SaveSettingsToStorage() {
            SaveBool(DontPromptBeforeRunningWithBuildErrorSetting, !_promptBeforeRunningWithBuildError);
            SaveBool(WaitOnAbnormalExitSetting, _waitOnAbnormalExit);
            SaveBool(WaitOnNormalExitSetting, _waitOnNormalExit);
            SaveBool(AutoAnalysisSetting, _autoAnalysis);
            SaveBool(TeeStandardOutSetting, _teeStdOut);
            SaveEnum(IndentationInconsistencySeveritySetting, _indentationInconsistencySeverity);
        }

    }
}
