/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace AnalysisTests {
    [TestClass]
    public class ProcessOutputTests {
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
                foreach (var interp in PythonPaths.Versions.Where(p => File.Exists(p.Path))) {
                    yield return new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Interpreter",
                        new InterpreterConfiguration(Path.GetDirectoryName(interp.Path), interp.Path, "", "", "",
                            interp.Isx64 ? ProcessorArchitecture.Amd64 : ProcessorArchitecture.X86,
                            interp.Version.ToVersion()
                        )
                    );
                }
            }
        }

        [TestMethod, Priority(0)]
        public void RunInterpreterOutput() {
            foreach (var fact in Factories) {
                using (var output = fact.Run("-c", "import sys; print(sys.version)")) {
                    Assert.IsTrue(output.Wait(TimeSpan.FromSeconds(10)));

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
        public void RunInterpreterError() {
            foreach(var fact in Factories) {
                using (var output = fact.Run("-c", "assert False")) {
                    Console.WriteLine(output.Arguments);
                    Assert.IsTrue(output.Wait(TimeSpan.FromSeconds(10)));

                    Assert.AreEqual(0, output.StandardOutputLines.Count());
                    var error = output.StandardErrorLines.ToList();
                    Assert.AreEqual(3, error.Count);
                    Assert.AreEqual("Traceback (most recent call last):", error[0]);
                    Assert.AreEqual("  File \"<string>\", line 1, in <module>", error[1]);
                    Assert.AreEqual("AssertionError", error[2]);
                }
            }
        }
    }
}
