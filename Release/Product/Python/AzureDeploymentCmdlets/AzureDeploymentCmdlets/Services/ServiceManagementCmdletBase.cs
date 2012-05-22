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

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet
{
    using System;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;

    public class ServiceManagementCmdletBase : CloudCmdlet<IServiceManagement>
    {
        protected override IServiceManagement CreateChannel()
        {
            if (this.ServiceBinding == null)
            {
                this.ServiceBinding = ConfigurationConstants.WebHttpBinding(this.MaxStringContentLength);
            }

            if (string.IsNullOrEmpty(this.ServiceEndpoint))
            {
                this.ServiceEndpoint = ConfigurationConstants.ServiceManagementEndpoint;
            }

            return ServiceManagementHelper.CreateServiceManagementChannel(this.ServiceBinding, new Uri(this.ServiceEndpoint), this.certificate);
        }
    }
}
