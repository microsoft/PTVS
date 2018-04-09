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
using System.Threading;
using static System.FormattableString;

namespace Microsoft.DsTools.Core.Disposables {
    public sealed class DisposeToken {
        private readonly Type _type;
        private readonly CancellationTokenSource _cts;
        private int _disposed;

        public static DisposeToken Create<T>() where T : IDisposable => new DisposeToken(typeof(T));

        private DisposeToken(Type type) {
            _type = type;
            _cts = new CancellationTokenSource();
            CancellationToken = _cts.Token;
        }

        public bool IsDisposed => _cts.IsCancellationRequested;

        public CancellationToken CancellationToken { get; }

        public void ThrowIfDisposed() {
            if (!_cts.IsCancellationRequested) {
                return;
            }

            throw CreateException();
        }

        public IDisposable Link(ref CancellationToken token) {
            if (!token.CanBeCanceled) {
                token = CancellationToken;
                token.ThrowIfCancellationRequested();
                return Disposable.Empty;
            }

            CancellationTokenSource linkedCts;
            try {
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, token);
            } catch (ObjectDisposedException) {
                throw CreateException();
            }

            token = linkedCts.Token;
            if (token.IsCancellationRequested) {
                linkedCts.Dispose();
                token.ThrowIfCancellationRequested();
            }

            return linkedCts;
        }

        public bool TryMarkDisposed() {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) {
                return false;
            }

            _cts.Cancel();
            _cts.Dispose();
            return true;
        }

        private ObjectDisposedException CreateException() => new ObjectDisposedException(_type.Name, Invariant($"{_type.Name} instance is disposed"));
    }
}