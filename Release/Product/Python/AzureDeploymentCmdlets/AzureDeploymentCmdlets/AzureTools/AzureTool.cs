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
using Microsoft.Win32;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities;
using System.IO;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.AzureTools
{
    public class AzureTool
    {
        public string AzureSdkDirectory { get; private set; }
        public string AzureSdkBinDirectory { get; private set; }
        public string AzureEmulatorDirectory { get; private set; }
        public string AzureSdkVersion { get; private set; }

        public AzureTool()
        {
            string min = Resources.MinSupportAzureSdkVersion;
            string max = Resources.MaxSupportAzureSdkVersion;
            RegistryKey key = Registry.LocalMachine.OpenSubKey(Resources.AzureSdkRegistryKeyName);
            AzureSdkVersion = key.GetSubKeyNames().Where(n => (n.CompareTo(min) == 1 && n.CompareTo(max) == -1) || n.CompareTo(min) == 0 || n.CompareTo(max) == 0).Max<string>();
            
            if (string.IsNullOrEmpty(AzureSdkVersion) && key.GetSubKeyNames().Length > 0)
            {
                throw new Exception(string.Format(Resources.AzureSdkVersionNotSupported, min, max));
            }
            else if (string.IsNullOrEmpty(AzureSdkVersion) && key.GetSubKeyNames().Length == 0)
            {
                throw new Exception(Resources.AzureSdkNotInstalledMessage);
            }
            else
            {
                string keyName = Path.Combine(Resources.AzureSdkRegistryKeyName, AzureSdkVersion);
                AzureSdkDirectory = (string)Registry.GetValue(Path.Combine(Registry.LocalMachine.Name, keyName), Resources.AzureSdkInstallPathRegistryKeyValue, null);
                AzureSdkBinDirectory = Path.Combine(AzureSdkDirectory, Resources.RoleBinFolderName);
                AzureEmulatorDirectory = AzureSdkDirectory.Replace(string.Format(Resources.AzureSdkDirectory, AzureSdkVersion), Resources.AzureEmulatorPathPortion);
            }
        }
    }
}