// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.DsTools.Core.Shell {
    public interface IApplication {
        /// <summary>
        /// Application name to use in log, system events, etc.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Application locale ID (LCID)
        /// </summary>
        int LocaleId { get; }

        /// <summary>
        /// Fires when host application has completed it's startup sequence
        /// </summary>
        event EventHandler Started;

        /// <summary>
        /// Fires when host application is terminating
        /// </summary>
        event EventHandler Terminating;

    }
}
