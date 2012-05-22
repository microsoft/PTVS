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

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Cmdlet
{
    using System;
    using System.Management.Automation;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.ServiceConfigurationSchema;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;
    using System.Linq;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;

    /// <summary>
    /// Configure the number of instances for a web/worker role. Updates the cscfg with the number of instances
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "AzureInstances")]
    public class SetAzureInstancesCommand : CmdletBase
    {
        [Parameter(Position = 0, Mandatory = true)]
        public string RoleName { get; set; }

        [Parameter(Position = 1, Mandatory = true)]
        public int Instances { get; set; }

        public void SetAzureInstancesProcess(string roleName, int instances, string rootPath)
        {
            AzureService service = new AzureService(rootPath, null);
            service.SetRoleInstances(service.Paths, roleName, instances);
        }

        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();
                this.SetAzureInstancesProcess(RoleName, Instances, base.GetServiceRootPath());
            }
            catch (Exception ex)
            {
                SafeWriteError(new ErrorRecord(ex, string.Empty, ErrorCategory.CloseError, null));
            }
        }
    }
}