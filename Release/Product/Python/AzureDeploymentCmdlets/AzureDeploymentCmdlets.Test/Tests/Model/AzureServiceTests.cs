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
using System.Linq;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Cmdlet;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Test.TestData;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Test.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Test.Tests.Model
{
    [TestClass]
    public class AzureServiceTests: TestBase
    {
        private const string serviceName = "AzureService";

        [TestMethod]
        public void AzureServiceCreateNew()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                AzureAssert.AzureServiceExists(Path.Combine(files.RootPath, serviceName), Resources.GeneralScaffolding, serviceName);
            }
        }

        [TestMethod]
        public void AzureServiceCreateNewEmptyParentDirectoryFail()
        {
            Testing.AssertThrows<ArgumentException>(() => new AzureService(string.Empty, serviceName, null), string.Format(Resources.InvalidOrEmptyArgumentMessage, "service parent directory"));
        }

        [TestMethod]
        public void AzureServiceCreateNewNullParentDirectoryFail()
        {
            Testing.AssertThrows<ArgumentException>(() => new AzureService(null, serviceName, null), string.Format(Resources.InvalidOrEmptyArgumentMessage, "service parent directory"));
        }

        [TestMethod]
        public void AzureServiceCreateNewInvalidParentDirectoryFail()
        {
            foreach (string invalidName in Data.InvalidFileName)
            {
                Testing.AssertThrows<ArgumentException>(() => new AzureService(string.Empty, serviceName, null), string.Format(Resources.InvalidOrEmptyArgumentMessage, "service parent directory"));
            }
        }

        [TestMethod]
        public void AzureServiceCreateNewDoesNotExistParentDirectoryFail()
        {
            Testing.AssertThrows<FileNotFoundException>(() => new AzureService("DoesNotExist", serviceName, null), string.Format(Resources.PathDoesNotExistForElement, Resources.ServiceParentDirectory, "DoesNotExist"));
        }

        [TestMethod]
        public void AzureServiceCreateNewEmptyServiceNameFail()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                Testing.AssertThrows<ArgumentException>(() => new AzureService(files.RootPath, string.Empty, null), string.Format(Resources.InvalidOrEmptyArgumentMessage, "Name"));
            }
        }

        [TestMethod]
        public void AzureServiceCreateNewNullServiceNameFail()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                Testing.AssertThrows<ArgumentException>(() => new AzureService(files.RootPath, null, null), string.Format(Resources.InvalidOrEmptyArgumentMessage, "Name"));
            }
        }

        [TestMethod]
        public void AzureServiceCreateNewInvalidServiceNameFail()
        {
            foreach (string invalidFileName in Data.InvalidFileName)
            {
                using (FileSystemHelper files = new FileSystemHelper(this))
                {
                    Testing.AssertThrows<ArgumentException>(() => new AzureService(files.RootPath, invalidFileName, null), string.Format(Resources.InvalidFileName, "Name"));
                }
            }
        }

        [TestMethod]
        public void AzureServiceCreateNewInvalidDnsServiceNameFail()
        {
            char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

            foreach (string invalidDnsName in Data.InvalidServiceName)
            {
                using (FileSystemHelper files = new FileSystemHelper(this))
                {
                    // This case is handled in AzureServiceCreateNewInvalidDnsServiceNameFail test
                    //
                    if (invalidFileNameChars.Any(c => invalidFileNameChars.Contains<char>(c))) continue;
                    Testing.AssertThrows<ArgumentException>(() => new AzureService(files.RootPath, invalidDnsName, null), string.Format(Resources.InvalidDnsName, invalidDnsName, "Name"));
                }
            }
        }

        [TestMethod]
        public void AzureServiceCreateNewExistingServiceFail()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                new AzureService(files.RootPath, serviceName, null);
                Testing.AssertThrows<ArgumentException>(() => new AzureService(files.RootPath, serviceName, null), string.Format(Resources.ServiceAlreadyExistsOnDisk, serviceName, Path.Combine(files.RootPath, serviceName)));
            }
        }

        [TestMethod]
        public void AzureServiceLoadExistingSimpleService()
        {
            AddRoleTest(0, 0, 0);
        }

        [TestMethod]
        public void AzureServiceLoadExistingOneWebRoleService()
        {
            AddRoleTest(1, 0, 0);
        }

        [TestMethod]
        public void AzureServiceLoadExistingOneWorkerRoleService()
        {
            AddRoleTest(0, 1, 0);
        }

        [TestMethod]
        public void AzureServiceLoadExistingMultipleWebRolesService()
        {
            AddRoleTest(5, 0, 0);
        }

        [TestMethod]
        public void AzureServiceLoadExistingMultipleWorkerRolesService()
        {
            AddRoleTest(0, 5, 0);
        }

        [TestMethod]
        public void AzureServiceLoadExistingMultipleWebAndOneWorkerRolesService()
        {
            int order = 0;
            AddRoleTest(3, 4, order++);
            AddRoleTest(2, 4, order++);
            AddRoleTest(4, 2, order++);
            AddRoleTest(3, 5, order++);
        }

        [TestMethod]
        public void AzureServiceAddNewWebRoleTest()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                RoleInfo webRole = service.AddWebRole("MyWebRole", 10);

                AzureAssert.AzureServiceExists(Path.Combine(files.RootPath, serviceName), Resources.GeneralScaffolding, serviceName, webRoles: new WebRoleInfo[] { (WebRoleInfo)webRole }, webScaff: Path.Combine(Resources.PythonScaffolding, Resources.WebRole), roles: new RoleInfo[] { webRole });
            }
        }

        [TestMethod]
        public void AzureServiceAddNewWorkerRoleTest()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                RoleInfo workerRole = service.AddWorkerRole("MyWorkerRole", 10);

                AzureAssert.AzureServiceExists(Path.Combine(files.RootPath, serviceName), Resources.GeneralScaffolding, serviceName, workerRoles: new WorkerRoleInfo[] { (WorkerRoleInfo)workerRole }, workerScaff: Path.Combine(Resources.PythonScaffolding, Resources.WorkerRole), roles: new RoleInfo[] { workerRole });
            }
        }

        [TestMethod]
        public void AzureServiceAddNewWorkerRoleWithWhiteCharFail()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                Testing.AssertThrows<ArgumentException>(() => new AzureService(files.RootPath, serviceName, null).AddWebRole("\tRole"), string.Format(Resources.InvalidRoleNameMessage, "\tRole"));
            }
        }

        [TestMethod]
        public void AzureServiceAddExistingRoleFail()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                service.AddWebRole("WebRole");
                Testing.AssertThrows<ArgumentException>(() => service.AddWebRole("WebRole"), string.Format(Resources.AddRoleMessageRoleExists, "WebRole"));
            }
        }

        [TestMethod]
        public void GetServiceNameTest()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                new NewAzureServiceCommand().NewAzureServiceProcess(files.RootPath, serviceName);
                Assert.AreEqual<string>(serviceName, new AzureService(Path.Combine(files.RootPath, serviceName), null).ServiceName);
            }
        }

        [TestMethod]
        public void ChangeServiceNameTest()
        {
            string newName = "NodeAppService";

            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                service.ChangeServiceName(newName, service.Paths);
                Assert.AreEqual<string>(newName, service.Components.CloudConfig.serviceName);
                Assert.AreEqual<string>(newName, service.Components.LocalConfig.serviceName);
                Assert.AreEqual<string>(newName, service.Components.Definition.name);
            }
        }

        [TestMethod]
        public void SetRoleInstancesTest()
        {
            int newInstances = 10;

            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                service.AddWebRole("WebRole", 1);
                service.SetRoleInstances(service.Paths, "WebRole", newInstances);
                Assert.AreEqual<int>(service.Components.CloudConfig.Role[0].Instances.count, newInstances);
                Assert.AreEqual<int>(service.Components.LocalConfig.Role[0].Instances.count, newInstances);
            }
        }

        /// <summary>
        /// This method handles most possible cases that user can do to create role
        /// </summary>
        /// <param name="webRole">Count of web roles to add</param>
        /// <param name="workerRole">Count of worker role to add</param>
        /// <param name="addWebBeforeWorker">Decides in what order to add roles. There are three options, note that value between brackets is the value to pass:
        /// 1. Web then, interleaving (0): interleave adding and start by adding web role first.
        /// 2. Worker then, interleaving (1): interleave adding and start by adding worker role first.
        /// 3. Web then worker (2): add all web roles then worker roles.
        /// 4. Worker then web (3): add all worker roles then web roles.
        /// By default this parameter is set to 0
        /// </param>
        private void AddRoleTest(int webRole, int workerRole, int order = 0)
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                AzureServiceWrapper wrappedService = new AzureServiceWrapper(files.RootPath, serviceName, null);
                AzureService service = new AzureService(Path.Combine(files.RootPath, serviceName), null);
                
                WebRoleInfo[] webRoles = null;
                if (webRole > 0)
                {
                     webRoles = new WebRoleInfo[webRole];
                     for (int i = 0; i < webRoles.Length; i++)
                     {
                         webRoles[i] = new WebRoleInfo(string.Format("{0}{1}", Resources.WebRole, i + 1), 1);
                     }
                }


                WorkerRoleInfo[] workerRoles = null;
                if (workerRole > 0)
                {
                    workerRoles = new WorkerRoleInfo[workerRole];
                    for (int i = 0; i < workerRoles.Length; i++)
                    {
                        workerRoles[i] = new WorkerRoleInfo(string.Format("{0}{1}", Resources.WorkerRole, i + 1), 1);
                    }
                }

                RoleInfo[] roles = (webRole + workerRole > 0) ? new RoleInfo[webRole + workerRole] : null;
                if (order == 0)
                {
                    for (int i = 0, w = 0, wo = 0; i < webRole + workerRole;)
                    {
                        if (w++ < webRole) roles[i++] = wrappedService.AddWebRole();
                        if (wo++ < workerRole) roles[i++] = wrappedService.AddWorkerRole();
                    }
                }
                else if (order == 1)
                {
                    for (int i = 0, w = 0, wo = 0; i < webRole + workerRole;)
                    {
                        if (wo++ < workerRole) roles[i++] = wrappedService.AddWorkerRole();
                        if (w++ < webRole) roles[i++] = wrappedService.AddWebRole();
                    }
                }
                else if (order == 2)
                {
                    wrappedService.AddRole(webRole, workerRole);
                    webRoles.CopyTo(roles, 0);
                    Array.Copy(workerRoles, 0, roles, webRole, workerRoles.Length);
                }
                else if (order == 3)
                {
                    wrappedService.AddRole(0, workerRole);
                    workerRoles.CopyTo(roles, 0);
                    wrappedService.AddRole(webRole, 0);
                    Array.Copy(webRoles, 0, roles, workerRole, webRoles.Length);
                }
                else
                {
                    throw new ArgumentException("value for order parameter is unknown");
                }

                AzureAssert.AzureServiceExists(Path.Combine(files.RootPath, serviceName), Resources.GeneralScaffolding, serviceName, webRoles: webRoles, workerRoles: workerRoles, webScaff: Path.Combine(Resources.PythonScaffolding, Resources.WebRole), workerScaff: Path.Combine(Resources.PythonScaffolding, Resources.WorkerRole), roles: roles);
            }
        }
    }
}