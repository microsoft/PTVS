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
using System.Reflection;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Infrastructure {
    static class PythonToolsInstallPath {
        private static string GetFromAssembly(Assembly assembly, string filename) {
            string path = Path.Combine(
                Path.GetDirectoryName(assembly.Location),
                filename
            );
            if (File.Exists(path)) {
                return path;
            }
            return string.Empty;
        }

        private static string GetFromRegistry(string filename) {
            const string ROOT_KEY = "Software\\Microsoft\\PythonTools\\" + AssemblyVersionInfo.VSVersion;

            string installDir = null;
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var configKey = baseKey.OpenSubKey(ROOT_KEY)) {
                if (configKey != null) {
                    installDir = configKey.GetValue("InstallDir") as string;
                }
            }

            if (string.IsNullOrEmpty(installDir)) {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32))
                using (var configKey = baseKey.OpenSubKey(ROOT_KEY)) {
                    if (configKey != null) {
                        installDir = configKey.GetValue("InstallDir") as string;
                    }
                }
            }

            if (!String.IsNullOrEmpty(installDir)) {
                var path = Path.Combine(installDir, filename);
                if (File.Exists(path)) {
                    return path;
                }
            }

            return string.Empty;
        }

        public static string TryGetFile(string filename, Assembly assembly = null) {
            string path = GetFromAssembly(assembly ?? typeof(PythonToolsInstallPath).Assembly, filename);

            if (string.IsNullOrEmpty(path)) {
                path = GetFromRegistry(filename);
            }

            return path;
        }

        public static string GetFile(string filename, Assembly assembly = null) {
            var path = TryGetFile(filename, assembly);

#if DEBUG
            if (string.IsNullOrEmpty(path)) {
                Debugger.Launch();
                path =  TryGetFile(filename);
            }
#endif

            if (string.IsNullOrEmpty(path)) {
                throw new InvalidOperationException(
                    "Unable to determine Python Tools installation path"
                );
            }

            return path;
        }
    }
}
