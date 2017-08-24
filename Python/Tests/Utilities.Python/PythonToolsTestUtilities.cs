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
            bool suppressTaskProvider = false
        ) {
            var serviceProvider = new MockServiceProvider();

            serviceProvider.ComponentModel.AddExtension(
                typeof(IErrorProviderFactory),
                () => new MockErrorProviderFactory()
            );
            serviceProvider.ComponentModel.AddExtension(
                typeof(IContentTypeRegistryService),
                CreateContentTypeRegistryService
            );

            serviceProvider.ComponentModel.AddExtension(
                typeof(IInteractiveWindowCommandsFactory),
                () => new MockInteractiveWindowCommandsFactory()
            );

            var optService = new Lazy<MockInterpreterOptionsService>(() => new MockInterpreterOptionsService());
            serviceProvider.ComponentModel.AddExtension<IInterpreterRegistryService>(() => optService.Value);
            serviceProvider.ComponentModel.AddExtension<IInterpreterOptionsService>(() => optService.Value);

            var editorServices = CreatePythonEditorServices(serviceProvider, serviceProvider.ComponentModel);
            serviceProvider.ComponentModel.AddExtension(() => editorServices);

            var analysisEntryServiceCreator = new Lazy<AnalysisEntryService>(() => new AnalysisEntryService(editorServices));
            serviceProvider.ComponentModel.AddExtension<IAnalysisEntryService>(() => analysisEntryServiceCreator.Value);
            serviceProvider.ComponentModel.AddExtension(() => analysisEntryServiceCreator.Value);

            if (suppressTaskProvider) {
                serviceProvider.AddService(typeof(ErrorTaskProvider), null, true);
                serviceProvider.AddService(typeof(CommentTaskProvider), null, true);
            } else {
                serviceProvider.AddService(typeof(ErrorTaskProvider), CreateTaskProviderService, true);
                serviceProvider.AddService(typeof(CommentTaskProvider), CreateTaskProviderService, true);
            }
            serviceProvider.AddService(typeof(UIThreadBase), new MockUIThread());
            var optionsService = new MockPythonToolsOptionsService();
            serviceProvider.AddService(typeof(IPythonToolsOptionsService), optionsService, true);

            var ptvsService = new PythonToolsService(serviceProvider);
            serviceProvider.AddService(typeof(PythonToolsService), ptvsService);
            return serviceProvider;
        }

        class LazyComponentGetter<T> : Lazy<T> {
            public LazyComponentGetter(MockComponentModel model) : base(() => (T)model.GetService(typeof(T))) { }
        }

        private static PythonEditorServices CreatePythonEditorServices(IServiceContainer site, MockComponentModel model) {
            var services = new PythonEditorServices(site);

            //services.ComponentModel.DefaultCompositionService.SatisfyImportsOnce(services);
            foreach (var field in services.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                if (!field.GetCustomAttributes().OfType<ImportAttribute>().Any()) {
                    continue;
                }
                if (!field.FieldType.IsGenericType || field.FieldType.GetGenericTypeDefinition() != typeof(Lazy<>)) {
                    field.SetValue(services, model.GetService(field.FieldType));
                } else {
                    var svcType = field.FieldType.GetGenericArguments()[0];
                    var svc = model.GetService(svcType);
                    field.SetValue(
                        services,
                        Activator.CreateInstance(typeof(LazyComponentGetter<>).MakeGenericType(svcType), model)
                    );
                }
            }
            return services;
        }

        private static object CreateTaskProviderService(IServiceContainer container, Type type) {
            var errorProvider = container.GetComponentModel().GetService<IErrorProviderFactory>();
            if (type == typeof(ErrorTaskProvider)) {
                return new ErrorTaskProvider(container, null, errorProvider);
            } else if (type == typeof(CommentTaskProvider)) {
                return new CommentTaskProvider(container, null, errorProvider);
            } else {
                return null;
            }
        }

    }
}
