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
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Interpreter {
    sealed class SentinelModule : IPythonModule, IDisposable {
        private readonly Thread _thread;
        private volatile TaskCompletionSource<IPythonModule> _tcs;
        private volatile IPythonModule _realModule;

        public SentinelModule(string name, bool importing) {
            _thread = Thread.CurrentThread;
            Name = name;
            if (!importing) {
                _realModule = this;
            }
        }

        public Task<IPythonModule> WaitForImportAsync(CancellationToken cancel) {
            var mod = _realModule;
            if (mod != null) {
                return Task.FromResult(mod);
            }
            if (_thread == Thread.CurrentThread) {
                return Task.FromResult<IPythonModule>(this);
            }

            var tcs = _tcs;
            if (tcs == null) {
                tcs = new TaskCompletionSource<IPythonModule>();
                tcs = Interlocked.CompareExchange(ref _tcs, tcs, null) ?? tcs;
            }

            if (cancel.CanBeCanceled) {
                var tcs2 = new TaskCompletionSource<IPythonModule>();
                tcs.Task.ContinueWith(t => tcs2.TrySetResult(t.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                tcs.Task.ContinueWith(t => tcs2.TrySetCanceled(), TaskContinuationOptions.OnlyOnCanceled);
                cancel.Register(() => tcs2.TrySetCanceled());
                return tcs2.Task;
            }

            return tcs.Task;
        }

        public void Complete(IPythonModule module) {
            _realModule = module;
            _tcs?.TrySetResult(module);
        }

        public void Dispose() {
            Interlocked.Exchange(ref _tcs, null)?.TrySetCanceled();
        }

        public string Name { get; }
        public string Documentation => null;
        public PythonMemberType MemberType => PythonMemberType.Module;
        public IEnumerable<string> GetChildrenModules() => Enumerable.Empty<string>();
        public IMember GetMember(IModuleContext context, string name) => null;
        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => Enumerable.Empty<string>();
        public void Imported(IModuleContext context) { }
    }
}
