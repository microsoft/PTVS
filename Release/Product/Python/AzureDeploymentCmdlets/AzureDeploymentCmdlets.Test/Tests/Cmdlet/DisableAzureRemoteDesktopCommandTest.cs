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

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Cmdlet;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Python.Cmdlet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Test
{
    /// <summary>
    /// Basic unit tests for the Enable-AzureRemoteDesktop command.
    /// </summary>
    [TestClass]
    public class DisableAzureRemoteDesktopCommandTest : TestBase
    {
        private static void VerifyDisableRoleSettings(AzureService service)
        {
            IEnumerable<ServiceConfigurationSchema.RoleSettings> settings =
                Enumerable.Concat(
                    service.Components.CloudConfig.Role,
                    service.Components.LocalConfig.Role);
            foreach (ServiceConfigurationSchema.RoleSettings roleSettings in settings)
            {
                Assert.AreEqual(
                    1,
                    roleSettings.ConfigurationSettings
                        .Where(c => c.name == "Microsoft.WindowsAzure.Plugins.RemoteAccess.Enabled" && c.value == "false")
                        .Count());
            }
        }

        /// <summary>
        /// Enable remote desktop for an empty service.
        /// </summary>
        [TestMethod]
        public void DisableRemoteDesktopForEmptyService()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                files.CreateAzureSdkDirectoryAndImportPublishSettings();
                files.CreateNewService("NEW_SERVICE");
                new DisableAzureRemoteDesktopCommand().DisableRemoteDesktop();
            }
        }

        /// <summary>
        /// Disable remote desktop for a simple web role.
        /// </summary>
        [TestMethod]
        public void DisableRemoteDesktopForWebRole()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                files.CreateAzureSdkDirectoryAndImportPublishSettings();
                string root = files.CreateNewService("NEW_SERVICE");
                new AddAzureDjangoWebRoleCommand().AddAzureDjangoWebRoleProcess("WebRole", 1, root);
                new DisableAzureRemoteDesktopCommand().DisableRemoteDesktop();
            }
        }

        /// <summary>
        /// Disable remote desktop for web and worker roles.
        /// </summary>
        [TestMethod]
        public void DisableRemoteDesktopForWebAndWorkerRoles()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                files.CreateAzureSdkDirectoryAndImportPublishSettings();
                string root = files.CreateNewService("NEW_SERVICE");
                new AddAzureDjangoWebRoleCommand().AddAzureDjangoWebRoleProcess("WebRole", 1, root);
                //new AddAzureNodeWorkerRoleCommand().AddAzureNodeWorkerRoleProcess("WorkerRole", 1, root);
                new DisableAzureRemoteDesktopCommand().DisableRemoteDesktop();
            }
        }

        /// <summary>
        /// Enable then disable remote desktop for a simple web role.
        /// </summary>
        [TestMethod]
        public void EnableDisableRemoteDesktopForWebRole()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                files.CreateAzureSdkDirectoryAndImportPublishSettings();
                string root = files.CreateNewService("NEW_SERVICE");
                new AddAzureDjangoWebRoleCommand().AddAzureDjangoWebRoleProcess("WebRole", 1, root);
                EnableAzureRemoteDesktopCommandTest.EnableRemoteDesktop("user", "GoodPassword!");
                new DisableAzureRemoteDesktopCommand().DisableRemoteDesktop();
                // Verify the role has been setup with forwarding, access,
                // and certs
                AzureService service = new AzureService(root, null);
                EnableAzureRemoteDesktopCommandTest.VerifyWebRole(service.Components.Definition.WebRole[0], true);
                VerifyDisableRoleSettings(service);
            }
        }

        /// <summary>
        /// Enable then disable remote desktop for web and worker roles.
        /// </summary>
        [TestMethod]
        public void EnableDisableRemoteDesktopForWebAndWorkerRoles()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                files.CreateAzureSdkDirectoryAndImportPublishSettings();
                string root = files.CreateNewService("NEW_SERVICE");
                new AddAzureDjangoWebRoleCommand().AddAzureDjangoWebRoleProcess("WebRole", 1, root);
                //new AddAzureNodeWorkerRoleCommand().AddAzureNodeWorkerRoleProcess("WorkerRole", 1, root);
                EnableAzureRemoteDesktopCommandTest.EnableRemoteDesktop("user", "GoodPassword!");
                new DisableAzureRemoteDesktopCommand().DisableRemoteDesktop();
                // Verify the roles have been setup with forwarding, access,
                // and certs
                AzureService service = new AzureService(root, null);
                EnableAzureRemoteDesktopCommandTest.VerifyWebRole(service.Components.Definition.WebRole[0], false);
                EnableAzureRemoteDesktopCommandTest.VerifyWorkerRole(service.Components.Definition.WorkerRole[0], true);
                VerifyDisableRoleSettings(service);
            }
        }

        /// <summary>
        /// Enable then disable remote desktop for web and worker roles.
        /// </summary>
        [TestMethod]
        public void EnableDisableEnableRemoteDesktopForWebAndWorkerRoles()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                files.CreateAzureSdkDirectoryAndImportPublishSettings();
                string root = files.CreateNewService("NEW_SERVICE");
                new AddAzureDjangoWebRoleCommand().AddAzureDjangoWebRoleProcess("WebRole", 1, root);
                //new AddAzureNodeWorkerRoleCommand().AddAzureNodeWorkerRoleProcess("WorkerRole", 1, root);
                EnableAzureRemoteDesktopCommandTest.EnableRemoteDesktop("user", "GoodPassword!");
                new DisableAzureRemoteDesktopCommand().DisableRemoteDesktop();
                EnableAzureRemoteDesktopCommandTest.EnableRemoteDesktop("user", "GoodPassword!");
                // Verify the roles have been setup with forwarding, access,
                // and certs
                AzureService service = new AzureService(root, null);
                EnableAzureRemoteDesktopCommandTest.VerifyWebRole(service.Components.Definition.WebRole[0], false);
                EnableAzureRemoteDesktopCommandTest.VerifyWorkerRole(service.Components.Definition.WorkerRole[0], true);
                EnableAzureRemoteDesktopCommandTest.VerifyRoleSettings(service);
            }
        }    
    }
}
