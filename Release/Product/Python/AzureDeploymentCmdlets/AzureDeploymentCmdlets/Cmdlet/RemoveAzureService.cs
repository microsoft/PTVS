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
    using System.ServiceModel;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;

    /// <summary>
    /// Deletes the specified hosted service from Windows Azure.
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "AzureService", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public class RemoveAzureServiceCommand : ServiceManagementCmdletBase
    {
        [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true, HelpMessage = "name of subscription which has this service")]
        public string Subscription
        {
            get;
            set;
        }

        public RemoveAzureServiceCommand() { }

        public RemoveAzureServiceCommand(IServiceManagement channel)
        {
            this.Channel = channel;
        }

        public bool RemoveAzureServiceProcess(string rootName, string inSubscription)
        {
            string serviceName;
            ServiceSettings settings = base.GetDefaultSettings(rootName, null, null, null, null, inSubscription, out serviceName);
            subscriptionId = new GlobalComponents(GlobalPathInfo.GlobalSettingsDirectory).GetSubscriptionId(settings.Subscription);
            if (!ShouldProcess("", string.Format(Resources.RemoveServiceWarning, serviceName), Resources.ShouldProcessCaption))
            {
                return false;
            }
            SafeWriteObjectWithTimestamp(Resources.RemoveServiceStartMessage, serviceName);
            SafeWriteObjectWithTimestamp(Resources.RemoveDeploymentMessage);
            StopAndRemove(rootName, serviceName, settings.Subscription, ArgumentConstants.Slots[Slot.Production]);
            StopAndRemove(rootName, serviceName, settings.Subscription, ArgumentConstants.Slots[Slot.Staging]);
            SafeWriteObjectWithTimestamp(Resources.RemoveServiceMessage);
            RemoveService(serviceName);
            return true;
        }

        private void StopAndRemove(string rootName, string serviceName, string subscription, string slot)
        {
            GetDeploymentStatus getDeployment = new GetDeploymentStatus(this.Channel);

            if (getDeployment.DeploymentExists(rootName, serviceName, slot, subscription))
            {
                DeploymentStatusManager setDeployment = new DeploymentStatusManager(this.Channel);
                setDeployment.SetDeploymentStatusProcess(rootName, DeploymentStatus.Suspended, slot, subscription, serviceName);

                getDeployment.WaitForState(DeploymentStatus.Suspended, rootName, serviceName, slot, subscription);

                RemoveAzureDeploymentCommand removeDeployment = new RemoveAzureDeploymentCommand(this.Channel);
                removeDeployment.RemoveAzureDeploymentProcess(rootName, serviceName, slot, subscription);

                while (getDeployment.DeploymentExists(rootName, serviceName, slot, subscription)) ;
            }
        }

        private void RemoveService(string serviceName)
        {
            SafeWriteObjectWithTimestamp(string.Format(Resources.RemoveAzureServiceWaitMessage, serviceName));

            InvokeInOperationContext(() =>
                {
                    this.RetryCall(s => this.Channel.DeleteHostedService(s, serviceName));
                });
        }

        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();

                if (RemoveAzureServiceProcess(base.GetServiceRootPath(), Subscription))
                {
                    SafeWriteObjectWithTimestamp(Resources.CompleteMessage);
                }
            }
            catch (Exception ex)
            {
                SafeWriteError(new ErrorRecord(ex, string.Empty, ErrorCategory.CloseError, null));
            }
        }
    }
}