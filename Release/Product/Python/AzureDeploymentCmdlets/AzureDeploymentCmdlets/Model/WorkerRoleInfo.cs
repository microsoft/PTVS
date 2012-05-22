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
using System.Management.Automation;
using System.Reflection;
using System.Xml.Serialization;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
using Microsoft.PythonTools.AzureDeploymentCmdlets.ServiceConfigurationSchema;
using Microsoft.PythonTools.AzureDeploymentCmdlets.ServiceDefinitionSchema;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities;
using System.Linq;
using System.Collections;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;


namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Model
{
    /// <summary>
    /// Role Info for a worker role
    /// </summary>
    internal class WorkerRoleInfo : RoleInfo
    {
        public WorkerRoleInfo(string name, int instanceCount = 1) : base(name, instanceCount) { }

        internal override void AddRoleToDefinition(ServiceDefinition def, object template)
        {
            base.AddRoleToDefinition(def, template);
            WorkerRole workerRole = template as WorkerRole;
            var toAdd = new WorkerRole[] { workerRole };

            if (def.WorkerRole != null)
            {
                def.WorkerRole = def.WorkerRole.Concat(toAdd).ToArray();
            }
            else
            {
                def.WorkerRole = toAdd;
            }
        }
    }
}
