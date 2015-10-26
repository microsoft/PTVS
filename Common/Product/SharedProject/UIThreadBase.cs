// Visual Studio Shared Project
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

namespace Microsoft.VisualStudioTools {
    /// <summary>
    /// Provides the ability to run code on the VS UI thread.
    /// 
    /// UIThreadBase must be an abstract class rather than an interface because the CLR
    /// doesn't take assembly names into account when generating an interfaces GUID, resulting 
    /// in resolution issues when we reference the interface from multiple assemblies.
    /// </summary>
    public abstract class UIThreadBase {
        public abstract void Invoke(Action action);
        public abstract T Invoke<T>(Func<T> func);
        public abstract Task InvokeAsync(Action action);
        public abstract Task<T> InvokeAsync<T>(Func<T> func);
        public abstract Task InvokeTask(Func<Task> func);
        public abstract Task<T> InvokeTask<T>(Func<Task<T>> func);
        public abstract void MustBeCalledFromUIThreadOrThrow();

        public abstract bool InvokeRequired {
            get;
        }
    }

    /// <summary>
    /// Identifies mock implementations of IUIThread.
    /// </summary>
    public abstract class MockUIThreadBase : UIThreadBase {
        public override void Invoke(Action action) {
            throw new NotImplementedException();
        }

        public override T Invoke<T>(Func<T> func) {
            throw new NotImplementedException();
        }

        public override Task InvokeAsync(Action action) {
            throw new NotImplementedException();
        }

        public override Task<T> InvokeAsync<T>(Func<T> func) {
            throw new NotImplementedException();
        }

        public override Task InvokeTask(Func<Task> func) {
            throw new NotImplementedException();
        }

        public override Task<T> InvokeTask<T>(Func<Task<T>> func) {
            throw new NotImplementedException();
        }

        public override void MustBeCalledFromUIThreadOrThrow() {
            throw new NotImplementedException();
        }

        public override bool InvokeRequired {
            get { throw new NotImplementedException(); }
        }
    }
}
