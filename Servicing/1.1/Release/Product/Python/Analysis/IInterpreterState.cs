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
using System.Collections.Generic;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Provides APIs that interpreters can consume.  An instance of this is provided via the IPythonInterpreter.Initialize
    /// method.
    /// 
    /// This interface is deprecated in v1.1 and instead you should access additional functionality by casting the IInterpreterState
    /// to a PythonAnalyzer object.
    /// </summary>
    public interface IInterpreterState {
        /// <summary>
        /// Enables a Python interpreter to be notified of when a specific function has been called
        /// during the analysis of the program  .
        /// </summary>
        void SpecializeFunction(string moduleName, string name, Action<CallExpression> dlg);

        /// <summary>
        /// Gets the list of additional directories which should be analyzed
        /// </summary>
        IEnumerable<string> AnalysisDirectories {
            get;
        }
    }
}
