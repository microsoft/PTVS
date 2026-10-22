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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Threading;
using TestUtilities;

namespace PythonToolsTests {
    [TestClass]
    public class CondaInterpreterFactoryTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CondaWatchEnvironmentsTxtWithoutCondafolder() {
            // We start with no .conda folder
            var userProfileFolder = TestData.GetTempPath();
            string condaFolder = Path.Combine(userProfileFolder, ".conda");

            // We create .conda folder and environments.txt
            Action triggerDiscovery = () => {
                Directory.CreateDirectory(condaFolder);
                File.WriteAllText(Path.Combine(condaFolder, "environments.txt"), string.Empty);
            };

            TestTriggerDiscovery(userProfileFolder, triggerDiscovery);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CondaWatchEnvironmentsTxtWithCondafolder() {
            // We start with a .conda folder but no environments.txt
            var userProfileFolder = TestData.GetTempPath();
            string condaFolder = Path.Combine(userProfileFolder, ".conda");
            Directory.CreateDirectory(condaFolder);

            // We create environments.txt
            Action triggerDiscovery = () => {
                File.WriteAllText(Path.Combine(condaFolder, "environments.txt"), string.Empty);
            };

            TestTriggerDiscovery(userProfileFolder, triggerDiscovery);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void CondaWatchEnvironmentsTxtWithCondafolderAndEnvTxt() {
            // We start with a .conda folder and environments.txt
            var userProfileFolder = TestData.GetTempPath();
            string condaFolder = Path.Combine(userProfileFolder, ".conda");
            Directory.CreateDirectory(condaFolder);
            File.WriteAllText(Path.Combine(condaFolder, "environments.txt"), string.Empty);

            // We modify environments.txt
            Action triggerDiscovery = () => {
                File.WriteAllText(Path.Combine(condaFolder, "environments.txt"), string.Empty);
            };

            TestTriggerDiscovery(userProfileFolder, triggerDiscovery);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        [TestCategory("10s")]
        public void CondaActivationTimesOut() {
            var condaRoot = TestData.GetTempPath();
            try {
                var scripts = Path.Combine(condaRoot, "scripts");
                Directory.CreateDirectory(scripts);

                var condaExe = Path.Combine(scripts, "conda.exe");
                File.WriteAllText(condaExe, string.Empty);
                File.WriteAllText(Path.Combine(scripts, "activate.bat"), "@echo off\r\n:wait\r\ngoto wait\r\n");

                var sw = Stopwatch.StartNew();
                var env = CondaUtils.GetActivationEnvironmentVariablesForPrefixAsync(condaExe, null, TimeSpan.FromMilliseconds(100)).WaitAndUnwrapExceptions();
                sw.Stop();

                Assert.AreEqual(0, env.Count());
                Assert.IsTrue(sw.Elapsed < TimeSpan.FromSeconds(5), "Conda activation did not honor the timeout.");
            } finally {
                try {
                    Directory.Delete(condaRoot, true);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        [TestCategory("10s")]
        public void CondaActivationTimeoutKillsProcessTree() {
            var condaRoot = TestData.GetTempPath();
            Task<IEnumerable<KeyValuePair<string, string>>> activationTask = null;
            Process wrapper = null;
            Process child = null;
            Process grandchild = null;
            Process unrelated = null;

            try {
                var scripts = Path.Combine(condaRoot, "scripts");
                Directory.CreateDirectory(scripts);

                var condaExe = Path.Combine(scripts, "conda.exe");
                var processIdsPath = Path.Combine(scripts, "process-ids.txt");
                var intermediaryReleasePath = Path.Combine(scripts, "release-intermediary");
                var waitScript = Path.Combine(scripts, "wait-tree.ps1");
                File.WriteAllText(condaExe, string.Empty);
                File.WriteAllText(
                    waitScript,
                    "$grandchild = Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep -Seconds 300') -PassThru\r\n" +
                    "Set-Content -LiteralPath $args[0] -Value @($PID, $grandchild.Id)\r\n" +
                    "while (-not (Test-Path -LiteralPath $args[1])) { Start-Sleep -Milliseconds 10 }\r\n"
                );
                File.WriteAllText(
                    Path.Combine(scripts, "activate.bat"),
                    "@echo off\r\n" +
                    "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%~dp0wait-tree.ps1\" \"%~dp0process-ids.txt\" \"%~dp0release-intermediary\"\r\n" +
                    ":wait\r\n" +
                    "goto wait\r\n"
                );

                unrelated = Process.Start(new ProcessStartInfo {
                    FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                    Arguments = "-NoProfile -Command \"Start-Sleep -Seconds 300\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                });
                activationTask = CondaUtils.GetActivationEnvironmentVariablesForPrefixAsync(
                    condaExe,
                    null,
                    TimeSpan.FromSeconds(5)
                );

                var processIds = WaitForProcessIds(processIdsPath, 2, TimeSpan.FromSeconds(4));
                child = Process.GetProcessById(processIds[0]);
                grandchild = Process.GetProcessById(processIds[1]);
                wrapper = Process.GetProcessById(GetParentProcessId(child.Id));
                File.WriteAllText(intermediaryReleasePath, string.Empty);
                AssertProcessExited(child, "Short-lived Conda activation intermediary");

                Assert.IsTrue(
                    SpinWait.SpinUntil(() => activationTask.IsCompleted, TimeSpan.FromSeconds(10)),
                    "Conda activation process-tree cleanup did not complete within the test bound."
                );
                var env = activationTask.WaitAndUnwrapExceptions();

                Assert.AreEqual(0, env.Count());
                AssertProcessExited(wrapper, "Conda activation wrapper");
                AssertProcessExited(grandchild, "Conda activation grandchild");
                Assert.IsFalse(unrelated.HasExited, "An unrelated process was terminated with the Conda activation tree.");
            } finally {
                TerminateTestProcess(grandchild);
                TerminateTestProcess(child);
                TerminateTestProcess(wrapper);
                TerminateTestProcess(unrelated);

                if (activationTask != null && !activationTask.IsCompleted) {
                    try {
                        activationTask.Wait(TimeSpan.FromSeconds(5));
                    } catch (AggregateException) {
                    }
                }

                grandchild?.Dispose();
                child?.Dispose();
                wrapper?.Dispose();
                unrelated?.Dispose();

                try {
                    Directory.Delete(condaRoot, true);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        [TestCategory("10s")]
        public void CondaActivationTimeoutLogsTelemetry() {
            var condaRoot = TestData.GetTempPath();
            try {
                var scripts = Path.Combine(condaRoot, "scripts");
                Directory.CreateDirectory(scripts);

                var condaExe = Path.Combine(scripts, "conda.exe");
                File.WriteAllText(condaExe, string.Empty);
                File.WriteAllText(Path.Combine(scripts, "activate.bat"), "@echo off\r\n:wait\r\ngoto wait\r\n");

                var logger = new TestLogger();
                var env = CondaUtils.GetActivationEnvironmentVariablesForPrefixAsync(condaExe, null, TimeSpan.FromMilliseconds(100), logger).WaitAndUnwrapExceptions();

                Assert.AreEqual(0, env.Count());
                Assert.AreEqual("CondaActivationTimeout", logger.EventName);
                Assert.AreEqual(true, logger.Properties["VS.Python.CondaActivation.IsRootEnvironment"]);
                Assert.AreEqual(100.0, logger.Measurements["VS.Python.CondaActivation.TimeoutMilliseconds"]);
            } finally {
                try {
                    Directory.Delete(condaRoot, true);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        [TestCategory("10s")]
        public void CondaActivationTimeoutIsNotCached() {
            var condaRoot = Path.Combine(TestData.GetTempPath(), "conda%&^()!root");
            var prefixPath = Path.Combine(condaRoot, "prefix%&^()!path");
            try {
                var scripts = Path.Combine(condaRoot, "scripts");
                Directory.CreateDirectory(scripts);

                var condaExe = Path.Combine(scripts, "conda.exe");
                var activateBat = Path.Combine(scripts, "activate.bat");
                File.WriteAllText(condaExe, string.Empty);
                File.WriteAllText(activateBat, "@echo off\r\n:wait\r\ngoto wait\r\n");

                var timedOutEnv = CondaUtils.GetActivationEnvironmentVariablesForPrefixAsync(condaExe, prefixPath, TimeSpan.FromMilliseconds(100)).WaitAndUnwrapExceptions();
                Assert.AreEqual(0, timedOutEnv.Count());

                var emitEnv = Path.Combine(scripts, "emit-env.ps1");
                File.WriteAllText(emitEnv, "$payload = 'x' * 65536\r\nWrite-Output '!!!ENVIRONMENT MARKER!!!'\r\nWrite-Output ('{\"PTVS_TEST_ENV\":\"success\",\"PTVS_TEST_PAYLOAD\":\"' + $payload + '\"}')\r\n");
                File.WriteAllText(activateBat, "@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%~dp0emit-env.ps1\"\r\nexit 0\r\n");

                var env = CondaUtils.GetActivationEnvironmentVariablesForPrefixAsync(condaExe, prefixPath, TimeSpan.FromSeconds(5)).WaitAndUnwrapExceptions();

                Assert.AreEqual("success", env.Single(kv => kv.Key == "PTVS_TEST_ENV").Value);
                Assert.AreEqual(65536, env.Single(kv => kv.Key == "PTVS_TEST_PAYLOAD").Value.Length);
            } finally {
                try {
                    Directory.Delete(condaRoot, true);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
            }
        }

        private static int[] WaitForProcessIds(string path, int count, TimeSpan timeout) {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout) {
                try {
                    if (File.Exists(path)) {
                        var processIds = File.ReadAllLines(path)
                            .Select(line => {
                                int processId;
                                return int.TryParse(line, out processId) ? processId : 0;
                            })
                            .Where(processId => processId > 0)
                            .ToArray();
                        if (processIds.Length == count) {
                            return processIds;
                        }
                    }
                } catch (IOException) {
                }

                Thread.Sleep(25);
            }

            Assert.Fail("Timed out waiting for the activation process tree to start.");
            return new int[0];
        }

        private static int GetParentProcessId(int processId) {
            var snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
            if (snapshot == InvalidHandleValue) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try {
                var entry = new ProcessEntry32 {
                    Size = (uint)Marshal.SizeOf(typeof(ProcessEntry32))
                };
                if (Process32First(snapshot, ref entry)) {
                    do {
                        if (entry.ProcessId == processId) {
                            return (int)entry.ParentProcessId;
                        }
                    } while (Process32Next(snapshot, ref entry));
                }
            } finally {
                CloseHandle(snapshot);
            }

            Assert.Fail("Unable to find the activation wrapper process.");
            return 0;
        }

        private static void AssertProcessExited(Process process, string description) {
            Assert.IsTrue(process.WaitForExit(5000), description + " did not exit after activation timed out.");
        }

        private static void TerminateTestProcess(Process process) {
            if (process == null) {
                return;
            }

            try {
                if (!process.HasExited) {
                    process.Kill();
                    process.WaitForExit(5000);
                }
            } catch (InvalidOperationException) {
            } catch (Win32Exception) {
            }
        }

        private static void TestTriggerDiscovery(string userProfileFolder, Action triggerDiscovery) {
            using (var evt = new AutoResetEvent(false))
            using (var globalProvider = new CPythonInterpreterFactoryProvider(null, false))
            using (var condaProvider = new CondaEnvironmentFactoryProvider(globalProvider, null, new JoinableTaskFactory(new JoinableTaskContext()), true, userProfileFolder)) {
                // This initializes the provider, discovers the initial set
                // of factories and starts watching the filesystem.
                var configs = condaProvider.GetInterpreterConfigurations();
                condaProvider.DiscoveryStarted += (sender, e) => {
                    evt.Set();
                };
                triggerDiscovery();
                Assert.IsTrue(evt.WaitOne(5000), "Failed to trigger discovery.");
            }
        }

        class TestLogger : IPythonToolsLogger {
            public string EventName { get; private set; }

            public IReadOnlyDictionary<string, object> Properties { get; private set; }

            public IReadOnlyDictionary<string, double> Measurements { get; private set; }

            public void LogEvent(PythonLogEvent logEvent, object argument) { }

            public void LogEvent(string eventName, IReadOnlyDictionary<string, object> properties, IReadOnlyDictionary<string, double> measurements) {
                EventName = eventName;
                Properties = properties;
                Measurements = measurements;
            }

            public void LogFault(Exception ex, string description, bool dumpProcess) { }
        }

        private const uint Th32csSnapProcess = 0x00000002;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry32 {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int BasePriority;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExecutableFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
