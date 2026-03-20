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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace TestRunnerInterop {
    public sealed class VsTestContext : IDisposable {

        private readonly string _testDataRoot;
        private VsInstance _vs;
        private string _devenvExe;
        private string _devenvExeSource;

        private Dictionary<string, DateTime> _testDataFiles;

        private static readonly Lazy<VsTestContext> _instance = new Lazy<VsTestContext>(() => new VsTestContext());
        public static VsTestContext Instance => _instance.Value;

        private VsTestContext() {
            DefaultTimeout = TimeSpan.FromSeconds(120.0);
            _testDataRoot = GetDefaultTestDataDirectory();
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("_TESTDATA_NO_ROOTSUFFIX"))) {
                RootSuffix = "Exp";
            }
        }

        public TimeSpan DefaultTimeout { get; set; }

        public void RunTest(string container, string fullTestName, TimeSpan timeout, object[] arguments) {
            if (_vs == null) {
                throw new InvalidOperationException("TestInitialize was not called");
            }
            for (int retries = 3;  retries >= 0; --retries) {
                if (!_vs.IsRunning) {
                    Console.WriteLine("Restarting VS because it is not running!");
                    _vs.Restart();
                }
                if (_vs.RunTest(container, fullTestName, timeout, arguments, retries > 0)) {
                    return;
                }
            }
        }

        public void Dispose() {
            if (_vs != null) {
                _vs.Dispose();
                _vs = null;
            }
        }

        public string DevEnvExe {
            get {
                if (_devenvExe == null) {
                    var probes = new List<string>();

                    bool IsPathUnderRoot(string candidatePath, string rootPath) {
                        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(rootPath)) {
                            return false;
                        }

                        try {
                            var fullCandidate = Path.GetFullPath(candidatePath)
                                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            var fullRoot = Path.GetFullPath(rootPath)
                                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                            return fullCandidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(fullCandidate, fullRoot, StringComparison.OrdinalIgnoreCase);
                        } catch (Exception ex) {
                            probes.Add($"path-compare-error: {ex.GetType().Name}: {ex.Message}");
                            return false;
                        }
                    }

                    string ValidateResolvedDevenv(string source, string candidate) {
                        if (string.IsNullOrWhiteSpace(candidate)) {
                            return null;
                        }

                        string fullCandidate;
                        try {
                            fullCandidate = Path.GetFullPath(candidate);
                        } catch (Exception ex) {
                            probes.Add($"{source}: full-path-error: {ex.GetType().Name}: {ex.Message}");
                            return null;
                        }

                        probes.Add($"resolved: {source} => {fullCandidate}");

                        var installUnderTest = Environment.GetEnvironmentVariable("VisualStudio.InstallationUnderTest.Path");
                        if (!string.IsNullOrWhiteSpace(installUnderTest) && !IsPathUnderRoot(fullCandidate, installUnderTest)) {
                            probes.Add($"rejected-outside-install-under-test: {fullCandidate} (root={installUnderTest})");
                            return null;
                        }

                        _devenvExeSource = source;
                        return fullCandidate;
                    }

                    string TryResolveFromBasePath(string envVar, string basePath) {
                        if (string.IsNullOrWhiteSpace(basePath)) {
                            return null;
                        }

                        var trimmed = basePath.Trim();
                        string[] candidates;
                        if (trimmed.EndsWith("devenv.exe", StringComparison.OrdinalIgnoreCase)) {
                            candidates = new[] { trimmed };
                        } else {
                            candidates = new[] {
                                Path.Combine(trimmed, "devenv.exe"),
                                Path.Combine(trimmed, "Common7", "IDE", "devenv.exe")
                            };
                        }

                        foreach (var candidate in candidates) {
                            probes.Add($"{envVar}: {candidate}");
                            if (File.Exists(candidate)) {
                                return ValidateResolvedDevenv(envVar, candidate);
                            }
                        }

                        return null;
                    }

                    string TryResolveViaVsWhere() {
                        var vswhereCandidates = new[] {
                            Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? string.Empty, "Microsoft Visual Studio", "Installer", "vswhere.exe"),
                            Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles") ?? string.Empty, "Microsoft Visual Studio", "Installer", "vswhere.exe")
                        };

                        foreach (var vswhere in vswhereCandidates.Where(File.Exists)) {
                            probes.Add($"vswhere: {vswhere}");

                            try {
                                var psi = new ProcessStartInfo {
                                    FileName = vswhere,
                                    Arguments = "-version [17.0,19.0) -latest -products * -requires Microsoft.VisualStudio.PackageGroup.TestTools.Core -property installationPath",
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    CreateNoWindow = true
                                };

                                using (var process = Process.Start(psi)) {
                                    if (process == null) {
                                        continue;
                                    }

                                    var output = process.StandardOutput.ReadToEnd();
                                    process.WaitForExit(5000);

                                    var installPath = output?.Trim();
                                    if (string.IsNullOrWhiteSpace(installPath)) {
                                        continue;
                                    }

                                    var resolved = TryResolveFromBasePath("vswhere", installPath);
                                    if (!string.IsNullOrEmpty(resolved)) {
                                        return resolved;
                                    }
                                }
                            } catch (Exception ex) {
                                probes.Add($"vswhere-error: {ex.GetType().Name}: {ex.Message}");
                            }
                        }

                        return null;
                    }

                    string TryResolveFromKnownInstallRoots() {
                        foreach (var programFiles in new[] {
                            Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
                            Environment.GetEnvironmentVariable("ProgramFiles"),
                            @"C:\Program Files (x86)",
                            @"C:\Program Files"
                        }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase)) {
                            if (string.IsNullOrWhiteSpace(programFiles)) {
                                continue;
                            }

                            var vsRoot = Path.Combine(programFiles, "Microsoft Visual Studio");
                            probes.Add($"known-root: {vsRoot}");
                            if (!Directory.Exists(vsRoot)) {
                                continue;
                            }

                            foreach (var yearDir in FileUtils.EnumerateDirectories(vsRoot)) {
                                foreach (var skuDir in FileUtils.EnumerateDirectories(yearDir)) {
                                    var resolved = TryResolveFromBasePath("known-install", skuDir);
                                    if (!string.IsNullOrEmpty(resolved)) {
                                        return resolved;
                                    }
                                }
                            }
                        }

                        return null;
                    }

                    foreach (var envVar in new string[] {
                        "VisualStudio.InstallationUnderTest.Path",
                        $"VisualStudio_IDE_{AssemblyVersionInfo.VSVersion}",
                        "VisualStudio_IDE",
                        "VSAPPIDDIR",
                        "DevEnvDir",
                        "VSINSTALLDIR"
                    }) {
                        var envValue = Environment.GetEnvironmentVariable(envVar);
                        if (string.IsNullOrWhiteSpace(envValue)) {
                            continue;
                        }

                        _devenvExe = TryResolveFromBasePath(envVar, envValue);
                        if (!string.IsNullOrEmpty(_devenvExe)) {
                            return _devenvExe;
                        }

                        // Some env vars point directly at IDE folder while others point at install root.
                        var parent = Path.GetDirectoryName(envValue);
                        _devenvExe = TryResolveFromBasePath(envVar + ":Parent", parent);
                        if (!string.IsNullOrEmpty(_devenvExe)) {
                            return _devenvExe;
                        }
                    }

                    _devenvExe = TryResolveViaVsWhere();
                    if (!string.IsNullOrEmpty(_devenvExe)) {
                        return _devenvExe;
                    }

                    _devenvExe = TryResolveFromKnownInstallRoots();
                    if (!string.IsNullOrEmpty(_devenvExe)) {
                        return _devenvExe;
                    }

                    throw new InvalidOperationException(
                        "Cannot locate devenv.exe. "
                        + "CurrentDirectory=" + Environment.CurrentDirectory + "; "
                        + "VisualStudio.InstallationUnderTest.Path=" + (Environment.GetEnvironmentVariable("VisualStudio.InstallationUnderTest.Path") ?? "<null>") + "; "
                        + "VisualStudio_IDE_" + AssemblyVersionInfo.VSVersion + "=" + (Environment.GetEnvironmentVariable($"VisualStudio_IDE_{AssemblyVersionInfo.VSVersion}") ?? "<null>") + "; "
                        + "VisualStudio_IDE=" + (Environment.GetEnvironmentVariable("VisualStudio_IDE") ?? "<null>") + "; "
                        + "VSAPPIDDIR=" + (Environment.GetEnvironmentVariable("VSAPPIDDIR") ?? "<null>") + "; "
                        + "DevEnvDir=" + (Environment.GetEnvironmentVariable("DevEnvDir") ?? "<null>") + "; "
                        + "VSINSTALLDIR=" + (Environment.GetEnvironmentVariable("VSINSTALLDIR") ?? "<null>") + ". "
                        + "Probes=" + string.Join(" | ", probes)
                    );
                }

                if (!string.IsNullOrEmpty(_devenvExe)) {
                    Console.WriteLine("Resolved devenv.exe: " + _devenvExe + (string.IsNullOrEmpty(_devenvExeSource) ? string.Empty : " (source=" + _devenvExeSource + ")"));
                }
                return _devenvExe;
            }
            set {
                _devenvExe = value;
                _devenvExeSource = null;
            }
        }

        public string RootSuffix { get; set; }

        public void TestInitialize(string deploymentDirectory) {
            _testDataFiles = GetAllFileInfo(_testDataRoot);

            if (_vs == null || !_vs.IsRunning) {
                _vs?.Dispose();
                _vs = new VsInstance();
                _vs.StartOrRestart(
                    DevEnvExe,
                    string.IsNullOrEmpty(RootSuffix) ? null : $"/rootSuffix {RootSuffix}",
                    _testDataRoot,
                    Path.Combine(deploymentDirectory, "Temp")
                );
            }
        }

        public void TestCleanup() {
            // TODO: Reset VS state, or close and restart

            // Clean out any ".vs" folders that were created
            foreach (var vsDir in FileUtils.EnumerateDirectories(_testDataRoot)) {
                if (Path.GetFileName(vsDir) == ".vs") {
                    Console.WriteLine($"Deleting: {vsDir}");
                    FileUtils.DeleteDirectory(vsDir);
                }
            }

            // Recursive delete out any ".vs" folders that were created
            //foreach (var dir in FileUtils.EnumerateDirectories(_testDataRoot)) {
            //    foreach (var vsDir in FileUtils.EnumerateDirectories(dir)) {
            //        if (Path.GetFileName(vsDir) == ".vs") {
            //            Console.WriteLine($"Deleting: {vsDir}");
            //            for(int retries = 10; retries > 0; --retries) {
            //                try {
            //                    FileUtils.DeleteDirectory(vsDir);
            //                    break;
            //                } catch (Exception) {
            //                    Thread.Sleep(100);
            //                }
            //            }                       
            //        }
            //    }
            //}

            // Validate that no files in TestData were modified
            var after = GetAllFileInfo(_testDataRoot);
            var deleted = new List<string>();
            var added = new List<string>();
            var modified = new List<string>();
            foreach (var kv in _testDataFiles) {
                if (after.TryGetValue(kv.Key, out DateTime m)) {
                    if (m > kv.Value) {
                        modified.Add(kv.Key);
                    }
                } else {
                    deleted.Add(kv.Key);
                    //if (!kv.Key.Contains(".vs")) {
                    //    deleted.Add(kv.Key);
                    //}

                }
            }
            foreach (var k in after.Keys) {
                if (!_testDataFiles.ContainsKey(k)) {
                    added.Add(k);
                }
            }

            if (added.Count > 0) {
                Console.WriteLine("New files in TestData:\n  " + string.Join("\n  ", added));
                foreach (var f in added.OrderByDescending(s => s.Length)) {
                    if (Directory.Exists(f)) {
                        FileUtils.DeleteDirectory(f);
                    } else {
                        FileUtils.Delete(f);
                    }
                }
            }
            if (deleted.Count > 0) {
                Console.WriteLine("Files missing from TestData:\n  " + string.Join("\n  ", deleted));
            }
            if (modified.Count > 0) {
                Console.WriteLine("Files changed in TestData:\n  " + string.Join("\n  ", modified));
            }
            if (added.Count > 0 || deleted.Count > 0 || modified.Count > 0) {
                throw new Exception("TestData was modified. See console for details");
            }

            _testDataFiles = null;
        }

        private static Dictionary<string, DateTime> GetAllFileInfo(string path) {
            var result = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();
            queue.Enqueue(path);

            bool IsVsArtifactsPath(string p) {
                // Normalize directory separators and check if any segment is '.vs'
                // We only want to ignore .vs directories and all of their descendants.
                // Example: C:\root\TestData\.vs or C:\root\TestData\.vs\Something
                var parts = p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (var part in parts) {
                    if (string.Equals(part, ".vs", StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
                return false;
            }

            foreach(var d in FileUtils.EnumerateDirectories(path)) {
                // Skip any .vs directory and any directory beneath a .vs directory
                if (IsVsArtifactsPath(d)) {
                    continue;
                }
                queue.Enqueue(d);
                result[d] = DateTime.MinValue;
            }
            while (queue.Count > 0) {
                var dir = queue.Dequeue();
                foreach (var f in FileUtils.EnumerateFiles(dir, recurse: false)) {
                    if (IsVsArtifactsPath(f)) {
                        continue; // ignore files generated under .vs
                    }
                    for (int retries = 10; retries > 0; --retries) {
                        try {
                            result[f] = File.GetLastWriteTimeUtc(f);
                            break;
                        } catch (Exception) {
                            Thread.Sleep(100);
                        }
                    }
                }
            }

            return result;
        }

        private static string GetDirectoryAboveContaningFile(string path, string filename) {
            while (!string.IsNullOrEmpty(path) && !File.Exists(Path.Combine(path, filename))) {
                var newPath = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(newPath) || newPath == path) {
                    return null;
                }
                path = newPath;
            }
            return string.IsNullOrEmpty(path) ? null : path;
        }

        private static string GetDefaultTestDataDirectory() {
            var probes = new List<string>();
            void AddProbe(string label, string path) {
                if (!string.IsNullOrEmpty(path)) {
                    probes.Add(label + ": " + path);
                }
            }

            AddProbe("CurrentDirectory", Environment.CurrentDirectory);
            AddProbe("AssemblyLocation", typeof(VsTestContext).Assembly.Location);

            var candidate = Environment.GetEnvironmentVariable("_TESTDATA_ROOT_PATH");
            AddProbe("Env:_TESTDATA_ROOT_PATH", candidate);
            if (Directory.Exists(candidate)) {
                // Support callers that pass the TestData folder itself.
                if (string.Equals(Path.GetFileName(candidate), "TestData", StringComparison.OrdinalIgnoreCase)) {
                    var parent = Path.GetDirectoryName(candidate);
                    if (!string.IsNullOrEmpty(parent)) {
                        AddProbe("ResolvedFromEnvParent", parent);
                        return parent;
                    }
                }

                AddProbe("ResolvedFromEnv", candidate);
                return candidate;
            }

            // Integration test gate runs tests from a binaries drop where TestData
            // is copied adjacent to the test assemblies.
            candidate = Path.GetDirectoryName(typeof(VsTestContext).Assembly.Location);
            while (!string.IsNullOrEmpty(candidate)) {
                AddProbe("Probe:AssemblyAncestor", Path.Combine(candidate, "TestData"));
                if (Directory.Exists(Path.Combine(candidate, "TestData"))) {
                    return candidate;
                }

                var parent = Path.GetDirectoryName(candidate);
                if (string.IsNullOrEmpty(parent) || parent == candidate) {
                    break;
                }
                candidate = parent;
            }

            var rootDir = GetDirectoryAboveContaningFile(Path.GetDirectoryName(typeof(VsTestContext).Assembly.Location), "build.root");
            AddProbe("Probe:build.root", rootDir);
            if (!string.IsNullOrEmpty(rootDir)) {
                AddProbe("Probe:rootDir/TestData", Path.Combine(rootDir, "TestData"));
                if (Directory.Exists(Path.Combine(rootDir, "TestData"))) {
                    return rootDir;
                }
                candidate = Path.Combine(rootDir, "Python", "Tests");
                AddProbe("Probe:Python/Tests/TestData", Path.Combine(candidate, "TestData"));
                if (Directory.Exists(Path.Combine(candidate, "TestData"))) {
                    return candidate;
                }
            }

            throw new InvalidOperationException(
                "Cannot locate TestData directory. "
                + "CurrentDirectory=" + Environment.CurrentDirectory + "; "
                + "AssemblyLocation=" + typeof(VsTestContext).Assembly.Location + ". "
                + "Probes=" + string.Join(" | ", probes)
            );
        }

    }
}
