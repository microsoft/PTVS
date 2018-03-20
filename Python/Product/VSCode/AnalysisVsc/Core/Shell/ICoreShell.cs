// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.DsTools.Core.Services.Shell {
    /// <summary>
    /// Basic shell provides access to services such as 
    /// composition container, export provider, global VS IDE
    /// services and so on.
    /// </summary>
    public interface ICoreShell {
        /// <summary>
        /// Application-global services access
        /// </summary>
        IServiceContainer Services { get; }
    }
}
