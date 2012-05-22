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
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;

    /// <summary>
    /// Configure the default subscription for deploying. Stores the new subscription name in service settings
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "AzureDeploymentSubscription")]
    public class SetAzureDeploymentSubscriptionCommand : SetSettings
    {
        [Parameter(Position = 0, Mandatory = true)]
        [Alias("sn")]
        public string Subscription { get; set; }

        public void SetAzureDeploymentSubscriptionProcess(string newSubscription, string settingsPath)
        {
            ServiceSettings settings = ServiceSettings.Load(settingsPath);
            settings.Subscription = newSubscription;
            settings.Save(settingsPath);
        }

        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();
                this.SetAzureDeploymentSubscriptionProcess(Subscription, base.GetServiceSettingsPath(false));
            }
            catch (Exception ex)
            {
                SafeWriteError(new ErrorRecord(ex, string.Empty, ErrorCategory.CloseError, null));
            }
        }
    }
}