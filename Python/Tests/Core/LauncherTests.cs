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
using Microsoft.PythonTools;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class LauncherTests {
        [TestMethod, Priority(UnitTestPriority.P0)]
        public void LaunchWebBrowserUriTests() {
            var testCases = new[] {
                new { Url = "/fob", Port = 1, Expected = "http://localhost:1/fob" },
                new { Url = "http://localhost:9999/fob", Port = 9999, Expected = "http://localhost:9999/fob" },
                new { Url = "http://localhost/fob", Port = 9999, Expected = "http://localhost:9999/fob" },
                new { Url = "fob", Port = 9999, Expected = "http://localhost:9999/fob" },
                new { Url = "/hello/world", Port = 367, Expected = "http://localhost:367/hello/world" },
                new { Url = "/fob", Port = -1, Expected = "http://localhost:{port}/fob" },
            };

            foreach (var testCase in testCases) {
                Console.WriteLine("{0} {1} == {2}", testCase.Url, testCase.Port, testCase.Expected);

                Uri url;
                int port;

                var config = new LaunchConfiguration(null, new Dictionary<string, string> {
                    { PythonConstants.WebBrowserUrlSetting, testCase.Url }
                });
                if (testCase.Port >= 0) {
                    config.LaunchOptions[PythonConstants.WebBrowserPortSetting] = testCase.Port.ToString();
                }
                PythonWebLauncher.GetFullUrl(null, config, out url, out port);
                Assert.AreEqual(
                    testCase.Expected.Replace("{port}", port.ToString()),
                    url.AbsoluteUri
                );
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void PythonVersionSupportForDebugging() {
            Assert.IsTrue(DebugLaunchHelper.IsPythonVersionSupportedForDebugging(new Version(0, 0)));
            Assert.IsFalse(DebugLaunchHelper.IsPythonVersionSupportedForDebugging(new Version(2, 7)));
            Assert.IsFalse(DebugLaunchHelper.IsPythonVersionSupportedForDebugging(new Version(3, 9)));
            Assert.IsFalse(DebugLaunchHelper.IsPythonVersionSupportedForDebugging(new Version(3, 9, 99)));
            Assert.IsTrue(DebugLaunchHelper.IsPythonVersionSupportedForDebugging(new Version(3, 10)));
            Assert.IsTrue(DebugLaunchHelper.IsPythonVersionSupportedForDebugging(new Version(3, 15)));
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void UnsupportedPythonVersionDebugMessage() {
            try {
                DebugLaunchHelper.ThrowIfPythonVersionNotSupportedForDebugging(new Version(3, 9, 99));
            } catch (NotSupportedException ex) {
                Assert.AreEqual("Debugging is not supported for Python 3.9 and earlier.", ex.Message);
                return;
            }

            Assert.Fail("Expected Python 3.9 debugging to be rejected.");
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void LocalAttachUsesSupportedDefaultAdapterHost() {
            var defaultInterpreter = CreateInterpreter("default", new Version(3, 10));
            var newerInterpreter = CreateInterpreter("newer", new Version(3, 14));

            var selected = DebugAdapterLauncher.GetDebugAdapterHostInterpreter(
                defaultInterpreter,
                new[] { newerInterpreter }
            );

            Assert.AreSame(defaultInterpreter, selected);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void LocalAttachSelectsNewestRunnableAdapterHost() {
            var defaultInterpreter = CreateInterpreter("default", new Version(3, 9));
            var supported310 = CreateInterpreter("supported310", new Version(3, 10));
            var supported314 = CreateInterpreter("supported314", new Version(3, 14));
            var missing315 = CreateInterpreter(
                "missing315",
                new Version(3, 15),
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "python.exe")
            );

            var selected = DebugAdapterLauncher.GetDebugAdapterHostInterpreter(
                defaultInterpreter,
                new[] { supported310, missing315, supported314 }
            );

            Assert.AreSame(supported314, selected);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void LocalAttachPrefersAutoDefaultAdapterHost() {
            var defaultInterpreter = CreateInterpreter("default", new Version(3, 9));
            var cannotBeAutoDefault = CreateInterpreter(
                "a",
                new Version(3, 14),
                uiMode: InterpreterUIMode.CannotBeAutoDefault
            );
            var canBeAutoDefault = CreateInterpreter("z", new Version(3, 14));

            var selected = DebugAdapterLauncher.GetDebugAdapterHostInterpreter(
                defaultInterpreter,
                new[] { cannotBeAutoDefault, canBeAutoDefault }
            );

            Assert.AreSame(canBeAutoDefault, selected);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void LocalAttachRejectsUnsupportedAdapterHosts() {
            var defaultInterpreter = CreateInterpreter("default", new Version(3, 9));
            var unsupportedFallback = CreateInterpreter("fallback", new Version(3, 8));

            try {
                DebugAdapterLauncher.GetDebugAdapterHostInterpreter(
                    defaultInterpreter,
                    new[] { unsupportedFallback }
                );
            } catch (NotSupportedException ex) {
                Assert.AreEqual(Strings.DebuggerPythonVersionNotSupported, ex.Message);
                return;
            }

            Assert.Fail("Expected unsupported adapter hosts to be rejected.");
        }

        private static IPythonInterpreterFactory CreateInterpreter(
            string id,
            Version version,
            string interpreterPath = null,
            InterpreterUIMode uiMode = InterpreterUIMode.Normal
        ) {
            return new MockPythonInterpreterFactory(new VisualStudioInterpreterConfiguration(
                id,
                id,
                pythonExePath: interpreterPath ?? typeof(LauncherTests).Assembly.Location,
                version: version,
                uiMode: uiMode
            ));
        }
    }
}
