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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Security.Principal;
using Microsoft.PythonTools.AzureDeploymentCmdlets.AzureTools;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Scaffolding;
using Microsoft.PythonTools.AzureDeploymentCmdlets.ServiceDefinitionSchema;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities;
using Microsoft.Win32;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Model
{
    /// <summary>
    /// Class that encapsulates all of the info about a service, to which we can add roles.  This is all in memory, so no disk operations occur.
    /// </summary>
    public class AzureService
    {
        public ServicePathInfo Paths { get; private set; }
        public ServiceComponents Components { get; private set; }
        private CsPack packageTool;
        private CsRun runTool;
        private string scaffoldingFolderPath;

        public string ServiceName { get { return this.Components.Definition.name; } }

        public AzureService(string serviceParentDirectory, string name, string scaffoldingPath)
            : this()
        {
            Validate.ValidateDirectoryFull(serviceParentDirectory, Resources.ServiceParentDirectory);
            Validate.ValidateStringIsNullOrEmpty(name, "Name");
            Validate.ValidateFileName(name, string.Format(Resources.InvalidFileName, "Name"));
            Validate.ValidateDnsName(name, "Name");

            string newServicePath = Path.Combine(serviceParentDirectory, name);
            if (Directory.Exists(newServicePath))
            {
                throw new ArgumentException(string.Format(Resources.ServiceAlreadyExistsOnDisk, name, newServicePath));
            }

            packageTool = new CsPack();
            SetScaffolding(scaffoldingPath);
            Paths = new ServicePathInfo(newServicePath);
            CreateNewService(Paths.RootPath, name);
            Components = new ServiceComponents(Paths);
            ConfigureNewService(Components, Paths, name);
        }

        //for stopping the emulator none of the path info is required
        public AzureService()
        {
            runTool = new CsRun();
        }

        public AzureService(string rootPath, string scaffoldingPath)
        {
            SetScaffolding(scaffoldingPath);
            Paths = new ServicePathInfo(rootPath);
            Components = new ServiceComponents(Paths);
            packageTool = new CsPack();
            runTool = new CsRun();
        }

        private void SetScaffolding(string scaffoldingFolderDirectory)
        {
            if (string.IsNullOrEmpty(scaffoldingFolderDirectory))
            {
                scaffoldingFolderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }
            else
            {
                Validate.ValidateDirectoryExists(scaffoldingFolderDirectory);

                scaffoldingFolderPath = scaffoldingFolderDirectory;
            }
        }

        private void ConfigureNewService(ServiceComponents components, ServicePathInfo paths, string serviceName)
        {
            Components.Definition.name = serviceName;
            Components.CloudConfig.serviceName = serviceName;
            Components.LocalConfig.serviceName = serviceName;
            Components.Save(paths);
        }

        private void CreateNewService(string serviceRootPath, string serviceName)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters[ScaffoldParams.Slot] = string.Empty;
            parameters[ScaffoldParams.Subscription] = string.Empty;
            parameters[ScaffoldParams.Location] = string.Empty;
            parameters[ScaffoldParams.StorageAccountName] = string.Empty;
            parameters[ScaffoldParams.ServiceName] = serviceName;

            Scaffold.GenerateScaffolding(Path.Combine(scaffoldingFolderPath, Resources.GeneralScaffolding), serviceRootPath, parameters);
        }

        /// <summary>
        /// Creates a role name, ensuring it doesn't already exist.  If null is passed in, a number will be appended to the defaultRoleName.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="defaultName"></param>
        /// <param name="existingNames"></param>
        /// <returns></returns>
        private string GetRoleName(string name, string defaultName, IEnumerable<string> existingNames)
        {
            if (!string.IsNullOrEmpty(name))
            {
                if (existingNames.Contains(name.ToLower()))
                {
                    // Role does exist, user should pick a unique name
                    //
                    throw new ArgumentException(string.Format(Resources.AddRoleMessageRoleExists, name));
                }

                if (!ServiceComponents.ValidRoleName(name))
                {
                    // The provided name is invalid role name
                    //
                    throw new ArgumentException(string.Format(Resources.InvalidRoleNameMessage, name));
                }
            }

            if (name == null)
            {
                name = defaultName;
            }
            else
            {
                return name;
            }

            int index = 1;
            string curName = name + index.ToString();
            while (existingNames.Contains(curName.ToLower()))
            {
                curName = name + (++index).ToString();
            }
            return curName;
        }

        /// <summary>
        /// Adds the given role to both config files and the service def.
        /// </summary>
        /// <param name="role"></param>
        private void AddRoleCore(RoleInfo role, RoleType type)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters[ScaffoldParams.Role] = role;
            parameters[ScaffoldParams.Components] = Components;
            parameters[ScaffoldParams.RoleName] = role.Name;
            parameters[ScaffoldParams.InstancesCount] = role.InstanceCount;
            parameters[ScaffoldParams.Port] = Components.GetNextPort();
            parameters[ScaffoldParams.Paths] = Paths;
            parameters[ScaffoldParams.PythonSupportPath] = Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\PythonTools", "PowerShellFolder", null) ??
                Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\PythonTools", "PowerShellFolder", null);

            string scaffoldPath = Path.Combine(Path.Combine(scaffoldingFolderPath, Resources.PythonScaffolding), type.ToString());
            Scaffold.GenerateScaffolding(scaffoldPath, Path.Combine(Paths.RootPath, role.Name), parameters);
        }

        public RoleInfo AddWebRole(string name = null, int instanceCount = 1)
        {
            name = GetRoleName(name, Resources.WebRole, Components.Definition.WebRole == null ? new String[0] : Components.Definition.WebRole.Select(wr => wr.name.ToLower()));
            WebRoleInfo role = new WebRoleInfo(name, instanceCount);
            AddRoleCore(role, RoleType.WebRole);

            return role;
        }

        public RoleInfo AddWorkerRole(string name = null, int instanceCount = 1)
        {
            name = GetRoleName(name, Resources.WorkerRole, Components.Definition.WorkerRole == null ? new String[0] : Components.Definition.WorkerRole.Select(wr => wr.name.ToLower()));
            WorkerRoleInfo role = new WorkerRoleInfo(name, instanceCount);
            AddRoleCore(role, RoleType.WorkerRole);

            return role;
        }

        public void ChangeRolePermissions(RoleInfo role)
        {
            string rolePath = Path.Combine(Paths.RootPath, role.Name);
            DirectoryInfo directoryInfo = new DirectoryInfo(rolePath);
            DirectorySecurity directoryAccess = directoryInfo.GetAccessControl(AccessControlSections.All);
            directoryAccess.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null), 
                FileSystemRights.ReadAndExecute | FileSystemRights.Write, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            directoryInfo.SetAccessControl(directoryAccess);
        }

        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        public void CreatePackage(DevEnv type, out string standardOutput, out string standardError)
        {
            packageTool.CreatePackage(Components.Definition, Paths.RootPath, type, out standardOutput, out standardError);
        }

        /// <summary>
        /// Starts azure emulator for this service.
        /// </summary>
        /// <remarks>This methods removes all deployments already in the emulator.</remarks>
        /// <param name="launch">Switch to control opening a browser for web roles.</param>
        /// <param name="standardOutput">Output result from csrun.exe</param>
        /// <param name="standardError">Error result from csrun.exe</param>
        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        public void StartEmulator(bool launch, out string standardOutput, out string standardError)
        {
            runTool.StartEmulator(Paths.LocalPackage, Paths.LocalConfiguration, launch, out standardOutput, out standardError);
        }

        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        public void StopEmulator(out string standardOutput, out string standardError)
        {
            runTool.StopEmulator(out standardOutput, out standardError);
        }

        public void ChangeServiceName(string newName, ServicePathInfo paths)
        {
            Validate.ValidateDnsName(newName, "service name");

            Components.Definition.name = newName;
            Components.CloudConfig.serviceName = newName;
            Components.LocalConfig.serviceName = newName;
            Components.Save(paths);
        }

        public void SetRoleInstances(ServicePathInfo paths, string roleName, int instances)
        {
            Components.SetRoleInstances(roleName, instances);
            Components.Save(paths);
        }
    }
}