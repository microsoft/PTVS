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
            var condaRoot = TestData.GetTempPath();
            try {
                var scripts = Path.Combine(condaRoot, "scripts");
                Directory.CreateDirectory(scripts);

                var condaExe = Path.Combine(scripts, "conda.exe");
                var activateBat = Path.Combine(scripts, "activate.bat");
                File.WriteAllText(condaExe, string.Empty);
                File.WriteAllText(activateBat, "@echo off\r\n:wait\r\ngoto wait\r\n");

                var timedOutEnv = CondaUtils.GetActivationEnvironmentVariablesForPrefixAsync(condaExe, null, TimeSpan.FromMilliseconds(100)).WaitAndUnwrapExceptions();
                Assert.AreEqual(0, timedOutEnv.Count());

                var emitEnv = Path.Combine(scripts, "emit-env.ps1");
                File.WriteAllText(emitEnv, "$payload = 'x' * 65536\r\nWrite-Output '!!!ENVIRONMENT MARKER!!!'\r\nWrite-Output ('{\"PTVS_TEST_ENV\":\"success\",\"PTVS_TEST_PAYLOAD\":\"' + $payload + '\"}')\r\n");
                File.WriteAllText(activateBat, "@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%~dp0emit-env.ps1\"\r\n");

                var env = CondaUtils.GetActivationEnvironmentVariablesForPrefixAsync(condaExe, null, TimeSpan.FromSeconds(5)).WaitAndUnwrapExceptions();

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
    }
}
