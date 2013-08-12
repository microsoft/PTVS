/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Microsoft.PythonTools {
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

            string installDir;
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var configKey = baseKey.OpenSubKey(ROOT_KEY)) {
                installDir = configKey.GetValue("InstallDir") as string;
            }

            if (string.IsNullOrEmpty(installDir)) {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32))
                using (var configKey = baseKey.OpenSubKey(ROOT_KEY)) {
                    installDir = configKey.GetValue("InstallDir") as string;
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

        public static string GetFile(string filename) {
            string path = GetFromAssembly(typeof(PythonToolsInstallPath).Assembly, filename);
            if (!string.IsNullOrEmpty(path)) {
                return path;
            }

            path = GetFromRegistry(filename);
            if (!string.IsNullOrEmpty(path)) {
                return path;
            }

            throw new InvalidOperationException(
                "Unable to determine Python Tools installation path"
            );
        }
    }
}
