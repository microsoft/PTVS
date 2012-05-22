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
using Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Test.Tests.Cmdlet
{
    [TestClass]
    public class GetAzureStorageAccountsTests
    {
        [TestInitialize()]
        public void TestInitialize()
        {
            GlobalPathInfo.GlobalSettingsDirectory = Data.AzureSdkAppDir;
            new ImportAzurePublishSettingsCommand().ImportAzurePublishSettingsProcess(Resources.PublishSettingsFileName, Data.AzureSdkAppDir);
        }

        [TestCleanup()]
        public void TestCleanup()
        {
            try { new RemoveAzurePublishSettingsCommand().RemovePublishSettingsProcess(Data.AzureSdkAppDir); }
            catch { }
        }

        [TestMethod]
        public void GetStorageAccountsTestEmptyList()
        {
            SimpleServiceManagement channel = new SimpleServiceManagement();
            channel.ListStorageServicesThunk = ar => new StorageServiceList();
            GetAzureStorageAccountsCommand cmd = new GetAzureStorageAccountsCommand(channel);

            string result = cmd.GetStorageServicesProcess("TestSubscription1");
            
            Assert.IsTrue(string.IsNullOrEmpty(result));
        }

        [TestMethod]
        public void GetStorageAccountsTestOneEntryList()
        {
            SimpleServiceManagement channel = new SimpleServiceManagement();
            StorageServiceList list = new StorageServiceList();
            list.Add(Data.ValidStorageService[0]);
            StringBuilder expectedResult = new StringBuilder();
            expectedResult.AppendFormat("{0, -16}{1}", Resources.StorageAccountName, list[0].ServiceName);
            expectedResult.AppendLine();
            expectedResult.AppendFormat("{0, -16}{1}", Resources.StoragePrimaryKey, list[0].StorageServiceKeys.Primary);
            expectedResult.AppendLine();
            expectedResult.AppendFormat("{0, -16}{1}", Resources.StorageSecondaryKey, list[0].StorageServiceKeys.Secondary);
            channel.ListStorageServicesThunk = ar => list;
            channel.GetStorageKeysThunk = ar => list[0];
            GetAzureStorageAccountsCommand cmd = new GetAzureStorageAccountsCommand(channel);

            string actualResult = cmd.GetStorageServicesProcess("TestSubscription1");

            Assert.AreEqual<string>(expectedResult.ToString(), actualResult);
        }

        [TestMethod]
        public void GetStorageAccountsTestManyEntriesList()
        {
            SimpleServiceManagement channel = new SimpleServiceManagement();
            string expectedResult = CreateExpectedResult(Data.ValidStorageService);

            channel.ListStorageServicesThunk = ar => Data.ValidStorageService;
            channel.GetStorageKeysThunk = ar => Data.ValidStorageService[0];
            GetAzureStorageAccountsCommand cmd = new GetAzureStorageAccountsCommand(channel);

            string actualResult = cmd.GetStorageServicesProcess("TestSubscription1");

            Assert.AreEqual<string>(expectedResult, actualResult);
        }

        private string CreateExpectedResult(StorageServiceList storageServiceList)
        {
            System.Text.StringBuilder result = new System.Text.StringBuilder();
            bool needsSpacing = false;

            foreach (StorageService service in storageServiceList)
            {
                if (needsSpacing)
                {
                    result.AppendLine().AppendLine();
                }
                needsSpacing = true;
                result.AppendFormat("{0, -16}{1}", Resources.StorageAccountName, service.ServiceName);
                result.AppendLine();
                result.AppendFormat("{0, -16}{1}", Resources.StoragePrimaryKey, storageServiceList[0].StorageServiceKeys.Primary);
                result.AppendLine();
                result.AppendFormat("{0, -16}{1}", Resources.StorageSecondaryKey, storageServiceList[0].StorageServiceKeys.Secondary);
            }

            return result.ToString();
        }

        [TestMethod]
        public void GetStorageAccountsTestEmptySubscriptionFail()
        {
            string doesNotExistSubscription = "DoesNotExistSubscription";
            string argumentErrorExpectedMsg = string.Format(Resources.SubscriptionIdNotFoundMessage, doesNotExistSubscription, Path.Combine(Data.AzureSdkAppDir, Resources.PublishSettingsFileName));
            
            try
            {
                new ImportAzurePublishSettingsCommand().ImportAzurePublishSettingsProcess(Resources.PublishSettingsFileName, Data.AzureSdkAppDir);
                SimpleServiceManagement channel = new SimpleServiceManagement();
                channel.ListStorageServicesThunk = ar => new StorageServiceList();
                GetAzureStorageAccountsCommand cmd = new GetAzureStorageAccountsCommand(channel);

                string result = cmd.GetStorageServicesProcess("DoesNotExistSubscription");
                Assert.Fail("No exception was thrown");
            }
            catch (Exception ex)
            {
                Assert.AreEqual<string>(argumentErrorExpectedMsg, ex.Message);
            }
        }
    }
}