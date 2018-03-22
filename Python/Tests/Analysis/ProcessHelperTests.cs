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
using System.Security.AccessControl;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace AnalysisTests {
    [TestClass]
    public class ProcessHelperTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }


        [TestMethod, Priority(0)]
        public void ArgumentQuoting() {
            foreach (var testCase in new[] {
                new { Source = "Abc", Expected = "Abc" },
                new { Source = "Abc 123", Expected = "\"Abc 123\"" },
                // A"B"C => "A\"B\"C"
                new { Source = "A\"B\"C", Expected = "\"A\\\"B\\\"C\"" },
                // "AB\"C" => "AB\"C"
                new { Source = "\"AB\\\"C\"", Expected = "\"AB\\\"C\"" },
                // "AB"C" => "\"AB\"C\""
                new { Source = "\"AB\"C\"", Expected = "\"AB\\\"C\"" },
                // C:\Program Files\Application Path\ => "C:\Program Files\Application Path\\"
                new { Source = @"C:\Program Files\Application Path\", Expected = "\"C:\\Program Files\\Application Path\\\\\"" },
                // C:\Program Files\Application Path => "C:\Program Files\Application Path"
                new { Source = @"C:\Program Files\Application Path", Expected = "\"C:\\Program Files\\Application Path\"" },
            }) {
                Assert.AreEqual(testCase.Expected, testCase.Source.QuoteArgument(), string.Format("Source:<{0}>", testCase.Source));
            }
        }

        private static IEnumerable<IPythonInterpreterFactory> Factories {
            get {
                foreach (var interp in PythonPaths.Versions) {
                    yield return new MockPythonInterpreterFactory(interp.Configuration);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void RunInterpreterOutput() {
            foreach (var fact in Factories) {
                using (var output = new ProcessHelper(fact.Configuration.InterpreterPath, new[] { "-c", "import sys; print(sys.version)" })) {
                    var outputLines = new List<string>();
                    var errorLines = new List<string>();
                    output.OnOutputLine = outputLines.Add;
                    output.OnErrorLine = errorLines.Add;

                    output.Start();
                    var ec = output.Wait(30000);
                    Assert.IsNotNull(ec, "Running " + fact.Configuration.Description + " exceeded timeout");
                    Assert.AreEqual(0, ec, "Running " + fact.Configuration.Description + " failed");

                    foreach (var line in outputLines) {
                        Console.WriteLine(line);
                    }
                    Console.WriteLine("END OF STDOUT");

                    foreach (var line in errorLines) {
                        Console.WriteLine(line);
                    }
                    Console.WriteLine("END OF STDERR");

                    Assert.AreEqual(0, errorLines.Count());
                    Assert.AreEqual(1, outputLines.Count());
                }
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void RunInterpreterError() {
            foreach (var fact in Factories) {
                using (var output = new ProcessHelper(fact.Configuration.InterpreterPath, new[] { "-c", "assert False" })) {
                    var outputLines = new List<string>();
                    var errorLines = new List<string>();
                    output.OnOutputLine = outputLines.Add;
                    output.OnErrorLine = errorLines.Add;

                    Console.WriteLine(output.Arguments);
                    output.Start();
                    var ec = output.Wait(30000);
                    Assert.IsNotNull(ec, "Running " + fact.Configuration.Description + " exceeded timeout");
                    Assert.AreNotEqual(0, ec, "Running " + fact.Configuration.Description + " succeeded (but shouldn't have)");

                    foreach (var line in outputLines) {
                        Console.WriteLine(line);
                    }
                    Console.WriteLine("END OF STDOUT");

                    foreach (var line in errorLines) {
                        Console.WriteLine(line);
                    }
                    Console.WriteLine("END OF STDERR");

                    // IronPython inserts empty lines
                    errorLines.RemoveAll(string.IsNullOrEmpty);

                    Assert.AreEqual(0, outputLines.Count, "Expected no standard output");
                    Assert.AreEqual(3, errorLines.Count, "Expected 3 lines on standard error");
                    Assert.AreEqual("Traceback (most recent call last):", errorLines[0]);
                    Assert.AreEqual("  File \"<string>\", line 1, in <module>", errorLines[1]);
                    Assert.AreEqual("AssertionError", errorLines[2]);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void RunUnrunnableInterpreterError() {
            var fact = PythonPaths.Versions.LastOrDefault(f => f.IsCPython);
            fact.AssertInstalled();

            var runDir = TestData.GetTempPath();
            var runExe = Path.Combine(runDir, "python.exe");
            File.Copy(fact.InterpreterPath, runExe);

            // Deny ourselves the ability to execute the file
            var sec = new FileSecurity(runExe, AccessControlSections.Access);
            var user = $"{Environment.UserDomainName}\\{Environment.UserName}";
            sec.AddAccessRule(new FileSystemAccessRule(user,
                FileSystemRights.ExecuteFile,
                AccessControlType.Deny
            ));
            File.SetAccessControl(runExe, sec);

            using (var output = new ProcessHelper(runExe, new[] { "-V" }, runDir)) {
                var outputLines = new List<string>();
                var errorLines = new List<string>();
                output.OnOutputLine = outputLines.Add;
                output.OnErrorLine = errorLines.Add;

                output.Start();
                var ec = output.Wait(30000);
                Assert.IsNotNull(ec, "Running " + fact.Configuration.Description + " exceeded timeout");

                foreach (var line in outputLines) {
                    Console.WriteLine(line);
                }
                Console.WriteLine("END OF STDOUT");

                foreach (var line in errorLines) {
                    Console.WriteLine(line);
                }
                Console.WriteLine("END OF STDERR");

                Assert.AreNotEqual(0, ec, "Running " + fact.Configuration.Description + " succeeded (but shouldn't have)");
                Assert.AreNotEqual(0, errorLines.Count);
            }
        }
    }
}
