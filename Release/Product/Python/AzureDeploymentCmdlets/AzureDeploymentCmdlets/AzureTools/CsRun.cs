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
using System.Diagnostics;
using System.IO;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.AzureTools
{
    public class CsRun : AzureTool
    {
        public int DeploymentId { get; private set; }
        private string csrunPath;

        public CsRun()
        {
            csrunPath = Path.Combine(base.AzureEmulatorDirectory, Resources.CsRunExe);
        }

        /// <summary>
        /// Deploys package on local machine. This method does following:
        /// 1. Starts compute emulator.
        /// 2. Starts storage emulator.
        /// 3. Remove all previous deployments in the emulator.
        /// 4. Deploys the package on local machine.
        /// </summary>
        /// <param name="packagePath">Path to package which will be deployed</param>
        /// <param name="configPath">Local configuration path to used with the package</param>
        /// <param name="launch">Switch which opens browser for web roles after deployment</param>
        /// <param name="standardOutput">Standard output of deployment</param>
        /// <param name="standardError">Standard error of deployment</param>
        /// <returns>Deployment id associated with the deployment</returns>
        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        public int StartEmulator(string packagePath, string configPath, bool launch, out string standardOutput, out string standardError)
        {
            // Starts compute emulator.
            StartComputeEmulator(out standardOutput, out standardError);

            // Starts storage emulator.
            StartStorageEmulator(out standardOutput, out standardError);

            // Remove all previous deployments in the emulator.
            RemoveAllDeployments(out standardOutput, out standardError);

            // Deploys the package on local machine.
            string arguments = string.Format(Resources.RunInEmulatorArguments, packagePath, configPath, (launch) ? Resources.CsRunLanuchBrowserArg : string.Empty);
            StartCsRunProcess(arguments, out standardOutput, out standardError);

            // Get deployment id for future use.
            DeploymentId = GetDeploymentCount(standardOutput);
            standardOutput = GetRoleInfoMessage(standardOutput);

            return DeploymentId;
        }

        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        public void StopEmulator(out string standardOutput, out string standardError)
        {
            StartCsRunProcess(Resources.CsRunStopEmulatorArg, out standardOutput, out standardError);
        }

        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        private void StartStorageEmulator(out string standardOutput, out string standardError)
        {
            StartCsRunProcess(Resources.CsRunStartStorageEmulatorArg, out standardOutput, out standardError);
        }

        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        private void StartComputeEmulator(out string standardOutput, out string standardError)
        {
            StartCsRunProcess(Resources.CsRunStartComputeEmulatorArg, out standardOutput, out standardError);
        }

        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        public void RemoveDeployment(int deploymentId, out string standardOutput, out string standardError)
        {
            StartCsRunProcess(string.Format(Resources.CsRunRemoveDeploymentArg, deploymentId), out standardOutput, out standardError);
        }

        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        public void RemoveAllDeployments(out string standardOutput, out string standardError)
        {
            StartCsRunProcess(Resources.CsRunRemoveAllDeploymentsArg, out standardOutput, out standardError);
        }

        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        public void UpdateDeployment(int deploymentId, string configPath, out string standardOutput, out string standardError)
        {
            StartCsRunProcess(string.Format(Resources.CsRunUpdateDeploymentArg, deploymentId, configPath), out standardOutput, out standardError);
        }

        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        private void StartCsRunProcess(string arguments, out string standardOutput, out string standardError)
        {
            ProcessStartInfo pi = new ProcessStartInfo(csrunPath, arguments);
            ProcessHelper.StartAndWaitForProcess(pi, out standardOutput, out standardError);

            // If there's an error from the CsRun tool, we want to display that
            // error message.
            if (!string.IsNullOrEmpty(standardError))
            {
                if (!IsStorageEmulatorError(standardError))
                {
                    throw new InvalidOperationException(
                        string.Format(Resources.CsRun_StartCsRunProcess_UnexpectedFailure, standardError));
                }
            }
        }

        private bool IsStorageEmulatorError(string error)
        {
            return error.IndexOf("storage emulator", StringComparison.OrdinalIgnoreCase) != -1;
        }

        private int GetDeploymentCount(string text)
        {
            Regex deploymentRegex = new Regex("deployment16\\((\\d+)\\)", RegexOptions.Multiline);
            int value = -1;

            Match match = deploymentRegex.Match(text);

            if (match.Success)
            {
                string digits = match.Captures[0].Value;
                int.TryParse(digits, out value);
            }

            return value;

        }

        public static string GetRoleInfoMessage(string emulatorOutput)
        {
            var regex = new Regex(Resources.EmulatorOutputSitesRegex);
            var match = regex.Match(emulatorOutput);
            var builder = new StringBuilder();
            while (match.Success)
            {
                builder.AppendLine(string.Format(Resources.EmulatorRoleRunningMessage, match.Value.Substring(0, match.Value.Length - 2)));
                match = match.NextMatch();
            }
            var roleInfo = builder.ToString(0, builder.Length - 2);
            return roleInfo;
        }
    }
}