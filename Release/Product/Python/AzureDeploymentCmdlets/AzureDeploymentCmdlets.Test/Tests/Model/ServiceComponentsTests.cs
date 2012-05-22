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
    public class ServiceComponentsTests : TestBase
    {
        private const string serviceName = "NodeService";

        [TestCleanup]
        public void TestCleanup()
        {
            if (Directory.Exists(serviceName))
            {                
                Directory.Delete(serviceName, true);
            }
        }


        [TestMethod]
        public void ServiceComponentsTest()
        {
            new NewAzureServiceCommand().NewAzureServiceProcess(Directory.GetCurrentDirectory(), serviceName);
            ServiceComponents components = new ServiceComponents(new ServicePathInfo(serviceName));
            AzureAssert.AreEqualServiceComponents(components);
        }

        [TestMethod]
        public void ServiceComponentsTestNullPathsFail()
        {
            try
            {
                ServiceComponents components = new ServiceComponents(null);
                Assert.Fail("No exception was thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is ArgumentException);
                Assert.AreEqual<string>(ex.Message, string.Format(Resources.NullObjectMessage, "paths"));
            }
        }

        [TestMethod]
        public void ServiceComponentsTestCloudConfigDoesNotExistFail()
        {
            new NewAzureServiceCommand().NewAzureServiceProcess(Directory.GetCurrentDirectory(), serviceName);
            ServicePathInfo paths = new ServicePathInfo(serviceName);

            try
            {
                File.Delete(paths.CloudConfiguration);
                ServiceComponents components = new ServiceComponents(paths);
                Assert.Fail("No exception was thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is FileNotFoundException);
                Assert.AreEqual<string>(ex.Message, string.Format(Resources.PathDoesNotExistForElement, Resources.ServiceConfiguration, paths.CloudConfiguration));
            }
        }

        [TestMethod]
        public void ServiceComponentsTestLocalConfigDoesNotExistFail()
        {
            new NewAzureServiceCommand().NewAzureServiceProcess(Directory.GetCurrentDirectory(), serviceName);
            ServicePathInfo paths = new ServicePathInfo(serviceName);

            try
            {
                File.Delete(paths.LocalConfiguration);
                ServiceComponents components = new ServiceComponents(paths);
                Assert.Fail("No exception was thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is FileNotFoundException);
                Assert.AreEqual<string>(string.Format(Resources.PathDoesNotExistForElement, Resources.ServiceConfiguration, paths.LocalConfiguration), ex.Message);
            }
        }

        [TestMethod]
        public void ServiceComponentsTestSettingsDoesNotExistFail()
        {
            new NewAzureServiceCommand().NewAzureServiceProcess(Directory.GetCurrentDirectory(), serviceName);
            ServicePathInfo paths = new ServicePathInfo(serviceName);

            try
            {
                File.Delete(paths.Settings);
                ServiceComponents components = new ServiceComponents(paths);
                Assert.Fail("No exception was thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is FileNotFoundException);
                Assert.AreEqual<string>(string.Format(Resources.PathDoesNotExistForElement, Resources.ServiceSettings, paths.Settings), ex.Message);
            }
        }

        [TestMethod]
        public void ServiceComponentsTestDefinitionDoesNotExistFail()
        {
            new NewAzureServiceCommand().NewAzureServiceProcess(Directory.GetCurrentDirectory(), serviceName);
            ServicePathInfo paths = new ServicePathInfo(serviceName);

            try
            {
                File.Delete(paths.Definition);
                ServiceComponents components = new ServiceComponents(paths);
                Assert.Fail("No exception was thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is FileNotFoundException);
                Assert.AreEqual<string>(string.Format(Resources.PathDoesNotExistForElement, Resources.ServiceDefinition, paths.Definition), ex.Message);
            }
        }

        [TestMethod]
        public void GetNextPortAllNull()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                int expectedPort = int.Parse(Resources.DefaultWebPort);
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                int nextPort = service.Components.GetNextPort();
                Assert.AreEqual<int>(expectedPort, nextPort);
            }
        }

        [TestMethod]
        public void GetNextPortWorkerRoleNull()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                int expectedPort = int.Parse(Resources.DefaultPort);
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                service.AddWebRole();
                service = new AzureService(service.Paths.RootPath, null);
                int nextPort = service.Components.GetNextPort();
                Assert.AreEqual<int>(expectedPort, nextPort);
            }
        }

        [TestMethod]
        public void GetNextPortWebRoleNull()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                int expectedPort = int.Parse(Resources.DefaultPort);
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                service.AddWorkerRole();
                service = new AzureService(service.Paths.RootPath, null);
                int nextPort = service.Components.GetNextPort();
                Assert.AreEqual<int>(expectedPort, nextPort);
            }
        }

        [TestMethod]
        public void GetNextPortNullWebEndpointAndNullWorkerRole()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                int expectedPort = int.Parse(Resources.DefaultWebPort);
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                service.AddWebRole();
                service = new AzureService(service.Paths.RootPath, null);
                service.Components.Definition.WebRole.ToList().ForEach(wr => wr.Endpoints = null);
                int nextPort = service.Components.GetNextPort();
                Assert.AreEqual<int>(expectedPort, nextPort);
            }
        }

        [TestMethod]
        public void GetNextPortNullWebEndpointAndWorkerRole()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                int expectedPort = int.Parse(Resources.DefaultPort);
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                service.AddWebRole();
                service.Components.Definition.WebRole.ToList().ForEach(wr => wr.Endpoints = null);
                service.AddWorkerRole();
                service = new AzureService(service.Paths.RootPath, null);
                int nextPort = service.Components.GetNextPort();
                Assert.AreEqual<int>(expectedPort, nextPort);
            }
        }

        [TestMethod]
        public void GetNextPortWithEmptyPortIndpoints()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                int expectedPort = int.Parse(Resources.DefaultPort);
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                service.AddWebRole();
                service.Components.Definition.WebRole[0].Endpoints.InputEndpoint = null;
                service.Components.Save(service.Paths);
                service.AddWebRole();
                service = new AzureServiceWrapper(service.Paths.RootPath, null);
                int nextPort = service.Components.GetNextPort();
                Assert.AreEqual<int>(expectedPort, nextPort);
            }
        }

        [TestMethod]
        public void GetNextPortAddingThirdEndpoint()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                int expectedPort = int.Parse(Resources.DefaultPort) + 1;
                AzureService service = new AzureService(files.RootPath, serviceName, null);
                service.AddWebRole();
                service.AddWebRole();
                service = new AzureServiceWrapper(service.Paths.RootPath, null);
                int nextPort = service.Components.GetNextPort();
                Assert.AreEqual<int>(expectedPort, nextPort);
            }
        }
    }
}