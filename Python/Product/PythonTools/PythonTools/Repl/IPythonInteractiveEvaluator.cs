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

namespace Microsoft.PythonTools.Repl
{
    /// <summary>
    /// The Python repl evaluator.  An instance of this can be acquired by creating a REPL window
    /// via PythonToolsPackage.CreatePythonRepl and getting the Evaluator from the resulting
    /// window.
    /// 
    /// This interface provides additional functionality for interacting with the Python REPL
    /// above and beyond the standard IReplEvaluator interface.
    /// </summary>
    interface IPythonInteractiveEvaluator
    {
        /// <summary>
        /// Executes the specified file in the REPL window.
        /// 
        /// Does not reset the process, and the process will remain after the file is executed.
        /// </summary>
        Task<bool> ExecuteFileAsync(string filename, string extraArgs);

        /// <summary>
        /// Returns true if the REPL window process has exited.
        /// </summary>
        bool IsDisconnected { get; }

        /// <summary>
        /// Returns true if the REPL window is currently executing user code.
        /// </summary>
        bool IsExecuting { get; }

        /// <summary>
        /// User friendly name of the evaluator.
        /// </summary>
        string DisplayName { get; }
    }
}
