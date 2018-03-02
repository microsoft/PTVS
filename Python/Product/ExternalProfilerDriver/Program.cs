using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Utility.CommandLine;

namespace Microsoft.PythonTools.Profiling.ExternalProfilerDriver {
    class Program {

        // [Argument('p', "path")]
        private static bool ReportVTunePath { get; set; }

        // [Argument('n', "dry-run")]
        private static bool DryRunRequested { get; set; }

        // [Operands]
        private static string[] RestArgs { get; set; }

        static void PrintUsage() {
        }

        static int Main(string[] args)
        {
#if false
            try
            {
                Arguments.Populate();
            } catch (ArgumentException aex) {
                Console.WriteLine($"Incorrect form of arguments: {aex.Message}");
                return 1;
            } catch (Exception ex)
            {
                Console.WriteLine("Unidentified error condition");
                return 1;
            }
#endif

            if (true || ReportVTunePath)
            {
                try
                {
                    Console.WriteLine($"The path of VTune is: {VTuneInvoker.VTunePath()}");
                    return 0;
                } catch (VTuneNotInstalledException ex)
                {
                    Console.WriteLine($"VTune not found in expected path: {ex.Message}");
                    return 1;
                }
            }

            string vtuneExec = VTuneInvoker.VTunePath();

            VTuneCollectHotspotsSpec spec = new VTuneCollectHotspotsSpec()
            {
                WorkloadSpec = String.Join(" ", RestArgs)
            };
            string vtuneCollectArgs = spec.FullCLI();

            VTuneReportCallstacksSpec repspec = new VTuneReportCallstacksSpec();
            string vtuneReportArgs = repspec.FullCLI();

            VTuneCPUUtilizationSpec reptimespec = new VTuneCPUUtilizationSpec();
            string vtuneReportTimeArgs = reptimespec.FullCLI();

            if (!DryRunRequested)
            {
                ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneCollectArgs);
                ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneReportArgs);
                ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneReportTimeArgs);
            }

            string tempOutDir = Environment.GetEnvironmentVariable("USERPROFILE");
            string tempOutReportFName = "r_stacks_0001.csv";

            VTuneReportCallstacksSpec repstackspec = new VTuneReportCallstacksSpec()
            {
                ReportOutputFile = Path.Combine(tempOutDir, tempOutReportFName)
            };

            if (!File.Exists(repstackspec.ReportOutputFile))
            {
                Console.WriteLine("Cannot find the VTune report, something went wrong with the profiler process.");
                return 1;
            } else
            {
                var samples = VTuneToDWJSON.ParseFromFile(repspec.ReportOutputFile);
                foreach (var s in samples)
                {
                    Console.WriteLine("{0} : {1}", s.TOSFrame.Function, s.TOSFrame.CPUTime);
                }
            }

            string tracejsonfname = Path.Combine(tempOutDir, "Sampletest.dwjson");
            string cpujsonfname = Path.Combine(tempOutDir, "Sampletest.counters");

            try
            {
                double timeTotal = VTuneToDWJSON.CSReportToDWJson(repspec.ReportOutputFile, tracejsonfname);
                Console.WriteLine($"Time in seconds accounted: {timeTotal}");
                VTuneToDWJSON.CPUReportToDWJson(reptimespec.ReportOutputFile, cpujsonfname, timeTotal);
            } catch (Exception ex)
            {
                Console.WriteLine($"Errors occurred during the processing: {ex.Message}");
                return 1;
            }

            Console.WriteLine("Done!");
            return 0;
        }
    }
}
