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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities {
    public enum VirtualEnvName {
        First,
        Second,
        Third,
        Fourth
    }

    public class PythonPaths {
        private static readonly List<PythonInterpreterInformation> _foundInRegistry = PythonRegistrySearch
            .PerformDefaultSearch()
            .Where(pii => pii.Configuration.Id.Contains("PythonCore|") || pii.Configuration.Id.Contains("ContinuumAnalytics|"))
            .ToList();

        public static readonly PythonVersion Python27 = GetCPythonVersion(PythonLanguageVersion.V27, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python35 = GetCPythonVersion(PythonLanguageVersion.V35, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python36 = GetCPythonVersion(PythonLanguageVersion.V36, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python37 = GetCPythonVersion(PythonLanguageVersion.V37, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python38 = GetCPythonVersion(PythonLanguageVersion.V38, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python39 = GetCPythonVersion(PythonLanguageVersion.V39, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python310 = GetCPythonVersion(PythonLanguageVersion.V310, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python311 = GetCPythonVersion(PythonLanguageVersion.V311, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python312 = GetCPythonVersion(PythonLanguageVersion.V312, InterpreterArchitecture.x86);
        public static readonly PythonVersion Python27_x64 = GetCPythonVersion(PythonLanguageVersion.V27, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python35_x64 = GetCPythonVersion(PythonLanguageVersion.V35, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python36_x64 = GetCPythonVersion(PythonLanguageVersion.V36, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python37_x64 = GetCPythonVersion(PythonLanguageVersion.V37, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python38_x64 = GetCPythonVersion(PythonLanguageVersion.V38, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python39_x64 = GetCPythonVersion(PythonLanguageVersion.V39, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python310_x64 = GetCPythonVersion(PythonLanguageVersion.V310, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python311_x64 = GetCPythonVersion(PythonLanguageVersion.V311, InterpreterArchitecture.x64);
        public static readonly PythonVersion Python312_x64 = GetCPythonVersion(PythonLanguageVersion.V312, InterpreterArchitecture.x64);
        public static readonly PythonVersion Anaconda27 = GetAnacondaVersion(PythonLanguageVersion.V27, InterpreterArchitecture.x86);
        public static readonly PythonVersion Anaconda27_x64 = GetAnacondaVersion(PythonLanguageVersion.V27, InterpreterArchitecture.x64);
        public static readonly PythonVersion Anaconda36 = GetAnacondaVersion(PythonLanguageVersion.V36, InterpreterArchitecture.x86);
        public static readonly PythonVersion Anaconda36_x64 = GetAnacondaVersion(PythonLanguageVersion.V36, InterpreterArchitecture.x64);
        public static readonly PythonVersion Anaconda37 = GetAnacondaVersion(PythonLanguageVersion.V37, InterpreterArchitecture.x86);
        public static readonly PythonVersion Anaconda37_x64 = GetAnacondaVersion(PythonLanguageVersion.V37, InterpreterArchitecture.x64);

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
                    return new PythonVersion(new VisualStudioInterpreterConfiguration(
                        CPythonInterpreterFactoryConstants.GetInterpreterId("PythonCore", tag),
                        "Python {0} {1}".FormatInvariant(arch, ver),
                        prefixPath,
                        exePath,
                        pathVar: CPythonInterpreterFactoryConstants.PathEnvironmentVariableName,
                        architecture: arch,
                        version: ver
                    ));
                }
            }
            return null;
        }

        public static IEnumerable<PythonVersion> AnacondaVersions {
            get {
                if (Anaconda37 != null) yield return Anaconda37;
                if (Anaconda37_x64 != null) yield return Anaconda37_x64;
                if (Anaconda36 != null) yield return Anaconda36;
                if (Anaconda36_x64 != null) yield return Anaconda36_x64;
                if (Anaconda27 != null) yield return Anaconda27;
                if (Anaconda27_x64 != null) yield return Anaconda27_x64;
            }
        }

        public static IEnumerable<PythonVersion> Versions {
            get {
                if (Python27 != null) yield return Python27;
                if (Python35 != null) yield return Python35;
                if (Python36 != null) yield return Python36;
                if (Python37 != null) yield return Python37;
                if (Python38 != null) yield return Python38;
                if (Python39 != null) yield return Python39;
                if (Python27_x64 != null) yield return Python27_x64;
                if (Python35_x64 != null) yield return Python35_x64;
                if (Python36_x64 != null) yield return Python36_x64;
                if (Python37_x64 != null) yield return Python37_x64;
                if (Python38_x64 != null) yield return Python38_x64;
                if (Python39_x64 != null) yield return Python39_x64;
            }
        }

        public static PythonVersion LatestVersion {
            get {
                if (Python39 != null) return Python39;
                if (Python38 != null) return Python38;
                if (Python37 != null) return Python37;
                if (Python36 != null) return Python36;
                if (Python35 != null) return Python35;
                if (Python27 != null) return Python27;
                if (Python39_x64 != null) return Python39_x64;
                if (Python38_x64 != null) return Python38_x64;
                if (Python37_x64 != null) return Python37_x64;
                if (Python36_x64 != null) return Python36_x64;
                if (Python35_x64 != null) return Python35_x64;
                if (Python27_x64 != null) return Python27_x64;
                return null;
            }
        }

        /// <summary>
        /// Get the installed Python versions that match the specified name expression and filter.
        /// </summary>
        /// <param name="nameExpr">Name or '|' separated list of names that match the fields on <c>PythonPaths</c>. Ex: "Python36", "Python36|Python36_x64"</param>
        /// <param name="filter">Additional filter.</param>
        /// <returns>Installed Python versions that match the names and filter.</returns>
        public static PythonVersion[] GetVersionsByName(string nameExpr, Predicate<PythonVersion> filter = null) {
            return nameExpr
                .Split('|')
                .Select(v => typeof(PythonPaths).GetField(v, BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as PythonVersion)
                .Where(v => v != null && (filter != null ? filter(v) : true))
                .ToArray();
        }
    }

    public class PythonVersion {
        public readonly Microsoft.PythonTools.Interpreter.InterpreterConfiguration Configuration;
        public readonly bool IsCPython;

        public PythonVersion(Microsoft.PythonTools.Interpreter.InterpreterConfiguration config, bool cPython = false) {
            Configuration = config;
            IsCPython = cPython;
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
        }

        public override string ToString() => Configuration.Description;
        public string PrefixPath => Configuration.GetPrefixPath();
        public string InterpreterPath => Configuration.InterpreterPath;
        public PythonLanguageVersion Version => Configuration.Version.ToLanguageVersion();
        public string Id => Configuration.Id;
        public bool Isx64 => Configuration.Architecture == InterpreterArchitecture.x64;
        public InterpreterArchitecture Architecture => Configuration.Architecture;
    }

    public static class PythonVersionExtensions {
        public static void AssertInstalled(this PythonVersion pyVersion) {
            if (pyVersion == null || !File.Exists(pyVersion.InterpreterPath)) {
                if(pyVersion == null) {
                    Assert.Inconclusive("Python interpreter is not installed. pyVersion is null. ");
                } else {
                    Assert.Inconclusive(string.Format("Python version {0} is not installed.", pyVersion.Configuration.Version.ToString()));
                }
            }
        }

        public static void AssertInstalled(this PythonVersion pyVersion, string customMessage) {
            if (pyVersion == null || !File.Exists(pyVersion.InterpreterPath)) {
                Assert.Inconclusive(customMessage);
            }
        }

        /// <summary>
        /// Creates a Python virtual environment in specified directory and installs the specified packages.
        /// </summary>
        public static string CreateVirtualEnv(this PythonVersion pyVersion, VirtualEnvName envName, IEnumerable<string> packages, string rootDirectory = null) {
            var envPath = pyVersion.CreateVirtualEnv(envName, rootDirectory);
            var envPythonExePath = Path.Combine(envPath, "scripts", "python.exe");
            foreach (var package in packages.MaybeEnumerate()) {
                using (var output = ProcessOutput.RunHiddenAndCapture(envPythonExePath, "-m", "pip", "install", package)) {
                    Assert.IsTrue(output.Wait(TimeSpan.FromSeconds(30)));
                    Assert.AreEqual(0, output.ExitCode);
                }
            }
            return envPath;
        }

        /// <summary>
        /// Creates a Python virtual environment in specified directory.
        /// </summary>
        public static string CreateVirtualEnv(this PythonVersion pyVersion, VirtualEnvName envName, string rootDirectory = null) {
            // Only create this virtual env if it doesn't already exist.
            var envPath = GetVirtualEnvPath(envName, rootDirectory);
            if (!File.Exists(Path.Combine(envPath, "scripts", "python.exe"))) {
                var virtualEnvModule = (pyVersion.Version < PythonLanguageVersion.V30) ? "virtualenv" : "venv";
                using (var p = ProcessOutput.RunHiddenAndCapture(pyVersion.InterpreterPath, "-m", virtualEnvModule, envPath)) {
                    Console.WriteLine(p.Arguments);
                    Assert.IsTrue(p.Wait(TimeSpan.FromMinutes(3)));
                    Console.WriteLine(string.Join(Environment.NewLine, p.StandardOutputLines.Concat(p.StandardErrorLines)));
                    Assert.AreEqual(0, p.ExitCode);
                }
            }

            Assert.IsTrue(File.Exists(Path.Combine(envPath, "scripts", "python.exe")));
            return envPath;
        }

        private static string GetVirtualEnvPath(VirtualEnvName envName, string rootDirectory = null) {
            if (rootDirectory != null) {
                return rootDirectory;
            }
            var root = TestData.GetTempPath("envs");
            return Path.Combine(root, envName.ToString());
        }
    }
}
