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
using System.Threading.Tasks;
using Microsoft.DsTools.Core.Diagnostics;

namespace Microsoft.DsTools.Core.Disposables {
    /// <summary>
    /// Provides a set of static methods for creating Disposables. 
    /// </summary>
    public static class Disposable {
        /// <summary>
        /// Creates a disposable object that invokes the specified action when disposed.
        /// </summary>
        /// <param name="dispose">Action to run during the first call to <see cref="M:System.IDisposable.Dispose" />. The action is guaranteed to be run at most once.</param>
        /// <returns>The disposable object that runs the given action upon disposal.</returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="dispose" /> is null.</exception>
        public static IDisposable Create(Action dispose) {
            Check.ArgumentNull(nameof(dispose), dispose);
            return new AnonymousDisposable(dispose);
        }

        /// <summary>
        /// Creates a disposable wrapper for a disposable that will be invoked at most once.
        /// </summary>
        /// <param name="disposable">Disposable that will be wrapped. Disposable is guaranteed to be run at most once.</param>
        /// <returns>The disposable object that disposes wrapped object.</returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="disposable" /> is null.</exception>
        public static IDisposable Create(IDisposable disposable) {
            Check.ArgumentNull(nameof(disposable), disposable);
            return new DisposableWrapper(disposable);
        }

        /// <summary>
        /// Gets the disposable that does nothing when disposed.
        /// </summary>
        public static IDisposable Empty => DefaultDisposable.Instance;

        public static Task<IDisposable> EmptyTask { get; } = Task.FromResult<IDisposable>(DefaultDisposable.Instance);

        /// <summary>
        /// Represents an Action-based disposable.
        /// </summary>
        private sealed class AnonymousDisposable : IDisposable
        {
            private Action _dispose;

            /// <summary>
            /// Constructs a new disposable with the given action used for disposal.
            /// </summary>
            /// <param name="dispose">Disposal action which will be run upon calling Dispose.</param>
            public AnonymousDisposable(Action dispose) {
                _dispose = dispose;
            }

            /// <summary>
            /// Calls the disposal action if and only if the current instance hasn't been disposed yet.
            /// </summary>
            public void Dispose() {
                Action action = Interlocked.Exchange(ref _dispose, null);
                action?.Invoke();
            }
        }

        /// <summary>
        /// Represents a disposable wrapper.
        /// </summary>
        private sealed class DisposableWrapper : IDisposable
        {
            private IDisposable _disposable;

            public DisposableWrapper(IDisposable disposable) {
                _disposable = disposable;
            }

            /// <summary>
            /// Disposes wrapped object if and only if the current instance hasn't been disposed yet.
            /// </summary>
            public void Dispose() {
                var disposable = Interlocked.Exchange(ref _disposable, null);
                disposable?.Dispose();
            }
        }
    }
}
