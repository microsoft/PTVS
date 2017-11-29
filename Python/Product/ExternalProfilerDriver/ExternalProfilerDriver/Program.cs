using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.CommandLineUtils;

namespace ExternalProfilerDriver
{
    public class Program
    {
        static int Main(string[] args)
        {
#if false
            CommandLineApplication clapp = new CommandLineApplication(throwOnUnexpectedArg: false);
	    // CommandArgument names = null;

       CommandOption vtpathRequested = clapp.Option("-v|--vtunepath", "Displays the path where VTune is installed, if any", CommandOptionType.NoValue);

        clapp.HelpOption("-?|-h|--help");
	    clapp.OnExecute(() => {

            if (vtpathRequested.HasValue())
            {
                string ev = GetVTunePath();
                Console.WriteLine(string.Format("The value of the envvar is [ {0} ]", ev));
            }
            else
            {
                Console.WriteLine("Need a command line to profile");
            }

	      return 0;
	    });
	    
	    int ret = clapp.Execute(args);
#else
            int ret = 0;
#endif

#if false
            VTuneInvoker inv = new VTuneInvoker();
            Console.WriteLine(string.Format("The answer from the invoker is [{0}]", inv.Report()));
#endif
            string vtuneExec = VTuneInvoker.VTunePath();

#if true
            VTuneCollectHotspotsSpec spec = new VTuneCollectHotspotsSpec()
            {
                WorkloadSpec = @"c:\users\perf\Anaconda3\python C:\Users\perf\Documents\Projects\twittersent\TwitterSentiment.py"
            };
            string vtuneCollectArgs = spec.FullCLI();
            Console.WriteLine("Going to run the following spec for hotspots spec is : [{0}]", vtuneCollectArgs);

            VTuneReportCallstacksSpec repspec = new VTuneReportCallstacksSpec();
            string vtuneReportArgs = repspec.FullCLI();
            Console.WriteLine("Going to run the following spec for callstack report spec is : [{0}]", vtuneReportArgs);
#if false
            VTuneCPUUtilizationSpec cpuspec = new VTuneCPUUtilizationSpec();
            Console.WriteLine("The spec for cpu util spec is : [{0}]", cpuspec.FullCLI());
#endif
            ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneCollectArgs);
            ProcessAsyncRunner.RunWrapper(vtuneExec, vtuneReportArgs);
#endif

#if false
            VTuneReportCallstacksSpec repspec = new VTuneReportCallstacksSpec()
            {
                //WorkloadSpec = @"c:\users\perf\Anaconda3\python C:\Users\perf\Documents\Projects\twittersent\TwitterSentiment.py",
                ReportOutputFile = @"C:\Users\perf\AppData\Local\Temp\results_20171128\r_stacks_0001.csv"
            };
#endif

            if (!File.Exists(repspec.ReportOutputFile))
            {
                Console.WriteLine("Cannot find the file I'm expecting, something went wrong with the generation.");
            }
            else
            {
                var samples = VTuneToDWJSON.ParseFromFile(repspec.ReportOutputFile);
                foreach (var s in samples)
                {
                    Console.WriteLine("{0} : {1}", s.TOSFrame.Function, s.TOSFrame.CPUTime);
                }
                //VTuneCPUUtilizationParser.CPURecordsFromFilename(@"c:\users\perf\Downloads\samplereport.csv");
            }
            Console.WriteLine("Press a key...");
            Console.ReadLine();
            return ret;
        }
    }
}
