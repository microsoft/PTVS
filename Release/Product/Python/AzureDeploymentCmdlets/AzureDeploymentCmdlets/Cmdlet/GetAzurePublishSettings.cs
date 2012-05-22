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
    using System.Security.Permissions;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities;
    using System.Net.Sockets;
    using System.Net;

    /// <summary>
    /// Get publish profile
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "AzurePublishSettings")]
    public class GetAzurePublishSettingsCommand : CmdletBase
    {
        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();
                this.GetAzurePublishSettingsProcess(Resources.PublishSettingsUrl);
            }
            catch (Exception ex)
            {
                SafeWriteError(new ErrorRecord(ex, string.Empty, ErrorCategory.CloseError, null));
            }
        }

        [EnvironmentPermission(SecurityAction.LinkDemand, Unrestricted = true)]
        internal void GetAzurePublishSettingsProcess(string url)
        {
            Validate.ValidateStringIsNullOrEmpty(url, "publish settings url");
            Validate.ValidateInternetConnection();

            General.LaunchWebPage(url);
        }
    }
}