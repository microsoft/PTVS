// Visual Studio Shared Project
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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System;
using System.Globalization;

namespace Microsoft.VisualStudioTools.TestAdapter
{
    internal static class ServiceProviderExtensions
    {
        public static T GetService<T>(this IServiceProvider serviceProvider)
            where T : class
        {
            return serviceProvider.GetService<T>(typeof(T));
        }

        public static T GetService<T>(this IServiceProvider serviceProvider, Type serviceType)
            where T : class
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            var serviceInstance = serviceProvider.GetService(serviceType) as T;
            if (serviceInstance == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, serviceType.Name));
            }

            return serviceInstance;
        }
    }
}
