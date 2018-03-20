// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Microsoft.DsTools.Core.Disposables {
    public sealed class DisposableBag {
        private readonly string _objectName;
        private readonly string _message;
        private ConcurrentStack<Action> _disposables;

        public static DisposableBag Create(IDisposable instance) => Create(instance.GetType());
        public static DisposableBag Create<T>() where T : IDisposable => Create(typeof(T));

        private static DisposableBag Create(Type type) => new DisposableBag(type.Name, FormattableString.Invariant($"{type.Name} instance is disposed"));

        public DisposableBag(string objectName, string message = null) {
            _objectName = objectName;
            _message = message;
            _disposables = new ConcurrentStack<Action>();
        }

        public DisposableBag Add(IDisposable disposable) => Add(disposable.Dispose);

        public DisposableBag Add(Action action) {
            _disposables?.Push(action);
            ThrowIfDisposed();
            return this;
        }

        public bool TryAdd(IDisposable disposable) => TryAdd(disposable.Dispose);

        public bool TryAdd(Action action) {
            _disposables?.Push(action);
            return _disposables != null;
        }

        public void ThrowIfDisposed() {
            if (_disposables != null) {
                return;
            }

            if (_message == null) {
                throw new ObjectDisposedException(_objectName);
            }

            throw new ObjectDisposedException(_objectName, _message);
        }

        public bool TryDispose() {
            var disposables = Interlocked.Exchange(ref _disposables, null);
            if (disposables == null) {
                return false;
            }

            foreach (var disposable in disposables) {
                disposable();
            }

            return true;
        }
    }
}