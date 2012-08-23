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
using System.Collections.Generic;
using System.IO;
using Microsoft.PythonTools.Parsing;
using Microsoft.Win32;

namespace TestUtilities {
    public class PythonPaths {
        const string PythonCorePath = "SOFTWARE\\Python\\PythonCore";

        public static readonly PythonVersion Python25 = GetCPythonVersion(PythonLanguageVersion.V25); 
        public static readonly PythonVersion Python26 = GetCPythonVersion(PythonLanguageVersion.V26);
        public static readonly PythonVersion Python27 = GetCPythonVersion(PythonLanguageVersion.V27);
        public static readonly PythonVersion Python30 = GetCPythonVersion(PythonLanguageVersion.V30);
        public static readonly PythonVersion Python31 = GetCPythonVersion(PythonLanguageVersion.V31);
        public static readonly PythonVersion Python32 = GetCPythonVersion(PythonLanguageVersion.V32);
        public static readonly PythonVersion IronPython27 = new PythonVersion("C:\\Program Files (x86)\\IronPython 2.7\\ipy.exe", PythonLanguageVersion.V27);

        private static PythonVersion GetIronPythonVersion() {
            using (var ipy = Registry.LocalMachine.OpenSubKey("SOFTWARE\\IronPython")) {
                if (ipy != null) {
                    using (var twoSeven = ipy.OpenSubKey("2.7")) {
                        if (twoSeven != null) {
                            var installPath = twoSeven.OpenSubKey("InstallPath");
                            if (installPath != null) {
                                var res = installPath.GetValue("") as string;
                                if (res != null) {
                                    return new PythonVersion(Path.Combine(res, "ipy.exe"), PythonLanguageVersion.V27);
                                }
                            }
                        }
                    }
                }
            }

            return new PythonVersion("C:\\Program Files (x86)\\IronPython 2.7\\ipy.exe", PythonLanguageVersion.V27);
        }
        
        private static PythonVersion GetCPythonVersion(PythonLanguageVersion version) {
            foreach (var baseKey in new[] { Registry.LocalMachine, Registry.CurrentUser }) {
                using (var python = baseKey.OpenSubKey(PythonCorePath)) {
                    var res = TryGetCPythonPath(version, python);
                    if (res != null) {
                        return res;
                    }
                }
            }

            if (Environment.Is64BitOperatingSystem) {
                foreach (var baseHive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser }) {
                    var python64 = RegistryKey.OpenBaseKey(baseHive, RegistryView.Registry64).OpenSubKey(PythonCorePath);
                    var res = TryGetCPythonPath(version, python64);
                    if (res != null) {
                        return res;
                    }
                }
            }

            return new PythonVersion("C:\\Python" + version.ToString().Substring(1) + "\\python.exe", version);
        }

        private static PythonVersion TryGetCPythonPath(PythonLanguageVersion version, RegistryKey python) {
            if (python != null) {
                string versionStr = version.ToString().Substring(1);
                versionStr = versionStr[0] + "." + versionStr[1];

                using (var versionKey = python.OpenSubKey(versionStr + "\\InstallPath")) {
                    if (versionKey != null) {
                        var installPath = versionKey.GetValue("");
                        if (installPath != null) {
                            return new PythonVersion(Path.Combine(installPath.ToString(), "python.exe"), version);
                        }
                    }
                }
            }
            return null;
        }

        public static IEnumerable<PythonVersion> Versions {
            get {
                yield return Python25;
                yield return Python26;
                yield return Python27;
                yield return Python30;
                yield return Python31;
                yield return Python31;
                yield return IronPython27;
            }
        }
    }

    public class PythonVersion {
        public readonly string Path;
        public readonly PythonLanguageVersion Version;

        public PythonVersion(string path, PythonLanguageVersion pythonLanguageVersion) {
            Path = path;
            Version = pythonLanguageVersion;
        }
    }
}
