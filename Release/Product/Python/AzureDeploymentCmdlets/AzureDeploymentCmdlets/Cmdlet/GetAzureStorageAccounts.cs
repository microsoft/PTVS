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
    using System.Linq;
    using System.Management.Automation;
    using System.ServiceModel;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;
    using System.Text;

    /// <summary>
    /// Lists all storage services underneath the subscription.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "AzureStorageAccounts")]
    public class GetAzureStorageAccountsCommand : ServiceManagementCmdletBase
    {
        [Parameter(Position = 0, Mandatory = false, HelpMessage = "Subscription name")]
        [Alias("sn")]
        public string Subscription { get; set; }

        public GetAzureStorageAccountsCommand() { }

        public GetAzureStorageAccountsCommand(IServiceManagement channel)
        {
            this.Channel = channel;
        }

        public StorageServiceList GetStorageAccounts()
        {
            StorageServiceList storageServices = null;
            storageServices = this.RetryCall(s => this.Channel.ListStorageServices(s));

            return storageServices;
        }

        public string GetStorageServicesProcess(string subscription)
        {
            InitializeArguments(subscription);
            StorageServiceList storageAccounts = GetStorageAccounts();
            GetStorageAccountsKey(storageAccounts);
            string result = FormatResult(storageAccounts);

            return result;
        }

        private void GetStorageAccountsKey(StorageServiceList storageAccounts)
        {
            foreach (StorageService service in storageAccounts)
            {
                service.StorageServiceKeys = this.RetryCall(s => this.Channel.GetStorageKeys(s, service.ServiceName)).StorageServiceKeys;
            }
        }

        public string FormatResult(StorageServiceList storageServices)
        {
            StringBuilder sb = new StringBuilder();

            bool needsSpacing = false;
            foreach (StorageService service in storageServices)
            {
                if (needsSpacing)
                {
                    sb.AppendLine().AppendLine();
                }
                needsSpacing = true;

                sb.AppendFormat("{0, -16}{1}", Resources.StorageAccountName, service.ServiceName);
                sb.AppendLine();
                sb.AppendFormat("{0, -16}{1}", Resources.StoragePrimaryKey, service.StorageServiceKeys.Primary);
                sb.AppendLine();
                sb.AppendFormat("{0, -16}{1}", Resources.StorageSecondaryKey, service.StorageServiceKeys.Secondary);
            }

            return sb.ToString();
        }

        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();
                string result = this.GetStorageServicesProcess(Subscription);
                SafeWriteObject(result);
            }
            catch (Exception ex)
            {
                SafeWriteError(new ErrorRecord(ex, string.Empty, ErrorCategory.CloseError, null));
            }
        }

        private void InitializeArguments(string subscription)
        {
            string serviceName;
            string subscriptionName = base.GetDefaultSettings(null, null, null, null, null, subscription, out serviceName).Subscription;
            subscriptionId = new GlobalComponents(GlobalPathInfo.GlobalSettingsDirectory).GetSubscriptionId(subscriptionName);
        }
    }
}