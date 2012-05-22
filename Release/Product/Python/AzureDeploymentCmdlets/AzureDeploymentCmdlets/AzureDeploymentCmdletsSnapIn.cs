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

namespace Microsoft.PythonTools.AzureDeploymentCmdlets
{
    using System.ComponentModel;
    using System.Management.Automation;

    /// <summary>
    /// Snap-in package definition for the Azure Deployment Tool
    /// </summary>
    [RunInstaller(true)]
    public class AzureDeploymentCmdletsSnapIn : PSSnapIn
    {
        /// <summary>
        /// Create an instance of AzureDeploymentCmdletsSnapIn class
        /// </summary>
        public AzureDeploymentCmdletsSnapIn()
            : base()
        {
        }

        /// <summary>
        /// Specify a description of the AzureDeploymentCmdlets powershell snap-in
        /// </summary>
        public override string Description
        {
            get { return "Cmdlets to create, configure, emulate and deploy Azure services"; }
        }

        /// <summary>
        /// Specify the name of the AzureDeploymentCmdlets powershell snap-in
        /// </summary>
        public override string Name
        {
            get { return "AzureDeploymentCmdlets"; }
        }

        /// <summary>
        /// Specify the vendor for the AzureDeploymentCmdlets powershell snap-in
        /// </summary>
        public override string Vendor
        {
            get { return "Microsoft Corporation"; }
        }
    }
}
