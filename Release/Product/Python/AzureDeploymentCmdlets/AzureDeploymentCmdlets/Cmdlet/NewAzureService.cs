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
    using System.IO;
    using System.Management.Automation;
    using System.Reflection;
    using System.Xml.Serialization;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.ServiceConfigurationSchema;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.ServiceDefinitionSchema;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;

    /// <summary>
    /// Create scaffolding for a new hosted service. Generates a basic folder structure, 
    /// default cscfg file which wires up Django at startup in Azure.
    /// </summary>
    [Cmdlet(VerbsCommon.New, "AzureService")]
    public class NewAzureServiceCommand : CmdletBase
    {
        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Name of the Django project")]
        public string Name { get; set; }

        internal string NewAzureServiceProcess(string parentDirectory, string serviceName)
        {
            string message;
            AzureService newService;

            // Create scaffolding structure
            //
            newService = new AzureService(parentDirectory, serviceName, null);
            
            message = string.Format(Resources.NewServiceCreatedMessage, newService.Paths.RootPath);

            return message;
        }

        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();
                
                // Create new service
                //
                string result = this.NewAzureServiceProcess(base.CurrentPath(), Name);
                // Set current directory to the root of the new service
                //
                SessionState.Path.SetLocation(Path.Combine(base.CurrentPath(), Name));
                SafeWriteObject(result);
            }
            catch (Exception ex)
            {
                SafeWriteError(new ErrorRecord(ex, string.Empty, ErrorCategory.CloseError, null));
            }
        }
    }
}