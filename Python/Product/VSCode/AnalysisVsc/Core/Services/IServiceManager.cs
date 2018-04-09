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
