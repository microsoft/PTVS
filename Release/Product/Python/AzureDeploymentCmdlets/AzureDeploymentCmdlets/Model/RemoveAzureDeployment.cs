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

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Model
{
    using System;
    using System.Management.Automation;
    using System.ServiceModel;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;

    /// <summary>
    /// Deletes the specified deployment. Note that the deployment should be in suspended state.
    /// </summary>
    class RemoveAzureDeploymentCommand : ServiceManagementCmdletBase
    {
        [Parameter(Position = 0, Mandatory = false, ValueFromPipelineByPropertyName = true, HelpMessage = "Deployment slot. Staging | Production")]
        public string Slot
        {
            get;
            set;
        }

        [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true, HelpMessage = "Service name.")]
        public string ServiceName
        {
            get;
            set;
        }

        [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true, HelpMessage = "Subscription")]
        public string Subscription
        {
            get;
            set;
        }

        public RemoveAzureDeploymentCommand()
        {
        }

        public RemoveAzureDeploymentCommand(IServiceManagement channel)
        {
            this.Channel = channel;
        }

        public string RemoveAzureDeploymentProcess(string rootPath, string inServiceName, string inSlot, string inSubscription)
        {
            string results;
            string serviceName;
            ServiceSettings settings = InitializeArguments(rootPath, inServiceName, inSlot, inSubscription, out serviceName);
            results = RemoveDeployment(serviceName, settings.Slot);

            return results;
        }

        private ServiceSettings InitializeArguments(string rootPath, string inServiceName, string inSlot, string inSubscription, out string serviceName)
        {
            ServiceSettings settings = base.GetDefaultSettings(rootPath, inServiceName, inSlot, null, null, inSubscription, out serviceName);
            subscriptionId = new GlobalComponents(GlobalPathInfo.GlobalSettingsDirectory).GetSubscriptionId(settings.Subscription);
            return settings;
        }

        private string RemoveDeployment(string serviceName, string slot)
        {
            string results;

            InvokeInOperationContext(() =>
            {
                this.RetryCall(s => this.Channel.DeleteDeploymentBySlot(s, serviceName, slot));
            });

            results = string.Format(Resources.DeploymentRemovedMessage, slot, serviceName);

            return results;
        }

        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();
                string results = this.RemoveAzureDeploymentProcess(base.GetServiceRootPath(), ServiceName, Slot, Subscription);
                SafeWriteObject(results);
            }
            catch (Exception ex)
            {
                SafeWriteError(new ErrorRecord(ex, string.Empty, ErrorCategory.CloseError, null));
            }
        }

        public void Initialize()
        {
            this.ProcessRecord();
        }
    }
}