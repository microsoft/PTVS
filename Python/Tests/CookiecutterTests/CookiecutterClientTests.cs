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
    public class CookiecutterClientTests
    {
        private const string GitHubTemplatePath = "https://github.com/audreyr/Cookiecutter-pypackage";
        private const string NoUserConfigFilePath = "";

        private static string LocalTemplatePath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "template");
        private static string LocalTemplateForVSPath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "templateforvs");
        private static string UserConfigFilePath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "userconfig.yaml");

        private ICookiecutterClient _client;
        private MockRedirector _redirector = new MockRedirector();

        private static ContextItem[] LocalTemplateNoUserConfigContextItems { get; } = new ContextItem[] {
            new ContextItem("full_name", Selectors.String, "Default Full Name"),
            new ContextItem("email", Selectors.String, "default@email"),
            new ContextItem("github_username", Selectors.String, "defaultgitusername"),
            new ContextItem("project_name", Selectors.String, "Default Project Name"),
            new ContextItem("project_slug", Selectors.String, "{{ cookiecutter.project_name.lower().replace(' ', '_') }}"),
            new ContextItem("pypi_username", Selectors.String, "{{ cookiecutter.github_username }}"),
            new ContextItem("version", Selectors.String, "0.1.0"),
            new ContextItem("use_azure", Selectors.String, "y"),
            new ContextItem("open_source_license", Selectors.List, "MIT license", new string[] { "MIT license", "BSD license", "ISC license", "Apache Software License 2.0", "GNU General Public License v3", "Not open source" }),
            new ContextItem("port", Selectors.String, "5000"),
            // Note that _copy_without_render item should not appear
        };

        private static ContextItem[] LocalTemplateForVSNoUserConfigContextItems { get; } = new ContextItem[] {
            new ContextItem("full_name", Selectors.String, "Default Full Name") { Label="Author", Description="Full name of author." },
            new ContextItem("email", Selectors.String, "default@email"),
            new ContextItem("github_username", Selectors.String, "defaultgitusername"),
            new ContextItem("project_name", Selectors.String, "Default Project Name") { Label="Project Name", Description="Description for the application."},
            new ContextItem("project_slug", Selectors.String, "{{ cookiecutter.project_name.lower().replace(' ', '_') }}") { Label="Package Name", Description="Pythonic name for the application.", Url="http://www.python.org" },
            new ContextItem("pypi_username", Selectors.String, "{{ cookiecutter.github_username }}"),
            new ContextItem("version", Selectors.String, "0.1.0"),
            new ContextItem("db_connection", Selectors.OdbcConnection, "") { Label="ODBC Connection String", Url="https://www.microsoft.com/en-us/sql-server/sql-server-2016" },
            new ContextItem("use_azure", Selectors.YesNo, "y") { Description="Enable Azure support.", Url="http://azure.microsoft.com" },
            new ContextItem("open_source_license", Selectors.List, "MIT license", new string[] { "MIT license", "BSD license", "ISC license", "Apache Software License 2.0", "GNU General Public License v3", "Not open source" }) { Label="Open Source License", Description="License under which you will distribute the generated files.", Url="https://opensource.org/licenses" },
            new ContextItem("port", Selectors.String, "5000") { Label="Port" },
            new ContextItem("from_src_is_new_item", Selectors.String, "") { Visible=false, ValueSource=KnownValueSources.IsNewItem },
            new ContextItem("from_src_is_new_project", Selectors.String, "") { Visible=false, ValueSource=KnownValueSources.IsNewProject },
            new ContextItem("from_src_is_from_project_wizard", Selectors.String, "") { Visible=false, ValueSource=KnownValueSources.IsFromProjectWizard },
            new ContextItem("from_src_project_name", Selectors.String, "") { Visible=false, ValueSource=KnownValueSources.ProjectName },
            // Note that _copy_without_render item should not appear
        };

        private static ContextItem[] LocalTemplateWithUserConfigContextItems { get; } = new ContextItem[] {
            new ContextItem("full_name", Selectors.String, "Configured User"),
            new ContextItem("email", Selectors.String, "configured@email"),
            new ContextItem("github_username", Selectors.String, "configuredgithubuser"),
            new ContextItem("project_name", Selectors.String, "Default Project Name"),
            new ContextItem("project_slug", Selectors.String, "{{ cookiecutter.project_name.lower().replace(' ', '_') }}"),
            new ContextItem("pypi_username", Selectors.String, "{{ cookiecutter.github_username }}"),
            new ContextItem("version", Selectors.String, "0.1.0"),
            new ContextItem("use_azure", Selectors.String, "y"),
            new ContextItem("open_source_license", Selectors.List, "BSD license", new string[] { "MIT license", "BSD license", "ISC license", "Apache Software License 2.0", "GNU General Public License v3", "Not open source" }),
            new ContextItem("port", Selectors.String, "5000"),
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
            _client = CookiecutterClientProvider.Create(null, _redirector);
            Assert.IsNotNull(_client, "The system doesn't have any compatible Python interpreters.");
        }

        private async Task EnsureCookiecutterInstalledAsync()
        {
            if (!_client.CookiecutterInstalled)
            {
                await _client.CreateCookiecutterEnv();
                await _client.InstallPackage();
            }
        }

        [TestMethod]
        public async Task LoadContextNoUserConfig()
        {
            await EnsureCookiecutterInstalledAsync();

            var context = await _client.LoadUnrenderedContextAsync(LocalTemplatePath, NoUserConfigFilePath);

            CollectionAssert.AreEqual(LocalTemplateNoUserConfigContextItems, context.Items, new ContextItemComparer());
        }

        [TestMethod]
        public async Task LoadContextWithUserConfig()
        {
            await EnsureCookiecutterInstalledAsync();

            var context = await _client.LoadUnrenderedContextAsync(LocalTemplatePath, UserConfigFilePath);

            CollectionAssert.AreEqual(LocalTemplateWithUserConfigContextItems, context.Items, new ContextItemComparer());
        }

        [TestMethod]
        public async Task LoadContextForVSNoUserConfig()
        {
            await EnsureCookiecutterInstalledAsync();

            var context = await _client.LoadUnrenderedContextAsync(LocalTemplateForVSPath, NoUserConfigFilePath);

            CollectionAssert.AreEqual(LocalTemplateForVSNoUserConfigContextItems, context.Items, new ContextItemComparer());
        }

        [TestMethod]
        public async Task GenerateWithoutUserConfig()
        {
            await EnsureCookiecutterInstalledAsync();

            Dictionary<string, string> actual = await GenerateFromLocalTemplate(NoUserConfigFilePath);

            var expected = new Dictionary<string, string>() {
                { "full_name", "Default Full Name" },
                { "email", "default@email" },
                { "github_username", "defaultgitusername" },
                { "project_name", "Default Project Name" },
                { "project_slug", "default_project_name" },
                { "pypi_username", "defaultgitusername" },
                { "version", "0.1.0" },
                { "use_azure", "y" },
                { "open_source_license", "MIT license" },
                { "port", "5000" },
            };
            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public async Task GenerateWithUserConfig()
        {
            await EnsureCookiecutterInstalledAsync();

            Dictionary<string, string> actual = await GenerateFromLocalTemplate(UserConfigFilePath);

            var expected = new Dictionary<string, string>() {
                { "full_name", "Configured User" },
                { "email", "configured@email" },
                { "github_username", "configuredgithubuser" },
                { "project_name", "Default Project Name" },
                { "project_slug", "default_project_name" },
                { "pypi_username", "configuredgithubuser" },
                { "version", "0.1.0" },
                { "use_azure", "y" },
                { "open_source_license", "BSD license" },
                { "port", "5000" },
            };
            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public async Task CompareFiles()
        {
            Random rnd = new Random();
            var original = new byte[32768 * 3 + 1024];
            rnd.NextBytes(original);

            var tempFolder = TestData.GetTempPath();
            var originalPath = Path.Combine(tempFolder, "original.dat");
            var identicalPath = Path.Combine(tempFolder, "identical.dat");
            var largerPath = Path.Combine(tempFolder, "larger.dat");
            var modifiedPath = Path.Combine(tempFolder, "modified.dat");

            File.WriteAllBytes(originalPath, original);
            File.WriteAllBytes(identicalPath, original);
            File.WriteAllBytes(largerPath, original);
            File.WriteAllBytes(modifiedPath, original);

            using (var stream = new FileStream(largerPath, FileMode.Append, FileAccess.Write))
            {
                stream.WriteByte(42);
            }

            using (var stream = new FileStream(modifiedPath, FileMode.Open, FileAccess.Write))
            {
                stream.Seek(32768 + 10, SeekOrigin.Begin);
                stream.WriteByte(42);
            }

            Assert.IsTrue(await CookiecutterClient.AreFilesSameAsync(originalPath, identicalPath));
            Assert.IsFalse(await CookiecutterClient.AreFilesSameAsync(originalPath, largerPath));
            Assert.IsFalse(await CookiecutterClient.AreFilesSameAsync(originalPath, modifiedPath));
        }

        private async Task<Dictionary<string, string>> GenerateFromLocalTemplate(string userConfigFilePath)
        {
            var context = await _client.LoadUnrenderedContextAsync(LocalTemplatePath, userConfigFilePath);

            var output = TestData.GetTempPath();
            var outputProjectFolder = Path.Combine(output, "project");
            var contextFilePath = Path.Combine(output, "context.json");

            var vm = new CookiecutterViewModel();
            foreach (var item in context.Items)
            {
                vm.ContextItems.Add(new ContextItemViewModel(item.Name, item.Selector, item.Label, item.Description, item.Url, item.DefaultValue, item.Visible, item.Values));
            }

            vm.SaveUserInput(contextFilePath);

            Directory.CreateDirectory(outputProjectFolder);

            await _client.CreateFilesAsync(LocalTemplatePath, userConfigFilePath, contextFilePath, outputProjectFolder);

            var reportFilePath = Path.Combine(outputProjectFolder, "report.txt");
            Assert.IsTrue(File.Exists(reportFilePath), "Failed to generate some project files.");
            return ReadReport(reportFilePath);
        }

        internal static Dictionary<string, string> ReadReport(string filePath)
        {
            var dict = new Dictionary<string, string>();
            var report = File.ReadAllLines(filePath);
            foreach (var line in report)
            {
                var parts = line.Split(':');
                dict.Add(parts[0], parts[1]);
            }
            return dict;
        }

        class ContextItemComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                if (x == y)
                {
                    return 0;
                }

                var a = x as ContextItem;
                var b = y as ContextItem;

                if (a == null)
                {
                    return -1;
                }

                if (b == null)
                {
                    return -1;
                }

                int res;
                res = a.Name.CompareTo(b.Name);
                if (res != 0)
                {
                    return res;
                }

                res = a.DefaultValue.CompareTo(b.DefaultValue);
                if (res != 0)
                {
                    return res;
                }

                res = a.Selector.CompareTo(b.Selector);
                if (res != 0)
                {
                    return res;
                }

                res = SafeCompare(a.Description, b.Description);
                if (res != 0)
                {
                    return res;
                }

                return 0;
            }

            private int SafeCompare(IComparable a, IComparable b)
            {
                if (a == null)
                {
                    return b == null ? 0 : -1;
                }

                return a.CompareTo(b);
            }
        }
    }
}
