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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class PythonWorkspaceContextTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(0)]
        public void DefaultInterpreter() {
            var data = PrepareWorkspace(WorkspaceTestHelper.PythonNoId);

            var workspaceContext = new PythonWorkspaceContext(data.Workspace, data.OptionsService, data.RegistryService);
            workspaceContext.Initialize();

            var interpreter = workspaceContext.ReadInterpreterSetting();
            Assert.AreEqual(WorkspaceTestHelper.PythonNoId, interpreter);
            Assert.AreEqual(WorkspaceTestHelper.DefaultFactory, workspaceContext.CurrentFactory);
        }

        [TestMethod, Priority(0)]
        public void InstalledInterpreter() {
            var data = PrepareWorkspace(WorkspaceTestHelper.Python27Id);

            var workspaceContext = new PythonWorkspaceContext(data.Workspace, data.OptionsService, data.RegistryService);
            workspaceContext.Initialize();

            var interpreter = workspaceContext.ReadInterpreterSetting();
            Assert.AreEqual(WorkspaceTestHelper.Python27Id, interpreter);
            Assert.AreEqual(WorkspaceTestHelper.Python27Factory, workspaceContext.CurrentFactory);
        }

        [TestMethod, Priority(0)]
        public void UnavailableInterpreter() {
            var data = PrepareWorkspace(WorkspaceTestHelper.PythonUnavailableId);

            var workspaceContext = new PythonWorkspaceContext(data.Workspace, data.OptionsService, data.RegistryService);
            workspaceContext.Initialize();

            var interpreter = workspaceContext.ReadInterpreterSetting();
            Assert.AreEqual(WorkspaceTestHelper.PythonUnavailableId, interpreter);
            Assert.AreEqual(WorkspaceTestHelper.DefaultFactory, workspaceContext.CurrentFactory);
        }

        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
        public void EnumerateWorkspaceFiles() {

            (var testDataSetup, var includedWorkspaceFilePaths) = GenerateWorkspace();
            var workspaceContext = new PythonWorkspaceContext(testDataSetup.Workspace, testDataSetup.OptionsService, testDataSetup.RegistryService);

            TestRegexOne(workspaceContext, includedWorkspaceFilePaths);
            TestRegexTwo(workspaceContext, includedWorkspaceFilePaths);

        }

        private void TestRegexOne(PythonWorkspaceContext workspaceContext, IList<string> includedWorkspaceFilePaths) {
            var testFileFilterRegex = new Regex(@".*\.(py|txt)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var filteredFilePaths = workspaceContext.EnumerateUserFiles((x) => testFileFilterRegex.IsMatch(x)).ToList();

            // Goes through the entire list of included files in the workspace and uses 
            //      the "if" statement to check if the file should be detected by the workspace's EnumerateUserFilesFunction
            // If the file should be detected, it is removed from the filteredFilePaths list. 
            //      If the filteredFilePaths list does not contain the file, it means it failed to detect it and throws an excpetion
            foreach (var filePath in includedWorkspaceFilePaths) {
                var fileName = PathUtils.GetFileOrDirectoryName(filePath).ToLower();

                if ((fileName.EndsWith(".txt") || fileName.EndsWith(".py"))) {
                    Assert.IsTrue(filteredFilePaths.Remove(filePath));
                }
            }

            Assert.AreEqual(filteredFilePaths.Count, 0);
        }

        private void TestRegexTwo(PythonWorkspaceContext workspaceContext, IList<string> includedWorkspaceFilePaths) {
            var testFileFilterRegex = new Regex(@"((^test.*)|(^.*_test))\.(py|txt)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var filteredFilePaths = workspaceContext.EnumerateUserFiles((x) => testFileFilterRegex.IsMatch(x)).ToList();

            foreach (var filePath in includedWorkspaceFilePaths) {
                var fileName = PathUtils.GetFileOrDirectoryName(filePath).ToLower();
                
                if ((fileName.StartsWith("test") && (fileName.EndsWith(".py") || fileName.EndsWith(".txt"))) ||
                    fileName.EndsWith("_test.py") || fileName.EndsWith("_test.txt")) {
                    Assert.IsTrue(filteredFilePaths.Remove(filePath));
                }
            }

            Assert.AreEqual(filteredFilePaths.Count, 0);
        }

        private (TestSetupData, IList<string>) GenerateWorkspace() {
            var virtualEnvName = "virtualEnv";

            var Workspace = WorkspaceTestHelper.CreateMockWorkspace(WorkspaceTestHelper.CreateWorkspaceFolder(),  WorkspaceTestHelper.Python37Id);
            var OptionsService = new WorkspaceTestHelper.MockOptionsService(WorkspaceTestHelper.DefaultFactory);
            var includedWorkspaceFilePaths = GenerateWorkspaceFiles(Workspace.Location, virtualEnvName, out string virtualEnvPath);
            includedWorkspaceFilePaths.Add(Path.Combine(Workspace.Location, "app.py")); //Created by WorkspaceTestHelper.CreateWorkspaceFolder() function which is used above

            var workspaceInterpreterFactories = new List<IPythonInterpreterFactory>();
            workspaceInterpreterFactories.Add(
                new MockPythonInterpreterFactory(
                    new VisualStudioInterpreterConfiguration("Python|3.7", "Fake interpreter 3.7", Path.Combine(Workspace.Location, virtualEnvName), virtualEnvPath)
                )
            );
            var RegistryService = new WorkspaceTestHelper.MockRegistryService(workspaceInterpreterFactories);

            TestSetupData testDataSetup = new TestSetupData {
                OptionsService = OptionsService,
                RegistryService = RegistryService,
                Workspace = Workspace,
            };

            return (testDataSetup, includedWorkspaceFilePaths);

        }

        /// <summary>
        /// Creates a set of files for the workspace which include a fake virtual environment, folders, and files inside all folders. 
        /// /// </summary>
        /// <returns>A list of all the file paths that are are in the workspace but not in execluded folders</returns>
        private IList<string> GenerateWorkspaceFiles(string workspacePath, string virtualEnvName, out string virtualEnvPath) {
            var excludedFolderNames = new List<string>() { virtualEnvName, ".vs" };
            var workspaceFolderNames = new List<string>() { "", "test_folder", "randomFolder", string.Concat(virtualEnvName, "folder") };
            var workspaceFileNames = new List<string>() {
                "something.py", "test.py", "test_foo.py", "foo_test.py", "TEST_SOMETHING.py",
                "something.txt", "testing.txt", "something_test.txt", "somethingtest.txt",
                "testing.pyed", "testdoo.ztxt", "test_foo.xkcd", "something", ".py",
            };

            var includedWorkspaceFilePaths = new List<string>();

            Directory.CreateDirectory(Path.Combine(workspacePath, virtualEnvName, "scripts"));
            virtualEnvPath = Path.Combine(workspacePath, virtualEnvName, "scripts", "python.exe");
            File.AppendAllText(virtualEnvPath, "some text");

            foreach (var directoryName in excludedFolderNames) {
                Directory.CreateDirectory(Path.Combine(workspacePath, directoryName));

                foreach (var fileName in workspaceFileNames) {
                    var filePath = Path.Combine(workspacePath, directoryName, fileName);
                    File.AppendAllText(filePath, "some text");
                }
            }

            foreach (var directoryName in workspaceFolderNames) {
                Directory.CreateDirectory(Path.Combine(workspacePath, directoryName));

                foreach (var fileName in workspaceFileNames) {
                    var filePath = Path.Combine(workspacePath, directoryName, fileName);
                    File.AppendAllText(filePath, "some text");
                    includedWorkspaceFilePaths.Add(filePath);
                }
            }

            return includedWorkspaceFilePaths;
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
