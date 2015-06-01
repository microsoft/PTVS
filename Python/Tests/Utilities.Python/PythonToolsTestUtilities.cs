/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;
using TestUtilities.Mocks;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow.Commands;
#endif

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

        /// <summary>
        /// Sets up a limited service provider which can be used for testing.  
        /// 
        /// This will not include many of the services which are typically available in
        /// VS but is suitable for simple test cases which need just some base functionality.
        /// </summary>
        public static MockServiceProvider CreateMockServiceProvider() {
            var serviceProvider = new MockServiceProvider();
            var errorProvider = new MockErrorProviderFactory();

            serviceProvider.ComponentModel.AddExtension(
                typeof(IErrorProviderFactory),
                () => new MockErrorProviderFactory()
            );
            serviceProvider.ComponentModel.AddExtension(
                typeof(IContentTypeRegistryService),
                () => new MockClassificationTypeRegistryService()
            );

#if DEV14_OR_LATER
            serviceProvider.ComponentModel.AddExtension(
                typeof(IInteractiveWindowCommandsFactory),
                () => new MockInteractiveWindowCommandsFactory()
            );
#endif

            serviceProvider.AddService(
                typeof(ErrorTaskProvider),
                (container, type) => new ErrorTaskProvider(serviceProvider, null, errorProvider),
                true
            );
            serviceProvider.AddService(
                typeof(CommentTaskProvider),
                (container, type) => new CommentTaskProvider(serviceProvider, null, errorProvider),
                true
            );
            serviceProvider.AddService(typeof(UIThreadBase), new MockUIThread());
            var optionsService = new MockPythonToolsOptionsService();
            serviceProvider.AddService(typeof(IPythonToolsOptionsService), optionsService, true);

            var ptvsService = new PythonToolsService(serviceProvider);
            serviceProvider.AddService(typeof(PythonToolsService), ptvsService);
            return serviceProvider;
        }

    }
}
