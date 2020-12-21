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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.IO;
using EnvDTE;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Infrastructure {
    internal static class SettingsManagerCreator {
        public static SettingsManager GetSettingsManager(IServiceProvider provider) {
            SettingsManager settings = null;
            string devenvPath = null;
            if (provider == null) {
                provider = ServiceProvider.GlobalProvider;
            }

            if (provider != null) {
                try {
                    settings = new ShellSettingsManager(provider);
                } catch (NotSupportedException) {
                    var dte = (DTE)provider.GetService(typeof(DTE));
                    if (dte != null) {
                        devenvPath = dte.FullName;
                    }
                }
            }

            if (settings == null) {
                if (!File.Exists(devenvPath)) {
                    // Running outside VS, so we need to guess which SKU of VS
                    // is being used. This will work correctly if any one SKU is
                    // installed, but may select the wrong SKU if multiple are
                    // installed. As a result, custom environments may not be
                    // available when building or testing Python projects.
                    string devenvRoot = null;
                    using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    using (var key = root.OpenSubKey(string.Format(@"Software\Microsoft\VisualStudio\{0}\Setup\VS", AssemblyVersionInfo.VSVersion))) {
                        if (key != null) {
                            devenvRoot = key.GetValue("ProductDir") as string;
                        }
                    }
                    if (Directory.Exists(devenvRoot)) {
                        foreach (var subPath in new[] {
                            "Common7\\IDE\\devenv.exe",
                            "Common7\\IDE\\vwdexpress.exe",
                            "Common7\\IDE\\wdexpress.exe"
                        }) {
                            devenvPath = Path.Combine(devenvRoot, subPath);
                            if (File.Exists(devenvPath)) {
                                break;
                            }
                            devenvPath = null;
                        }
                    }
                }
                if (!File.Exists(devenvPath)) {
                    throw new InvalidOperationException("Cannot find settings store for Visual Studio " + AssemblyVersionInfo.VSVersion);
                }
#if DEBUG
                settings = ExternalSettingsManager.CreateForApplication(devenvPath, "Exp");
#else
                settings = ExternalSettingsManager.CreateForApplication(devenvPath);
#endif
            }

            return settings;
        }
    }
}
