// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;

namespace Microsoft.DsTools.Core {
    public static class Lazy {
        public static Lazy<T> Create<T>(Func<T> valueFactory) => new Lazy<T>(valueFactory);

        public static Lazy<T> Create<T>(Func<T> valueFactory, bool isThreadSafe) => new Lazy<T>(valueFactory, isThreadSafe);

        public static Lazy<T> Create<T>(Func<T> valueFactory, LazyThreadSafetyMode mode) => new Lazy<T>(valueFactory, mode);
    }
}
