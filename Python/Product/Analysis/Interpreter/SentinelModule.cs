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
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Interpreter {
    sealed class SentinelModule : IPythonModule {
        private readonly AnalysisQueue _scope;
        private readonly SemaphoreSlim _semaphore;
        private volatile IPythonModule _realModule;

        public SentinelModule(string name, bool importing) {
            _scope = AnalysisQueue.Current;
            Name = name;
            if (importing) {
                _semaphore = new SemaphoreSlim(0, 1000);
            } else {
                _realModule = this;
            }
        }

        public async Task<IPythonModule> WaitForImportAsync(CancellationToken cancellationToken) {
            var mod = _realModule;
            if (mod != null) {
                return mod;
            }
            if (_scope == AnalysisQueue.Current) {
                // If we're trying to import the same module again from the same analyzer,
                // there is no point waiting to see if the first import finishes,
                // so just return the sentinel object immediately.
                return this;
            }

            try {
                await _semaphore.WaitAsync(cancellationToken);
                _semaphore.Release();
            } catch (ObjectDisposedException) {
                throw new OperationCanceledException();
            }
            return _realModule;
        }

        public void Complete(IPythonModule module) {
            if (_realModule == null) {
                _realModule = module;
                // Release all the waiters at once (unless we have more
                // than than 1000 threads trying to import at once, which
                // should never happen)
                _semaphore.Release(1000);
            }
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
