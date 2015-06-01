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
