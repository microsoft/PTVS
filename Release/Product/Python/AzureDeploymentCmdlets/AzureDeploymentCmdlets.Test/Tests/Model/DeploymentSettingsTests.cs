// ----------------------------------------------------------------------------------
//
// Copyright 2011 Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.IO;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Cmdlet;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Test.Model;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Test.TestData;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Test.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Test.Tests.Model
{
    [TestClass]
    public class DeploymentSettingsTests : TestBase
    {
        static AzureServiceWrapper service;
        static string packagePath;
        static string configPath;
        static ServiceSettings settings;
        string serviceName;

        /// <summary>
        /// When running this test double check that the certificate used in Azure.PublishSettings has not expired.
        /// </summary>
        [TestInitialize()]
        public void TestInitialize()
        {
            serviceName = Path.GetRandomFileName();
            GlobalPathInfo.GlobalSettingsDirectory = Data.AzureSdkAppDir;
            service = new AzureServiceWrapper(Directory.GetCurrentDirectory(), Path.GetRandomFileName(), null);
            service.CreateVirtualCloudPackage();
            packagePath = service.Paths.CloudPackage;
            configPath = service.Paths.CloudConfiguration;
            settings = ServiceSettingsTestData.Instance.Data[ServiceSettingsState.Default];
            new ImportAzurePublishSettingsCommand().ImportAzurePublishSettingsProcess(Resources.PublishSettingsFileName, Data.AzureSdkAppDir);
        }

        [TestCleanup()]
        public void TestCleanup()
        {
            if (Directory.Exists(Data.AzureSdkAppDir))
            {
                new RemoveAzurePublishSettingsCommand().RemovePublishSettingsProcess(Data.AzureSdkAppDir);
            }
        }

        #region settings

        [TestMethod]
        public void TestDeploymentSettingsTestWithDefaultServiceSettings()
        {
            string label = "MyLabel";
            string deploymentName = service.ServiceName;
            settings.Subscription = "TestSubscription2";
            DeploymentSettings deploySettings = new DeploymentSettings(settings, packagePath, configPath, label, deploymentName);

            AzureAssert.AreEqualDeploymentSettings(settings, configPath, deploymentName, label, packagePath, "f62b1e05-af8f-4205-8f98-325079adc155", deploySettings);
        }

        [TestMethod]
        public void TestDeploymentSettingsTestWithFullServiceSettings()
        {
            string label = "MyLabel";
            string deploymentName = service.ServiceName;
            ServiceSettings fullSettings = ServiceSettingsTestData.Instance.Data[ServiceSettingsState.Sample1];
            DeploymentSettings deploySettings = new DeploymentSettings(fullSettings, packagePath, configPath, label, deploymentName);

            AzureAssert.AreEqualDeploymentSettings(fullSettings, configPath, deploymentName, label, packagePath, "f62b1e05-af8f-4205-8f98-325079adc155", deploySettings);
        }

        [TestMethod]
        public void TestDeploymentSettingsTestNullSettingsFail()
        {
            string label = "MyLabel";
            string deploymentName = service.ServiceName;

            try
            {
                DeploymentSettings deploySettings = new DeploymentSettings(null, packagePath, configPath, label, deploymentName);
                Assert.Fail("No exception was thrown");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(ArgumentException));
                Assert.AreEqual<string>(Resources.InvalidServiceSettingMessage, ex.Message);
            }
        }

        #endregion

        #region packagePath

        [TestMethod]
        public void TestDeploymentSettingsTestEmptyPackagePathFail()
        {
            string label = "MyLabel";
            string deploymentName = service.ServiceName;
            string expectedMessage = string.Format(Resources.InvalidOrEmptyArgumentMessage, "package");

            Testing.AssertThrows<ArgumentException>(() => new DeploymentSettings(settings, string.Empty, configPath, label, deploymentName), expectedMessage);
        }

        [TestMethod]
        public void TestDeploymentSettingsTestNullPackagePathFail()
        {
            string label = "MyLabel";
            string deploymentName = service.ServiceName;
            string expectedMessage = string.Format(Resources.InvalidOrEmptyArgumentMessage, "package");

            Testing.AssertThrows<ArgumentException>(() => new DeploymentSettings(settings, null, configPath, label, deploymentName), expectedMessage);
        }

        [TestMethod]
        public void TestDeploymentSettingsTestDoesNotPackagePathFail()
        {
            string label = "MyLabel";
            string deploymentName = service.ServiceName;
            string doesNotExistDir = Path.Combine(Directory.GetCurrentDirectory(), "qewindw443298.txt");
            string expectedMessage = string.Format(Resources.PathDoesNotExistForElement, Resources.Package, doesNotExistDir);

            Testing.AssertThrows<FileNotFoundException>(() => new DeploymentSettings(settings, doesNotExistDir, configPath, label, deploymentName), expectedMessage);
        }

        #endregion

        #region configPath

        [TestMethod]
        public void TestDeploymentSettingsTestEmptyConfigPathFail()
        {
            string label = "MyLabel";
            string deploymentName = service.ServiceName;
            string expectedMessage = string.Format(Resources.InvalidOrEmptyArgumentMessage, Resources.ServiceConfiguration);

            Testing.AssertThrows<ArgumentException>(() => new DeploymentSettings(settings, packagePath, string.Empty, label, deploymentName), expectedMessage);
        }

        [TestMethod]
        public void TestDeploymentSettingsTestNullConfigPathFail()
        {
            string label = "MyLabel";
            string deploymentName = service.ServiceName;
            string expectedMessage = string.Format(Resources.InvalidOrEmptyArgumentMessage, Resources.ServiceConfiguration);

            Testing.AssertThrows<ArgumentException>(() => new DeploymentSettings(settings, packagePath, null, label, deploymentName), expectedMessage);
        }

        [TestMethod]
        public void TestDeploymentSettingsTestDoesNotConfigPathFail()
        {
            string label = "MyLabel";
            string deploymentName = service.ServiceName;
            string doesNotExistDir = Path.Combine(Directory.GetCurrentDirectory(), "qewindw443298.cscfg");

            try
            {
                DeploymentSettings deploySettings = new DeploymentSettings(settings, packagePath, doesNotExistDir, label, deploymentName);
                Assert.Fail("No exception was thrown");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(FileNotFoundException));
                Assert.AreEqual<string>(string.Format(Resources.PathDoesNotExistForElement, Resources.ServiceConfiguration, doesNotExistDir), ex.Message);
            }
        }

        #endregion

        #region label

        [TestMethod]
        public void TestDeploymentSettingsTestEmptyLabelFail()
        {
            string deploymentName = service.ServiceName;

            try
            {
                DeploymentSettings deploySettings = new DeploymentSettings(settings, packagePath, configPath, string.Empty, deploymentName);
                Assert.Fail("No exception was thrown");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(ArgumentException));
                Assert.IsTrue(string.Compare(string.Format(Resources.InvalidOrEmptyArgumentMessage, "Label"), ex.Message, true) == 0);
            }
        }

        [TestMethod]
        public void TestDeploymentSettingsTestNullLabelFail()
        {
            string deploymentName = service.ServiceName;

            try
            {
                DeploymentSettings deploySettings = new DeploymentSettings(settings, packagePath, configPath, null, deploymentName);
                Assert.Fail("No exception was thrown");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(ArgumentException));
                Assert.IsTrue(string.Compare(string.Format(Resources.InvalidOrEmptyArgumentMessage, "Label"), ex.Message, true) == 0);
            }
        }

        #endregion

        #region deploymentName

        [TestMethod]
        public void TestDeploymentSettingsTestEmptyDeploymentNameFail()
        {
            try
            {
                DeploymentSettings deploySettings = new DeploymentSettings(settings, packagePath, configPath, service.ServiceName, string.Empty);
                Assert.Fail("No exception was thrown");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(ArgumentException));
                Assert.IsTrue(string.Compare(string.Format(Resources.InvalidOrEmptyArgumentMessage, "Deployment name"), ex.Message, true) == 0);
            }
        }

        [TestMethod]
        public void TestDeploymentSettingsTestNullDeploymentFail()
        {
            string deploymentName = service.ServiceName;

            try
            {
                DeploymentSettings deploySettings = new DeploymentSettings(settings, packagePath, configPath, service.ServiceName, null);
                Assert.Fail("No exception was thrown");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(ArgumentException));
                Assert.IsTrue(string.Compare(string.Format(Resources.InvalidOrEmptyArgumentMessage, "Deployment name"), ex.Message, true) == 0);
            }
        }

        #endregion
    }
}