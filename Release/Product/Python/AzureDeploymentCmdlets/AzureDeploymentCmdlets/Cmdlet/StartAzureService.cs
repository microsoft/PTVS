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
using System.Linq;
using System.Text;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;
using System.Management.Automation;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
using Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Cmdlet
{
    /// <summary>
    /// Starts the deployment of specified slot in the azure service
    /// </summary>
    [Cmdlet(VerbsLifecycle.Start, "AzureService")]
    public class StartAzureService : DeploymentStatusManager
    {
        /// <summary>
        /// SetDeploymentStatus will handle the execution of this cmdlet
        /// </summary>
        public StartAzureService()
        {
            Status = DeploymentStatus.Running;
        }

        public StartAzureService(IServiceManagement channel): base(channel)
        {
            Status = DeploymentStatus.Running;
        }

        public override string SetDeploymentStatusProcess(string rootPath, string newStatus, string slot, string subscription, string serviceName)
        {
            SafeWriteObjectWithTimestamp(Resources.StartServiceMessage, serviceName);
            var message = base.SetDeploymentStatusProcess(rootPath, newStatus, slot, subscription, serviceName);
            if (string.IsNullOrEmpty(message))
            {
                SafeWriteObjectWithTimestamp(Resources.CompleteMessage);
            }
            else
            {
                SafeWriteObjectWithTimestamp(message);
            }
            return message;
        }
    }
}