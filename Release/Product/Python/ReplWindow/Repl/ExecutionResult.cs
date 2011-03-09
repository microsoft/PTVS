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


namespace Microsoft.VisualStudio.Repl {
    /// <summary>
    /// Provides the result of execution code to the REPL.  
    /// </summary>
    public class ExecutionResult {
        private readonly bool _success;

        public ExecutionResult(bool success) {
            _success = success;
        }

        public bool Success {
            get {
                return _success;
            }
        }
    }
}
