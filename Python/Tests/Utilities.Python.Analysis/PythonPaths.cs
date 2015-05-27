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
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Interpreter;
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

        // Not currently used for auto-detection
        public static readonly Guid JythonGuid = new Guid("{844BA471-72F7-431B-AA3F-675AFD18E230}");

        public static readonly PythonVersion Python25 = GetCPythonVersion(PythonLanguageVersion.V25);
        public static readonly PythonVersion Python26 = GetCPythonVersion(PythonLanguageVersion.V26);
        public static readonly PythonVersion Python27 = GetCPythonVersion(PythonLanguageVersion.V27);
        public static readonly PythonVersion Python30 = GetCPythonVersion(PythonLanguageVersion.V30);
        public static readonly PythonVersion Python31 = GetCPythonVersion(PythonLanguageVersion.V31);
        public static readonly PythonVersion Python32 = GetCPythonVersion(PythonLanguageVersion.V32);
        public static readonly PythonVersion Python33 = GetCPythonVersion(PythonLanguageVersion.V33);
        public static readonly PythonVersion Python34 = GetCPythonVersion(PythonLanguageVersion.V34);
        public static readonly PythonVersion Python35 = GetCPythonVersion(PythonLanguageVersion.V35);
        public static readonly PythonVersion IronPython27 = GetIronPythonVersion(false);
        public static readonly PythonVersion Python25_x64 = GetCPythonVersion(PythonLanguageVersion.V25, true);
        public static readonly PythonVersion Python26_x64 = GetCPythonVersion(PythonLanguageVersion.V26, true);
        public static readonly PythonVersion Python27_x64 = GetCPythonVersion(PythonLanguageVersion.V27, true);
        public static readonly PythonVersion Python30_x64 = GetCPythonVersion(PythonLanguageVersion.V30, true);
        public static readonly PythonVersion Python31_x64 = GetCPythonVersion(PythonLanguageVersion.V31, true);
        public static readonly PythonVersion Python32_x64 = GetCPythonVersion(PythonLanguageVersion.V32, true);
        public static readonly PythonVersion Python33_x64 = GetCPythonVersion(PythonLanguageVersion.V33, true);
        public static readonly PythonVersion Python34_x64 = GetCPythonVersion(PythonLanguageVersion.V34, true);
        public static readonly PythonVersion Python35_x64 = GetCPythonVersion(PythonLanguageVersion.V35, true);
        public static readonly PythonVersion IronPython27_x64 = GetIronPythonVersion(true);

        public static readonly PythonVersion Jython27 = GetJythonVersion(PythonLanguageVersion.V27);

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
                                    return new PythonVersion(
                                        Path.Combine(res, exeName),
                                        PythonLanguageVersion.V27,
                                        x64 ? IronPython64Guid : IronPythonGuid
                                    );
                                }
                            }
                        }
                    }
                }
            }

            var ver = new PythonVersion("C:\\Program Files (x86)\\IronPython 2.7\\" + exeName, PythonLanguageVersion.V27, IronPythonGuid);
            if (File.Exists(ver.InterpreterPath)) {
                return ver;
            }
            return null;
        }

        private static PythonVersion GetCPythonVersion(PythonLanguageVersion version, bool x64 = false) {
            if (!x64) {
                foreach (var baseKey in new[] { Registry.LocalMachine, Registry.CurrentUser }) {
                    using (var python = baseKey.OpenSubKey(PythonCorePath)) {
                        var res = TryGetCPythonPath(version, python, x64);
                        if (res != null) {
                            return res;
                        }
                    }
                }
            }

            if (Environment.Is64BitOperatingSystem && x64) {
                foreach (var baseHive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser }) {
                    var python64 = RegistryKey.OpenBaseKey(baseHive, RegistryView.Registry64).OpenSubKey(PythonCorePath);
                    var res = TryGetCPythonPath(version, python64, x64);
                    if (res != null) {
                        return res;
                    }
                }
            }

            var path = "C:\\Python" + version.ToString().Substring(1) + "\\python.exe";
            var arch = NativeMethods.GetBinaryType(path);
            if (arch == ProcessorArchitecture.X86 && !x64) {
                return new PythonVersion(path, version, CPythonGuid);
            } else if (arch == ProcessorArchitecture.Amd64 && x64) {
                return new PythonVersion(path, version, CPython64Guid);
            }

            if (x64) {
                path = "C:\\Python" + version.ToString().Substring(1) + "_x64\\python.exe";
                arch = NativeMethods.GetBinaryType(path);
                if (arch == ProcessorArchitecture.Amd64) {
                    return new PythonVersion(path, version, CPython64Guid);
                }
            }

            return null;
        }

        private static PythonVersion TryGetCPythonPath(PythonLanguageVersion version, RegistryKey python, bool x64) {
            if (python != null) {
                string versionStr = version.ToString().Substring(1);
                versionStr = versionStr[0] + "." + versionStr[1];
                if (!x64 && version >= PythonLanguageVersion.V35) {
                    versionStr += "-32";
                }

                using (var versionKey = python.OpenSubKey(versionStr + "\\InstallPath")) {
                    if (versionKey != null) {
                        var installPath = versionKey.GetValue("");
                        if (installPath != null) {
                            var path = Path.Combine(installPath.ToString(), "python.exe");
                            var arch = NativeMethods.GetBinaryType(path);
                            if (arch == ProcessorArchitecture.X86) {
                                return x64 ? null : new PythonVersion(path, version, CPythonGuid);
                            } else if (arch == ProcessorArchitecture.Amd64) {
                                return x64 ? new PythonVersion(path, version, CPython64Guid) : null;
                            } else {
                                return null;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private static PythonVersion GetJythonVersion(PythonLanguageVersion version) {
            var candidates = new List<DirectoryInfo>();
            var ver = version.ToVersion();
            var path1 = string.Format("jython{0}{1}*", ver.Major, ver.Minor);
            var path2 = string.Format("jython{0}.{1}*", ver.Major, ver.Minor);
            foreach (var drive in DriveInfo.GetDrives()) {
                if (drive.DriveType != DriveType.Fixed) {
                    continue;
                }

                try {
                    candidates.AddRange(drive.RootDirectory.EnumerateDirectories(path1));
                    candidates.AddRange(drive.RootDirectory.EnumerateDirectories(path2));
                } catch {
                }
            }

            foreach (var dir in candidates) {
                var interpreter = dir.EnumerateFiles("jython.bat").FirstOrDefault();
                if (interpreter == null) {
                    continue;
                }
                var libPath = dir.EnumerateDirectories("Lib").FirstOrDefault();
                if (libPath == null || !libPath.EnumerateFiles("site.py").Any()) {
                    continue;
                }
                return new PythonVersion(interpreter.FullName, version, JythonGuid);
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
                if (Python34 != null) yield return Python34;
                if (Python35 != null) yield return Python35;
                if (IronPython27 != null) yield return IronPython27;
                if (Python25_x64 != null) yield return Python25_x64;
                if (Python26_x64 != null) yield return Python26_x64;
                if (Python27_x64 != null) yield return Python27_x64;
                if (Python30_x64 != null) yield return Python30_x64;
                if (Python31_x64 != null) yield return Python31_x64;
                if (Python32_x64 != null) yield return Python32_x64;
                if (Python33_x64 != null) yield return Python33_x64;
                if (Python34_x64 != null) yield return Python34_x64;
                if (Python35_x64 != null) yield return Python35_x64;
                if (IronPython27_x64 != null) yield return IronPython27_x64;
                if (Jython27 != null) yield return Jython27;
            }
        }

        static class NativeMethods {
            [DllImport("kernel32", EntryPoint = "GetBinaryTypeW", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
            private static extern bool _GetBinaryType(string lpApplicationName, out GetBinaryTypeResult lpBinaryType);

            private enum GetBinaryTypeResult : uint {
                SCS_32BIT_BINARY = 0,
                SCS_DOS_BINARY = 1,
                SCS_WOW_BINARY = 2,
                SCS_PIF_BINARY = 3,
                SCS_POSIX_BINARY = 4,
                SCS_OS216_BINARY = 5,
                SCS_64BIT_BINARY = 6
            }

            public static ProcessorArchitecture GetBinaryType(string path) {
                GetBinaryTypeResult result;

                if (_GetBinaryType(path, out result)) {
                    switch (result) {
                        case GetBinaryTypeResult.SCS_32BIT_BINARY:
                            return ProcessorArchitecture.X86;
                        case GetBinaryTypeResult.SCS_64BIT_BINARY:
                            return ProcessorArchitecture.Amd64;
                        case GetBinaryTypeResult.SCS_DOS_BINARY:
                        case GetBinaryTypeResult.SCS_WOW_BINARY:
                        case GetBinaryTypeResult.SCS_PIF_BINARY:
                        case GetBinaryTypeResult.SCS_POSIX_BINARY:
                        case GetBinaryTypeResult.SCS_OS216_BINARY:
                        default:
                            break;
                    }
                }

                return ProcessorArchitecture.None;
            }
        }
    }

    public class PythonVersion {
        public readonly string InterpreterPath;
        public readonly PythonLanguageVersion Version;
        public readonly Guid Id;
        public readonly bool IsCPython;
        public readonly bool IsIronPython;
        public readonly bool Isx64;

        public PythonVersion(string path, PythonLanguageVersion pythonLanguageVersion, Guid id) {
            InterpreterPath = path;
            Version = pythonLanguageVersion;
            Id = id;
            IsCPython = (Id == PythonPaths.CPythonGuid || Id == PythonPaths.CPython64Guid);
            Isx64 = (Id == PythonPaths.CPython64Guid || Id == PythonPaths.IronPython64Guid);
            IsIronPython = (Id == PythonPaths.IronPythonGuid || Id == PythonPaths.IronPython64Guid);
        }

        public override string ToString() {
            return string.Format(
                "{0}Python {1} {2}",
                IsCPython ? "C" : IsIronPython ? "Iron" : "Other ",
                Version,
                Isx64 ? "x64" : "x86"
            );
        }

        public string PrefixPath {
            get {
                return Path.GetDirectoryName(InterpreterPath);
            }
        }

        public string LibPath {
            get {
                return Path.Combine(PrefixPath, "Lib");
            }
        }

        public InterpreterConfiguration Configuration {
            get {
                return new InterpreterConfiguration(
                    PrefixPath, InterpreterPath, null, LibPath, "PYTHONPATH",
                    Isx64 ? ProcessorArchitecture.Amd64 : ProcessorArchitecture.X86,
                    Version.ToVersion()
                );
            }
        }
    }

    public static class PythonVersionExtensions {
        public static void AssertInstalled(this PythonVersion self) {
            if(self == null || !File.Exists(self.InterpreterPath)) {
                Assert.Inconclusive("Python interpreter not installed");
            }
        }
    }
}
