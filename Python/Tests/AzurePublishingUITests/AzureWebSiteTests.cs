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
    public class AzureWebSiteTests {
        private string _webSiteToDelete;
        private static string publishSettingsFilePath;

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();

#if DEV11_OR_LATER
            // The tests currently only support Azure Tools v2.2.
            // Support for other versions will be added later.
            var azureToolsVersion = AzureUtility.ToolsVersion.V22;
            if (!AzureUtility.AzureToolsInstalled(azureToolsVersion)) {
                Assert.Inconclusive(string.Format("Azure Tools v{0} required", azureToolsVersion));
            }
#else
            // Azure Tools v2.2 is not available for VS2010.
            // We'll need to support Azure Tools v2.1 to test VS2010
            Assert.Inconclusive("Test not yet compatible with VS2010 (Azure Tools v2.1)");
#endif

            publishSettingsFilePath = Environment.GetEnvironmentVariable("TEST_AZURE_SUBSCRIPTION_FILE");
            Assert.IsFalse(string.IsNullOrEmpty(publishSettingsFilePath), "TEST_AZURE_SUBSCRIPTION_FILE environment variable must be set to the path of a .publishSettings file for the Azure subscription.");
            Assert.IsTrue(File.Exists(publishSettingsFilePath), "Azure subscription settings file does not exist '{0}'.", publishSettingsFilePath);
        }

        [TestCleanup]
        public void Cleanup() {
            if (!string.IsNullOrEmpty(_webSiteToDelete)) {
                Assert.IsTrue(AzureUtility.DeleteWebSiteWithRetry(publishSettingsFilePath, _webSiteToDelete));
            }
        }

        public TestContext TestContext { get; set; }

        private void TestPublishToWebSite(
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

                _webSiteToDelete = Guid.NewGuid().ToString("N");
                var siteUri = app.PublishToAzureWebSite(_webSiteToDelete, publishSettingsFilePath);
                app.WaitForBuildComplete(publishTimeout);

                string text = WebDownloadUtility.GetString(siteUri);

                Console.WriteLine("Response from {0}", siteUri);
                Console.WriteLine(text);
                Assert.IsTrue(text.Contains(textInResponse), text);
            }
        }

        const int BottlePublishTimeout = 15 * 60 * 1000;

        [TestMethod, Priority(1), Timeout(BottlePublishTimeout)]
        [TestCategory("10s"), TestCategory("60s"), TestCategory("Installed"), TestCategory("Interactive")]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void BottlePublish() {
            TestPublishToWebSite(
                PythonVisualStudioApp.BottleWebProjectTemplate,
                "btlproj",
                "bottle",
                "<b>Hello world</b>!",
                "2.7",
                BottlePublishTimeout
            );
        }

        const int FlaskPublishTimeout = 15 * 60 * 1000;

        [TestMethod, Priority(1), Timeout(FlaskPublishTimeout)]
        [TestCategory("10s"), TestCategory("60s"), TestCategory("Installed"), TestCategory("Interactive")]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FlaskPublish() {
            TestPublishToWebSite(
                PythonVisualStudioApp.FlaskWebProjectTemplate,
                "flskproj",
                "flask",
                "Hello World!",
                "2.7",
                FlaskPublishTimeout
            );
        }

        const int DjangoPublishTimeout = 25 * 60 * 1000;

        [TestMethod, Priority(1), Timeout(DjangoPublishTimeout)]
        [TestCategory("10s"), TestCategory("60s"), TestCategory("Installed"), TestCategory("Interactive")]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DjangoPublish() {
            TestPublishToWebSite(
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
