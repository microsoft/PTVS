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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.MockVsTests;
using TestUtilities.Python;

namespace PythonToolsMockTests {
    using TaskProvider = Microsoft.PythonTools.Intellisense.TaskProvider;

    [Export(typeof(IMockPackage))]
    sealed class MockPythonToolsPackage : IMockPackage {
        private readonly IServiceContainer _serviceContainer;
        private readonly List<Action> _onDispose = new List<Action>();

        [ImportingConstructor]
        public MockPythonToolsPackage([Import(typeof(SVsServiceProvider))]IServiceContainer serviceProvider) {
            _serviceContainer = serviceProvider;
        }

        public void Initialize() {
            // Specifiy PythonTools\NoInterpreterFactories to suppress loading
            // all providers in tests.
            var settings = (IVsSettingsManager)_serviceContainer.GetService(typeof(SVsSettingsManager));
            IVsWritableSettingsStore store;
            ErrorHandler.ThrowOnFailure(settings.GetWritableSettingsStore((uint)SettingsScope.Configuration, out store));
            //ErrorHandler.ThrowOnFailure(store.CreateCollection(@"PythonTools\NoInterpreterFactories"));
            
            
            _serviceContainer.AddService(typeof(IPythonToolsOptionsService), new MockPythonToolsOptionsService());
            var errorProvider = new MockErrorProviderFactory();
            _serviceContainer.AddService(typeof(MockErrorProviderFactory), errorProvider, true);
            _serviceContainer.AddService(typeof(IClipboardService), new MockClipboardService());
            UIThread.EnsureService(_serviceContainer);

            _serviceContainer.AddService(typeof(TaskProvider), CreateTaskProviderService, true);

            var pyService = new PythonToolsService(_serviceContainer);
            _onDispose.Add(() => ((IDisposable)pyService).Dispose());
            _serviceContainer.AddService(typeof(PythonToolsService), pyService, true);

            _serviceContainer.AddService(typeof(IPythonLibraryManager), (object)null);

            // register our project factory...
            var regProjectTypes = (IVsRegisterProjectTypes)_serviceContainer.GetService(typeof(SVsRegisterProjectTypes));
            uint cookie;
            var guid = Guid.Parse(PythonConstants.ProjectFactoryGuid);
            regProjectTypes.RegisterProjectType(
                ref guid,
                new PythonProjectFactory(_serviceContainer),
                out cookie
            );
        }

        public void RemoveService(Type type) {
            _serviceContainer.RemoveService(type);
        }

        private static object CreateTaskProviderService(IServiceContainer container, Type type) {
            var errorProvider = container.GetComponentModel().GetService<IErrorProviderFactory>();
            if (type == typeof(TaskProvider)) {
                return new TaskProvider(container, null, errorProvider);
            } else {
                return null;
            }
        }

        public void Dispose() {
            List<Action> tasks;
            lock (_onDispose) {
                tasks = new List<Action>(_onDispose);
                _onDispose.Clear();
            }

            foreach (var t in tasks) {
                t();
            }
        }
    }
}
