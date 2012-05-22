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
using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;
using Microsoft.PythonTools.AzureDeploymentCmdlets.ServiceConfigurationSchema;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Test.TestData;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Test.Tests.Cmdlet
{
    [TestClass]
    public class SetAzureInstancesTests : TestBase
    {
        private const string serviceName = "AzureService";

        [TestMethod]
        public void SetAzureInstancesProcessTests()
        {
            int newRoleInstances = 10;

            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                service.AddWebRole();
                new SetAzureInstancesCommand().SetAzureInstancesProcess("WebRole1", newRoleInstances, service.Paths.RootPath);
                service = new AzureService(service.Paths.RootPath, null);

                Assert.AreEqual<int>(newRoleInstances, service.Components.CloudConfig.Role[0].Instances.count);
                Assert.AreEqual<int>(newRoleInstances, service.Components.LocalConfig.Role[0].Instances.count);
            }
        }

        [TestMethod]
        public void SetAzureInstancesProcessTestsRoleNameDoesNotExistFail()
        {
            string roleName = "WebRole1";

            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                Testing.AssertThrows<ArgumentException>(() => service.SetRoleInstances(service.Paths, roleName, 10), string.Format(Resources.RoleNotFoundMessage, roleName));
            }
        }

        [TestMethod]
        public void SetAzureInstancesProcessTestsRoleNameDoesNotExistServiceContainsWebRoleFail()
        {
            string roleName = "WebRole1";
            string invalidRoleName = "foo";

            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                service.AddWebRole(roleName, 1);
                Testing.AssertThrows<ArgumentException>(() => service.SetRoleInstances(service.Paths, invalidRoleName, 10), string.Format(Resources.RoleNotFoundMessage, invalidRoleName));
            }
        }

        [TestMethod]
        public void SetAzureInstancesProcessTestsRoleNameDoesNotExistServiceContainsWorkerRoleFail()
        {
            string roleName = "WorkerRole1";
            string invalidRoleName = "foo";

            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                service.AddWorkerRole(roleName, 1);
                Testing.AssertThrows<ArgumentException>(() => service.SetRoleInstances(service.Paths, invalidRoleName, 10), string.Format(Resources.RoleNotFoundMessage, invalidRoleName));
            }
        }

        [TestMethod]
        public void SetAzureInstancesProcessTestsEmptyRoleNameFail()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                Testing.AssertThrows<ArgumentException>(() => service.SetRoleInstances(service.Paths, string.Empty, 10), string.Format(Resources.InvalidOrEmptyArgumentMessage, Resources.RoleName));
            }
        }

        [TestMethod]
        public void SetAzureInstancesProcessTestsNullRoleNameFail()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                Testing.AssertThrows<ArgumentException>(() => service.SetRoleInstances(service.Paths, null, 10), string.Format(Resources.InvalidOrEmptyArgumentMessage, Resources.RoleName));
            }
        }

        [TestMethod]
        public void SetAzureInstancesProcessTestsLargeRoleInstanceFail()
        {
            string roleName = "WebRole1";

            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                Testing.AssertThrows<ArgumentException>(() => service.SetRoleInstances(service.Paths, roleName, 2000), string.Format(Resources.InvalidInstancesCount, roleName));
            }
        }

        [TestMethod]
        public void SetAzureInstancesProcessNegativeRoleInstanceFail()
        {
            string roleName = "WebRole1";

            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                Testing.AssertThrows<ArgumentException>(() => service.SetRoleInstances(service.Paths, roleName, -1), string.Format(Resources.InvalidInstancesCount, roleName));
            }
        }
    }
}