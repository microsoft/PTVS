// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.DsTools.Core.Services.Shell {
    /// <summary>
    /// Progress reporting service
    /// </summary>
    public interface IProgressService {
        /// <summary>
        /// Displays progress message in the application UI.
        /// </summary>
        IProgress BeginProgress();
    }

    public interface IProgress: IDisposable {
        /// <summary>
        /// Updates progress message in the application UI.
        /// </summary>
        Task Report(string message);
    }
}
