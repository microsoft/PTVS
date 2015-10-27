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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudioTools;

namespace TestUtilities.Mocks {
    public class MockUIThread : MockUIThreadBase {
        public override void Invoke(Action action) {
            action();
        }

        public override T Invoke<T>(Func<T> func) {
            return func();
        }

        public override Task InvokeAsync(Action action) {
            var tcs = new TaskCompletionSource<object>();
            UIThread.InvokeAsyncHelper(action, tcs);
            return tcs.Task;
        }

        public override Task<T> InvokeAsync<T>(Func<T> func) {
            var tcs = new TaskCompletionSource<T>();
            UIThread.InvokeAsyncHelper<T>(func, tcs);
            return tcs.Task;
        }

        public override Task InvokeTask(Func<Task> func) {
            var tcs = new TaskCompletionSource<object>();
            UIThread.InvokeTaskHelper(func, tcs);
            return tcs.Task;
        }

        public override Task<T> InvokeTask<T>(Func<Task<T>> func) {
            var tcs = new TaskCompletionSource<T>();
            UIThread.InvokeTaskHelper<T>(func, tcs);
            return tcs.Task;
        }

        public override void MustBeCalledFromUIThreadOrThrow() {
        }

        public override bool InvokeRequired {
            get { return false; }
        }
    }
}
