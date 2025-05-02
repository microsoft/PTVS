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

        public string DevEnvExe {
            get {
                if (_devenvExe == null) {
                    foreach (var envVar in new string[] {
                        $"VisualStudio_IDE_{AssemblyVersionInfo.VSVersion}",
                        "VisualStudio_IDE",
                        "VSAPPIDDIR"
                    }) {
                        _devenvExe = Environment.GetEnvironmentVariable(envVar);
                        if (string.IsNullOrEmpty(_devenvExe)) {
                            continue;
                        }
                        _devenvExe = Path.Combine(_devenvExe, "devenv.exe");
                        if (File.Exists(_devenvExe)) {
                            return _devenvExe;
                        }

                        _devenvExe = Path.Combine(Path.GetDirectoryName(_devenvExe), "Common7", "IDE", "devenv.exe");
                        if (File.Exists(_devenvExe)) {
                            return _devenvExe;
                        }

                        _devenvExe = null;
                    }
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

            foreach(var d in FileUtils.EnumerateDirectories(path)) {
                //if (!Path.GetFileName(d).Contains(".vs")) {
                if (Path.GetFileName(d) != ".vs") {
                    queue.Enqueue(d);
                    result[d] = DateTime.MinValue;
                }
            }
            while (queue.Count > 0) {
                var dir = queue.Dequeue();
                foreach (var f in FileUtils.EnumerateFiles(dir, recurse: false)) {
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
            while (!File.Exists(Path.Combine(path, filename))) {
                var newPath = Path.GetDirectoryName(path);
                if (newPath == path) {
                    return null;
                }
                path = newPath;
            }
            return path;
        }

        private static string GetDefaultTestDataDirectory() {
            var candidate = Environment.GetEnvironmentVariable("_TESTDATA_ROOT_PATH");
            if (Directory.Exists(candidate)) {
                return candidate;
            }

            var rootDir = GetDirectoryAboveContaningFile(Path.GetDirectoryName(typeof(VsTestContext).Assembly.Location), "build.root");
            if (!string.IsNullOrEmpty(rootDir)) {
                if (Directory.Exists(Path.Combine(rootDir, "TestData"))) {
                    return rootDir;
                }
                candidate = Path.Combine(rootDir, "Python", "Tests");
                if (Directory.Exists(Path.Combine(candidate, "TestData"))) {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Cannot locate TestData directory from " + typeof(VsTestContext).Assembly.Location);
        }

    }
}
