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
using System.Diagnostics;
using System.Security.Permissions;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities
{
    internal static class ProcessHelper
    {
        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        internal static void Start(string target)
        {
            Process.Start(target);
        }

        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        internal static void StartAndWaitForProcess(ProcessStartInfo processInfo, out string standardOutput, out string standardError)
        {
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;

            Process p = Process.Start(processInfo);
            p.WaitForExit();
            standardOutput = p.StandardOutput.ReadToEnd();
            standardError = p.StandardError.ReadToEnd();
        }
    }
}
