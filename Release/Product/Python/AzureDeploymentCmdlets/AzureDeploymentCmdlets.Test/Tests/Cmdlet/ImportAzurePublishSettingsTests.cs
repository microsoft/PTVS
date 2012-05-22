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
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Cmdlet;
using System.IO;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
using System.Security.Cryptography.X509Certificates;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Test.Model;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Test.Utilities;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;
using Microsoft.PythonTools.AzureDeploymentCmdlets.PublishSettingsSchema;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Test.TestData;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Test.Tests.Cmdlet
{
    [TestClass]
    public class ImportAzurePublishSettingsTests
    {
        [TestCleanup()]
        public void TestCleanup()
        {
            try { new RemoveAzurePublishSettingsCommand().RemovePublishSettingsProcess(Data.AzureSdkAppDir); }
            catch { }
        }

        [TestMethod]
        public void ImportAzurePublishSettingsProcessTests()
        {
            GlobalPathInfo globalPathInfo = new GlobalPathInfo(Data.AzureSdkAppDir);

            foreach (string filePath in Data.ValidPublishSettings)
            {
                new ImportAzurePublishSettingsCommand().ImportAzurePublishSettingsProcess(filePath, Data.AzureSdkAppDir);
                PublishData expectedPublishSettings = General.DeserializeXmlFile<PublishData>(filePath);
                PublishData actualPublishSettings = General.DeserializeXmlFile<PublishData>(globalPathInfo.PublishSettings);
                string thmbprint = actualPublishSettings.Items[0].ManagementCertificate;
                AzureAssert.AreEqualGlobalComponents(thmbprint, globalPathInfo, new ServiceSettings(), actualPublishSettings, new GlobalComponents(Data.AzureSdkAppDir));
            }
        }

        [TestMethod]
        public void ImportAzurePublishSettingsProcessTestsFail()
        {   
            foreach (string filePath in Data.InvalidPublishSettings)
            {
                try
                {
                    new ImportAzurePublishSettingsCommand().ImportAzurePublishSettingsProcess(filePath, Data.AzureSdkAppDir);
                    Assert.Fail("no exception thrown");
                }
                catch (Exception ex)
                {
                    Assert.IsTrue(ex is InvalidOperationException);
                    Assert.AreEqual<string>(ex.Message, string.Format(Resources.InvalidPublishSettingsSchema, filePath));
                }
            }
        }
    }
}