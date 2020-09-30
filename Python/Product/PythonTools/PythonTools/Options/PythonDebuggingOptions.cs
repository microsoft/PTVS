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

using System;

namespace Microsoft.PythonTools.Options {
    public sealed class PythonDebuggingOptions {
        private readonly PythonToolsService _service;

        public event EventHandler Changed;

        private const string Category = "Advanced";

        private const string DontPromptBeforeRunningWithBuildErrorSetting = "DontPromptBeforeRunningWithBuildError";
        private const string WaitOnAbnormalExitSetting = "WaitOnAbnormalExit";
        private const string WaitOnNormalExitSetting = "WaitOnNormalExit";
        private const string TeeStandardOutSetting = "TeeStandardOut";
        private const string BreakOnSystemExitZeroSetting = "BreakOnSystemExitZero";
        private const string DebugStdLibSetting = "DebugStdLib";
        private const string ShowFunctionReturnValueSetting = "ShowReturnValue";
        private const string UseLegacyDebuggerSetting = "UseLegacyDebugger";

        internal PythonDebuggingOptions(PythonToolsService service) {
            _service = service;
            Load();
        }

        public void Load() {
            PromptBeforeRunningWithBuildError = !(_service.LoadBool(DontPromptBeforeRunningWithBuildErrorSetting, Category) ?? false);
            WaitOnAbnormalExit = _service.LoadBool(WaitOnAbnormalExitSetting, Category) ?? true;
            WaitOnNormalExit = _service.LoadBool(WaitOnNormalExitSetting, Category) ?? true;
            TeeStandardOutput = _service.LoadBool(TeeStandardOutSetting, Category) ?? true;
            BreakOnSystemExitZero = _service.LoadBool(BreakOnSystemExitZeroSetting, Category) ?? false;
            DebugStdLib = _service.LoadBool(DebugStdLibSetting, Category) ?? false;
            ShowFunctionReturnValue = _service.LoadBool(ShowFunctionReturnValueSetting, Category) ?? true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Save() {
            _service.SaveBool(DontPromptBeforeRunningWithBuildErrorSetting, Category, !PromptBeforeRunningWithBuildError);
            _service.SaveBool(WaitOnAbnormalExitSetting, Category, WaitOnAbnormalExit);
            _service.SaveBool(WaitOnNormalExitSetting, Category, WaitOnNormalExit);
            _service.SaveBool(TeeStandardOutSetting, Category, TeeStandardOutput);
            _service.SaveBool(BreakOnSystemExitZeroSetting, Category, BreakOnSystemExitZero);
            _service.SaveBool(DebugStdLibSetting, Category, DebugStdLib);
            _service.SaveBool(ShowFunctionReturnValueSetting, Category, ShowFunctionReturnValue);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Reset() {
            PromptBeforeRunningWithBuildError = false;
            WaitOnAbnormalExit = true;
            WaitOnNormalExit = true;
            TeeStandardOutput = true;
            BreakOnSystemExitZero = false;
            DebugStdLib = false;
            ShowFunctionReturnValue = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// True to ask the user whether to run when their code contains errors.
        /// Default is false.
        /// </summary>
        public bool PromptBeforeRunningWithBuildError {
            get;
            set;
        }

        /// <summary>
        /// True to copy standard output from a Python process into the Output
        /// window. Default is true.
        /// </summary>
        public bool TeeStandardOutput {
            get;
            set;
        }

        /// <summary>
        /// True to pause at the end of execution when an error occurs. Default
        /// is true.
        /// </summary>
        public bool WaitOnAbnormalExit {
            get;
            set;
        }

        /// <summary>
        /// True to pause at the end of execution when completing successfully.
        /// Default is true.
        /// </summary>
        public bool WaitOnNormalExit {
            get;
            set;
        }

        /// <summary>
        /// True to break on a SystemExit exception even when its exit code is
        /// zero. This applies only when the debugger would normally break on
        /// a SystemExit exception. Default is false.
        /// </summary>
        /// <remarks>New in 1.1</remarks>
        public bool BreakOnSystemExitZero {
            get;
            set;
        }

        /// <summary>
        /// True if the standard launcher should allow debugging of the standard
        /// library. Default is false.
        /// </summary>
        /// <remarks>New in 1.1</remarks>
        public bool DebugStdLib {
            get;
            set;
        }

        /// <summary>
        /// Show the function return value in locals window
        /// Default is true
        /// </summary>
        public bool ShowFunctionReturnValue {
            get;
            set;
        }
    }
}
