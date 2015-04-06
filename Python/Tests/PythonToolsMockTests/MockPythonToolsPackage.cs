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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.MockVsTests;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsMockTests {
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
            ErrorHandler.ThrowOnFailure(store.CreateCollection(@"PythonTools\NoInterpreterFactories"));
            
            
            _serviceContainer.AddService(typeof(IPythonToolsOptionsService), new MockPythonToolsOptionsService());
            var errorProvider = new MockErrorProviderFactory();
            _serviceContainer.AddService(typeof(MockErrorProviderFactory), errorProvider, true);
            _serviceContainer.AddService(typeof(IClipboardService), new MockClipboardService());
            UIThread.EnsureService(_serviceContainer);

            _serviceContainer.AddService(
                typeof(Microsoft.PythonTools.Intellisense.ErrorTaskProvider),
                new ServiceCreatorCallback((container, type) => {
                    var p = new Microsoft.PythonTools.Intellisense.ErrorTaskProvider(_serviceContainer, null, errorProvider);
                    lock (_onDispose) {
                        _onDispose.Add(() => p.Dispose());
                    }
                    return p;
                }), 
                true
            );

            _serviceContainer.AddService(
                typeof(Microsoft.PythonTools.Intellisense.CommentTaskProvider),
                new ServiceCreatorCallback((container, type) => {
                    var p = new Microsoft.PythonTools.Intellisense.CommentTaskProvider(_serviceContainer, null, errorProvider);
                    lock (_onDispose) {
                        _onDispose.Add(() => p.Dispose());
                    }
                    return p;
                }),
                true
            );

            var pyService = new PythonToolsService(_serviceContainer);
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
