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

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Concrete
{
    public class RoleInstance
    {
        private Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet.RoleInstance innerInstance;

        public RoleInstance(Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet.RoleInstance innerInstance)
        {
            this.innerInstance = innerInstance;
        }

        public string RoleName
        {
            get
            {
                return this.innerInstance.RoleName;
            }
        }

        public string InstanceName
        {
            get
            {
                return this.innerInstance.InstanceName;
            }
        }

        public string InstanceStatus
        {
            get
            {
                return this.innerInstance.InstanceStatus;
            }
        }
    }
}