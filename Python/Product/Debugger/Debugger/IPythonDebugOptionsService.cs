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

namespace Microsoft.PythonTools.Debugger {
    public interface IPythonDebugOptionsService {
        /// <summary>
        /// True to ask the user whether to run when their code contains errors.
        /// Default is false.
        /// </summary>
        bool PromptBeforeRunningWithBuildError { get; }
        
        /// <summary>
        /// True to copy standard output from a Python process into the Output
        /// window. Default is true.
        /// </summary>
        bool TeeStandardOutput { get; }
        
        /// <summary>
        /// True to pause at the end of execution when an error occurs. Default.
        /// is true.
        /// </summary>
        bool WaitOnAbnormalExit { get; }
        
        /// <summary>
        /// True to pause at the end of execution when completing successfully.
        /// Default is true.
        /// </summary>
        bool WaitOnNormalExit { get; }
        
        /// <summary>
        /// True to break on a SystemExit exception even when its exit code is
        /// zero. This applies only when the debugger would normally break on
        /// a SystemExit exception. Default is false.
        /// </summary>
        /// <remarks>New in 1.1</remarks>
        bool BreakOnSystemExitZero { get; }
        
        /// <summary>
        /// True if the standard launcher should allow debugging of the standard
        /// library. Default is false.
        /// </summary>
        /// <remarks>New in 1.1</remarks>
        bool DebugStdLib { get; }
        
        /// <summary>
        /// Show the function return value in locals window.
        /// Default is true.
        /// </summary>
        bool ShowFunctionReturnValue { get; }
        
        /// <summary>
        /// True to use the legacy debugger. Default is false.
        /// </summary>
        bool UseLegacyDebugger { get; }

        PresentationMode VariablePresentationForClasses { get; }
        PresentationMode VariablePresentationForFunctions { get; }
        PresentationMode VariablePresentationForProtected { get; }
        PresentationMode VariablePresentationForSpecial { get; }
    }
}
