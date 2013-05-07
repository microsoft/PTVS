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
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace TestUtilities {
    public class PythonPaths {
        const string PythonCorePath = "SOFTWARE\\Python\\PythonCore";

        public static readonly Guid CPythonGuid = new Guid("{2AF0F10D-7135-4994-9156-5D01C9C11B7E}");
        public static readonly Guid CPython64Guid = new Guid("{9A7A9026-48C1-4688-9D5D-E5699D47D074}");
        public static readonly Guid IronPythonGuid = new Guid("{80659AB7-4D53-4E0C-8588-A766116CBD46}");
        public static readonly Guid IronPython64Guid = new Guid("{FCC291AA-427C-498C-A4D7-4502D6449B8C}");

        public static readonly PythonVersion Python25 = GetCPythonVersion(PythonLanguageVersion.V25);
        public static readonly PythonVersion Python26 = GetCPythonVersion(PythonLanguageVersion.V26);
        public static readonly PythonVersion Python27 = GetCPythonVersion(PythonLanguageVersion.V27);
        public static readonly PythonVersion Python30 = GetCPythonVersion(PythonLanguageVersion.V30);
        public static readonly PythonVersion Python31 = GetCPythonVersion(PythonLanguageVersion.V31);
        public static readonly PythonVersion Python32 = GetCPythonVersion(PythonLanguageVersion.V32);
        public static readonly PythonVersion Python33 = GetCPythonVersion(PythonLanguageVersion.V33);
        public static readonly PythonVersion IronPython27 = GetIronPythonVersion(false);
        public static readonly PythonVersion IronPython27_x64 = GetIronPythonVersion(true);

        private static PythonVersion GetIronPythonVersion(bool x64) {
            var exeName = x64 ? "ipy64.exe" : "ipy.exe";
            
            using (var ipy = Registry.LocalMachine.OpenSubKey("SOFTWARE\\IronPython")) {
                if (ipy != null) {
                    using (var twoSeven = ipy.OpenSubKey("2.7")) {
                        if (twoSeven != null) {
                            var installPath = twoSeven.OpenSubKey("InstallPath");
                            if (installPath != null) {
                                var res = installPath.GetValue("") as string;
                                if (res != null) {
                                    return new PythonVersion(Path.Combine(res, exeName), PythonLanguageVersion.V27, IronPythonGuid);
                                }
                            }
                        }
                    }
                }
            }

            var ver = new PythonVersion("C:\\Program Files (x86)\\IronPython 2.7\\" + exeName, PythonLanguageVersion.V27, IronPythonGuid);
            if (File.Exists(ver.Path)) {
                return ver;
            }
            return null;
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

            var path = "C:\\Python" + version.ToString().Substring(1) + "\\python.exe";
            var arch = Microsoft.PythonTools.Analysis.NativeMethods.GetBinaryType(path);
            if (arch == ProcessorArchitecture.X86) {
                return new PythonVersion(path, version, CPythonGuid);
            } else if (arch == ProcessorArchitecture.Amd64) {
                return new PythonVersion(path, version, CPython64Guid);
            } else {
                return null;
            }
        }

        private static PythonVersion TryGetCPythonPath(PythonLanguageVersion version, RegistryKey python) {
            if (python != null) {
                string versionStr = version.ToString().Substring(1);
                versionStr = versionStr[0] + "." + versionStr[1];

                using (var versionKey = python.OpenSubKey(versionStr + "\\InstallPath")) {
                    if (versionKey != null) {
                        var installPath = versionKey.GetValue("");
                        if (installPath != null) {
                            var path = Path.Combine(installPath.ToString(), "python.exe");
                            var arch = Microsoft.PythonTools.Analysis.NativeMethods.GetBinaryType(path);
                            if (arch == ProcessorArchitecture.X86) {
                                return new PythonVersion(path, version, CPythonGuid);
                            } else if (arch == ProcessorArchitecture.Amd64) {
                                return new PythonVersion(path, version, CPython64Guid);
                            } else {
                                return null;
                            }
                        }
                    }
                }
            }
            return null;
        }

        public static IEnumerable<PythonVersion> Versions {
            get {
                if (Python25 != null) yield return Python25;
                if (Python26 != null) yield return Python26;
                if (Python27 != null) yield return Python27;
                if (Python30 != null) yield return Python30;
                if (Python31 != null) yield return Python31;
                if (Python32 != null) yield return Python32;
                if (Python33 != null) yield return Python33;
                if (IronPython27 != null) yield return IronPython27;
                if (IronPython27_x64 != null) yield return IronPython27_x64;
            }
        }
    }

    public class PythonVersion {
        public readonly string Path;
        public readonly PythonLanguageVersion Version;
        public readonly Guid Interpreter;
        public readonly bool IsCPython;

        public PythonVersion(string path, PythonLanguageVersion pythonLanguageVersion, Guid interpreter) {
            Path = path;
            Version = pythonLanguageVersion;
            Interpreter = interpreter;
            IsCPython = (Interpreter == PythonPaths.CPythonGuid || Interpreter == PythonPaths.CPython64Guid);
        }

        public string LibPath {
            get {
                return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), "Lib");
            }
        }
    }

    public static class PythonVersionExtensions {
        public static void AssertInstalled(this PythonVersion self) {
            if(self == null || !File.Exists(self.Path)) {
                Assert.Inconclusive("Python interpreter not installed");
            }
        }
    }
}
