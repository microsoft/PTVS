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

                WebProjectTests.InstallWebFramework(moduleName, packageName ?? moduleName, factory);

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

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(BottlePublishTimeout)]
        [HostType("VSTestHost")]
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

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(FlaskPublishTimeout)]
        [HostType("VSTestHost")]
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

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(DjangoPublishTimeout)]
        [HostType("VSTestHost")]
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
