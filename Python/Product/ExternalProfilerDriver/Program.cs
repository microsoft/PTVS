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

        [Option('s', "sympath", HelpText = "Specify the path(s) to search symbols in")]
        public string SymbolPath { get; set; }

        [Option('d', "dwjsondir", HelpText = "Specify the directory in which to dump resulting dwjson (contents are overwritten)")]
        public string DWJsonOutDir { get; set; }

        [Value(0)]
        public IEnumerable<string> Rest { get; set; }
    }

    class Program {
        static void Main(string[] args) {

            string dwjsonDir = "";

            var parser = new Parser(config => {
                config.EnableDashDash = true;
            });

            var res = parser.ParseArguments<ProgramOptions>(args)
                            .WithParsed<ProgramOptions>(opts => {

                string vtuneExec = "";
                try {
                    vtuneExec = VTuneInvoker.VTunePath();
                } catch (VTuneNotInstalledException ex) {
                    Console.WriteLine($"VTune not found in expected path: {ex.Message}");
                    Environment.Exit(1);
                }

                if (opts.ReportVTunePath) {
                    Console.WriteLine($"The path of VTune is: {vtuneExec}");
                    Environment.Exit(0);
                }

                var RestArgs = opts.Rest.ToList();
                VTuneCollectHotspotsSpec spec = new VTuneCollectHotspotsSpec() {
                    WorkloadSpec = String.Join(" ", RestArgs)
                };

                if (opts.SymbolPath != string.Empty) {
                    Console.WriteLine("Symbol path specified");
                    spec.SymbolPath = opts.SymbolPath;
                }

                string vtuneCollectArgs = spec.FullCLI();

                VTuneReportCallstacksSpec repspec = new VTuneReportCallstacksSpec();
                string vtuneReportArgs = repspec.FullCLI();

                VTuneCPUUtilizationSpec reptimespec = new VTuneCPUUtilizationSpec();
                string vtuneReportTimeArgs = reptimespec.FullCLI();

                // If output directory requested and it does not exist, create it
                if (!opts.DryRunRequested) {
                    if (opts.DWJsonOutDir == null) {
                        Console.WriteLine($"Need an output directory unless in dry run.");
                        Environment.Exit(1);
                    } else {
                        if (!Directory.Exists(opts.DWJsonOutDir)) {
                            try {
                                Directory.CreateDirectory(opts.DWJsonOutDir);
                            } catch (Exception ex) {
                                Console.WriteLine($"Couldn't create specified directory [{opts.DWJsonOutDir}]: {ex.Message}");
                                Environment.Exit(1);
                            }
                        }
                        dwjsonDir = opts.DWJsonOutDir;
                    }
                }

                if (!opts.DryRunRequested) {
                    Console.WriteLine($"Collect command line is: [ {vtuneExec} {vtuneCollectArgs} ]");
                    ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneCollectArgs);

                    Console.WriteLine($"Report callstacks line: [ {vtuneExec} {vtuneReportArgs} ]");
                    ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneReportArgs);

                    Console.WriteLine($"Report timing line: [ {vtuneExec} {vtuneReportTimeArgs} ]");
                    ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneReportTimeArgs);
                } else {
                    Console.WriteLine($"Collect command line is: [ {vtuneExec} {vtuneCollectArgs} ]");
                    Console.WriteLine("Report command lines");
                    Console.WriteLine($"[ {vtuneExec} {vtuneReportArgs} ]");
                    Console.WriteLine($"[ {vtuneExec} {vtuneReportTimeArgs} ]");

                    Environment.Exit(0);
                }

                double runtime = VTuneToDWJSON.CSReportToDWJson(repspec.ReportOutputFile, Path.Combine(dwjsonDir,"Sample.dwjson"));
                VTuneToDWJSON.CPUReportToDWJson(reptimespec.ReportOutputFile, Path.Combine(dwjsonDir, "Session.counters"), runtime);

            })
            .WithNotParsed(errors => {
                Console.WriteLine("Incorrect command line.");
                Environment.Exit(1);
            });

            Environment.Exit(0);
        }
    }

}