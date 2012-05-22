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

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Python.Cmdlet
{
    using System;
    using System.Management.Automation;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;

    /// <summary>
    /// Create scaffolding for a new Python Django web role, change cscfg file and csdef to include the added web role
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "AzureDjangoWebRole")]
    public class AddAzureDjangoWebRoleCommand : AddRole
    {
        internal string AddAzureDjangoWebRoleProcess(string webRoleName, int instances, string rootPath)
        {
            string result;
            AzureService service = new AzureService(rootPath, null);
            RoleInfo webRole = service.AddWebRole(webRoleName, instances);
            try
            {
                service.ChangeRolePermissions(webRole);
            }
            catch (UnauthorizedAccessException)
            {
                SafeWriteObject(Resources.AddRoleMessageInsufficientPermissions);
                SafeWriteObject(Environment.NewLine);
            }

            result = string.Format(Resources.AddRoleMessageCreate, rootPath, webRole.Name);
            return result;
        }

        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();
                string result = AddAzureDjangoWebRoleProcess(Name, Instances, base.GetServiceRootPath());
                SafeWriteObject(result);
            }
            catch (Exception ex)
            {
                SafeWriteError(new ErrorRecord(ex, string.Empty, ErrorCategory.CloseError, null));
            }
        }
    }
}