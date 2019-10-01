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
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Profiling;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace ProfilingTests {
    [TestClass]
    public class ProfilingTests {
        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        // Update the test from version 3.1/3.4 to 3.5-3.7. 
        /*
        [TestMethod, UnitTestPriority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public async Task ProfileWithEncoding() {
            var proflaun = Path.Combine(
                Path.GetDirectoryName(typeof(IPythonProfiling).Assembly.Location),
                "proflaun.py"
            );
            var vspyprof = Path.Combine(
                Path.GetDirectoryName(proflaun),
                "vspyprofX86.dll"
            );

            Assert.IsTrue(File.Exists(proflaun), "Did not find " + proflaun);
            Assert.IsTrue(File.Exists(vspyprof), "Did not find " + vspyprof);

            var testFiles = new[] { "UTF8", "UTF8BOM" }
                .Select(encoding => TestData.GetPath(string.Format("TestData\\ProfileTest\\{0}Profile.py", encoding)))
                .ToList();
            foreach (var testFile in testFiles) {
                Assert.IsTrue(File.Exists(testFile), "Did not find " + testFile);
            }

            // Python 2.x uses execfile() and we do not handle encoding at all
            foreach (var python in new[] { PythonPaths.Python37, PythonPaths.Python36 }) {
                if (python == null) {
                    continue;
                }

                Trace.TraceInformation(python.InterpreterPath);

                foreach (var testFile in testFiles) {
                    Trace.TraceInformation("  {0}", Path.GetFileName(testFile));

                    using (var p = ProcessOutput.Run(
                        python.InterpreterPath,
                        new[] { proflaun, vspyprof, Path.GetDirectoryName(testFile), testFile },
                        Environment.CurrentDirectory,
                        new[] { new KeyValuePair<string, string>("PYTHONIOENCODING", "utf-8") },
                        false,
                        null,
                        outputEncoding: Encoding.UTF8
                    )) {
                        Trace.TraceInformation(p.Arguments);
                        var exitCode = await p;
                        foreach (var line in p.StandardErrorLines) {
                            Trace.TraceError("STDERR: " + line);
                        }
                        foreach (var line in p.StandardOutputLines) {
                            Trace.TraceWarning("STDOUT: " + line);
                        }
                        Assert.AreEqual(0, exitCode);
                    }
                }

                Trace.TraceInformation("OK");
            }
        }*/
    }
}
