// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Settings;

namespace Microsoft.PythonTools.Debugger {
    [ComVisible(true)]
    [Guid(DebugAdapterLauncherCLSIDNoBraces)]
    public sealed class DebugAdapterLauncher : IAdapterLauncher {
        public const string DebugAdapterLauncherCLSIDNoBraces = "C2990BF1-A87B-4459-9478-322482C535D6";
        public const string DebugAdapterLauncherCLSID = "{"+ DebugAdapterLauncherCLSIDNoBraces + "}";
        public const string VSCodeDebugEngineId = "{86432F39-ADFD-4C56-AA8F-AF8FCDC66039}";
        public static Guid VSCodeDebugEngine = new Guid(VSCodeDebugEngineId);

        public DebugAdapterLauncher(){}

        public void Initialize(IDebugAdapterHostContext context) {
        }

        public ITargetHostProcess LaunchAdapter(IAdapterLaunchInfo launchInfo, ITargetHostInterop targetInterop) {
            // ITargetHostInterop provides a convenience wrapper to start the process
            // return targetInterop.ExecuteCommandAsync(path, "");

            // If you need more control use the DebugAdapterProcess
            if(launchInfo.LaunchType == LaunchType.Attach) {
                return DebugAdapterRemoteProcess.Attach(launchInfo.LaunchJson);
            }
            return DebugAdapterProcess.Start(launchInfo.LaunchJson);
        }


        private string LoadString(string name, string category)
        {
            const string _optionsKey = "Options";
            const string BaseRegistryKey = "PythonTools";

            var settingsManager = SettingsManagerCreator.GetSettingsManager((IServiceProvider)Package.GetGlobalService(typeof(IServiceProvider)));
            var settingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
            var path = BaseRegistryKey + "\\" + _optionsKey + "\\" + category;

            if (!settingsStore.CollectionExists(path) || !settingsStore.PropertyExists(path, name))
            {
                return null;
            }

            return settingsStore.GetString(path, name, "");
        }

        private bool? LoadBoolean(string name, string category)
        {
            string res = LoadString(name, category);
            if (res == null)
            {
                return null;
            }

            bool val;
            if (bool.TryParse(res, out val))
            {
                return val;
            }
            return null;
        }

        public void UpdateLaunchOptions(IAdapterLaunchInfo launchInfo) {
            if(launchInfo.LaunchType == LaunchType.Attach) {
                launchInfo.DebugPort.GetPortName(out string uri);

                bool tempValue = LoadBoolean("ShowReturnValue", "Advanced") ?? true;
                
                JObject obj = new JObject()
                {
                    ["remote"] = uri,
                    ["options"] = "SHOW_RETURN_VALUE={0}".FormatInvariant(tempValue)
                };

                launchInfo.LaunchJson = obj.ToString();
            }
        }
    }
}
