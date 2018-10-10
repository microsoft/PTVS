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
using System.Globalization;
using System.Threading.Tasks;

using CommandLine;
using CommandLine.Text;

namespace Microsoft.PythonTools.Profiling.ExternalProfilerDriver {

    class ProgramOptions {
        [Option('p', "path")]
        public bool ReportVTunePath { get; set; }

        [Option('n', "dry-run")]
        public bool DryRunRequested { get; set; }

        [Option('c', "callstack")]
        public string CallStackFNameToParse { get; set; }

        [Option('s', "sympath")]
        public string SymbolPath { get; set; }

        [Option('d', "dwjsondir")]
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
                    Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.VTuneNotFoundInExpectedPath, ex.Message));
                    Environment.Exit(1);
                }

                if (opts.ReportVTunePath) {
                    Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.VTuneFoundInPath, vtuneExec));
                    Environment.Exit(0);
                }

                var RestArgs = opts.Rest.ToList();
                VTuneCollectHotspotsSpec spec = new VTuneCollectHotspotsSpec() {
                    WorkloadSpec = String.Join(" ", RestArgs)
                };

#if false
                if (opts.SymbolPath != string.Empty) {
                    Console.WriteLine(Strings.SymbolPathSpecifiedNotification);
                    spec.SymbolPath = opts.SymbolPath;
                }
#else
                var workloadCLI = spec.WorkloadSpec.Split(' ');
                if (workloadCLI.Length >= 2) {
                    spec.SymbolPath = Path.GetDirectoryName(workloadCLI[1]);
                }
#endif

                string vtuneCollectArgs = spec.FullCLI();

                VTuneReportCallstacksSpec repspec = new VTuneReportCallstacksSpec();
                string vtuneReportArgs = repspec.FullCLI();

                VTuneCPUUtilizationSpec reptimespec = new VTuneCPUUtilizationSpec();
                string vtuneReportTimeArgs = reptimespec.FullCLI();

                // If output directory requested and it does not exist, create it
                if (!opts.DryRunRequested) {
                    if (opts.DWJsonOutDir == null) {
                        Console.WriteLine(Strings.OutputDirRequired);
                        Environment.Exit(1);
                    } else {
                        if (!Directory.Exists(opts.DWJsonOutDir)) {
                            try {
                                Directory.CreateDirectory(opts.DWJsonOutDir);
                            } catch (Exception ex) {
                                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.DirCreationFailed, opts.DWJsonOutDir, ex.Message));
                                Environment.Exit(1);
                            }
                        }
                        dwjsonDir = opts.DWJsonOutDir;
                    }
                }

                if (!opts.DryRunRequested) {
                    Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.CollectCmdLineDump, vtuneExec, vtuneCollectArgs));
                    ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneCollectArgs);

                    Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.CallstackReportCmdLineDump, vtuneExec, vtuneReportArgs));
                    ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneReportArgs);

                    Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.TimingReportCmdLineDump, vtuneExec, vtuneReportTimeArgs));
                    ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneReportTimeArgs);
                } else {
                    Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.CollectCmdLineDump, vtuneExec, vtuneCollectArgs));
                    Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.CallstackReportCmdLineDump, vtuneExec, vtuneReportArgs));
                    Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.TimingReportCmdLineDump, vtuneExec, vtuneReportTimeArgs));

                    Environment.Exit(0);
                }

                double runtime = VTuneToDWJSON.CSReportToDWJson(repspec.ReportOutputFile, Path.Combine(dwjsonDir,"Sample.dwjson"), spec.SymbolPath);
                VTuneToDWJSON.CPUReportToDWJson(reptimespec.ReportOutputFile, Path.Combine(dwjsonDir, "Session.counters"), runtime);
            })
            .WithNotParsed(errors => {
                Console.WriteLine(Strings.IncorrectCommandLine);
                Environment.Exit(1);
            });

            Environment.Exit(0);
        }
    }

}