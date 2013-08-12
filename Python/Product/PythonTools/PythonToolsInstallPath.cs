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
        public static string GetFromAssembly(Assembly assembly, string filename) {
            string path = Path.Combine(
                Path.GetDirectoryName(assembly.Location),
                filename
            );
            if (File.Exists(path)) {
                return path;
            }
            return string.Empty;
        }

        public static string GetFromRegistry(
            string extensionName,
            string extensionVersion,
            string filename,
            RegistryKey preferredKey = null
        ) {
            if (preferredKey != null) {
                var installDir = preferredKey.GetValue("InstallDir") as string;
                if (!String.IsNullOrEmpty(installDir)) {
                    var path = Path.Combine(
                        installDir,
                        "Extensions",
                        "Microsoft",
                        extensionName,
                        extensionVersion,
                        filename
                    );
                    if (File.Exists(path)) {
                        return path;
                    }
                }
            }
            
            const string VS_KEY = "Software\\Microsoft\\VisualStudio\\" + AssemblyVersionInfo.VSVersion;

            // Look to the VS install dir.
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var configKey = baseKey.OpenSubKey(VS_KEY)) {
                var installDir = configKey.GetValue("InstallDir") as string;
                if (!String.IsNullOrEmpty(installDir)) {
                    var path = Path.Combine(
                        installDir,
                        "Extensions",
                        "Microsoft",
                        extensionName,
                        extensionVersion,
                        filename
                    );
                    if (File.Exists(path)) {
                        return path;
                    }
                }
            }

            return string.Empty;
        }

        public static string GetFile(string filename) {
            string path = GetFromAssembly(Assembly.GetExecutingAssembly(), filename);
            if (!string.IsNullOrEmpty(path)) {
                return path;
            }

            path = GetFromAssembly(Assembly.GetEntryAssembly(), filename);
            if (!string.IsNullOrEmpty(path)) {
                return path;
            }

            path = GetFromRegistry("Python Tools for Visual Studio", AssemblyVersionInfo.ReleaseVersion, filename);
            if (!string.IsNullOrEmpty(path)) {
                return path;
            }

            Debug.Fail("Unable to determine Python Tools installation path");
            return string.Empty;
        }
    }
}
