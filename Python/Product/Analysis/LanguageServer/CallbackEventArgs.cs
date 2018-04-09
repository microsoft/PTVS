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
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public abstract class CallbackEventArgs<T> : EventArgs where T : struct {
        private TaskCompletionSource<object> _task;

        internal CallbackEventArgs(TaskCompletionSource<object> task) {
            _task = task;
        }

        public T? @params { get; set; }

        public void SetResult() {
            _task.TrySetResult(null);
        }

        public void SetError(ResponseError error) {
            _task.TrySetException(new LanguageServerException(error.code, error.message));
        }
    }

    public abstract class CallbackEventArgs<T, U> : EventArgs where T : class where U : class {
        private readonly TaskCompletionSource<U> _task;

        internal CallbackEventArgs(TaskCompletionSource<U> task) {
            _task = task;
        }

        public T @params { get; set; }

        public void SetResult(U result) {
            _task.TrySetResult(result);
        }

        public void SetError(ResponseError error) {
            _task.TrySetException(new LanguageServerException(error.code, error.message));
        }
    }
}
