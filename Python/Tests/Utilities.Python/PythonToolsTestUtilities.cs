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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;
using TestUtilities.Mocks;

namespace TestUtilities.Python {
    public static class PythonToolsTestUtilities {
        /// <summary>
        /// Returns a PythonToolsService instance which can be used for testing limited scenarios.
        /// 
        /// This is the same service which is available via CreateMockServiceProvider but 
        /// is a more convenient way to get an instance when the full service provider
        /// isn't needed.
        /// </summary>
        /// <returns></returns>
        public static PythonToolsService CreatePythonService() {
            var serviceProvider = CreateMockServiceProvider();
            return serviceProvider.GetPythonToolsService();
        }

        private static IContentTypeRegistryService CreateContentTypeRegistryService() {
            var service = new MockContentTypeRegistryService();
            service.AddContentType(PythonCoreConstants.ContentType, new[] { "code" });

            service.AddContentType("Interactive Command", new[] { "code" });
            return service;
        }

        /// <summary>
        /// Sets up a limited service provider which can be used for testing.  
        /// 
        /// This will not include many of the services which are typically available in
        /// VS but is suitable for simple test cases which need just some base functionality.
        /// </summary>
        public static MockServiceProvider CreateMockServiceProvider(
            bool suppressTaskProvider = true
        ) {
            var componentModel = new MockComponentModel();
            var serviceProvider = new MockServiceProvider(componentModel);

            componentModel.AddExtension(
                typeof(IErrorProviderFactory),
                () => new MockErrorProviderFactory()
            );
            componentModel.AddExtension(
                typeof(IContentTypeRegistryService),
                CreateContentTypeRegistryService
            );

            componentModel.AddExtension(
                typeof(IInteractiveWindowCommandsFactory),
                () => new MockInteractiveWindowCommandsFactory()
            );

            var optService = new Lazy<MockInterpreterOptionsService>(() => new MockInterpreterOptionsService());
            componentModel.AddExtension<IInterpreterRegistryService>(() => optService.Value);
            componentModel.AddExtension<IInterpreterOptionsService>(() => optService.Value);

            serviceProvider.AddService(typeof(UIThreadBase), new MockUIThread());
            var optionsService = new MockPythonToolsOptionsService();
            serviceProvider.AddService(typeof(IPythonToolsOptionsService), optionsService, true);

            var ptvsService = new PythonToolsService(serviceProvider, true);
            serviceProvider.AddService(typeof(PythonToolsService), ptvsService);
            return serviceProvider;
        }

        class LazyComponentGetter<T> : Lazy<T> {
            public LazyComponentGetter(MockComponentModel model) : base(() => (T)model.GetService(typeof(T))) { }
        }
    }
}
