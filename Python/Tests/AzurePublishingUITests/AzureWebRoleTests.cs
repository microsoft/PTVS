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
using System.IO;
using System.Threading;
using Microsoft.PythonTools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using PythonToolsUITests;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace AzurePublishingUITests {
    [TestClass]
    public class AzureWebRoleTests {
        private string _cloudServiceToDelete;
        private static string publishSettingsFilePath;

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();

            // The tests currently only support Azure Tools v2.2.
            // Support for other versions will be added later.
            var azureToolsVersion = AzureUtility.ToolsVersion.V22;
            if (!AzureUtility.AzureToolsInstalled(azureToolsVersion)) {
                Assert.Inconclusive(string.Format("Azure Tools v{0} required", azureToolsVersion));
            }

            publishSettingsFilePath = Environment.GetEnvironmentVariable("TEST_AZURE_SUBSCRIPTION_FILE");
            Assert.IsFalse(string.IsNullOrEmpty(publishSettingsFilePath), "TEST_AZURE_SUBSCRIPTION_FILE environment variable must be set to the path of a .publishSettings file for the Azure subscription.");
            Assert.IsTrue(File.Exists(publishSettingsFilePath), "Azure subscription settings file does not exist '{0}'.", publishSettingsFilePath);
        }

        [TestCleanup]
        public void Cleanup() {
            if (!string.IsNullOrEmpty(_cloudServiceToDelete)) {
                Assert.IsTrue(AzureUtility.DeleteCloudServiceWithRetry(publishSettingsFilePath, _cloudServiceToDelete), "Failed to delete cloud service.");
            }
        }

        public TestContext TestContext { get; set; }

        private void TestPublishToWebRole(
            string templateName,
            string projectName,
            string moduleName,
            string textInResponse,
            string pythonVersion,
            int publishTimeout,
            string packageName = null
        ) {
            using (var app = new VisualStudioApp()) {
                var pyProj = app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    templateName,
                    TestData.GetTempPath(),
                    projectName
                ).GetPythonProject();

                var factory = WebProjectTests.CreateVirtualEnvironment(pythonVersion, app, pyProj);

                WebProjectTests.InstallWebFramework(app, moduleName, packageName ?? moduleName, factory);

                app.Dte.ExecuteCommand("Project.ConverttoWindowsAzureCloudServiceProject");

                _cloudServiceToDelete = Guid.NewGuid().ToString("N");
                var siteUri = app.PublishToAzureCloudService(_cloudServiceToDelete, publishSettingsFilePath);
                app.WaitForBuildComplete(publishTimeout);

                app.AzureActivityLog.WaitForPublishComplete(_cloudServiceToDelete, publishTimeout);

                string text = WebDownloadUtility.GetString(siteUri);

                Console.WriteLine("Response from {0}", siteUri);
                Console.WriteLine(text);
                Assert.IsTrue(text.Contains(textInResponse), text);
            }
        }

        const int BottlePublishTimeout = 30 * 60 * 1000;

        [TestMethod, Priority(1), Timeout(BottlePublishTimeout)]
        [TestCategory("10s"), TestCategory("60s"), TestCategory("Installed"), TestCategory("Interactive")]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void BottlePublish() {
            TestPublishToWebRole(
                PythonVisualStudioApp.BottleWebProjectTemplate,
                "btlproj",
                "bottle",
                "<b>Hello world</b>!",
                "2.7",
                BottlePublishTimeout
            );
        }

        const int FlaskPublishTimeout = 30 * 60 * 1000;

        [TestMethod, Priority(1), Timeout(FlaskPublishTimeout)]
        [TestCategory("10s"), TestCategory("60s"), TestCategory("Installed"), TestCategory("Interactive")]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FlaskPublish() {
            TestPublishToWebRole(
                PythonVisualStudioApp.FlaskWebProjectTemplate,
                "flskproj",
                "flask",
                "Hello World!",
                "2.7",
                FlaskPublishTimeout
            );
        }

        const int DjangoPublishTimeout = 30 * 60 * 1000;

        [TestMethod, Priority(1), Timeout(DjangoPublishTimeout)]
        [TestCategory("10s"), TestCategory("60s"), TestCategory("Installed"), TestCategory("Interactive")]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DjangoPublish() {
            TestPublishToWebRole(
                PythonVisualStudioApp.DjangoWebProjectTemplate,
                "djproj",
                "django",
                "Congratulations on your first Django-powered page.",
                "2.7",
                DjangoPublishTimeout
            );
        }
    }
}
