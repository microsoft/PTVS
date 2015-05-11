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

using System.Threading.Tasks;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
#else
using Microsoft.VisualStudio.Repl;
#endif


namespace Microsoft.PythonTools.Repl {
    /// <summary>
    /// The Python repl evaluator.  An instance of this can be acquired by creating a REPL window
    /// via PythonToolsPackage.CreatePythonRepl and getting the Evaluator from the resulting
    /// window.
    /// 
    /// This interface provides additional functionality for interacting with the Python REPL
    /// above and beyond the standard IReplEvaluator interface.
    /// </summary>
    public interface IPythonReplEvaluator :
#if DEV14_OR_LATER
        IInteractiveEvaluator
#else
        IReplEvaluator 
#endif
        {
        /// <summary>
        /// Executes the specified file in the REPL window.
        /// 
        /// Does not reset the process, and the process will remain after the file is executed.
        /// </summary>
        Task<ExecutionResult> ExecuteFile(string filename, string extraArgs);

        /// <summary>
        /// Returns true if the REPL window process has exited.
        /// </summary>
        bool IsDisconnected {
            get;
        }

        /// <summary>
        /// Returns true if the REPL window is currently executing user code.
        /// </summary>
        bool IsExecuting {
            get;
        }
    }
}
