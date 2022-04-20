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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Core.Disposables;

namespace Microsoft.PythonTools.Common.Core {
    public class AsyncCountdownEvent {
        private readonly AsyncManualResetEvent _mre = new AsyncManualResetEvent();
        private int _count;

        public AsyncCountdownEvent(int initialCount) {
            if (initialCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(initialCount));
            }

            _count = initialCount;
            if (initialCount == 0) {
                _mre.Set();
            }
        }

        public Task WaitAsync() => _mre.WaitAsync();

        public Task WaitAsync(CancellationToken cancellationToken) => _mre.WaitAsync(cancellationToken);

        public void Signal() {
            if (_count <= 0) {
                throw new InvalidOperationException();
            }

            var count = Interlocked.Decrement(ref _count);
            if (count < 0) {
                throw new InvalidOperationException();
            }

            if (count == 0) {
                _mre.Set();
            }
        }

        public void AddOne() {
            _mre.Reset();
            Interlocked.Increment(ref _count);
        }

        public IDisposable AddOneDisposable() {
            AddOne();
            return Disposable.Create(Signal);
        }
    }
}
