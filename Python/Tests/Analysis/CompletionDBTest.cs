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
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace PythonToolsTests {
    [TestClass]
    public class CompletionDBTest {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        private string CompletionDB {
            get {
                var completionDB = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), "CompletionDB");
                Assert.IsTrue(Directory.Exists(completionDB), $"Did not find {completionDB}");
                return completionDB;
            }
        }

        private void TestOpen(PythonVersion path) {
            path.AssertInstalled();
            Console.WriteLine(path.InterpreterPath);

            Guid testId = Guid.NewGuid();
            var testDir = TestData.GetTempPath(testId.ToString());

            var scraper = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), "PythonScraper.py");
            Assert.IsTrue(File.Exists(scraper), $"Did not find {scraper}");

            // run the scraper
            using (var proc = ProcessOutput.RunHiddenAndCapture(
                path.InterpreterPath,
                scraper,
                testDir,
                CompletionDB
            )) {
                Console.WriteLine("Command: " + proc.Arguments);

                proc.Wait();

                // it should succeed
                Console.WriteLine("**Stdout**");
                foreach (var line in proc.StandardOutputLines) {
                    Console.WriteLine(line);
                }
                Console.WriteLine("");
                Console.WriteLine("**Stdout**");
                foreach (var line in proc.StandardErrorLines) {
                    Console.WriteLine(line);
                }
                
                Assert.AreEqual(0, proc.ExitCode, "Bad exit code: " + proc.ExitCode);
            }

            // perform some basic validation
            dynamic builtinDb = Unpickle.Load(new FileStream(Path.Combine(testDir, path.Version.Is3x() ? "builtins.idb" : "__builtin__.idb"), FileMode.Open, FileAccess.Read));
            if (path.Version.Is2x()) { // no open in 3.x
                foreach (var overload in builtinDb["members"]["open"]["value"]["overloads"]) {
                    Assert.AreEqual("__builtin__", overload["ret_type"][0][0]);
                    Assert.AreEqual("file", overload["ret_type"][0][1]);
                }

                if (!path.InterpreterPath.Contains("Iron")) {
                    // http://pytools.codeplex.com/workitem/799
                    var arr = (IList<object>)builtinDb["members"]["list"]["value"]["members"]["__init__"]["value"]["overloads"];
                    Assert.AreEqual(
                        "args",
                        ((dynamic)(arr[0]))["args"][1]["name"]
                    );
                }
            }

            if (!path.InterpreterPath.Contains("Iron")) {
                dynamic itertoolsDb = Unpickle.Load(new FileStream(Path.Combine(testDir, "itertools.idb"), FileMode.Open, FileAccess.Read));
                var tee = itertoolsDb["members"]["tee"]["value"];
                var overloads = tee["overloads"];
                var nArg = overloads[0]["args"][1];
                Assert.AreEqual("n", nArg["name"]);
                Assert.AreEqual("2", nArg["default_value"]);

                dynamic sreDb = Unpickle.Load(new FileStream(Path.Combine(testDir, "_sre.idb"), FileMode.Open, FileAccess.Read));
                var members = sreDb["members"];
                Assert.IsTrue(members.ContainsKey("SRE_Pattern"));
                Assert.IsTrue(members.ContainsKey("SRE_Match"));
            }

            Console.WriteLine("Passed: {0}", path.InterpreterPath);
        }

        [TestMethod, Priority(0)]
        public void TestOpen26() {
            TestOpen(PythonPaths.Python26 ?? PythonPaths.Python26_x64);
        }

        [TestMethod, Priority(0)]
        public void TestOpen27() {
            TestOpen(PythonPaths.Python27 ?? PythonPaths.Python27_x64);
        }

        [TestMethod, Priority(0)]
        public void TestOpen31() {
            TestOpen(PythonPaths.Python31 ?? PythonPaths.Python31_x64);
        }

        [TestMethod, Priority(0)]
        public void TestOpen32() {
            TestOpen(PythonPaths.Python32 ?? PythonPaths.Python32_x64);
        }

        [TestMethod, Priority(0)]
        public void TestOpen33() {
            TestOpen(PythonPaths.Python33 ?? PythonPaths.Python33_x64);
        }

        [TestMethod, Priority(0)]
        public void TestOpen34() {
            TestOpen(PythonPaths.Python34 ?? PythonPaths.Python34_x64);
        }

        [TestMethod, Priority(0)]
        public void TestOpen35() {
            TestOpen(PythonPaths.Python35 ?? PythonPaths.Python35_x64);
        }

        [TestMethod, Priority(0)]
        public void TestPthFiles() {
            var outputPath = TestData.GetTempPath();
            Console.WriteLine("Writing to: " + outputPath);

            // run the analyzer
            using (var output = ProcessOutput.RunHiddenAndCapture("Microsoft.PythonTools.Analyzer.exe",
                "/lib", TestData.GetPath("TestData", "PathStdLib"),
                "/version", "2.7",
                "/outdir", outputPath,
                "/indir", CompletionDB,
                "/unittest",
                "/log", "AnalysisLog.txt")) {
                output.Wait();
                Console.WriteLine("* Stdout *");
                foreach (var line in output.StandardOutputLines) {
                    Console.WriteLine(line);
                }
                Console.WriteLine("* Stderr *");
                foreach (var line in output.StandardErrorLines) {
                    Console.WriteLine(line);
                }
                Assert.AreEqual(0, output.ExitCode);
            }

            File.Copy(Path.Combine(CompletionDB, "__builtin__.idb"), Path.Combine(outputPath, "__builtin__.idb"));

            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7));
            var paths = new List<string> { outputPath };
            paths.AddRange(Directory.EnumerateDirectories(outputPath));
            var typeDb = new PythonTypeDatabase(fact, paths);
            var module = typeDb.GetModule("SomeLib");
            Assert.IsNotNull(module, "Could not import SomeLib");
            var fobMod = typeDb.GetModule("SomeLib.fob");
            Assert.IsNotNull(fobMod, "Could not import SomeLib.fob");

            var cClass = ((IPythonModule)fobMod).GetMember(null, "C");
            Assert.IsNotNull(cClass, "Could not get SomeLib.fob.C");

            Assert.AreEqual(PythonMemberType.Class, cClass.MemberType);
        }

        [TestMethod, Priority(0)]
        public void PydInPackage() {
            PythonPaths.Python27.AssertInstalled();

            var outputPath = TestData.GetTempPath();
            Console.WriteLine("Writing to: " + outputPath);

            // run the analyzer
            using (var output = ProcessOutput.RunHiddenAndCapture("Microsoft.PythonTools.Analyzer.exe",
                "/python", PythonPaths.Python27.InterpreterPath,
                "/lib", TestData.GetPath(@"TestData\PydStdLib"),
                "/version", "2.7",
                "/outdir", outputPath,
                "/indir", CompletionDB,
                "/unittest",
                "/log", "AnalysisLog.txt")) {
                output.Wait();
                Console.WriteLine("* Stdout *");
                foreach (var line in output.StandardOutputLines) {
                    Console.WriteLine(line);
                }
                Console.WriteLine("* Stderr *");
                foreach (var line in output.StandardErrorLines) {
                    Console.WriteLine(line);
                }
                Assert.AreEqual(0, output.ExitCode);
            }

            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7));
            var paths = new List<string> { outputPath };
            paths.AddRange(Directory.EnumerateDirectories(outputPath));
            var typeDb = new PythonTypeDatabase(fact, paths);
            var module = typeDb.GetModule("Package.winsound");
            Assert.IsNotNull(module, "Package.winsound was not analyzed");
            var package = typeDb.GetModule("Package");
            Assert.IsNotNull(package, "Could not import Package");
            var member = package.GetMember(null, "winsound");
            Assert.IsNotNull(member, "Could not get member Package.winsound");
            Assert.AreSame(module, member);

            module = typeDb.GetModule("Package._testcapi");
            Assert.IsNotNull(module, "Package._testcapi was not analyzed");
            package = typeDb.GetModule("Package");
            Assert.IsNotNull(package, "Could not import Package");
            member = package.GetMember(null, "_testcapi");
            Assert.IsNotNull(member, "Could not get member Package._testcapi");
            Assert.IsNotInstanceOfType(member, typeof(CPythonMultipleMembers));
            Assert.AreSame(module, member);

            module = typeDb.GetModule("Package.select");
            Assert.IsNotNull(module, "Package.select was not analyzed");
            package = typeDb.GetModule("Package");
            Assert.IsNotNull(package, "Could not import Package");
            member = package.GetMember(null, "select");
            Assert.IsNotNull(member, "Could not get member Package.select");
            Assert.IsInstanceOfType(member, typeof(CPythonMultipleMembers));
            var mm = (CPythonMultipleMembers)member;
            AssertUtil.ContainsExactly(mm.Members.Select(m => m.MemberType),
                PythonMemberType.Module,
                PythonMemberType.Constant,
                PythonMemberType.Class
            );
            Assert.IsNotNull(mm.Members.Contains(module));

            try {
                // Only clean up if the test passed
                Directory.Delete(outputPath, true);
            } catch { }
        }

        /// <summary>
        /// Checks that members removed or introduced in later versions show up or don't in
        /// earlier versions as appropriate.
        /// </summary>
        [TestMethod, Priority(0)]
        public void VersionedSharedDatabase() {
            var twoFive = PythonTypeDatabase.CreateDefaultTypeDatabase(new Version(2, 5));
            var twoSix = PythonTypeDatabase.CreateDefaultTypeDatabase(new Version(2, 6));
            var twoSeven = PythonTypeDatabase.CreateDefaultTypeDatabase(new Version(2, 7));
            var threeOh = PythonTypeDatabase.CreateDefaultTypeDatabase(new Version(3, 0));
            var threeOne = PythonTypeDatabase.CreateDefaultTypeDatabase(new Version(3, 1));
            var threeTwo = PythonTypeDatabase.CreateDefaultTypeDatabase(new Version(3, 2));

            // new in 2.6
            Assert.AreEqual(null, twoFive.BuiltinModule.GetAnyMember("bytearray"));
            foreach (var version in new[] { twoSix, twoSeven, threeOh, threeOne, threeTwo }) {
                Assert.AreNotEqual(version, version.BuiltinModule.GetAnyMember("bytearray"));
            }

            // new in 2.7
            Assert.AreEqual(null, twoSix.BuiltinModule.GetAnyMember("memoryview"));
            foreach (var version in new[] { twoSeven, threeOh, threeOne, threeTwo }) {
                Assert.AreNotEqual(version, version.BuiltinModule.GetAnyMember("memoryview"));
            }

            // not in 3.0
            foreach (var version in new[] { twoFive, twoSix, twoSeven }) {
                Assert.AreNotEqual(null, version.BuiltinModule.GetAnyMember("StandardError"));
            }

            foreach (var version in new[] { threeOh, threeOne, threeTwo }) {
                Assert.AreEqual(null, version.BuiltinModule.GetAnyMember("StandardError"));
            }

            // new in 3.0
            foreach (var version in new[] { twoFive, twoSix, twoSeven }) {
                Assert.AreEqual(null, version.BuiltinModule.GetAnyMember("exec"));
                Assert.AreEqual(null, version.BuiltinModule.GetAnyMember("print"));
            }

            foreach (var version in new[] { threeOh, threeOne, threeTwo }) {
                Assert.AreNotEqual(null, version.BuiltinModule.GetAnyMember("exec"));
                Assert.AreNotEqual(null, version.BuiltinModule.GetAnyMember("print"));
            }


            // new in 3.1
            foreach (var version in new[] { twoFive, twoSix, twoSeven, threeOh }) {
                Assert.AreEqual(null, version.GetModule("sys").GetMember(null, "int_info"));
            }

            foreach (var version in new[] { threeOne, threeTwo }) {
                Assert.AreNotEqual(null, version.GetModule("sys").GetMember(null, "int_info"));
            }

            // new in 3.2
            foreach (var version in new[] { twoFive, twoSix, twoSeven, threeOh, threeOne }) {
                Assert.AreEqual(null, version.GetModule("sys").GetMember(null, "setswitchinterval"));
            }

            foreach (var version in new[] { threeTwo }) {
                Assert.AreNotEqual(null, version.GetModule("sys").GetMember(null, "setswitchinterval"));
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("60s")]
        public void CheckObsoleteGenerateFunction() {
            var path = PythonPaths.Versions.LastOrDefault(p => p != null && p.IsCPython);
            path.AssertInstalled();

            var factory = InterpreterFactoryCreator.CreateInterpreterFactory(path.Configuration) as PythonInterpreterFactoryWithDatabase;
            if (factory == null) {
                Assert.Inconclusive("Test requires PythonInterpreterFactoryWithDatabase");
            }

            var tcs = new TaskCompletionSource<int>();
            var beforeProc = Process.GetProcessesByName("Microsoft.PythonTools.Analyzer");

            var request = new PythonTypeDatabaseCreationRequest {
                Factory = factory,
                OutputPath = TestData.GetTempPath(),
                SkipUnchanged = true,
                OnExit = tcs.SetResult
            };
            Console.WriteLine("OutputPath: {0}", request.OutputPath);

#pragma warning disable 618
            PythonTypeDatabase.Generate(request);
#pragma warning restore 618

            int expected = 0;

            if (!tcs.Task.Wait(TimeSpan.FromMinutes(1.0))) {
                var proc = Process.GetProcessesByName("Microsoft.PythonTools.Analyzer")
                    .Except(beforeProc)
                    .ToArray();
                
                // Ensure we actually started running
                Assert.AreNotEqual(0, proc.Length, "Process is not running");

                expected = -1;

                // Kill the process
                foreach (var p in proc) {
                    Console.WriteLine("Killing process {0}", p.Id);
                    p.Kill();
                }

                Assert.IsTrue(tcs.Task.Wait(TimeSpan.FromMinutes(1.0)), "Process did not die");
            }

            Assert.AreEqual(expected, tcs.Task.Result, "Incorrect exit code");
        }
    }
}
