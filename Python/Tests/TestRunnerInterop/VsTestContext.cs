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

        private static string TryResolveDevenvFromBasePath(string basePath) {
            if (string.IsNullOrWhiteSpace(basePath)) {
                return null;
            }

            var trimmedPath = basePath.Trim();
            var candidatePaths = new[] {
                trimmedPath,
                Path.Combine(trimmedPath, "devenv.exe"),
                Path.Combine(trimmedPath, "Common7", "IDE", "devenv.exe")
            }
            .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var candidatePath in candidatePaths) {
                if (File.Exists(candidatePath) && string.Equals(Path.GetFileName(candidatePath), "devenv.exe", StringComparison.OrdinalIgnoreCase)) {
                    return candidatePath;
                }
            }

            return null;
        }

        private static string TryResolveDevenvViaVswhere() {
            var vswherePaths = new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio", "Installer", "vswhere.exe")
            }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var vswherePath in vswherePaths) {
                if (!File.Exists(vswherePath)) {
                    continue;
                }

                try {
                    var startInfo = new ProcessStartInfo {
                        FileName = vswherePath,
                        Arguments = "-version [17.0,19.0) -latest -products * -requires Microsoft.VisualStudio.PackageGroup.TestTools.Core -property installationPath",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(startInfo)) {
                        var output = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();

                        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output)) {
                            continue;
                        }

                        var devenvPath = Path.Combine(output, "Common7", "IDE", "devenv.exe");
                        if (File.Exists(devenvPath)) {
                            return devenvPath;
                        }
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"Failed to resolve devenv via vswhere '{vswherePath}': {ex.Message}");
                }
            }

            return null;
        }

        private static string TryResolveDevenvFromProgramFiles() {
            var roots = new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio")
            }
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var root in roots) {
                try {
                    var devenvPath = Directory.EnumerateFiles(root, "devenv.exe", SearchOption.AllDirectories)
                        .Where(path => path.IndexOf(Path.Combine("Common7", "IDE", "devenv.exe"), StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(devenvPath)) {
                        return devenvPath;
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"Failed to enumerate Visual Studio installs under '{root}': {ex.Message}");
                }
            }

            return null;
        }

        public string DevEnvExe {
            get {
                if (_devenvExe == null) {
                    foreach (var envVar in new string[] {
                        $"VisualStudio_IDE_{AssemblyVersionInfo.VSVersion}",
                        "VisualStudio_IDE",
                        "VSAPPIDDIR",
                        "DevEnvDir",
                        "VSINSTALLDIR"
                    }) {
                        var envValue = Environment.GetEnvironmentVariable(envVar);
                        if (string.IsNullOrEmpty(envValue)) {
                            continue;
                        }

                        Console.WriteLine($"Checking devenv environment variable {envVar}: '{envValue}'");
                        _devenvExe = TryResolveDevenvFromBasePath(envValue);
                        if (!string.IsNullOrEmpty(_devenvExe)) {
                            return _devenvExe;
                        }
                    }

                    _devenvExe = TryResolveDevenvViaVswhere();
                    if (!string.IsNullOrEmpty(_devenvExe)) {
                        Console.WriteLine($"Resolved devenv via vswhere: '{_devenvExe}'");
                        return _devenvExe;
                    }

                    _devenvExe = TryResolveDevenvFromProgramFiles();
                    if (!string.IsNullOrEmpty(_devenvExe)) {
                        Console.WriteLine($"Resolved devenv via Program Files search: '{_devenvExe}'");
                        return _devenvExe;
                    }

                    throw new InvalidOperationException("Could not locate devenv.exe from VisualStudio_IDE_*, VisualStudio_IDE, VSAPPIDDIR, DevEnvDir, VSINSTALLDIR, vswhere, or Program Files search.");
                }
                return _devenvExe;
            }
            set {
                _devenvExe = value;
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

        private static string GetParentDirectory(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return null;
            }

            var trimmedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetDirectoryName(trimmedPath);
        }

        private static bool HasTestDataDirectory(string rootPath) {
            return !string.IsNullOrWhiteSpace(rootPath) &&
                Directory.Exists(rootPath) &&
                Directory.Exists(Path.Combine(rootPath, "TestData"));
        }

        private static string ResolveTestDataRoot(string candidatePath) {
            if (string.IsNullOrWhiteSpace(candidatePath) || !Directory.Exists(candidatePath)) {
                return null;
            }

            if (string.Equals(Path.GetFileName(candidatePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "TestData", StringComparison.OrdinalIgnoreCase)) {
                return GetParentDirectory(candidatePath);
            }

            if (HasTestDataDirectory(candidatePath)) {
                return candidatePath;
            }

            return null;
        }

        private static void LogExistingDirectories(string label, IEnumerable<string> candidatePaths) {
            var existingDirectories = candidatePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(Directory.Exists)
                .ToArray();

            Console.WriteLine(label);
            if (existingDirectories.Length == 0) {
                Console.WriteLine("  <none>");
                return;
            }

            foreach (var existingDirectory in existingDirectories) {
                Console.WriteLine($"  {existingDirectory}");
            }
        }

        private static void LogChildDirectories(string label, IEnumerable<string> candidatePaths) {
            Console.WriteLine(label);

            foreach (var candidatePath in candidatePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(Directory.Exists)) {
                Console.WriteLine($"  {candidatePath}");

                try {
                    var childDirectories = Directory.EnumerateDirectories(candidatePath)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    if (childDirectories.Length == 0) {
                        Console.WriteLine("    <no child directories>");
                        continue;
                    }

                    foreach (var childDirectory in childDirectories) {
                        Console.WriteLine($"    {childDirectory}");
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"    <failed to enumerate child directories: {ex.Message}>");
                }
            }
        }

        private static string GetDirectoryAboveContaningFile(string path, string filename) {
            while (!string.IsNullOrEmpty(path) && !File.Exists(Path.Combine(path, filename))) {
                var newPath = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(newPath) || string.Equals(newPath, path, StringComparison.OrdinalIgnoreCase)) {
                    return null;
                }
                path = newPath;
            }
            return path;
        }

        private static string GetDefaultTestDataDirectory() {
            var envRoot = Environment.GetEnvironmentVariable("_TESTDATA_ROOT_PATH");
            var appBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var assemblyDirectory = Path.GetDirectoryName(typeof(VsTestContext).Assembly.Location);
            var rootDir = GetDirectoryAboveContaningFile(assemblyDirectory, "build.root");
            var pythonTestsRoot = !string.IsNullOrEmpty(rootDir)
                ? Path.Combine(rootDir, "Python", "Tests")
                : null;

            var candidateRoots = new[] {
                envRoot,
                rootDir,
                pythonTestsRoot,
                appBaseDirectory,
                GetParentDirectory(appBaseDirectory),
                assemblyDirectory,
                GetParentDirectory(assemblyDirectory),
                @"C:\Test\Containers\PythonToolsUITestsRunner\TestData",
                @"C:\Test\Containers\PythonToolsUITestsRunner",
                @"C:\Test\Containers",
                @"C:\Test"
            }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

            LogExistingDirectories("Existing TestData directory candidates:", candidateRoots);
            LogChildDirectories("Child directories under existing candidates:", candidateRoots);

            if (!string.IsNullOrWhiteSpace(envRoot)) {
                var envTestDataPath = Path.Combine(envRoot, "TestData");
                Console.WriteLine($"Checking _TESTDATA_ROOT_PATH candidate: '{envRoot}' (TestData exists: {Directory.Exists(envTestDataPath)})");
                var resolvedEnvRoot = ResolveTestDataRoot(envRoot);
                if (!string.IsNullOrEmpty(resolvedEnvRoot)) {
                    return resolvedEnvRoot;
                }
            }

            if (!string.IsNullOrEmpty(rootDir)) {
                Console.WriteLine($"Checking repo-root TestData candidate: '{rootDir}'");
                var resolvedRootDir = ResolveTestDataRoot(rootDir);
                if (!string.IsNullOrEmpty(resolvedRootDir)) {
                    return resolvedRootDir;
                }

                Console.WriteLine($"Checking Python\\Tests TestData candidate: '{pythonTestsRoot}'");
                var resolvedPythonTestsRoot = ResolveTestDataRoot(pythonTestsRoot);
                if (!string.IsNullOrEmpty(resolvedPythonTestsRoot)) {
                    return resolvedPythonTestsRoot;
                }
            }

            foreach (var candidateRoot in candidateRoots) {
                var testDataPath = Path.Combine(candidateRoot, "TestData");
                Console.WriteLine($"Checking TestData root candidate: '{candidateRoot}' (TestData exists: {Directory.Exists(testDataPath)})");
                var resolvedCandidateRoot = ResolveTestDataRoot(candidateRoot);
                if (!string.IsNullOrEmpty(resolvedCandidateRoot)) {
                    return resolvedCandidateRoot;
                }
            }

            throw new InvalidOperationException("Cannot locate TestData directory from " + typeof(VsTestContext).Assembly.Location);
        }

    }
}