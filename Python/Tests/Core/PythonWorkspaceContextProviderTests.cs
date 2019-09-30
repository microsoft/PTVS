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
using System.Threading;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace PythonToolsTests {
    [TestClass]
    public class PythonWorkspaceContextProviderTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void AlreadyOpenedWorkspace() {
            var workspaceFolder = WorkspaceTestHelper.CreateWorkspaceFolder();
            var workspace = WorkspaceTestHelper.CreateMockWorkspace(workspaceFolder, WorkspaceTestHelper.PythonNoId);
            var workspaceService = new WorkspaceTestHelper.MockWorkspaceService(workspace);
            var optionsService = new WorkspaceTestHelper.MockOptionsService(WorkspaceTestHelper.DefaultFactory);
            var registryService = new WorkspaceTestHelper.MockRegistryService(WorkspaceTestHelper.AllFactories);
            var provider = new PythonWorkspaceContextProvider(
                workspaceService,
                new Lazy<IInterpreterOptionsService>(() => optionsService),
                new Lazy<IInterpreterRegistryService>(() => registryService)
            );

            Assert.AreEqual(workspaceFolder, provider.Workspace.Location);
            Assert.AreEqual(WorkspaceTestHelper.DefaultFactory, provider.Workspace.CurrentFactory);
        }

        [TestMethod, Priority(TestExtensions.CORE_UNIT_TEST)]
        public void LoadWorkspace() {
            var workspaceFolder = WorkspaceTestHelper.CreateWorkspaceFolder();
            var workspace = WorkspaceTestHelper.CreateMockWorkspace(workspaceFolder, WorkspaceTestHelper.PythonNoId);
            var workspaceService = new WorkspaceTestHelper.MockWorkspaceService(null);
            var optionsService = new WorkspaceTestHelper.MockOptionsService(WorkspaceTestHelper.DefaultFactory);
            var registryService = new WorkspaceTestHelper.MockRegistryService(WorkspaceTestHelper.AllFactories);
            var provider = new PythonWorkspaceContextProvider(
                workspaceService,
                new Lazy<IInterpreterOptionsService>(() => optionsService),
                new Lazy<IInterpreterRegistryService>(() => registryService)
            );

            Assert.AreEqual(null, provider.Workspace);

            using (var openEvent = new AutoResetEvent(false))
            using (var initEvent = new AutoResetEvent(false))
            using (var closingEvent = new AutoResetEvent(false))
            using (var closedEvent = new AutoResetEvent(false)) {
                provider.WorkspaceOpening += (sender, e) => { openEvent.Set(); };
                provider.WorkspaceInitialized += (sender, e) => { initEvent.Set(); };
                provider.WorkspaceClosed += (sender, e) => { closedEvent.Set(); };
                provider.WorkspaceClosing += (sender, e) => { closingEvent.Set(); };

                workspaceService.SimulateChangeWorkspace(workspace);

                Assert.IsTrue(openEvent.WaitOne(1000), $"Expected {nameof(provider.WorkspaceOpening)}.");
                Assert.IsTrue(initEvent.WaitOne(1000), $"Expected {nameof(provider.WorkspaceInitialized)}.");
                Assert.IsFalse(closingEvent.WaitOne(1000), $"Unexpected {nameof(provider.WorkspaceClosing)}.");
                Assert.IsFalse(closedEvent.WaitOne(1000), $"Unexpected {nameof(provider.WorkspaceClosed)}.");

                Assert.AreEqual(workspaceFolder, provider.Workspace.Location);
                Assert.AreEqual(WorkspaceTestHelper.DefaultFactory, provider.Workspace.CurrentFactory);
            }
        }

        [TestMethod, Priority(TestExtensions.CORE_UNIT_TEST)]
        public void CloseWorkspace() {
            var workspaceFolder = WorkspaceTestHelper.CreateWorkspaceFolder();
            var workspace = WorkspaceTestHelper.CreateMockWorkspace(workspaceFolder, WorkspaceTestHelper.PythonNoId);
            var workspaceService = new WorkspaceTestHelper.MockWorkspaceService(workspace);
            var optionsService = new WorkspaceTestHelper.MockOptionsService(WorkspaceTestHelper.DefaultFactory);
            var registryService = new WorkspaceTestHelper.MockRegistryService(WorkspaceTestHelper.AllFactories);
            var provider = new PythonWorkspaceContextProvider(
                workspaceService,
                new Lazy<IInterpreterOptionsService>(() => optionsService),
                new Lazy<IInterpreterRegistryService>(() => registryService)
            );

            Assert.AreEqual(workspaceFolder, provider.Workspace.Location);
            Assert.AreEqual(WorkspaceTestHelper.DefaultFactory, provider.Workspace.CurrentFactory);

            using (var openEvent = new AutoResetEvent(false))
            using (var initEvent = new AutoResetEvent(false))
            using (var closingEvent = new AutoResetEvent(false))
            using (var closedEvent = new AutoResetEvent(false)) {
                provider.WorkspaceOpening += (sender, e) => { openEvent.Set(); };
                provider.WorkspaceInitialized += (sender, e) => { initEvent.Set();};
                provider.WorkspaceClosed += (sender, e) => { closedEvent.Set(); };
                provider.WorkspaceClosing += (sender, e) => { closingEvent.Set(); };

                workspaceService.SimulateChangeWorkspace(null);

                Assert.IsFalse(openEvent.WaitOne(1000), $"Unexpected {nameof(provider.WorkspaceOpening)}.");
                Assert.IsFalse(initEvent.WaitOne(1000), $"Unexpected {nameof(provider.WorkspaceInitialized)}.");
                Assert.IsTrue(closingEvent.WaitOne(1000), $"Expected {nameof(provider.WorkspaceClosing)}.");
                Assert.IsTrue(closedEvent.WaitOne(1000), $"Expected {nameof(provider.WorkspaceClosed)}.");

                Assert.AreEqual(null, provider.Workspace);
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void SwitchWorkspace() {
            var workspaceFolder1 = WorkspaceTestHelper.CreateWorkspaceFolder();
            var workspaceFolder2 = WorkspaceTestHelper.CreateWorkspaceFolder();
            var workspace1 = WorkspaceTestHelper.CreateMockWorkspace(workspaceFolder1, WorkspaceTestHelper.Python27Id);
            var workspace2 = WorkspaceTestHelper.CreateMockWorkspace(workspaceFolder2, WorkspaceTestHelper.Python37Id);
            var workspaceService = new WorkspaceTestHelper.MockWorkspaceService(workspace1);
            var optionsService = new WorkspaceTestHelper.MockOptionsService(WorkspaceTestHelper.DefaultFactory);
            var registryService = new WorkspaceTestHelper.MockRegistryService(WorkspaceTestHelper.AllFactories);
            var provider = new PythonWorkspaceContextProvider(
                workspaceService,
                new Lazy<IInterpreterOptionsService>(() => optionsService),
                new Lazy<IInterpreterRegistryService>(() => registryService)
            );

            Assert.AreEqual(workspaceFolder1, provider.Workspace.Location);
            Assert.AreEqual(WorkspaceTestHelper.Python27Factory, provider.Workspace.CurrentFactory);

            using (var openEvent = new AutoResetEvent(false))
            using (var initEvent = new AutoResetEvent(false))
            using (var closingEvent = new AutoResetEvent(false))
            using (var closedEvent = new AutoResetEvent(false)) {
                provider.WorkspaceOpening += (sender, e) => { openEvent.Set(); };
                provider.WorkspaceInitialized += (sender, e) => { initEvent.Set(); };
                provider.WorkspaceClosed += (sender, e) => { closedEvent.Set(); };
                provider.WorkspaceClosing += (sender, e) => { closingEvent.Set(); };

                workspaceService.SimulateChangeWorkspace(workspace2);

                Assert.IsTrue(openEvent.WaitOne(1000), $"Expected {nameof(provider.WorkspaceOpening)}.");
                Assert.IsTrue(initEvent.WaitOne(1000), $"Expected {nameof(provider.WorkspaceInitialized)}.");
                Assert.IsTrue(closingEvent.WaitOne(1000), $"Expected {nameof(provider.WorkspaceClosing)}.");
                Assert.IsTrue(closedEvent.WaitOne(1000), $"Expected {nameof(provider.WorkspaceClosed)}.");

                Assert.AreEqual(workspaceFolder2, provider.Workspace.Location);
                Assert.AreEqual(WorkspaceTestHelper.Python37Factory, provider.Workspace.CurrentFactory);
            }
        }
    }
}
