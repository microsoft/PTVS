using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using Microsoft.DotNet.PlatformAbstractions;

namespace ExternalProfilerDriver
{
    public class VTuneInvoker
    {
        private static readonly string _vtuneCl = @"\bin32\amplxe-cl.exe";
        private static readonly string _vtune17Envvar = "VTUNE_AMPLIFIER_2017_DIR";
        private static readonly string _vtune18Envvar = "VTUNE_AMPLIFIER_2018_DIR";

        public static string VTunePath()
        {
            // expecting something like "C:\\Program Files (x86)\\IntelSWTools\\VTune Amplifier XE 2017";
            string envvarval;
            if (RuntimeEnvironment.OperatingSystemPlatform == Platform.Windows ||
                 RuntimeEnvironment.OperatingSystemPlatform == Platform.Linux)
            {
                envvarval = Environment.GetEnvironmentVariable(_vtune17Envvar);
                if (envvarval == null)
                {
                    envvarval = Environment.GetEnvironmentVariable(_vtune18Envvar);
                }
                if (envvarval == null)
                {
                    throw new VTuneNotInstalledException();
                }

            }
            else
            {
                envvarval = "OS not supported"; // should this throw an exception?
            }
            if (File.Exists(envvarval + _vtuneCl)) // not exactly sure why Path.Combine doesn't work here
            {
                return envvarval + _vtuneCl;
            }
            else
            {
                // TODO: probably should throw an exception here
                return string.Format("{0} does not exist, on path [{1}]", Path.Combine(envvarval, _vtuneCl), envvarval);
            }
        }

        private readonly string _path;        // vtune path
        private string _baseOutDir = "";  // user data dir
        private readonly string _resultDir = "";   // path of directory to store/retrieve collected results
                                                   // empty if collection has not started
        private readonly string _profiledCL;

        // VTune organizes its collections in a two-level hierarchy: BaseOutDir/ResultDir
        public string BaseOutDir { get { return _baseOutDir; } }
        public string ResultDir { get { return _resultDir; } }

        private VTuneCollectSpec CollectSpec { get; set; }
        private IEnumerable<VTuneReportSpec> ReportSpecs { get; set; }

        /// <summary>
        /// A reference to an invocation of VTune that has as base ("user data") directory <paramref name="baseOutDir"/>
        /// </summary>
        /// <param name="baseOutDir"></param>
        /// <param name="vtunePath"></param>
        public VTuneInvoker(string baseOutDir, string vtunePath = "")
        {
            _baseOutDir = baseOutDir;
            _path = vtunePath;
        }

        public string CollectCL()
        {
            return "teststring";
        }

        public string Report()
        {
            //return Path.Combine(_vtunePath, _vtuneCl);
            return "";// throw notimplemented
        }

        public void Start()
        {
            EnsureBaseDir();
            Console.WriteLine("Should be executing....");
        }

        private void EnsureBaseDir()
        {
            string possible = BaseOutDir;
            if (Directory.Exists(possible)) return;

            string filename = Path.GetFileNameWithoutExtension(possible);
            string date = DateTime.Now.ToString("yyyyMMdd");
            string candidatedirname = Path.Combine(Path.GetTempPath(), filename + "_" + date + ".vt");

            int count = 1;
            while (Directory.Exists(candidatedirname))
            {
                candidatedirname = Path.Combine(Path.GetTempPath(), filename + "_" + date + "(" + count + ").vt");
                count++;
            }
            Directory.CreateDirectory(candidatedirname);
            _baseOutDir = candidatedirname;
        }

        public void AddCollectorSpec(VTuneCollectSpec collector)
        {
            CollectSpec = collector;
        }

        private static string NextResultDirInDir(string basedir)
        {
            if (!Directory.Exists(basedir))
            {
                throw new ArgumentException($"Expected directory {basedir} does not exist");
            }

            int latest = 0;
            IEnumerable<string> previous = Directory.GetDirectories(basedir, "r*hs");
            if (previous.Count() != 0)
            {
                latest = previous
                           .Select(x => { var n = new FileInfo(x).Name; return n.Substring(1, n.Length - 3); })
                           .Select(x => Int32.Parse(x))
                           .Max();
                latest += 1;
            }

            var latestReportName = "r" + latest.ToString("D3") + "hs"; // what happens if there's none?
            return latestReportName;
        }

        public static string FreshDirectory(string baseName = "results")
        {
            string date = DateTime.Now.ToString("yyyyMMdd");
            string outPath = Path.Combine(Path.GetTempPath(), baseName + "_" + date);

            int count = 1;
            while (File.Exists(outPath))
            {
                //outPath = Path.Combine(Path.GetTempPath(), baseName + "_" + date + "(" + count + ")");
                outPath = Path.Combine(Path.GetTempPath(), $"{baseName}_{date} ({count})");
                count++;
            }
            Directory.CreateDirectory(outPath);

            return outPath;
        }

        public static string FreshResultDir(string basedir)
        {
            const string rdirtemplate = "r_{0:D4}";
            if (basedir == "" || basedir == null || basedir == string.Empty)
            {
                basedir = Path.GetTempPath();
            }
            if (!Directory.Exists(basedir))
            {
                throw new ArgumentException($"Directory {basedir} does not exist");
            }
            int count = 1;
            string candidate = string.Format(rdirtemplate, count);
            while (Directory.Exists(Path.Combine(basedir, candidate)))
            {
                count++;
                candidate = string.Format(rdirtemplate, count);
            }
            return candidate;
        }
    }

    public class VTuneNotInstalledException : Exception
    {
        public override string Message
        {
            get
            {
                return "Only VTune 2017 or 2018 supported, see https://software.intel.com/en-us/intel-vtune-amplifier-xe";
            }
        }
    }

    public abstract class VTuneSpec
    {
        public string UserDataDir { get; set; }
        public string ResultDir { get; set; }

        public string ResultDirCLI
        {
            get
            {
                if (ResultDir == null || ResultDir == string.Empty)
                {
                    if (UserDataDir == string.Empty || UserDataDir == null)
                    {
                        UserDataDir = VTuneInvoker.FreshDirectory();
                    }
                    ResultDir = VTuneInvoker.FreshResultDir(UserDataDir);
                }
                return "-result-dir " + ResultDir;
            }
        }

        public VTuneInvoker Invoker;
        public abstract string FullCLI();
        public string UserDataDirCLI()
        {
            if (UserDataDir == string.Empty || UserDataDir == null)
            {
                UserDataDir = VTuneInvoker.FreshDirectory();
            }
            return "-user-data-dir=" + UserDataDir; // should be outputdir in PTVS
        }
    }

    public abstract class VTuneCollectSpec : VTuneSpec
    {
        public abstract string AnalysisName { get; }
        public string WorkloadSpec { get; set; }
        public string AnalysisCLI
        {
            get { return "-collect" + " " + AnalysisName; }
        }

        public string WorkloadCLI
        {
            get { return " -- " + WorkloadSpec; }
        }
    }

    public class VTuneCollectHotspotsSpec : VTuneCollectSpec
    {
        public override string AnalysisName { get { return "hotspots"; } }
        public override string FullCLI()
        {
            // TODO: Make sure that userdatadir exists and workloadspec is not empty
            // (also that things are appropriately quoted)
            StringBuilder sb = new StringBuilder();
            sb.Append(AnalysisCLI);
            sb.Append(" " + UserDataDirCLI());
            sb.Append(" " + ResultDirCLI);
            sb.Append(WorkloadCLI);
            return sb.ToString();
        }
    }

    public abstract class VTuneReportSpec : VTuneSpec
    {
        virtual public string ReportFileTemplate { get; }
        private string _reportOutFile = null;
        public string ReportOutputFile
        {
            get { return _reportOutFile; }
            set { _reportOutFile = value; } // should prevent this value from changing once it's been validated
        }

        protected VTuneReportSpec(string reportName)
        {
            _reportPath = reportName;
        }
        public abstract string ReportName { get; }
        public string ReportNameCLI
        {
            get { return "-report " + ReportName; }
        }
        private string _reportPath;

        public string ReportCLI
        {
            get
            {
                ValidateReportPath();
                return "-format=csv -csv-delimiter=comma -report-output=" + ReportOutputFile;
            }
        }

        private bool ValidateReportPath()
        {
            if (ReportOutputFile != string.Empty && ReportOutputFile != null && ReportOutputFile.Length != 0)
            {
                return true;
            }

            // Generate a new name
            int count = 0;
            string candidate = string.Format(ReportFileTemplate, count);
            while (File.Exists(Path.Combine(UserDataDir, candidate)))
            {
                count++;
                candidate = string.Format(ReportFileTemplate, count);
            }
            _reportOutFile = Path.Combine(UserDataDir, candidate);
            return true;
        }
    }

    public class VTuneReportCallstacksSpec : VTuneReportSpec
    {
        public override string ReportFileTemplate { get { return "r_stacks_{0:D4}.csv"; } }
        public VTuneReportCallstacksSpec(string reportName = "") : base(reportName) { }
        public override string ReportName { get { return "callstacks"; } }

        public override string FullCLI()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(ReportNameCLI + " -call-stack-mode user-plus-one");
            sb.Append(" " + UserDataDirCLI());
            sb.Append(" " + ResultDirCLI);
            sb.Append(" " + ReportCLI);
            return sb.ToString();
        }
    }

    public class VTuneCPUUtilizationSpec : VTuneReportSpec
    {

        public override string ReportFileTemplate { get { return "r_cpu_{0:D4}.csv"; } }
        private Dictionary<string, string> _knobs;
        public VTuneCPUUtilizationSpec(string reportName = "") : base(reportName)
        {
            _knobs = new Dictionary<string, string>();
            _knobs.Add("column-by", "CPUTime"); // these are case-sensitive
            _knobs.Add("query-type", "overtime");
            _knobs.Add("bin_count", "15");
            _knobs.Add("group-by", "Process/Thread");
        }

        public override string ReportName { get { return "time"; } }

        public override string FullCLI()
        {
            StringBuilder sb = new StringBuilder(ReportNameCLI + " ");
            foreach (KeyValuePair<string, string> kv in _knobs)
            {
                sb.Append($"-r-k {kv.Key}={kv.Value}"); //should these be quoted?
                sb.Append(" ");
            }
            sb.Append(" " + UserDataDirCLI());
            sb.Append(" " + ResultDirCLI);
            sb.Append(" " + ReportCLI);

            return sb.ToString();
        }
    }
}
