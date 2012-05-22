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
using Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Test.TestData;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Cmdlet;
using System.ServiceModel;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Test.Tests.Cmdlet
{
    [TestClass]
    public class RemoveAzureServiceTests : TestBase
    {
        private const string serviceName = "AzureService";

        [TestMethod]
        public void RemoveAzureServiceProcessTest()
        {
            SimpleServiceManagement channel = new SimpleServiceManagement();
            bool serviceDeleted = false;
            bool deploymentDeleted = false;
            channel.GetDeploymentBySlotThunk = ar =>
            {
                if (deploymentDeleted) throw new EndpointNotFoundException();
                return new Deployment(serviceName, ArgumentConstants.Slots[Slot.Production], DeploymentStatus.Suspended);
            };
            channel.DeleteHostedServiceThunk = ar => serviceDeleted = true;
            channel.DeleteDeploymentBySlotThunk = ar =>
            {
                deploymentDeleted = true;
            };

            using (FileSystemHelper files = new FileSystemHelper(this))
            {

                files.CreateAzureSdkDirectoryAndImportPublishSettings();
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                new RemoveAzureServiceCommand(channel).RemoveAzureServiceProcess(service.Paths.RootPath, string.Empty);
                Assert.IsTrue(deploymentDeleted);
                Assert.IsTrue(serviceDeleted);
            }
        }
    }
}