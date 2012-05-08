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
using System.Text;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Options {
    [ComVisible(true)]
    public class PythonAdvancedOptionsPage : PythonDialogPage {
        private bool _promptBeforeRunningWithBuildError, _waitOnAbnormalExit, _autoAnalysis, _waitOnNormalExit, _teeStdOut, _breakOnSystemExitZero, 
                     _updateSearchPathsWhenAddingLinkedFiles, _debugStdLib;
        private int? _crossModuleAnalysisLimit; // not exposed via the UI
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

        public int? CrossModuleAnalysisLimit {
            get { return _crossModuleAnalysisLimit; }
            set { _crossModuleAnalysisLimit = value; }
        }

        /// <summary>
        /// Gets or sets whether or not the debugger will break on a SystemExit exception with
        /// an exit code is zero.  This is only used if we would otherwise break on a SystemExit
        /// exception as configured by the Debug->Exceptions window.
        /// 
        /// New in 1.1.
        /// </summary>
        public bool BreakOnSystemExitZero {
            get { return _breakOnSystemExitZero; }
            set { _breakOnSystemExitZero = value; }
        }

        /// <summary>
        /// Gets or sets whether search paths are updated when adding linked files.
        /// 
        /// New in v1.1.
        /// </summary>
        public bool UpdateSearchPathsWhenAddingLinkedFiles {
            get{ return _updateSearchPathsWhenAddingLinkedFiles;}
            set { _updateSearchPathsWhenAddingLinkedFiles = value; }
        }

        /// <summary>
        /// Gets or sets whether the standard launcher should enable debugging of the standard library.
        /// 
        /// New in v1.1.
        /// </summary>
        public bool DebugStdLib {
            get { return _debugStdLib; }
            set { _debugStdLib = value; }
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
            _breakOnSystemExitZero = false;
            _updateSearchPathsWhenAddingLinkedFiles = true;
            _debugStdLib = false;
        }

        private const string DontPromptBeforeRunningWithBuildErrorSetting = "DontPromptBeforeRunningWithBuildError";
        private const string IndentationInconsistencySeveritySetting = "IndentationInconsistencySeverity";
        private const string WaitOnAbnormalExitSetting = "WaitOnAbnormalExit";
        private const string WaitOnNormalExitSetting = "WaitOnNormalExit";
        private const string AutoAnalysisSetting = "AutoAnalysis";
        private const string TeeStandardOutSetting = "TeeStandardOut";
        private const string CrossModuleAnalysisLimitSetting = "CrossModuleAnalysisLimit";
        private const string BreakOnSystemExitZeroSetting = "BreakOnSystemExitZero";
        private const string UpdateSearchPathsWhenAddingLinkedFilesSetting = "UpdateSearchPathsWhenAddingLinkedFiles";
        private const string DebugStdLibSetting = "DebugStdLib";

        public override void LoadSettingsFromStorage() {
            _promptBeforeRunningWithBuildError = !(LoadBool(DontPromptBeforeRunningWithBuildErrorSetting) ?? false);
            _waitOnAbnormalExit = LoadBool(WaitOnAbnormalExitSetting) ?? true;
            _waitOnNormalExit = LoadBool(WaitOnNormalExitSetting) ?? false;
            _autoAnalysis = LoadBool(AutoAnalysisSetting) ?? true;
            _teeStdOut = LoadBool(TeeStandardOutSetting) ?? true;
            _breakOnSystemExitZero = LoadBool(BreakOnSystemExitZeroSetting) ?? false;
            _indentationInconsistencySeverity = LoadEnum<Severity>(IndentationInconsistencySeveritySetting) ?? Severity.Warning;
            _updateSearchPathsWhenAddingLinkedFiles = LoadBool(UpdateSearchPathsWhenAddingLinkedFilesSetting) ?? true;
            _debugStdLib = LoadBool(DebugStdLibSetting) ?? false;
            var analysisLimit = LoadString(CrossModuleAnalysisLimitSetting);
            if (analysisLimit == null) {
                _crossModuleAnalysisLimit = 1000;    // default analysis limit
            } else if (analysisLimit == "-") {
                _crossModuleAnalysisLimit = null;
            } else {
                _crossModuleAnalysisLimit = Convert.ToInt32(analysisLimit);
            }
        }

        public override void SaveSettingsToStorage() {
            SaveBool(DontPromptBeforeRunningWithBuildErrorSetting, !_promptBeforeRunningWithBuildError);
            SaveBool(WaitOnAbnormalExitSetting, _waitOnAbnormalExit);
            SaveBool(WaitOnNormalExitSetting, _waitOnNormalExit);
            SaveBool(AutoAnalysisSetting, _autoAnalysis);
            SaveBool(TeeStandardOutSetting, _teeStdOut);
            SaveBool(BreakOnSystemExitZeroSetting, _breakOnSystemExitZero);
            SaveBool(UpdateSearchPathsWhenAddingLinkedFilesSetting, _updateSearchPathsWhenAddingLinkedFiles);
            SaveEnum(IndentationInconsistencySeveritySetting, _indentationInconsistencySeverity);
            SaveBool(DebugStdLibSetting, _debugStdLib);
            if (_crossModuleAnalysisLimit != null) {
                SaveInt(CrossModuleAnalysisLimitSetting, _crossModuleAnalysisLimit.Value);
            } else {
                SaveString(CrossModuleAnalysisLimitSetting, "-");
            }

        }

    }
}
