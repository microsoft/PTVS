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
using System.Text;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace AnalysisTests {
    [TestClass]
    public class ProcessOutputTests {
        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize();

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

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
                new { Source = "\"AB\"C\"", Expected = "\"\\\"AB\\\"C\\\"\"" },
                // C:\Program Files\Application Path\ => "C:\Program Files\Application Path\\"
                new { Source = @"C:\Program Files\Application Path\", Expected = "\"C:\\Program Files\\Application Path\\\\\"" },
                // C:\Program Files\Application Path => "C:\Program Files\Application Path"
                new { Source = @"C:\Program Files\Application Path", Expected = "\"C:\\Program Files\\Application Path\"" },
            }) {
                Assert.AreEqual(testCase.Expected, ProcessOutput.QuoteSingleArgument(testCase.Source), string.Format("Source:<{0}>", testCase.Source));
            }
        }

        [TestMethod, Priority(0)]
        public void SplitLines() {
            foreach (var testCase in new[] {
                new { Source = "A\nB\nC\n", Expected = new[] { "A", "B", "C" } },
                new { Source = "A\r\nB\r\nC\r\n", Expected = new[] { "A", "B", "C" } },
                new { Source = "A\n\rB\n\rC\n\r", Expected = new[] { "A", "", "B", "", "C", "" } },
                new { Source = "A\n\nB\n\nC\n\n", Expected = new[] { "A", "", "B", "", "C", "" } },
                new { Source = "A\nB\nC\n ", Expected = new[] { "A", "B", "C", " " } },
                new { Source = "A", Expected = new[] { "A" } },
                new { Source = "\r\nABC", Expected = new[] { "", "ABC" } },
                new { Source = "", Expected = new[] { "" } },
                new { Source = "\r", Expected = new[] { "" } },
                new { Source = "\n", Expected = new[] { "" } },
                new { Source = "\r\n", Expected = new[] { "" } },
                new { Source = "\n\r", Expected = new[] { "", "" } },
            }) {
                var lines = ProcessOutput.SplitLines(testCase.Source).ToList();
                Assert.AreEqual(testCase.Expected.Length, lines.Count, string.Format("Source:<{0}>", testCase.Source));
                foreach (var pair in testCase.Expected.Zip(lines, Tuple.Create<string, string>)) {
                    Console.WriteLine("[" + pair.Item1 + "]");
                    Console.WriteLine("{" + pair.Item2 + "}");
                }
                Console.WriteLine();
                foreach (var pair in testCase.Expected.Zip(lines, Tuple.Create<string, string>)) {
                    Assert.AreEqual(pair.Item1, pair.Item2, string.Format("Source:<{0}>", testCase.Source));
                }
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
                using (var output = ProcessOutput.RunHiddenAndCapture(fact.Configuration.InterpreterPath, "-c", "import sys; print(sys.version)")) {
                    Assert.IsTrue(output.Wait(TimeSpan.FromSeconds(30)), "Running " + fact.Configuration.Description + " exceeded timeout");

                    foreach (var line in output.StandardOutputLines) {
                        Console.WriteLine(line);
                    }
                    Console.WriteLine("END OF STDOUT");

                    foreach (var line in output.StandardErrorLines) {
                        Console.WriteLine(line);
                    }
                    Console.WriteLine("END OF STDERR");

                    Assert.AreEqual(0, output.StandardErrorLines.Count());
                    Assert.AreEqual(1, output.StandardOutputLines.Count());
                }
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void RunInterpreterError() {
            foreach(var fact in Factories) {
                using (var output = ProcessOutput.RunHiddenAndCapture(fact.Configuration.InterpreterPath, "-c", "assert False")) {
                    Console.WriteLine(output.Arguments);
                    Assert.IsTrue(output.Wait(TimeSpan.FromSeconds(30)), "Running " + fact.Configuration.Description + " exceeded timeout");

                    foreach (var line in output.StandardOutputLines) {
                        Console.WriteLine(line);
                    }
                    Assert.AreEqual(0, output.StandardOutputLines.Count(), "Expected no standard output");
                    var error = output.StandardErrorLines.ToList();
                    Assert.AreEqual(3, error.Count, "Expected 3 lines on standard error");
                    Assert.AreEqual("Traceback (most recent call last):", error[0]);
                    Assert.AreEqual("  File \"<string>\", line 1, in <module>", error[1]);
                    Assert.AreEqual("AssertionError", error[2]);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void ProcessOutputEncoding() {
            var testDataPath = TestData.GetTempPath();
            var testData = Path.Combine(testDataPath, "ProcessOutputEncoding.txt");
            for (int i = 1; File.Exists(testData); ++i) {
                testData = Path.Combine(testDataPath, string.Format("ProcessOutputEncoding{0}.txt", i));
            }

            const string testString = "‚‡”¾‰œðÝ";

            File.WriteAllText(testData, testString, new UTF8Encoding(false));

            using (var output = ProcessOutput.Run(
                "cmd.exe",
                new[] { "/C", "type " + Path.GetFileName(testData) },
                testDataPath,
                null,
                false,
                null,
                outputEncoding: new UTF8Encoding(false)
            )) {
                output.Wait();
                Assert.AreEqual(0, output.ExitCode);

                foreach (var line in output.StandardOutputLines.Concat(output.StandardErrorLines)) {
                    Console.WriteLine(line);
                }

                Assert.AreEqual(testString, output.StandardOutputLines.Single());
            }
        }

        [TestMethod, Priority(0)]
        public void RunElevatedProcess() {
            var fact = Factories.First();
            var output = new List<string>();
            var redirector = new ListRedirector(output);
            using (var process = ProcessOutput.RunElevated(
                fact.Configuration.InterpreterPath,
                new[] { "-c", "import os, sys; print(sys.version[:3]); print(os.getcwd()); print(os.getenv('TEST_KEY')); sys.exit(7)" },
                fact.Configuration.PrefixPath,
                new[] { new KeyValuePair<string, string>("TEST_KEY", "TEST_VALUE") },
                redirector,
                quoteArgs: true,
                elevate: false      // don't really elevate for the test
            )) {
                Assert.IsTrue(process.Wait(TimeSpan.FromSeconds(30)), "Running " + fact.Configuration.Description + " exceeded timeout");

                Console.WriteLine(string.Join(Environment.NewLine, output));

                Assert.AreEqual(7, process.ExitCode);
                AssertUtil.AreEqual(output,
                    fact.Configuration.Version.ToString(),
                    PathUtils.TrimEndSeparator(fact.Configuration.PrefixPath),
                    "TEST_VALUE"
                );
            }
        }
    }
}
