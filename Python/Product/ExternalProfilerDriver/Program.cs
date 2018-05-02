// Python Tools for Visual Studio
// Copyright(c) 2018 Intel Corporation.  All rights reserved.
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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommandLine;
using CommandLine.Text;

namespace Microsoft.PythonTools.Profiling.ExternalProfilerDriver {

    class ProgramOptions {
        [Option('p', "path", HelpText = "Report VTune path")]
        public bool ReportVTunePath { get; set; }

        [Option('n', "dry-run", HelpText = "Whether I should execute or just pretend to execute")]
        public bool DryRunRequested { get; set; }

        [Option('c', "callstack", HelpText = "Specify the pre-generated callstack report to process")]
        public string CallStackFNameToParse { get; set; }

        [Value(0)]
        public IEnumerable<string> Rest { get; set; }
    }

    class Program {
        static void Main(string[] args) {

#if false
            var parser = new Parser(config => {
                config.EnableDashDash = true;
            });


            var res = parser.ParseArguments<ProgramOptions>(args)
                            .WithParsed<ProgramOptions>(opts => {

                                if (opts.CallStackFNameToParse != null) {
                                    // TODO: test /tmp/results_20180314/r_stacks_0004.csv
#if false
                                    ParseStackReport(opts.CallStackFNameToParse);
#endif
                                    Environment.Exit(0);
                                }
#if false

                                string vtuneExec = "";
                                try {
                                    vtuneExec = VTuneInvoker.VTunePath();
                                } catch (VTuneNotInstalledException ex) {
                                    Console.WriteLine($"VTune not found in expected path: {ex.Message}");
                                    Environment.Exit(1);
                                }

                                if (opts.ReportVTunePath)
                                {
                                    Console.WriteLine($"The path of VTune is: {vtuneExec}");
                                    Environment.Exit(0);
                                }

                                var RestArgs = opts.Rest.ToList();
                                VTuneCollectHotspotsSpec spec = new VTuneCollectHotspotsSpec()
                                {
                                    WorkloadSpec = String.Join(" ", RestArgs)
                                };
                                string vtuneCollectArgs = spec.FullCLI();

                                VTuneReportCallstacksSpec repspec = new VTuneReportCallstacksSpec();
                                string vtuneReportArgs = repspec.FullCLI();

                                VTuneCPUUtilizationSpec reptimespec = new VTuneCPUUtilizationSpec();
                                string vtuneReportTimeArgs = reptimespec.FullCLI();

                                if (!opts.DryRunRequested) {
#if false
                                    Console.WriteLine($"Collect command line is: [ {vtuneExec} {vtuneCollectArgs} ]");
                                    ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneCollectArgs);

                                    Console.WriteLine($"Report callstacks line: [ {vtuneExec} {vtuneReportArgs} ]");
                                    ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneReportArgs);

                                    Console.WriteLine($"Report timing line: [ {vtuneExec} {vtuneReportTimeArgs} ]");
                                    ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneReportTimeArgs);
#endif
                                } else {
                                    Console.WriteLine($"Collect command line is: [ {vtuneExec} {vtuneCollectArgs} ]");
                                    Console.WriteLine("Report command lines");
                                    Console.WriteLine($"[ {vtuneExec} {vtuneReportArgs} ]");
                                    Console.WriteLine($"[ {vtuneExec} {vtuneReportTimeArgs} ]");

                                    Environment.Exit(0);
                                }

                                var stackReportFName = repspec.ReportOutputFile;
                                if (!File.Exists(stackReportFName)) {
                                    Console.WriteLine("Cannot find the VTune report, something went wrong with the profiler process.");
                                    Environment.Exit(1);
                                }
#endif

                            })
                            .WithNotParsed(errors => {
                                Console.WriteLine("Incorrect command line.");
                                Environment.Exit(1);
                            });


            Environment.Exit(0);
#endif
        }

#if false

        private static void ParseStackReport(string fname)
        {
            string possibleFn = fname;
            if (!File.Exists(possibleFn)) {
                // The [old] argument parsing library chokes on absolute Linux paths (it gets confused apparently by leading '/')
                possibleFn = Path.DirectorySeparatorChar + possibleFn;
                if (!File.Exists(possibleFn))
                {
                    Console.WriteLine($"Cannot find {fname}");
                    return;
                }
            }

            try {
                var samples = VTuneToDWJSON.ParseFromFile(possibleFn);
                int sample_counter = 1;
                foreach (var s in samples.Take(5))
                {
                    int current_top = sample_counter;
                    //Console.WriteLine("{0}, {1}", s.TOSFrame.Function, s.TOSFrame.CPUTime);
                    Console.WriteLine($"<tr data-tt-id=\"{current_top}\"><td>{s.TOSFrame.Function}</td><td>{s.TOSFrame.CPUTime}</td><td>{s.TOSFrame.Module}</td><td>{s.TOSFrame.FunctionFull}</td><td>{s.TOSFrame.SourceFile}</td><td>{s.TOSFrame.StartAddress}</td></tr>");
                    foreach (var ss in s.Stacks.Take(1))
                    {
                        foreach (var p in ss.Take(5))
                        {
                            sample_counter += 1;
                            //Console.WriteLine($"\t{p.Function}");
                            Console.WriteLine($"<tr data-tt-id=\"{sample_counter}\" data-tt-parent-id=\"{current_top}\"><td>{p.Function}</td><td>{p.CPUTime}</td><td>{p.Module}</td><td>{p.FunctionFull}</td><td>{p.SourceFile}</td><td>{p.StartAddress}</td></tr>");
                        }
                    }
                }
                Console.WriteLine($"Got {samples.Count()} samples.");
            } catch (Exception ex) {
                Console.WriteLine($"Caught an error, with message: [{ex.StackTrace}]");
            }
        }
#endif
    }

}