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

    /// <summary>
    /// Change deployment status to running or suspended.
    /// </summary>
    public class DeploymentStatusManager : ServiceManagementCmdletBase
    {


        public DeploymentStatusManager()
        {

        }

        public DeploymentStatusManager(IServiceManagement channel)
        {
            this.Channel = channel;
        }

        public string Status
        {
            get;
            set;
        }

        [Parameter(Position = 0, Mandatory = false, ValueFromPipelineByPropertyName = true, HelpMessage = "Deployment slot. Staging | Production")]
        public string Slot
        {
            get;
            set;
        }

        [Parameter(Position = 2, Mandatory = false, ValueFromPipelineByPropertyName = true, HelpMessage = "Subscription name")]
        public string Subscription
        {
            get;
            set;
        }

        public virtual string SetDeploymentStatusProcess(string rootPath, string newStatus, string slot, string subscription, string serviceName)
        {
            string result;
            subscriptionId = new GlobalComponents(GlobalPathInfo.GlobalSettingsDirectory).GetSubscriptionId(subscription);
            result = CheckDeployment(newStatus, serviceName, slot);

            if (string.IsNullOrEmpty(result))
            {
                SetDeployment(newStatus, serviceName, slot);
                new GetDeploymentStatus(this.Channel).WaitForState(newStatus, rootPath, serviceName, slot, subscription);
            }

            return result;
        }

        private string CheckDeployment(string status, string serviceName, string slot)
        {
            string result = string.Empty;

            try
            {
                Deployment deployment = this.RetryCall(s => this.Channel.GetDeploymentBySlot(s, serviceName, slot));

                // Check to see if the service is in transitioning state
                //
                if (deployment.Status != DeploymentStatus.Running && deployment.Status != DeploymentStatus.Suspended)
                {
                    result = string.Format(Resources.ServiceIsInTransitionState, slot, serviceName, deployment.Status);
                }
                // Check to see if user trying to stop an already stopped service or 
                // starting an already starting service
                //
                else if (deployment.Status == DeploymentStatus.Running && status == DeploymentStatus.Running ||
                    deployment.Status == DeploymentStatus.Suspended && status == DeploymentStatus.Suspended)
                {
                    result = string.Format(Resources.DeploymentAlreadyInState, slot, serviceName, status);
                }
            }
            catch
            {
                // If we reach here that means the service or slot doesn't exist
                //
                result = string.Format(Resources.ServiceSlotDoesNotExist, serviceName, slot);
            }

            return result;
        }

        private void SetDeployment(string status, string serviceName, string slot)
        {
            var updateDeploymentStatus = new UpdateDeploymentStatusInput()
            {
                Status = status
            };

            InvokeInOperationContext(() =>
                {
                    this.RetryCall(s => this.Channel.UpdateDeploymentStatusBySlot(
                        s,
                        serviceName,
                        slot,
                        updateDeploymentStatus));
                });
        }

        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();
                string serviceName;
                var rootPath = base.GetServiceRootPath();
                ServiceSettings settings = base.GetDefaultSettings(rootPath, null, Slot, null, null, Subscription, out serviceName);
                SetDeploymentStatusProcess(rootPath, Status, settings.Slot, settings.Subscription, serviceName);
            }
            catch (Exception ex)
            {
                SafeWriteError(new ErrorRecord(ex, string.Empty, ErrorCategory.CloseError, null));
            }
        }
    }
}