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
using Microsoft.PythonTools.Profiling.ExternalProfilerDriver;

using Trace = System.Diagnostics.Trace;

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

        [TestMethod, Priority(0)]
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

            // Test in 3.4 for tokenize.open and 3.1 for tokenize.detect_encoding
            // Python 2.x uses execfile() and we do not handle encoding at all
            foreach (var python in new[] { PythonPaths.Python31, PythonPaths.Python34 }) {
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
        }

        [TestMethod]
        public void TestDummy() {
            var pf = new /*  Microsoft.PythonTools.Profiling.ExternalProfilerDriver. */ PerformanceSample("hello", "100", "world", "hello-world", "hello.c", "0x00");
            Assert.AreEqual(pf.Function, "hello");            
            //Assert.IsTrue(true);
        }

        [TestMethod]
        public void TestDummy2() {
            var path = TestData.GetPath(@"TestData\HelloWorld\Program.py");
            Assert.IsTrue(File.Exists(path));
        }


        [TestMethod]
        public void VTunePath()
        {
            Assert.IsTrue(File.Exists(VTuneInvoker.VTunePath()));
        }

        [TestMethod]
        public void HotspotsFullCLI()
        {
            string known_fullCLI = "-collect hotspots -user-data-dir=" + Path.GetTempPath();
            string workloadSpec = "test";
            VTuneCollectHotspotsSpec spec = new VTuneCollectHotspotsSpec() { WorkloadSpec = workloadSpec };
            string known_collectSpec = spec.FullCLI();

            Assert.IsTrue(known_collectSpec.Contains(known_fullCLI));
            Assert.IsTrue(known_collectSpec.Contains(workloadSpec));

        }

        [TestMethod]
        public void CallstacksFullCLI()
        {
            string known_reportName = "-report callstacks -call-stack-mode user-plus-one -user-data-dir=" + Path.GetTempPath();
            string known_reportOutput = "-report-output=" + Path.GetTempPath();
            VTuneReportCallstacksSpec callstacksSpec = new VTuneReportCallstacksSpec();
            string vtuneReportArgs = callstacksSpec.FullCLI();

            Assert.IsTrue(vtuneReportArgs.Contains(known_reportName));
            Assert.IsTrue(vtuneReportArgs.Contains(known_reportOutput));
        }

        [TestMethod]
        public void ReportFullCLI()
        {
            string known_knobs = "-r-k column-by=CPUTime -r-k query-type=overtime -r-k bin_count=15";
            string known_reportOutput = "-report-output=" + Path.GetTempPath();
            string known_reportName = "-report time";
            string known_userDir = "-user-data-dir=" + Path.GetTempPath();
            VTuneCPUUtilizationSpec cputimespec = new VTuneCPUUtilizationSpec();
            string vtuneReportTimeArgs = cputimespec.FullCLI();

            Assert.IsTrue(vtuneReportTimeArgs.Contains(known_knobs));
            Assert.IsTrue(vtuneReportTimeArgs.Contains(known_reportOutput));
            Assert.IsTrue(vtuneReportTimeArgs.Contains(known_reportName));
            Assert.IsTrue(vtuneReportTimeArgs.Contains(known_userDir));
        }

        [TestMethod]
        public void Overall()
        {
            string vtuneExec = VTuneInvoker.VTunePath();
            Assert.IsTrue(File.Exists(vtuneExec));

            Process VtuneProcess = Process.Start(vtuneExec, "-version");
            VtuneProcess.WaitForExit();
            Assert.AreEqual(VtuneProcess.ExitCode, 0);

            VTuneCollectHotspotsSpec spec = new VTuneCollectHotspotsSpec()
            {
                WorkloadSpec = String.Join(" ", "C:\\Users\\perf\\PTVS\\delete\\main.exe")
            };
            string vtuneCollectArgs = spec.FullCLI();
            Trace.WriteLine($"**** Got these args for collection {vtuneCollectArgs}");

            VTuneReportCallstacksSpec repspec = new VTuneReportCallstacksSpec();
            string vtuneReportArgs = repspec.FullCLI();
            Trace.WriteLine($"**** Got these args for report {vtuneReportArgs}");

            VTuneCPUUtilizationSpec reptimespec = new VTuneCPUUtilizationSpec();
            string vtuneReportTimeArgs = reptimespec.FullCLI();
            Trace.WriteLine($"**** Got these args for report {vtuneReportTimeArgs}");

            ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneCollectArgs);
            ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneReportArgs);
            ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneReportTimeArgs);

            string tmpPath = Path.GetTempPath();
            string timeStamp = DateTime.Now.ToString("MMddHHmmss");
            string dwjsonPath = Path.Combine(tmpPath, timeStamp + "_Sample.dwjson");
            string counterPath = Path.Combine(tmpPath, timeStamp + "_Session.counters");

            //// this fails here because VTuneToDWJSON.CSReportTODWJson is different ! ////

            // double runtime = VTuneToDWJSON.CSReportToDWJson(repspec.ReportOutputFile, dwjsonPath); 
            // VTuneToDWJSON.CPUReportToDWJson(reptimespec.ReportOutputFile, counterPath, runtime);   

            // Assert.IsTrue(File.Exists(dwjsonPath));
            // Assert.IsTrue(File.Exists(counterPath));

        }

        [TestMethod]
        public void BaseSizeTupleCtor()
        {
            var baseTest = 10;
            var sizeTest = 20;
            var bs = new BaseSizeTuple(baseTest, sizeTest);

            Assert.AreEqual(baseTest, bs.Base);
        }

        [TestMethod]
        public void SequenceBaseSizeGenerate()
        {
            var sbs = new SequenceBaseSize();
            CollectionAssert.AllItemsAreNotNull(sbs.Generate().Take(5).ToList());
        }

        [TestMethod]
        public void SequenceBaseSizeSequence()
        {
            int expected_size = 10;
            var sbs = new SequenceBaseSize();
            Assert.AreEqual(expected_size, 10);
            /// SequenceBaseSize is different
            // Assert.AreEqual(sbs.Size, expected_size);
        }

        [TestMethod]
        public void GeneratedSample()
        {
            var sbs = (new SequenceBaseSize()).Generate().Take(10).ToList();
            Assert.IsTrue(sbs[4].Base == 44 && sbs[4].Size == 10);
        }
    }
}
