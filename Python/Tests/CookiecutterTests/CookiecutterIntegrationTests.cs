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

namespace CookiecutterTests
{
	[TestClass]
	public class CookiecutterIntegrationTests
	{
		private const string GitHubTemplatePath = "https://github.com/audreyr/Cookiecutter-pypackage";
		private const string NoUserConfigFilePath = "";

		private const string OnlineNotTemplateUrl = "https://github.com/Microsoft/PTVS";
		private const string OnlineNonExistingUrl = "https://github.com/Microsoft/---";
		private const string OnlineTemplateUrl = "https://github.com/brettcannon/python-azure-web-app-cookiecutter";
		private const string OnlineTemplateRepoFullName = "brettcannon/python-azure-web-app-cookiecutter";
		private const string OnlineTemplateRepoOwner = "brettcannon";
		private const string OnlineTemplateRepoName = "python-azure-web-app-cookiecutter";

		private static string NonExistingLocalTemplatePath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "notemplate");
		private static string TestLocalTemplatePath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "template");
		private static string TestLocalTemplateOpenTxtPath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "template_opentxt");
		private static string TestLocalTemplateOpenTxtWithEditorPath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "template_opentxtwitheditor");
		private static string TestInstalledTemplateFolderPath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "installed");
		private static string TestUserConfigFilePath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "userconfig.yaml");
		private static string TestFeedPath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "feed.txt");

		private MockRedirector _redirector;
		private IGitClient _gitClient;
		private IGitHubClient _gitHubClient;
		private ICookiecutterClient _cutterClient;
		private ICookiecutterTelemetry _telemetry;
		private CookiecutterViewModel _vm;
		private ILocalTemplateSource _installedTemplateSource;
		private ITemplateSource _gitHubTemplateSource;
		private ITemplateSource _feedTemplateSource;
		private MockProjectSystemClient _projectSystemClient;
		private string _openedFolder;
		private string _openedFileArgs;

		private string DefaultBasePath => ((CookiecutterClient)_cutterClient)?.DefaultBasePath;

		internal static ContextItemViewModel[] LocalTemplateWithUserConfigContextItems { get; } = new ContextItemViewModel[] {
				new ContextItemViewModel("full_name", Selectors.String, null, null, null, "Configured User"),
				new ContextItemViewModel("email", Selectors.String, null, null, null, "configured@email"),
				new ContextItemViewModel("github_username", Selectors.String, null, null, null, "configuredgithubuser"),
				new ContextItemViewModel("project_name", Selectors.String, null, null, null, "Default Project Name"),
				new ContextItemViewModel("project_slug", Selectors.String, null, null, null, "{{ cookiecutter.project_name.lower().replace(' ', '_') }}"),
				new ContextItemViewModel("pypi_username", Selectors.String, null, null, null, "{{ cookiecutter.github_username }}"),
				new ContextItemViewModel("version", Selectors.String, null, null, null, "0.1.0"),
				new ContextItemViewModel("use_azure", Selectors.String, null, null, null, "y"),
				new ContextItemViewModel("open_source_license", Selectors.List, null, null, null, "BSD license", items: new string[] { "MIT license", "BSD license", "ISC license", "Apache Software License 2.0", "GNU General Public License v3", "Not open source" }),
				new ContextItemViewModel("port", Selectors.String, null, null, null, "5000"),
                // Note that _copy_without_render item should not appear
        };

		[ClassInitialize]
		public static void DoDeployment(TestContext context)
		{
			AssertListener.Initialize();
		}

		[TestInitialize]
		public void SetupTest()
		{
			_redirector = new MockRedirector();

			var output = TestData.GetTempPath();
			var outputProjectFolder = Path.Combine(output, "integration");
			var feedUrl = new Uri(TestFeedPath);
			var installedPath = TestInstalledTemplateFolderPath;
			var userConfigFilePath = TestUserConfigFilePath;

			_gitClient = GitClientProvider.Create(_redirector, null);
			_gitHubClient = new GitHubClient();
			_cutterClient = CookiecutterClientProvider.Create(null, _redirector);
			_telemetry = new CookiecutterTelemetry(new TelemetryTestService());
			CookiecutterTelemetry.Initialize(_telemetry.TelemetryService);
			_installedTemplateSource = new LocalTemplateSource(installedPath, _gitClient);
			_gitHubTemplateSource = new GitHubTemplateSource(_gitHubClient);
			_feedTemplateSource = new FeedTemplateSource(feedUrl);
			_projectSystemClient = new MockProjectSystemClient();

			_vm = new CookiecutterViewModel(
				_cutterClient,
				_gitHubClient,
				_gitClient,
				_telemetry,
				_redirector,
				_installedTemplateSource,
				_feedTemplateSource,
				_gitHubTemplateSource,
				ExecuteCommand,
				_projectSystemClient
			)
			{
				UserConfigFilePath = userConfigFilePath
			};
			((CookiecutterClient)_cutterClient).DefaultBasePath = outputProjectFolder;
		}

		private void ExecuteCommand(string name, string args)
		{
			if (name == "File.OpenFolder")
			{
				_openedFolder = args.Trim('"');
			}
			else if (name == "File.OpenFile")
			{
				_openedFileArgs = args;
			}
		}

		[TestMethod]
		public async Task Search()
		{
			await _vm.SearchAsync();

			Assert.AreEqual(1, _vm.Installed.Templates.Count);
			Assert.AreEqual(6, _vm.Recommended.Templates.Count);

			// For GitHub results, check for a range since the exact count is bit inconsistent.
			Assert.IsTrue(_vm.GitHub.Templates.Count > 20 && _vm.GitHub.Templates.Count < 32);
			Assert.AreEqual(_vm.GitHub.Templates.Count - 1, _vm.GitHub.Templates.OfType<TemplateViewModel>().Count());
			Assert.AreEqual(1, _vm.GitHub.Templates.OfType<ContinuationViewModel>().Count());

			var continuationVM = _vm.GitHub.Templates.Last() as ContinuationViewModel;
			Assert.IsNotNull(continuationVM);
			Assert.IsFalse(string.IsNullOrEmpty(continuationVM.ContinuationToken));

			await _vm.LoadMoreTemplatesAsync(continuationVM.ContinuationToken);
			Assert.IsTrue(_vm.GitHub.Templates.Count > 40 && _vm.GitHub.Templates.Count < 64);
			Assert.AreEqual(_vm.GitHub.Templates.Count - 1, _vm.GitHub.Templates.OfType<TemplateViewModel>().Count());
			Assert.AreEqual(1, _vm.GitHub.Templates.OfType<ContinuationViewModel>().Count());

			// The old "Load more" will be removed, but another one will be added after the new batch of results
			CollectionAssert.DoesNotContain(_vm.GitHub.Templates, continuationVM);
			continuationVM = _vm.GitHub.Templates.Last() as ContinuationViewModel;
			Assert.IsNotNull(continuationVM);

			var log = ((ITelemetryTestSupport)_telemetry.TelemetryService).SessionLog;
			Assert.IsTrue(log.Contains("Test/Cookiecutter/Search/Load"));
			Assert.IsTrue(log.Contains("Test/Cookiecutter/Search/More"));
		}

		[TestMethod]
		public async Task SearchLocalTemplate()
		{
			_vm.SearchTerm = TestLocalTemplatePath;
			await _vm.SearchAsync();

			Assert.AreEqual(0, _vm.Installed.Templates.Count);
			Assert.AreEqual(0, _vm.GitHub.Templates.Count);
			Assert.AreEqual(0, _vm.Recommended.Templates.Count);
			Assert.AreEqual(1, _vm.Custom.Templates.Count);

			var template = _vm.Custom.Templates[0] as TemplateViewModel;
			PrintTemplate(template);

			Assert.AreEqual(TestLocalTemplatePath, template.DisplayName);
			Assert.AreEqual(TestLocalTemplatePath, template.ClonedPath);
			Assert.IsTrue(string.IsNullOrEmpty(template.Description));
			Assert.IsTrue(string.IsNullOrEmpty(template.RemoteUrl));
		}

		[TestMethod]
		public async Task SearchNonExistingLocalTemplate()
		{
			_vm.SearchTerm = NonExistingLocalTemplatePath;
			await _vm.SearchAsync();

			Assert.AreEqual(0, _vm.Installed.Templates.Count);
			Assert.AreEqual(0, _vm.GitHub.Templates.Where(t => t.GetType() != typeof(ErrorViewModel)).Count());
			Assert.AreEqual(0, _vm.Recommended.Templates.Count);
			Assert.AreEqual(0, _vm.Custom.Templates.Count);
		}

		[TestMethod]
		public async Task SearchOnlineTemplate()
		{
			_vm.SearchTerm = OnlineTemplateUrl;
			await _vm.SearchAsync();

			Assert.AreEqual(0, _vm.Installed.Templates.Count);
			Assert.AreEqual(0, _vm.GitHub.Templates.Count);
			Assert.AreEqual(0, _vm.Recommended.Templates.Count);
			Assert.AreEqual(1, _vm.Custom.Templates.Count);

			var template = _vm.Custom.Templates[0] as TemplateViewModel;
			PrintTemplate(template);

			Assert.AreEqual(OnlineTemplateUrl, template.DisplayName);
			Assert.AreEqual(OnlineTemplateRepoOwner, template.RepositoryOwner);
			Assert.AreEqual(OnlineTemplateRepoName, template.RepositoryName);
			Assert.AreEqual(OnlineTemplateRepoFullName, template.RepositoryFullName);
			Assert.AreEqual(OnlineTemplateUrl, template.RemoteUrl);
			Assert.IsTrue(string.IsNullOrEmpty(template.Description));
			Assert.IsTrue(string.IsNullOrEmpty(template.ClonedPath));
		}

		[TestMethod]
		public async Task SearchOnlineNotTemplate()
		{
			// Right now, we don't validate that it's a cookiecutter template for user entered url
			_vm.SearchTerm = OnlineNotTemplateUrl;
			await _vm.SearchAsync();

			Assert.AreEqual(0, _vm.Installed.Templates.Count);
			Assert.AreEqual(0, _vm.GitHub.Templates.Count);
			Assert.AreEqual(0, _vm.Recommended.Templates.Count);
			Assert.AreEqual(1, _vm.Custom.Templates.Count);
		}

		[TestMethod]
		public async Task SearchOnlineNonExistingUrl()
		{
			// Right now, we don't validate that it's an existing url
			_vm.SearchTerm = OnlineNonExistingUrl;
			await _vm.SearchAsync();

			Assert.AreEqual(0, _vm.Installed.Templates.Count);
			Assert.AreEqual(0, _vm.GitHub.Templates.Count);
			Assert.AreEqual(0, _vm.Recommended.Templates.Count);
			Assert.AreEqual(1, _vm.Custom.Templates.Count);
		}

		[TestMethod]
		public async Task CreateFromLocalTemplate()
		{
			await EnsureCookiecutterInstalledAsync();

			await LoadLocalTemplate(TestLocalTemplatePath);
			CollectionAssert.AreEqual(LocalTemplateWithUserConfigContextItems, _vm.ContextItems, new ContextItemViewModelComparer());

			_vm.ContextItems.Single(item => item.Name == "full_name").Val = "Integration Test User";
			_vm.ContextItems.Single(item => item.Name == "open_source_license").Val = "Apache Software License 2.0";

			var targetPath = _vm.OutputFolderPath;
			Assert.IsTrue(Path.IsPathRooted(targetPath), "{0} is not a full path".FormatInvariant(targetPath));
			Assert.IsTrue(PathUtils.IsSubpathOf(DefaultBasePath, targetPath),
				"{0} is not in the {1} folder".FormatInvariant(targetPath, DefaultBasePath));

			try
			{
				await _vm.CreateFilesAsync();
				Assert.AreEqual(OperationStatus.Succeeded, _vm.CreatingStatus);
				VerifyLocalTemplateReport(
					fullNameOverride: "Integration Test User",
					licenseOverride: "Apache Software License 2.0"
				);
			}
			finally
			{
				FileUtils.DeleteDirectory(targetPath);
			}
		}

		[TestMethod]
		public async Task CreateFromLocalTemplateWithCommand()
		{
			await EnsureCookiecutterInstalledAsync();

			await LoadLocalTemplate(TestLocalTemplateOpenTxtPath);
			Assert.AreEqual(true, _vm.HasPostCommands);
			Assert.AreEqual(true, _vm.ShouldExecutePostCommands);

			var targetPath = _vm.OutputFolderPath;
			try
			{
				await _vm.CreateFilesAsync();
			}
			finally
			{
				FileUtils.DeleteDirectory(targetPath);
			}

			var expectedArgs = string.Format("\"{0}\"", Path.Combine(targetPath, "read me.txt"));
			Assert.AreEqual(expectedArgs, _openedFileArgs);
		}

		[TestMethod]
		public async Task CreateFromLocalTemplateWithCommandSwitchValue()
		{
			await EnsureCookiecutterInstalledAsync();

			await LoadLocalTemplate(TestLocalTemplateOpenTxtWithEditorPath);
			Assert.AreEqual(true, _vm.HasPostCommands);
			Assert.AreEqual(true, _vm.ShouldExecutePostCommands);

			var targetPath = _vm.OutputFolderPath;
			try
			{
				await _vm.CreateFilesAsync();
			}
			finally
			{
				FileUtils.DeleteDirectory(targetPath);
			}

			var expectedArgs = string.Format("\"{0}\" /e:\"Source Code (text) Editor\"", Path.Combine(targetPath, "read me.txt"));
			Assert.AreEqual(expectedArgs, _openedFileArgs);
		}

		[TestMethod]
		public async Task CreateFromLocalTemplateWithCommandSkipped()
		{
			await EnsureCookiecutterInstalledAsync();

			await LoadLocalTemplate(TestLocalTemplateOpenTxtPath);
			Assert.AreEqual(true, _vm.HasPostCommands);
			Assert.AreEqual(true, _vm.ShouldExecutePostCommands);

			_vm.ShouldExecutePostCommands = false;

			var targetPath = _vm.OutputFolderPath;
			try
			{
				await _vm.CreateFilesAsync();
			}
			finally
			{
				FileUtils.DeleteDirectory(targetPath);
			}

			Assert.AreEqual(null, _openedFileArgs);
		}

		[TestMethod]
		public async Task AddFromLocalTemplate()
		{
			await EnsureCookiecutterInstalledAsync();

			var targetPath = ((CookiecutterClient)_cutterClient).DefaultBasePath;
			Directory.CreateDirectory(targetPath);
			try
			{
				await AddFromTemplateAsync(targetPath, "Project1/Project1", "MockProjectKind", TestLocalTemplatePath);

				var addedLocation = _projectSystemClient.Added[0].Item1;
				Assert.AreEqual(targetPath, addedLocation.FolderPath);
				Assert.AreEqual("Project1/Project1", addedLocation.ProjectUniqueName);

				var createdFiles = _projectSystemClient.Added[0].Item2;
				CollectionAssert.AreEquivalent(
					createdFiles.FilesCreated,
					new string[] {
						"report.txt",
						"media\\test.bmp"
					}
				);
				CollectionAssert.AreEquivalent(
					createdFiles.FoldersCreated,
					new string[] {
						"media"
					}
				);
				Assert.AreEqual(0, createdFiles.FilesReplaced.Length);

				// Check the contents of the generated files
				VerifyLocalTemplateReport();

				var log = ((ITelemetryTestSupport)_telemetry.TelemetryService).SessionLog;

				Assert.IsTrue(log.Contains("Test/Cookiecutter/Template/AddToProject"));
				Assert.IsTrue(log.Contains("MockProjectKind"));

			}
			finally
			{
				FileUtils.DeleteDirectory(targetPath);
			}
		}

		[TestMethod]
		public async Task AddFromLocalTemplateReplaceAndBackup()
		{
			await EnsureCookiecutterInstalledAsync();

			var targetPath = ((CookiecutterClient)_cutterClient).DefaultBasePath;
			Directory.CreateDirectory(targetPath);
			try
			{
				// Create some existing files under the output folder to force a
				// backup of those existing files by cookiecutter client before
				// it replaces them.
				Directory.CreateDirectory(Path.Combine(targetPath, "media"));

				var oldReportContent = "this report.txt will be overwritten";
				var oldMediaContent = "this test.bmp will be overwritten";

				File.WriteAllText(Path.Combine(targetPath, "report.txt"), oldReportContent);
				File.WriteAllText(Path.Combine(targetPath, "media", "test.bmp"), oldMediaContent);

				await AddFromTemplateAsync(targetPath, "Project1/Project1", "MockProjectKind", TestLocalTemplatePath);

				var addedLocation = _projectSystemClient.Added[0].Item1;
				Assert.AreEqual(targetPath, addedLocation.FolderPath);
				Assert.AreEqual("Project1/Project1", addedLocation.ProjectUniqueName);

				var createdFiles = _projectSystemClient.Added[0].Item2;
				CollectionAssert.AreEquivalent(
					createdFiles.FilesCreated,
					new string[] {
						"report.txt",
						"media\\test.bmp"
					}
				);
				CollectionAssert.AreEquivalent(
					createdFiles.FoldersCreated,
					new string[] {
						"media"
					}
				);
				CollectionAssert.AreEqual(
					createdFiles.FilesReplaced,
					new ReplacedFile[] {
						new ReplacedFile("report.txt", "report.bak.txt"),
						new ReplacedFile("media\\test.bmp", "media\\test.bak.bmp"),
					},
					new ReplacedFileComparer()
				);

				// Check that the contents of the backup files is as expected
				Assert.AreEqual(oldReportContent, File.ReadAllText(Path.Combine(targetPath, "report.bak.txt")));
				Assert.AreEqual(oldMediaContent, File.ReadAllText(Path.Combine(targetPath, "media", "test.bak.bmp")));

				// Check the contents of the generated files
				VerifyLocalTemplateReport();
			}
			finally
			{
				FileUtils.DeleteDirectory(targetPath);
			}
		}

		private async Task AddFromTemplateAsync(string targetPath, string projectUniqueName, string projectKind, string templateLocation)
		{
			_vm.OutputFolderPath = targetPath;
			_vm.TargetProjectLocation = new ProjectLocation() { FolderPath = targetPath, ProjectUniqueName = projectUniqueName, ProjectKind = projectKind };
			_vm.FixedOutputFolder = true;
			_vm.SearchTerm = templateLocation;
			await _vm.SearchAsync();

			var template = _vm.Custom.Templates[0] as TemplateViewModel;
			await _vm.SelectTemplateAsync(template);
			await _vm.LoadTemplateAsync();

			await _vm.CreateFilesAsync();

			Assert.AreEqual(OperationStatus.Succeeded, _vm.CreatingStatus);

			// Check that we're calling the project system to add the files we generated
			Assert.AreEqual(1, _projectSystemClient.Added.Count);

			// Output folder should not have been changed automatically, since we specified it was a fixed path
			Assert.AreEqual(targetPath, _vm.OutputFolderPath);
		}

		[TestMethod]
		public async Task CreateFromOnlineTemplate()
		{
			await EnsureCookiecutterInstalledAsync();

			_vm.SearchTerm = OnlineTemplateUrl;
			await _vm.SearchAsync();

			var template = _vm.Custom.Templates[0] as TemplateViewModel;
			await _vm.SelectTemplateAsync(template);

			Assert.IsNotNull(_vm.SelectedImage);
			Assert.IsFalse(string.IsNullOrEmpty(_vm.SelectedDescription));

			await _vm.LoadTemplateAsync();

			// Online template needs to be cloned
			Assert.AreEqual(OperationStatus.Succeeded, _vm.CloningStatus);

			PrintContextItems(_vm.ContextItems);

			_vm.ContextItems.Single(item => item.Name == "static_assets_directory").Val = "static_files";
			var targetPath = _vm.OutputFolderPath;
			Assert.IsTrue(Path.IsPathRooted(targetPath), "{0} is not a full path".FormatInvariant(targetPath));
			Assert.IsTrue(PathUtils.IsSubpathOf(DefaultBasePath, targetPath),
				"{0} is not in the {1} folder".FormatInvariant(targetPath, DefaultBasePath));

			try
			{
				await _vm.CreateFilesAsync();

				Assert.AreEqual(OperationStatus.Succeeded, _vm.CreatingStatus);

				Assert.IsTrue(Directory.Exists(Path.Combine(targetPath, "static_files")));
				Assert.IsTrue(Directory.Exists(Path.Combine(targetPath, "post-deployment")));
				Assert.IsTrue(File.Exists(Path.Combine(targetPath, "web.config")));
				Assert.IsTrue(File.Exists(Path.Combine(targetPath, "static_files", "web.config")));
				Assert.IsTrue(File.Exists(Path.Combine(targetPath, "post-deployment", "install-requirements.ps1")));
				Assert.IsFalse(File.Exists(Path.Combine(targetPath, "install-requirements.ps1")));

				var log = ((ITelemetryTestSupport)_telemetry.TelemetryService).SessionLog;

				Assert.IsTrue(log.Contains("Test/Cookiecutter/Search/Load"));
				Assert.IsTrue(log.Contains("Test/Cookiecutter/Template/Clone"));
				Assert.IsTrue(log.Contains("Test/Cookiecutter/Template/Load"));
				Assert.IsTrue(log.Contains("Test/Cookiecutter/Template/Run"));

				Assert.IsFalse(log.Contains(template.Description));
				Assert.IsFalse(log.Contains(template.ClonedPath));

				Assert.IsTrue(log.Contains(PII(template.RemoteUrl)));
				Assert.IsTrue(log.Contains(PII(template.RepositoryFullName)));
				Assert.IsTrue(log.Contains(PII(template.RepositoryName)));
				Assert.IsTrue(log.Contains(PII(template.RepositoryOwner)));

				Assert.AreEqual(targetPath, _openedFolder);
			}
			finally
			{
				FileUtils.DeleteDirectory(targetPath);
			}
		}

		[TestMethod]
		public async Task InstallFromOnlineMultipleTimes()
		{
			await EnsureCookiecutterInstalledAsync();

			for (int i = 0; i < 3; i++)
			{
				_vm.SearchTerm = OnlineTemplateUrl;
				await _vm.SearchAsync();

				var template = _vm.Custom.Templates[0] as TemplateViewModel;
				await _vm.SelectTemplateAsync(template);
				await _vm.LoadTemplateAsync();
				Assert.AreEqual(OperationStatus.Succeeded, _vm.CloningStatus);
			}

			_vm.SearchTerm = string.Empty;
			await _vm.SearchAsync();

			// After cloning the same template multiple times, make sure it only appears once in the installed section
			var installed = _vm.Installed.Templates.OfType<TemplateViewModel>().Where(t => t.DisplayName == OnlineTemplateRepoName).ToArray();
			Assert.AreEqual(1, installed.Length);
		}

		private async Task LoadLocalTemplate(string templatePath)
		{
			_vm.SearchTerm = templatePath;
			await _vm.SearchAsync();

			var template = _vm.Custom.Templates[0] as TemplateViewModel;
			await _vm.SelectTemplateAsync(template);

			Assert.IsNull(_vm.SelectedImage);
			Assert.IsTrue(string.IsNullOrEmpty(_vm.SelectedDescription));

			await _vm.LoadTemplateAsync();

			// Local template doesn't need to be cloned
			Assert.AreEqual(OperationStatus.NotStarted, _vm.CloningStatus);

			PrintContextItems(_vm.ContextItems);
		}

		private void VerifyLocalTemplateReport(string fullNameOverride = null, string licenseOverride = null)
		{
			var reportFilePath = Path.Combine(_vm.OutputFolderPath, "report.txt");
			Assert.IsTrue(File.Exists(reportFilePath), "Failed to generate some project files.");
			var report = CookiecutterClientTests.ReadReport(reportFilePath);

			var expected = new Dictionary<string, string>() {
				{ "full_name", fullNameOverride ?? "Configured User" },
				{ "email", "configured@email" },
				{ "github_username", "configuredgithubuser" },
				{ "project_name", "Default Project Name" },
				{ "project_slug", "default_project_name" },
				{ "pypi_username", "configuredgithubuser" },
				{ "version", "0.1.0" },
				{ "use_azure", "y" },
				{ "open_source_license", licenseOverride ?? "BSD license" },
				{ "port", "5000" },
			};
			CollectionAssert.AreEqual(expected, report);
		}

		private static string PII(string text)
		{
			return $"PII({text})";
		}

		private async Task EnsureCookiecutterInstalledAsync()
		{
			if (!_cutterClient.CookiecutterInstalled)
			{
				await _cutterClient.CreateCookiecutterEnv();
				await _cutterClient.InstallPackage();
			}
		}

		private static void PrintResults(ObservableCollection<object> items)
		{
			foreach (var item in items)
			{
				var template = item as TemplateViewModel;
				if (template != null)
				{
					PrintTemplate(template);
				}
				else
				{
					Console.WriteLine(item);
				}
			}
		}

		private static void PrintContextItems(ObservableCollection<ContextItemViewModel> items)
		{
			Console.WriteLine("Context Items");
			foreach (var item in items)
			{
				Console.WriteLine($"Name: '{item.Name}', Value: '{item.Val}', Default: '{item.Default}'");
			}
		}

		private static void PrintTemplate(TemplateViewModel template)
		{
			Console.WriteLine($"DisplayName: '{template.DisplayName}', RemoteUrl: '{template.RemoteUrl}', ClonedPath: '{template.ClonedPath}', Desc: '{template.Description}'");
		}

		private class ReplacedFileComparer : IComparer
		{
			public int Compare(object x, object y)
			{
				if (x == y)
				{
					return 0;
				}

				if (x == null)
				{
					return -1;
				}

				if (y == null)
				{
					return 1;
				}

				var a = x as ReplacedFile;
				var b = y as ReplacedFile;

				var res = a.OriginalFilePath.CompareTo(b.OriginalFilePath);
				if (res != 0)
				{
					return res;
				}

				res = a.BackupFilePath.CompareTo(b.BackupFilePath);
				if (res != 0)
				{
					return res;
				}

				return 0;
			}
		}
	}
}
