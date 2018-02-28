// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.DsTools.Core.Services {
    public interface IServiceManager : IServiceContainer, IDisposable {
        /// <summary>
        /// Adds service instance
        /// </summary>
        /// <param name="service">Service instance</param>
        /// <param name="type">
        /// Optional type to register the instance for. In Visual Studio
        /// some global services are registered as 'SVsService` while
        /// actual interface type is IVsService.
        /// </param>
        IServiceManager AddService(object service, Type type = null);

        /// <summary>
        /// Adds on-demand created service
        /// </summary>
        /// <param name="factory">Service factory</param>
        IServiceManager AddService<T>(Func<IServiceContainer, T> factory) where T : class;

        /// <summary>
        /// Removes service from container by instance
        /// </summary>
        void RemoveService(object service);
    }
}
