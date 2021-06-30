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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CookiecutterTools.Model;
using Microsoft.CookiecutterTools.Telemetry;
using Microsoft.CookiecutterTools.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace CookiecutterTests {
    [TestClass]
    public class CookiecutterViewModelTests {
        private const string GitHubTemplatePath = "https://github.com/audreyr/Cookiecutter-pypackage";
        private const string NoUserConfigFilePath = "";

        private static string LocalTemplatePath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "template");
        private static string UserConfigFilePath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "userconfig.yaml");
        private static string LocalFeedPath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "feed.txt");

        private MockRedirector _redirector;
        private MockGitClient _gitClient;
        private MockGitHubClient _gitHubClient;
        private MockCookiecutterClient _cutterClient;
        private CookiecutterViewModel _vm;
        private MockTemplateSource _installedTemplateSource;
        private MockTemplateSource _gitHubTemplateSource;
        private MockTemplateSource _feedTemplateSource;
        private MockProjectSystemClient _projectSystemClient;
        private CookiecutterTelemetry _telemetry;

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestInitialize]
        public void SetupTest() {
            _redirector = new MockRedirector();
            _gitClient = new MockGitClient();
            _gitHubClient = new MockGitHubClient();
            _cutterClient = new MockCookiecutterClient();
            _installedTemplateSource = new MockTemplateSource();
            _gitHubTemplateSource = new MockTemplateSource();
            _feedTemplateSource = new MockTemplateSource();
            _projectSystemClient = new MockProjectSystemClient();

            var output = TestData.GetTempPath();
            var outputProjectFolder = Path.Combine(output, "project");

            _telemetry = new CookiecutterTelemetry(new TelemetryTestService());
            CookiecutterTelemetry.Initialize(_telemetry.TelemetryService);
            _vm = new CookiecutterViewModel(
                _cutterClient,
                _gitHubClient,
                _gitClient,
                _telemetry,
                _redirector,
                _installedTemplateSource,
                _feedTemplateSource,
                _gitHubTemplateSource,
                null,
                _projectSystemClient
            );
            _vm.UserConfigFilePath = UserConfigFilePath;
            _vm.OutputFolderPath = outputProjectFolder;
        }

        [TestMethod]
        public async Task Search() {
            PopulateInstalledSource();

            await _vm.SearchAsync();
            Assert.IsTrue(_vm.Installed.Templates.Count == 3);

            var log = ((ITelemetryTestSupport)_telemetry.TelemetryService).SessionLog;
            Assert.IsTrue(log.Contains("Test/Cookiecutter/Search/Load"));
        }

        [TestMethod]
        public async Task CheckForUpdates() {
            PopulateInstalledSource();

            _installedTemplateSource.UpdatesAvailable.Add("https://github.com/owner1/template1", true);
            _installedTemplateSource.UpdatesAvailable.Add("https://github.com/owner2/template3", true);

            await _vm.SearchAsync();

            _vm.CreatingStatus = OperationStatus.Succeeded;

            await _vm.CheckForUpdatesAsync();
            Assert.AreEqual(OperationStatus.Succeeded, _vm.CheckingUpdateStatus);

            // Checking for updates shouldn't be clearing the status of other operations, such as create
            Assert.AreEqual(OperationStatus.Succeeded, _vm.CreatingStatus);

            var t1 = _vm.Installed.Templates.OfType<TemplateViewModel>().SingleOrDefault(t => t.RepositoryName == "template1");
            Assert.IsTrue(t1.IsUpdateAvailable);

            var t2 = _vm.Installed.Templates.OfType<TemplateViewModel>().SingleOrDefault(t => t.RepositoryName == "template2");
            Assert.IsFalse(t2.IsUpdateAvailable);

            var t3 = _vm.Installed.Templates.OfType<TemplateViewModel>().SingleOrDefault(t => t.RepositoryName == "template3");
            Assert.IsTrue(t3.IsUpdateAvailable);
        }

        [TestMethod]
        public async Task SearchClearsStatus() {
            _vm.CloningStatus = OperationStatus.Failed;
            _vm.LoadingStatus = OperationStatus.Failed;
            _vm.CreatingStatus = OperationStatus.Failed;
            _vm.CheckingUpdateStatus = OperationStatus.Succeeded;
            _vm.UpdatingStatus = OperationStatus.Succeeded;

            await _vm.SearchAsync();

            // Search should clear operation status, except for update
            // which happen in the background / on a timer.
            Assert.AreEqual(OperationStatus.NotStarted, _vm.CloningStatus);
            Assert.AreEqual(OperationStatus.NotStarted, _vm.LoadingStatus);
            Assert.AreEqual(OperationStatus.NotStarted, _vm.CreatingStatus);
            Assert.AreEqual(OperationStatus.Succeeded, _vm.CheckingUpdateStatus);
            Assert.AreEqual(OperationStatus.Succeeded, _vm.UpdatingStatus);
        }

        [TestMethod]
        public async Task UpdateTemplate() {
            PopulateInstalledSource();

            _installedTemplateSource.UpdatesAvailable.Add("https://github.com/owner1/template1", true);

            await _vm.SearchAsync();

            var t1 = _vm.Installed.Templates.OfType<TemplateViewModel>().SingleOrDefault(t => t.RepositoryName == "template1");
            _vm.SelectedTemplate = t1;

            await _vm.UpdateTemplateAsync();
            Assert.AreEqual(OperationStatus.Succeeded, _vm.UpdatingStatus);
            Assert.IsFalse(t1.IsUpdateAvailable);

            CollectionAssert.AreEquivalent(
                new string[] { Path.Combine(_vm.InstalledFolderPath, "template1") },
                _installedTemplateSource.Updated);
        }

        private static readonly ContextItem[] itemsWithValueSources = new ContextItem[] {
            new ContextItem("is_new_item", Selectors.String, string.Empty) { ValueSource=KnownValueSources.IsNewItem },
            new ContextItem("is_new_project", Selectors.String, string.Empty) { ValueSource=KnownValueSources.IsNewProject },
            new ContextItem("is_from_project_wizard", Selectors.String, string.Empty) { ValueSource=KnownValueSources.IsFromProjectWizard },
            new ContextItem("project_name", Selectors.String, string.Empty) { ValueSource=KnownValueSources.ProjectName, Visible=false },
        };

        private static readonly DteCommand[] commandsWithOpenProject = new DteCommand[] {
            new DteCommand("File.OpenProject", "{{cookiecutter._output_folder_path}}\\{{cookiecutter.project_name}}.pyproj"),
        };

        private static readonly TemplateContext contextWithValueSourcesOpenFolder = new TemplateContext(
            itemsWithValueSources
        );

        private static readonly TemplateContext contextWithValueSourcesOpenProject = new TemplateContext(
            itemsWithValueSources,
            commandsWithOpenProject
        );

        [TestMethod]
        public async Task ContextItemsFromProjectWizardOpenFolder() {
            _projectSystemClient.IsSolutionOpen = false;
            _vm.TargetProjectLocation = null;
            _vm.ProjectName = "TestProjectName";
            _vm.InitializeContextItems(contextWithValueSourcesOpenFolder);

            var expected = new ContextItemViewModel[] {
                new ContextItemViewModel() { Name="is_new_item", Selector=Selectors.String, Val="n" },
                new ContextItemViewModel() { Name="is_new_project", Selector=Selectors.String, Val="y" },
                new ContextItemViewModel() { Name="is_from_project_wizard", Selector=Selectors.String, Val="y" },
                new ContextItemViewModel() { Name="project_name", Selector=Selectors.String, Val="TestProjectName", Visible=false },
            };

            CollectionAssert.AreEqual(expected, _vm.ContextItems, new ContextItemViewModelComparer());

            Assert.AreEqual(PostCreateAction.OpenFolder, _vm.PostCreate);
            Assert.IsFalse(_vm.HasPostCommands);
            Assert.IsFalse(_vm.AddingToExistingProject);
        }

        [TestMethod]
        public async Task ContextItemsFromProjectWizardOpenProject() {
            _projectSystemClient.IsSolutionOpen = false;
            _vm.TargetProjectLocation = null;
            _vm.ProjectName = "TestProjectName";
            _vm.InitializeContextItems(contextWithValueSourcesOpenProject);

            var expected = new ContextItemViewModel[] {
                new ContextItemViewModel() { Name="is_new_item", Selector=Selectors.String, Val="n" },
                new ContextItemViewModel() { Name="is_new_project", Selector=Selectors.String, Val="y" },
                new ContextItemViewModel() { Name="is_from_project_wizard", Selector=Selectors.String, Val="y" },
                new ContextItemViewModel() { Name="project_name", Selector=Selectors.String, Val="TestProjectName", Visible=false },
            };

            CollectionAssert.AreEqual(expected, _vm.ContextItems, new ContextItemViewModelComparer());

            Assert.AreEqual(PostCreateAction.OpenProject, _vm.PostCreate);
            Assert.IsTrue(_vm.HasPostCommands);
            Assert.IsFalse(_vm.AddingToExistingProject);
        }

        [TestMethod]
        public async Task ContextItemsAddNewProject() {
            _projectSystemClient.IsSolutionOpen = true;
            _vm.TargetProjectLocation = null;
            _vm.ProjectName = null;
            _vm.InitializeContextItems(contextWithValueSourcesOpenProject);

            var expected = new ContextItemViewModel[] {
                new ContextItemViewModel() { Name="is_new_item", Selector=Selectors.String, Val="n" },
                new ContextItemViewModel() { Name="is_new_project", Selector=Selectors.String, Val="y" },
                new ContextItemViewModel() { Name="is_from_project_wizard", Selector=Selectors.String, Val="n" },
                new ContextItemViewModel() { Name="project_name", Selector=Selectors.String, Val="", Visible=false },
            };

            CollectionAssert.AreEqual(expected, _vm.ContextItems, new ContextItemViewModelComparer());
            Assert.AreEqual(PostCreateAction.AddToSolution, _vm.PostCreate);
            Assert.IsTrue(_vm.HasPostCommands);
            Assert.IsFalse(_vm.AddingToExistingProject);
        }

        [TestMethod]
        public async Task ContextItemsAddNewItem() {
            _projectSystemClient.IsSolutionOpen = true;
            _vm.TargetProjectLocation = new ProjectLocation() {
                ProjectUniqueName = "TestProject",
                ProjectKind = "{EB4B2D97-897B-4A9B-926F-38D7FAAAF399}",
                FolderPath = "C:\\TestProject",
            };
            _vm.ProjectName = null;
            _vm.InitializeContextItems(contextWithValueSourcesOpenProject);

            var expected = new ContextItemViewModel[] {
                new ContextItemViewModel() { Name="is_new_item", Selector=Selectors.String, Val="y" },
                new ContextItemViewModel() { Name="is_new_project", Selector=Selectors.String, Val="n" },
                new ContextItemViewModel() { Name="is_from_project_wizard", Selector=Selectors.String, Val="n" },
                new ContextItemViewModel() { Name="project_name", Selector=Selectors.String, Val="", Visible=false },
            };

            CollectionAssert.AreEqual(expected, _vm.ContextItems, new ContextItemViewModelComparer());
            Assert.AreEqual(PostCreateAction.AddToProject, _vm.PostCreate);
            Assert.IsTrue(_vm.HasPostCommands);
            Assert.IsTrue(_vm.AddingToExistingProject);
        }

        private void PopulateInstalledSource() {
            _installedTemplateSource.Templates.Add(
                Tuple.Create((string)null, (string)null),
                Tuple.Create(new Template[] {
                    new Template() {
                        Name = "owner1/template1",
                        LocalFolderPath = Path.Combine(_vm.InstalledFolderPath, "template1"),
                        RemoteUrl = "https://github.com/owner1/template1",
                        Description = string.Empty },
                    new Template() {
                        Name = "owner1/template2",
                        LocalFolderPath = Path.Combine(_vm.InstalledFolderPath, "template2"),
                        RemoteUrl = "https://github.com/owner1/template2",
                        Description = string.Empty },
                    new Template() {
                        Name = "owner2/template3",
                        LocalFolderPath = Path.Combine(_vm.InstalledFolderPath, "template3"),
                        RemoteUrl = "https://github.com/owner2/template3",
                        Description = string.Empty },
                }, (string)null));
        }
    }
}
