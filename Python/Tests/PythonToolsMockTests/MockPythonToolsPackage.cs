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

namespace PythonToolsMockTests
{
    [Export(typeof(IMockPackage))]
    sealed class MockPythonToolsPackage : IMockPackage
    {
        private readonly IServiceContainer _serviceContainer;
        private readonly List<Action> _onDispose = new List<Action>();

        [ImportingConstructor]
        public MockPythonToolsPackage([Import(typeof(SVsServiceProvider))] IServiceContainer serviceProvider)
        {
            _serviceContainer = serviceProvider;
        }

        public void Initialize()
        {
            var settings = (IVsSettingsManager)_serviceContainer.GetService(typeof(SVsSettingsManager));
            IVsWritableSettingsStore store;
            ErrorHandler.ThrowOnFailure(settings.GetWritableSettingsStore((uint)SettingsScope.Configuration, out store));

            _serviceContainer.AddService(typeof(IPythonToolsOptionsService), (sp, t) => new MockPythonToolsOptionsService());
            _serviceContainer.AddService(typeof(IClipboardService), (sp, t) => new MockClipboardService());
            _serviceContainer.AddService(typeof(MockErrorProviderFactory), (sp, t) => new MockErrorProviderFactory(), true);
            _serviceContainer.AddService(typeof(PythonLanguageInfo), (sp, t) => new PythonLanguageInfo(sp), true);
            _serviceContainer.AddService(typeof(PythonToolsService), (sp, t) => new PythonToolsService(sp), true);
            _serviceContainer.AddService(typeof(ErrorTaskProvider), CreateTaskProviderService, true);
            _serviceContainer.AddService(typeof(CommentTaskProvider), CreateTaskProviderService, true);
            _serviceContainer.AddService(typeof(SolutionEventsListener), (sp, t) => new SolutionEventsListener(sp), true);
            _serviceContainer.AddService(typeof(UIThreadBase), new UIThread(null), true);

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

        public void RemoveService(Type type)
        {
            _serviceContainer.RemoveService(type);
        }

        public static bool SuppressTaskProvider { get; set; }

        private static object CreateTaskProviderService(IServiceContainer container, Type type)
        {
            if (SuppressTaskProvider)
            {
                return null;
            }
            if (typeof(ErrorTaskProvider).IsEquivalentTo(type) || typeof(ErrorTaskProvider).GUID == type.GUID)
            {
                return ErrorTaskProvider.CreateService(container, typeof(ErrorTaskProvider));
            }
            else if (typeof(CommentTaskProvider).IsEquivalentTo(type) || typeof(CommentTaskProvider).GUID == type.GUID)
            {
                return CommentTaskProvider.CreateService(container, typeof(CommentTaskProvider));
            }
            else
            {
                return null;
            }
        }

        public void Dispose()
        {
            List<Action> tasks;
            lock (_onDispose)
            {
                tasks = new List<Action>(_onDispose);
                _onDispose.Clear();
            }

            foreach (var t in tasks)
            {
                t();
            }
        }
    }
}
