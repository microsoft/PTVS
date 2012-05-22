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
    using System.Security.Cryptography.X509Certificates;
    using System.Xml;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;

    /// <summary>
    /// Register the azure publish file downloaded from the portal (includes the certificate and subscription information). Called once for the machine
    /// </summary>
    [Cmdlet(VerbsData.Import, "AzurePublishSettings")]
    public class ImportAzurePublishSettingsCommand : CmdletBase
    {
        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Name of *.PublishSettings file")]
        public string Path { get; set; }

        /// <summary>
        /// Acts as main method of setting the azure publish profile by doing the following:
        /// 1. Extracts the certificate binary from *.azurePublish file
        /// 2. Create a X509Certificate2 certificate and adds it to store
        /// 3. Save the extracted certificate and *.azurePublis file to ..\ApplicationData\Azure SDK folder
        /// </summary>
        /// <returns>Message to display for to the user</returns>
        public string ImportAzurePublishSettingsProcess(string publishSettingsFilePath, string azureSdkPath)
        {
            GlobalComponents components = new GlobalComponents(publishSettingsFilePath, azureSdkPath);
            SafeWriteObject(string.Format(Resources.CertificateImportedMessage, components.Certificate.FriendlyName));
            string msg = string.Format(Resources.PublishSettingsSetSuccessfully, GlobalPathInfo.GlobalSettingsDirectory);
            return msg;
        }

        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();
                string result = this.ImportAzurePublishSettingsProcess(this.ResolvePath(Path), GlobalPathInfo.GlobalSettingsDirectory);
                SafeWriteObject(result);
            }
            catch (Exception ex)
            {
                SafeWriteError(new ErrorRecord(ex, string.Empty, ErrorCategory.CloseError, null));
            }
        }
    }
}