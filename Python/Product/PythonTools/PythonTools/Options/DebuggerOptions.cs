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
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Options {
    public sealed class DebuggerOptions {
        private readonly PythonToolsService _service;

        private const string Category = "Advanced";

        private const string DontPromptBeforeRunningWithBuildErrorSetting = "DontPromptBeforeRunningWithBuildError";
        private const string WaitOnAbnormalExitSetting = "WaitOnAbnormalExit";
        private const string WaitOnNormalExitSetting = "WaitOnNormalExit";
        private const string TeeStandardOutSetting = "TeeStandardOut";
        private const string BreakOnSystemExitZeroSetting = "BreakOnSystemExitZero";
        private const string DebugStdLibSetting = "DebugStdLib";

        internal DebuggerOptions(PythonToolsService service) {
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
        }

        public void Save() {
            _service.SaveBool(DontPromptBeforeRunningWithBuildErrorSetting, Category, !PromptBeforeRunningWithBuildError);
            _service.SaveBool(WaitOnAbnormalExitSetting, Category, WaitOnAbnormalExit);
            _service.SaveBool(WaitOnNormalExitSetting, Category, WaitOnNormalExit);
            _service.SaveBool(TeeStandardOutSetting, Category, TeeStandardOutput);
            _service.SaveBool(BreakOnSystemExitZeroSetting, Category, BreakOnSystemExitZero);
            _service.SaveBool(DebugStdLibSetting, Category, DebugStdLib);
        }

        public void Reset() {
            PromptBeforeRunningWithBuildError = false;
            WaitOnAbnormalExit = true;
            WaitOnNormalExit = true;
            TeeStandardOutput = true;
            BreakOnSystemExitZero = false;
            DebugStdLib = false;
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
    }
}
