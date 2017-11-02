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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace TestUtilities {
    public class PythonPaths {
        private static readonly List<PythonInterpreterInformation> _foundInRegistry = PythonRegistrySearch
            .PerformDefaultSearch()
            .Where(pii => pii.Configuration.Id.Contains("PythonCore|") || pii.Configuration.Id.Contains("ContinuumAnalytics|"))
            .ToList();

        public static readonly PythonVersion Python26 = GetCPythonVersion(PythonLanguageVersion.V26, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python27 = GetCPythonVersion(PythonLanguageVersion.V27, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python31 = GetCPythonVersion(PythonLanguageVersion.V31, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python32 = GetCPythonVersion(PythonLanguageVersion.V32, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python33 = GetCPythonVersion(PythonLanguageVersion.V33, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python34 = GetCPythonVersion(PythonLanguageVersion.V34, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python35 = GetCPythonVersion(PythonLanguageVersion.V35, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python36 = GetCPythonVersion(PythonLanguageVersion.V36, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python37 = GetCPythonVersion(PythonLanguageVersion.V37, InterpreterArchitecture.x86);
        public static readonly PythonVersion IronPython27 = GetIronPythonVersion(false);
        public static readonly PythonVersion Python26_x64 = GetCPythonVersion(PythonLanguageVersion.V26, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python27_x64 = GetCPythonVersion(PythonLanguageVersion.V27, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python31_x64 = GetCPythonVersion(PythonLanguageVersion.V31, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python32_x64 = GetCPythonVersion(PythonLanguageVersion.V32, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python33_x64 = GetCPythonVersion(PythonLanguageVersion.V33, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python34_x64 = GetCPythonVersion(PythonLanguageVersion.V34, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python35_x64 = GetCPythonVersion(PythonLanguageVersion.V35, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python36_x64 = GetCPythonVersion(PythonLanguageVersion.V36, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python37_x64 = GetCPythonVersion(PythonLanguageVersion.V37, InterpreterArchitecture.x64);
        public static readonly PythonVersion Anaconda27 = GetAnacondaVersion(PythonLanguageVersion.V27, InterpreterArchitecture.x86);
        public static readonly PythonVersion Anaconda27_x64 = GetAnacondaVersion(PythonLanguageVersion.V27, InterpreterArchitecture.x64);
        public static readonly PythonVersion Anaconda36 = GetAnacondaVersion(PythonLanguageVersion.V36, InterpreterArchitecture.x86);
        public static readonly PythonVersion Anaconda36_x64 = GetAnacondaVersion(PythonLanguageVersion.V36, InterpreterArchitecture.x64);
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
                                        new InterpreterConfiguration(
                                            x64 ? "IronPython|2.7-64" : "IronPython|2.7-32",
                                            string.Format("IronPython {0} 2.7", x64 ? "64-bit" : "32-bit"),
                                            res,
                                            Path.Combine(res, exeName),
                                            arch: x64 ? InterpreterArchitecture.x64 : InterpreterArchitecture.x86,
                                            version: new Version(2, 7),
                                            pathVar: "IRONPYTHONPATH"
                                        ),
                                        ironPython: true
                                    );
                                }
                            }
                        }
                    }
                }
            }

            var ver = new PythonVersion(new InterpreterConfiguration(
                "IronPython|2.7-32",
                "IronPython 32-bit 2.7",
                "C:\\Program Files (x86)\\IronPython 2.7\\",
                "C:\\Program Files (x86)\\IronPython 2.7\\" + exeName, 
                arch: InterpreterArchitecture.x86,
                version: new Version(2, 7),
                pathVar: "IRONPYTHONPATH"
            ), ironPython: true);
            if (File.Exists(ver.InterpreterPath)) {
                return ver;
            }
            return null;
        }

        private static PythonVersion GetAnacondaVersion(PythonLanguageVersion version, InterpreterArchitecture arch) {
            var res = _foundInRegistry.FirstOrDefault(ii =>
                ii.Configuration.Id.StartsWith("Global|ContinuumAnalytics|") &&
                ii.Configuration.Architecture == arch &&
                ii.Configuration.Version == version.ToVersion()
            );
            if (res != null) {
                return new PythonVersion(res.Configuration, cPython: true);
            }

            return null;
        }

        private static PythonVersion GetCPythonVersion(PythonLanguageVersion version, InterpreterArchitecture arch) {
            var res = _foundInRegistry.FirstOrDefault(ii => 
                ii.Configuration.Id.StartsWith("Global|PythonCore|") &&
                ii.Configuration.Architecture == arch &&
                ii.Configuration.Version == version.ToVersion()
            );
            if (res != null) {
                return new PythonVersion(res.Configuration, cPython: true);
            }

            var ver = version.ToVersion();
            var tag = ver + (arch == InterpreterArchitecture.x86 ? "-32" : "");
            foreach (var path in new[] {
                string.Format("Python{0}{1}", ver.Major, ver.Minor),
                string.Format("Python{0}{1}_{2}", ver.Major, ver.Minor, arch.ToString("x")),
                string.Format("Python{0}{1}-{2}", ver.Major, ver.Minor, arch.ToString("#")),
                string.Format("Python{0}{1}_{2}", ver.Major, ver.Minor, arch.ToString("#")),
            }) {
                var prefixPath = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive"), "\\", path);
                var exePath = Path.Combine(prefixPath, CPythonInterpreterFactoryConstants.ConsoleExecutable);
                var procArch = (arch == InterpreterArchitecture.x86) ? ProcessorArchitecture.X86 :
                    (arch == InterpreterArchitecture.x64) ? ProcessorArchitecture.Amd64 :
                    ProcessorArchitecture.None;

                if (procArch == Microsoft.PythonTools.Infrastructure.NativeMethods.GetBinaryType(path)) {
                    return new PythonVersion(new InterpreterConfiguration(
                        CPythonInterpreterFactoryConstants.GetInterpreterId("PythonCore", tag),
                        "Python {0} {1}".FormatInvariant(arch, ver),
                        prefixPath,
                        exePath,
                        pathVar: CPythonInterpreterFactoryConstants.PathEnvironmentVariableName,
                        arch: arch,
                        version: ver
                    ));
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
                return new PythonVersion(new InterpreterConfiguration(
                    CPythonInterpreterFactoryConstants.GetInterpreterId(
                        "Jython",
                        version.ToVersion().ToString()
                    ),
                    string.Format("Jython {0}", version.ToVersion()),
                    dir.FullName,
                    interpreter.FullName,
                    version: version.ToVersion()
                ));
            }
            return null;
        }

        public static IEnumerable<PythonVersion> Versions {
            get {
                if (Python26 != null) yield return Python26;
                if (Python27 != null) yield return Python27;
                if (Python31 != null) yield return Python31;
                if (Python32 != null) yield return Python32;
                if (Python33 != null) yield return Python33;
                if (Python34 != null) yield return Python34;
                if (Python35 != null) yield return Python35;
                if (Python36 != null) yield return Python36;
                if (Python37 != null) yield return Python37;
                if (IronPython27 != null) yield return IronPython27;
                if (Python26_x64 != null) yield return Python26_x64;
                if (Python27_x64 != null) yield return Python27_x64;
                if (Python31_x64 != null) yield return Python31_x64;
                if (Python32_x64 != null) yield return Python32_x64;
                if (Python33_x64 != null) yield return Python33_x64;
                if (Python34_x64 != null) yield return Python34_x64;
                if (Python35_x64 != null) yield return Python35_x64;
                if (Python36_x64 != null) yield return Python36_x64;
                if (Python37_x64 != null) yield return Python37_x64;
                if (IronPython27_x64 != null) yield return IronPython27_x64;
                if (Jython27 != null) yield return Jython27;
            }
        }
    }

    public class PythonVersion {
        public readonly InterpreterConfiguration Configuration;
        public readonly bool IsCPython;
        public readonly bool IsIronPython;

        public PythonVersion(InterpreterConfiguration config, bool ironPython = false, bool cPython = false) {
            Configuration = config;
            IsCPython = cPython;
            IsIronPython = ironPython;
        }

        public PythonVersion(string version) {
            PythonVersion selected;
            if (version == "Anaconda27") {
                selected = PythonPaths.Anaconda27 ?? PythonPaths.Anaconda27_x64;
            } else if (version == "Anaconda36") {
                selected = PythonPaths.Anaconda36 ?? PythonPaths.Anaconda36_x64;
            } else {
                var v = System.Version.Parse(version).ToLanguageVersion();
                var candididates = PythonPaths.Versions.Where(pv => pv.IsCPython && pv.Version == v).ToArray();
                if (candididates.Length > 1) {
                    selected = candididates.FirstOrDefault(c => c.Isx64) ?? candididates.First();
                } else {
                    selected = candididates.FirstOrDefault();
                }
            }
            selected.AssertInstalled();

            Configuration = selected.Configuration;
            IsCPython = selected.IsCPython;
            IsIronPython = selected.IsIronPython;
        }

        public override string ToString() => Configuration.Description;
        public string PrefixPath => Configuration.PrefixPath;
        public string InterpreterPath => Configuration.InterpreterPath;
        public PythonLanguageVersion Version => Configuration.Version.ToLanguageVersion();
        public string Id => Configuration.Id;
        public bool Isx64 => Configuration.Architecture == InterpreterArchitecture.x64;
        public InterpreterArchitecture Architecture => Configuration.Architecture;
    }

    public static class PythonVersionExtensions {
        public static void AssertInstalled(this PythonVersion self) {
            if(self == null || !File.Exists(self.InterpreterPath)) {
                Assert.Inconclusive("Python interpreter not installed");
            }
        }
    }
}
