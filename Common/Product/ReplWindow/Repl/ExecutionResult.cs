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

namespace Microsoft.VisualStudio.Repl {
    /// <summary>
    /// The result of command execution.  
    /// </summary>
    public struct ExecutionResult {
        public static readonly ExecutionResult Success = new ExecutionResult(true);
        public static readonly ExecutionResult Failure = new ExecutionResult(false);
        public static readonly Task<ExecutionResult> Succeeded;
        public static readonly Task<ExecutionResult> Failed;
 
        private readonly bool _successful;

        public ExecutionResult(bool isSuccessful) {
            _successful = isSuccessful;
        }

        public bool IsSuccessful {
            get {
                return _successful;
            }
        }

        static ExecutionResult() {
            var taskSource = new TaskCompletionSource<ExecutionResult>();
            taskSource.SetResult(Success);
            Succeeded = taskSource.Task;

            taskSource = new TaskCompletionSource<ExecutionResult>();
            taskSource.SetResult(Failure);
            Failed = taskSource.Task;
        }
    }
}
