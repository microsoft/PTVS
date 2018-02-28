// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.DsTools.Core.Disposables {
    /// <summary>
    /// Represents a disposable that does nothing on disposal.
    /// Implementation is copied from System.Reactive.Core.dll
    /// </summary>
    internal sealed class DefaultDisposable : IDisposable {
        /// <summary>
        /// Singleton default disposable.
        /// </summary>
        public static readonly DefaultDisposable Instance = new DefaultDisposable();

        private DefaultDisposable() { }

        /// <summary>
        /// Does nothing.
        /// </summary>
        public void Dispose() { }
    }
}