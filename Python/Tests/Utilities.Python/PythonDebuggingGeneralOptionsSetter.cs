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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using EnvDTE;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;

namespace TestUtilities.Python {
    public class PythonDebuggingGeneralOptionsSetter : IDisposable {
        private readonly DTE _dte;
        private readonly bool? _promptBeforeRunningWithBuildErrorSetting;
        private readonly bool? _autoAnalyzeStandardLibrary;
        private readonly Severity? _indentationInconsistencySeverity;
        private readonly bool? _teeStandardOutput;
        private readonly bool? _waitOnAbnormalExit;
        private readonly bool? _waitOnNormalExit;
        private readonly bool? _useLegacyDebugger;

        public PythonDebuggingGeneralOptionsSetter(
            DTE dte,
            bool? promptBeforeRunningWithBuildErrorSetting = null,
            bool? autoAnalyzeStandardLibrary = null,
            Severity? indentationInconsistencySeverity = null,
            bool? teeStandardOutput = null,
            bool? waitOnAbnormalExit = null,
            bool? waitOnNormalExit = null,
            bool? useLegacyDebugger = null
        ) {
            _dte = dte;
            var options = GetOptions();

            if (promptBeforeRunningWithBuildErrorSetting.HasValue) {
                _promptBeforeRunningWithBuildErrorSetting = options.PromptBeforeRunningWithBuildErrorSetting;
                options.PromptBeforeRunningWithBuildErrorSetting = promptBeforeRunningWithBuildErrorSetting.Value;
            }

            if (autoAnalyzeStandardLibrary.HasValue) {
                _autoAnalyzeStandardLibrary = options.AutoAnalyzeStandardLibrary;
                options.AutoAnalyzeStandardLibrary = autoAnalyzeStandardLibrary.Value;
            }

            if (indentationInconsistencySeverity.HasValue) {
                _indentationInconsistencySeverity = options.IndentationInconsistencySeverity;
                options.IndentationInconsistencySeverity = indentationInconsistencySeverity.Value;
            }

            if (teeStandardOutput.HasValue) {
                _teeStandardOutput = options.TeeStandardOutput;
                options.TeeStandardOutput = teeStandardOutput.Value;
            }

            if (waitOnAbnormalExit.HasValue) {
                _waitOnAbnormalExit = options.WaitOnAbnormalExit;
                options.WaitOnAbnormalExit = waitOnAbnormalExit.Value;
            }

            if (waitOnNormalExit.HasValue) {
                _waitOnNormalExit = options.WaitOnNormalExit;
                options.WaitOnNormalExit = waitOnNormalExit.Value;
            }

            if (useLegacyDebugger.HasValue) {
                _useLegacyDebugger = options.UseLegacyDebugger;
                options.UseLegacyDebugger = useLegacyDebugger.Value;
            }
        }


        public void Dispose() {
            var options = GetOptions();

            if (_promptBeforeRunningWithBuildErrorSetting.HasValue) {
                options.PromptBeforeRunningWithBuildErrorSetting = _promptBeforeRunningWithBuildErrorSetting.Value;
            }

            if (_autoAnalyzeStandardLibrary.HasValue) {
                options.AutoAnalyzeStandardLibrary = _autoAnalyzeStandardLibrary.Value;
            }

            if (_indentationInconsistencySeverity.HasValue) {
                options.IndentationInconsistencySeverity = _indentationInconsistencySeverity.Value;
            }

            if (_teeStandardOutput.HasValue) {
                options.TeeStandardOutput = _teeStandardOutput.Value;
            }

            if (_waitOnAbnormalExit.HasValue) {
                options.WaitOnAbnormalExit = _waitOnAbnormalExit.Value;
            }

            if (_waitOnNormalExit.HasValue) {
                options.WaitOnNormalExit = _waitOnNormalExit.Value;
            }

            if (_useLegacyDebugger.HasValue) {
                options.UseLegacyDebugger = _useLegacyDebugger.Value;
            }
        }

        private IPythonOptions GetOptions() => (IPythonOptions)_dte.GetObject("VsPython");
    }
}
