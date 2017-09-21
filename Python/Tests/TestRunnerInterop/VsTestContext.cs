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
using System.Threading;

namespace TestRunnerInterop {
    public sealed class VsTestContext : IDisposable {
        private readonly string _container, _className, _testDataRoot;
        private VsInstance _vs;

        private Dictionary<string, DateTime> _testDataFiles;

        public VsTestContext(
            string container,
            string className,
            string testDataRoot
        ) {
            _container = container;
            _className = className;
            _testDataRoot = testDataRoot ?? GetDefaultTestDataDirectory();
        }

        public void RunTest(string testName, params object[] arguments) {
            if (_vs == null) {
                throw new InvalidOperationException("TestInitialize was not called");
            }
            _vs.RunTest(_container, $"{_className}.{testName}", arguments);
        }

        public void Dispose() {
            if (_vs != null) {
                _vs.Dispose();
                _vs = null;
            }
        }

        public void TestInitialize(string deploymentDirectory) {
            _testDataFiles = GetAllFileInfo(_testDataRoot);

            if (_vs == null || !_vs.IsRunning) {
                _vs?.Dispose();
                _vs = new VsInstance();
                _vs.StartOrRestart(
                    @"C:\Program Files (x86)\Microsoft Visual Studio\Preview\Enterprise\Common7\IDE\devenv.exe",
                    "/rootSuffix Exp",
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
                queue.Enqueue(d);
                result[d] = DateTime.MinValue;
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

        private string GetDefaultTestDataDirectory() {
            var candidate = Environment.GetEnvironmentVariable("_TESTDATA_ROOT_PATH");
            if (Directory.Exists(candidate)) {
                return candidate;
            }

            var rootDir = GetDirectoryAboveContaningFile(Path.GetDirectoryName(GetType().Assembly.Location), "build.root");
            if (!string.IsNullOrEmpty(rootDir)) {
                candidate = Path.Combine(rootDir, "Python", "Tests");
                if (Directory.Exists(Path.Combine(candidate, "TestData"))) {
                    return candidate;
                }
            }

            return null;
        }
    }
}
