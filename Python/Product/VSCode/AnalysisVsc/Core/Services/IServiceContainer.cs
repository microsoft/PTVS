// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DsTools.Core.Services {
    public interface IServiceContainer {
        /// <summary>
        /// Provides access to global application services
        /// </summary>
        T GetService<T>(Type type = null) where T : class;

        /// <summary>
        /// Enumerates all available services
        /// </summary>
        IEnumerable<Type> AllServices { get; }
    }
}
