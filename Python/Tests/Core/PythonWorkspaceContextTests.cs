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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace PythonToolsTests {
    [TestClass]
    public class PythonWorkspaceContextTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void DefaultInterpreter() {
            var data = PrepareWorkspace(WorkspaceTestHelper.PythonNoId);

            var workspaceContext = new PythonWorkspaceContext(data.Workspace, data.OptionsService, data.RegistryService);
            workspaceContext.Initialize();

            var interpreter = workspaceContext.ReadInterpreterSetting();
            Assert.AreEqual(WorkspaceTestHelper.PythonNoId, interpreter);
            Assert.AreEqual(WorkspaceTestHelper.DefaultFactory, workspaceContext.CurrentFactory);
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void InstalledInterpreter() {
            var data = PrepareWorkspace(WorkspaceTestHelper.Python27Id);

            var workspaceContext = new PythonWorkspaceContext(data.Workspace, data.OptionsService, data.RegistryService);
            workspaceContext.Initialize();

            var interpreter = workspaceContext.ReadInterpreterSetting();
            Assert.AreEqual(WorkspaceTestHelper.Python27Id, interpreter);
            Assert.AreEqual(WorkspaceTestHelper.Python27Factory, workspaceContext.CurrentFactory);
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void UnavailableInterpreter() {
            var data = PrepareWorkspace(WorkspaceTestHelper.PythonUnavailableId);

            var workspaceContext = new PythonWorkspaceContext(data.Workspace, data.OptionsService, data.RegistryService);
            workspaceContext.Initialize();

            var interpreter = workspaceContext.ReadInterpreterSetting();
            Assert.AreEqual(WorkspaceTestHelper.PythonUnavailableId, interpreter);
            Assert.AreEqual(WorkspaceTestHelper.DefaultFactory, workspaceContext.CurrentFactory);
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void ChangeInterpreterSetting() {
            var data = PrepareWorkspace(WorkspaceTestHelper.Python27Id);

            var workspaceContext = new PythonWorkspaceContext(data.Workspace, data.OptionsService, data.RegistryService);
            workspaceContext.Initialize();

            var interpreter = workspaceContext.ReadInterpreterSetting();
            Assert.AreEqual(WorkspaceTestHelper.Python27Id, interpreter);
            Assert.AreEqual(WorkspaceTestHelper.Python27Factory, workspaceContext.CurrentFactory);

            using (var activeInterpreterEvent = new AutoResetEvent(false))
            using (var interpreterSettingEvent = new AutoResetEvent(false)) {
                workspaceContext.ActiveInterpreterChanged += (sender, e) => {
                    activeInterpreterEvent.Set();
                };

                workspaceContext.InterpreterSettingChanged += (sender, e) => {
                    interpreterSettingEvent.Set();
                };

                var updatedSettings = new WorkspaceTestHelper.MockWorkspaceSettings(
                    new Dictionary<string, string> { { "Interpreter", WorkspaceTestHelper.Python37Id } }
                );
                data.Workspace.SettingsManager.SimulateChangeSettings(updatedSettings);

                Assert.IsTrue(activeInterpreterEvent.WaitOne(1000), "Failed to raise ActiveInterpreterChanged.");
                Assert.IsTrue(interpreterSettingEvent.WaitOne(1000), "Failed to raise InterpreterSettingChanged.");

                var updatedInterpreter = workspaceContext.ReadInterpreterSetting();
                Assert.AreEqual(WorkspaceTestHelper.Python37Id, updatedInterpreter);
                Assert.AreEqual(WorkspaceTestHelper.Python37Factory, workspaceContext.CurrentFactory);
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void RemoveInterpreterSetting() {
            var data = PrepareWorkspace(WorkspaceTestHelper.Python27Id);

            var workspaceContext = new PythonWorkspaceContext(data.Workspace, data.OptionsService, data.RegistryService);
            workspaceContext.Initialize();

            var interpreter = workspaceContext.ReadInterpreterSetting();
            Assert.AreEqual(WorkspaceTestHelper.Python27Id, interpreter);
            Assert.AreEqual(WorkspaceTestHelper.Python27Factory, workspaceContext.CurrentFactory);

            using (var activeInterpreterEvent = new AutoResetEvent(false))
            using (var interpreterSettingEvent = new AutoResetEvent(false)) {
                workspaceContext.ActiveInterpreterChanged += (sender, e) => {
                    activeInterpreterEvent.Set();
                };

                workspaceContext.InterpreterSettingChanged += (sender, e) => {
                    interpreterSettingEvent.Set();
                };

                var updatedSettings = new WorkspaceTestHelper.MockWorkspaceSettings(
                    new Dictionary<string, string> { { "Interpreter", WorkspaceTestHelper.PythonNoId } }
                );
                data.Workspace.SettingsManager.SimulateChangeSettings(updatedSettings);

                Assert.IsTrue(activeInterpreterEvent.WaitOne(1000), "Failed to raise ActiveInterpreterChanged.");
                Assert.IsTrue(interpreterSettingEvent.WaitOne(1000), "Failed to raise InterpreterSettingChanged.");

                var updatedInterpreter = workspaceContext.ReadInterpreterSetting();
                Assert.AreEqual(WorkspaceTestHelper.PythonNoId, updatedInterpreter);
                Assert.AreEqual(WorkspaceTestHelper.DefaultFactory, workspaceContext.CurrentFactory);
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void RemoveInterpreterSettingAlreadyDefault() {
            var data = PrepareWorkspace(WorkspaceTestHelper.DefaultFactory.Configuration.Id);

            var workspaceContext = new PythonWorkspaceContext(data.Workspace, data.OptionsService, data.RegistryService);
            workspaceContext.Initialize();

            var interpreter = workspaceContext.ReadInterpreterSetting();
            Assert.AreEqual(WorkspaceTestHelper.DefaultFactory.Configuration.Id, interpreter);
            Assert.AreEqual(WorkspaceTestHelper.DefaultFactory, workspaceContext.CurrentFactory);

            using (var activeInterpreterEvent = new AutoResetEvent(false))
            using (var interpreterSettingEvent = new AutoResetEvent(false)) {
                workspaceContext.ActiveInterpreterChanged += (sender, e) => {
                    activeInterpreterEvent.Set();
                };

                workspaceContext.InterpreterSettingChanged += (sender, e) => {
                    interpreterSettingEvent.Set();
                };

                var updatedSettings = new WorkspaceTestHelper.MockWorkspaceSettings(
                    new Dictionary<string, string> { { "Interpreter", WorkspaceTestHelper.PythonNoId } }
                );
                data.Workspace.SettingsManager.SimulateChangeSettings(updatedSettings);

                Assert.IsFalse(activeInterpreterEvent.WaitOne(1000), "Should not have raised ActiveInterpreterChanged.");
                Assert.IsTrue(interpreterSettingEvent.WaitOne(1000), "Failed to raise InterpreterSettingChanged.");

                var updatedInterpreter = workspaceContext.ReadInterpreterSetting();
                Assert.AreEqual(WorkspaceTestHelper.PythonNoId, updatedInterpreter);
                Assert.AreEqual(WorkspaceTestHelper.DefaultFactory, workspaceContext.CurrentFactory);
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void ChangeDefaultInterpreterInUse() {
            var data = PrepareWorkspace(WorkspaceTestHelper.PythonNoId);

            var workspaceContext = new PythonWorkspaceContext(data.Workspace, data.OptionsService, data.RegistryService);
            workspaceContext.Initialize();

            var interpreter = workspaceContext.ReadInterpreterSetting();
            Assert.AreEqual(WorkspaceTestHelper.PythonNoId, interpreter);
            Assert.AreEqual(WorkspaceTestHelper.DefaultFactory, workspaceContext.CurrentFactory);

            using (var activeInterpreterEvent = new AutoResetEvent(false)) {
                workspaceContext.ActiveInterpreterChanged += (sender, e) => {
                    activeInterpreterEvent.Set();
                };

                data.OptionsService.SimulateChangeDefaultInterpreter(WorkspaceTestHelper.Python37Factory);

                Assert.IsTrue(activeInterpreterEvent.WaitOne(1000), "Failed to raise ActiveInterpreterChanged.");

                var updatedInterpreter = workspaceContext.ReadInterpreterSetting();
                Assert.AreEqual(WorkspaceTestHelper.PythonNoId, updatedInterpreter);
                Assert.AreEqual(WorkspaceTestHelper.Python37Factory, workspaceContext.CurrentFactory);
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void ChangeDefaultInterpreterNotInUse() {
            // We don't use the global default
            var data = PrepareWorkspace(WorkspaceTestHelper.Python27Id);

            var workspaceContext = new PythonWorkspaceContext(data.Workspace, data.OptionsService, data.RegistryService);
            workspaceContext.Initialize();

            var interpreter = workspaceContext.ReadInterpreterSetting();
            Assert.AreEqual(WorkspaceTestHelper.Python27Id, interpreter);
            Assert.AreEqual(WorkspaceTestHelper.Python27Factory, workspaceContext.CurrentFactory);

            using (var activeInterpreterEvent = new AutoResetEvent(false)) {
                workspaceContext.ActiveInterpreterChanged += (sender, e) => {
                    activeInterpreterEvent.Set();
                };

                data.OptionsService.SimulateChangeDefaultInterpreter(WorkspaceTestHelper.Python37Factory);

                Assert.IsFalse(activeInterpreterEvent.WaitOne(1000), "Should not have raised ActiveInterpreterChanged.");

                var updatedInterpreter = workspaceContext.ReadInterpreterSetting();
                Assert.AreEqual(WorkspaceTestHelper.Python27Id, updatedInterpreter);
                Assert.AreEqual(WorkspaceTestHelper.Python27Factory, workspaceContext.CurrentFactory);
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void RemoveInterpreterInUse() {
            var data = PrepareWorkspace(WorkspaceTestHelper.Python27Id);

            var workspaceContext = new PythonWorkspaceContext(data.Workspace, data.OptionsService, data.RegistryService);
            workspaceContext.Initialize();

            var interpreter = workspaceContext.ReadInterpreterSetting();
            Assert.AreEqual(WorkspaceTestHelper.Python27Id, interpreter);
            Assert.AreEqual(WorkspaceTestHelper.Python27Factory, workspaceContext.CurrentFactory);

            using (var activeInterpreterEvent = new AutoResetEvent(false)) {
                workspaceContext.ActiveInterpreterChanged += (sender, e) => {
                    activeInterpreterEvent.Set();
                };

                // Uninstall Python 2.7 - in use
                var newFactories = WorkspaceTestHelper.AllFactories.Except(WorkspaceTestHelper.Python27Factory);
                data.RegistryService.SimulateChangeInterpreters(newFactories);

                Assert.IsTrue(activeInterpreterEvent.WaitOne(1000), "Failed to raise ActiveInterpreterChanged.");

                var updatedInterpreter = workspaceContext.ReadInterpreterSetting();
                Assert.AreEqual(WorkspaceTestHelper.Python27Id, updatedInterpreter);
                Assert.AreEqual(WorkspaceTestHelper.DefaultFactory, workspaceContext.CurrentFactory);
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void RemoveInterpreterNotInUse() {
            var data = PrepareWorkspace(WorkspaceTestHelper.Python27Id);

            var workspaceContext = new PythonWorkspaceContext(data.Workspace, data.OptionsService, data.RegistryService);
            workspaceContext.Initialize();

            var interpreter = workspaceContext.ReadInterpreterSetting();
            Assert.AreEqual(WorkspaceTestHelper.Python27Id, interpreter);
            Assert.AreEqual(WorkspaceTestHelper.Python27Factory, workspaceContext.CurrentFactory);

            using (var activeInterpreterEvent = new AutoResetEvent(false)) {
                workspaceContext.ActiveInterpreterChanged += (sender, e) => {
                    activeInterpreterEvent.Set();
                };

                // Uninstall Python 3.7 - not in use
                var newFactories = WorkspaceTestHelper.AllFactories.Except(WorkspaceTestHelper.Python37Factory);
                data.RegistryService.SimulateChangeInterpreters(newFactories);

                Assert.IsFalse(activeInterpreterEvent.WaitOne(1000), "Should not have raised ActiveInterpreterChanged.");

                var updatedInterpreter = workspaceContext.ReadInterpreterSetting();
                Assert.AreEqual(WorkspaceTestHelper.Python27Id, updatedInterpreter);
                Assert.AreEqual(WorkspaceTestHelper.Python27Factory, workspaceContext.CurrentFactory);
            }
        }

        private static TestSetupData PrepareWorkspace(string interpreterSetting) {
            return new TestSetupData {
                OptionsService = new WorkspaceTestHelper.MockOptionsService(WorkspaceTestHelper.DefaultFactory),
                RegistryService = new WorkspaceTestHelper.MockRegistryService(WorkspaceTestHelper.AllFactories),
                Workspace = WorkspaceTestHelper.CreateMockWorkspace(WorkspaceTestHelper.CreateWorkspaceFolder(), interpreterSetting),
            };
        }

        class TestSetupData {
            public WorkspaceTestHelper.MockOptionsService OptionsService { get; set; }

            public WorkspaceTestHelper.MockRegistryService RegistryService { get; set; }

            public WorkspaceTestHelper.MockWorkspace Workspace { get; set; }
        }
    }
}
