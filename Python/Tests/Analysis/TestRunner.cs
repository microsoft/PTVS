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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    public static class TestRunner {
        static bool HELP, PERF, TRACE, VERBOSE, STDLIB, DJANGO, PATH;
        static IEnumerable<PythonVersion> VERSIONS;
        static HashSet<string> OTHER_ARGS;

        public static int Main(string[] args) {
            var argset = new HashSet<string>(args, StringComparer.InvariantCultureIgnoreCase);

            HELP = argset.Count == 0 || (argset.Remove("H") | argset.Remove("HELP"));
            PERF = argset.Remove("PERF");
            VERBOSE = argset.Remove("VERBOSE") | argset.Remove("V");
            TRACE = VERBOSE | (argset.Remove("TRACE") | argset.Remove("T"));
            STDLIB = argset.Remove("STDLIB");
            DJANGO = argset.Remove("DJANGO");
            PATH = argset.Remove("PATH");

            var versionSpecs = new HashSet<string>(argset.Where(arg => Regex.IsMatch(arg, "^V[23][0-9]$", RegexOptions.IgnoreCase)),
                StringComparer.InvariantCultureIgnoreCase);

            OTHER_ARGS = new HashSet<string>(argset.Except(versionSpecs), StringComparer.InvariantCultureIgnoreCase);

            if (versionSpecs.Any()) {
                VERSIONS = PythonPaths.Versions
                    .Where(v => versionSpecs.Contains(Enum.GetName(typeof(PythonLanguageVersion), v.Version)))
                    .Where(v => v.IsCPython)
                    .ToList();
            } else {
                VERSIONS = PythonPaths.Versions;
            }

            if (HELP) {
                Console.WriteLine(@"AnalysisTest.exe [TRACE] [VERBOSE|V] [test names...]
    Runs the specified tests. Test names are the short method name only.

AnalysisTest.exe [TRACE|VERBOSE|V] [test name ...]

AnalysisTest.exe [TRACE|VERBOSE|V] PERF
    Runs all performance related tests.

AnalysisTest.exe [TRACE|VERBOSE|V] STDLIB [V## ...]
    Runs standard library analyses against the specified versions.
    Version numbers are V25, V26, V27, V30, V31, V32 or V33.
    Specifying V27 will only include CPython. Omitting all specifiers
    will include CPython 2.7 and IronPython 2.7 if installed.

AnalysisTest.exe [TRACE|VERBOSE|V] DJANGO [V## ...]
    Runs Django analyses against the specified versions.

AnalysisTest.exe [TRACE|VERBOSE|V] PATH ""library path"" [V## ...]
    Analyses the specified path with the specified versions.

Specifying TRACE will log messages to a file. Specifying VERBOSE or V implies
TRACE and will also log detailed analysis information to CSV file.
");
                return 0;
            }

            if (TRACE) {
                Stream traceOutput;
                try {
                    traceOutput = new FileStream("AnalysisTests.Trace.txt", FileMode.Create, FileAccess.Write, FileShare.Read);
                } catch (IOException) {
                    traceOutput = new FileStream(string.Format("AnalysisTests.Trace.{0}.txt", Process.GetCurrentProcess().Id), FileMode.Create, FileAccess.Write, FileShare.Read);
                }

                Trace.Listeners.Add(new TextWriterTraceListener(new StreamWriter(traceOutput, Encoding.UTF8)));
                Trace.AutoFlush = true;

                if (VERBOSE & !(STDLIB | DJANGO | PATH)) {
                    AnalysisLog.Output = "AnalysisTests.Trace.csv";
                    AnalysisLog.AsCSV = true;
                }
            } else {
                Trace.Listeners.Add(new ConsoleTraceListener());
            }

            int res = 0;

            if (STDLIB) {
                res += RunStdLibTests();
            }
            if (DJANGO) {
                res += RunDjangoTests();
            }
            if (PATH) {
                res += RunPathTests();
            }

            if (!(STDLIB | DJANGO | PATH)) {
                Type attrType = PERF ? typeof(PerfMethodAttribute) : typeof(TestMethodAttribute);

                foreach (var type in typeof(AnalysisTest).Assembly.GetTypes()) {
                    if (type.IsDefined(typeof(TestClassAttribute), false)) {
                        res += RunTests(type, attrType);
                    }
                }
            }
            return res;
        }

        private static bool RunOneTest(Action test, string name) {
            if (Debugger.IsAttached) {
                return RunOneTestWithoutEH(test, name);
            }

            var sw = new Stopwatch();
            try {
                sw.Start();
                return RunOneTestWithoutEH(test, name);
            } catch (Exception e) {
                sw.Stop();
                Console.ForegroundColor = ConsoleColor.Red;
#if TRACE
                Trace.TraceError("Test failed: {0}, {1} ({2}ms)", name, sw.Elapsed, sw.ElapsedMilliseconds);
#endif
                Console.WriteLine("Test failed: {0}, {1} ({2}ms)", name, sw.Elapsed, sw.ElapsedMilliseconds);
                Console.WriteLine(e);
                AnalysisLog.Flush();
                return false;
            }
        }

        private static bool RunOneTestWithoutEH(Action test, string name) {
            var sw = new Stopwatch();
            sw.Start();
            test();
            sw.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
#if TRACE
            Trace.TraceInformation("Test passed: {0}, {1} ({2}ms)", name, sw.Elapsed, sw.ElapsedMilliseconds);
#endif
            Console.WriteLine("Test passed: {0}, {1} ({2}ms)", name, sw.Elapsed, sw.ElapsedMilliseconds);
            return true;
        }

        private static int RunStdLibTests() {
            var fg = Console.ForegroundColor;

            foreach (var ver in VERSIONS) {
                if (VERBOSE) {
                    try {
                        if (File.Exists(string.Format("AnalysisTests.StdLib.{0}.csv", ver.Version))) {
                            File.Delete(string.Format("AnalysisTests.StdLib.{0}.csv", ver.Version));
                        }
                    } catch { }
                }
            }

            int failures = 0;

            foreach (var ver in VERSIONS) {
                if (VERBOSE) {
                    AnalysisLog.Output = string.Format("AnalysisTests.StdLib.{0}.csv", ver.Version);
                    AnalysisLog.AsCSV = true;
                    AnalysisLog.Add("StdLib Start", ver.InterpreterPath, ver.Version, DateTime.Now);
                }

                if (!RunOneTest(() => {
                    new AnalysisTest().AnalyzeDir(Path.Combine(ver.PrefixPath, "Lib"), ver.Version, new[] { "site-packages" }); 
                }, ver.InterpreterPath)) {
                    failures += 1;
                }
                Console.ForegroundColor = fg;

                if (VERBOSE) {
                    AnalysisLog.Flush();
                }

                IdDispenser.Clear();
            }
            return failures;
        }

        private static int RunDjangoTests() {
            var fg = Console.ForegroundColor;

            foreach (var ver in VERSIONS) {
                if (VERBOSE) {
                    try {
                        if (File.Exists(string.Format("AnalysisTests.Django.{0}.csv", ver.Version))) {
                            File.Delete(string.Format("AnalysisTests.Django.{0}.csv", ver.Version));
                        }
                    } catch { }
                }
            }

            int failures = 0;

            foreach (var ver in VERSIONS) {
                var djangoPath = Path.Combine(ver.PrefixPath, "Lib", "site-packages", "django");
                if (!Directory.Exists(djangoPath)) {
                    Trace.TraceInformation("Path {0} not found; skipping {1}", djangoPath, ver.Version);
                    continue;
                }

                if (VERBOSE) {
                    AnalysisLog.Output = string.Format("AnalysisTests.Django.{0}.csv", ver.Version);
                    AnalysisLog.AsCSV = true;
                    AnalysisLog.Add("Django Start", ver.InterpreterPath, ver.Version, DateTime.Now);
                }

                if (!RunOneTest(() => { new AnalysisTest().AnalyzeDir(djangoPath, ver.Version); }, ver.InterpreterPath)) {
                    failures += 1;
                }
                Console.ForegroundColor = fg;

                if (VERBOSE) {
                    AnalysisLog.Flush();
                }

                IdDispenser.Clear();
            }
            return failures;
        }

        private static int RunPathTests() {
            var fg = Console.ForegroundColor;

            foreach (var ver in VERSIONS) {
                if (VERBOSE) {
                    try {
                        if (File.Exists(string.Format("AnalysisTests.Path.{0}.csv", ver.Version))) {
                            File.Delete(string.Format("AnalysisTests.Path.{0}.csv", ver.Version));
                        }
                    } catch { }
                }
            }

            int failures = 0;

            foreach (var path in OTHER_ARGS) {
                if (!Directory.Exists(path)) {
                    continue;
                }

                foreach (var ver in VERSIONS) {
                    if (VERBOSE) {
                        AnalysisLog.Output = string.Format("AnalysisTests.Path.{0}.csv", ver.Version);
                        AnalysisLog.AsCSV = true;
                        AnalysisLog.Add("Path Start", path, ver.Version, DateTime.Now);
                    }

                    if (!RunOneTest(() => { new AnalysisTest().AnalyzeDir(path, ver.Version); }, string.Format("{0}: {1}", ver.Version, path))) {
                        failures += 1;
                    }
                    Console.ForegroundColor = fg;

                    if (VERBOSE) {
                        AnalysisLog.Flush();
                    }

                    IdDispenser.Clear();
                }
            }
            return failures;
        }


        private static int RunTests(Type instType, Type attrType) {
            var fg = Console.ForegroundColor;
            int failures = 0;
            object inst = null;

            foreach (var mi in instType.GetMethods()) {
                if ((!OTHER_ARGS.Any() || OTHER_ARGS.Contains(mi.Name)) && mi.IsDefined(attrType, false)) {

                    if (inst == null) {
                        inst = Activator.CreateInstance(instType);
                        Console.WriteLine("Running tests against: {0}", instType.FullName);
                    }

                    if (!RunOneTest(() => { mi.Invoke(inst, new object[0]); }, mi.Name)) {
                        failures += 1;
                    }
                    Console.ForegroundColor = fg;
                }
            }

            if (inst != null) {
                Console.WriteLine();
                if (failures == 0) {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("No failures");
                } else {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("{0} failures", failures);
                }
                Console.ForegroundColor = fg;
            }
            return failures;
        }

    }
}
