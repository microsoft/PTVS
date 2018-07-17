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
using System.Linq;
using System.Collections.Generic;
using System.Text;

using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.PythonTools.Profiling.ExternalProfilerDriver {

    public class VTuneInvoker {
        // In Linux, make sure to run: 
        // source $(VTUNE_INSTALL_PATH)/amplxe-vars.sh
        private const string _vtune17Envvar = "VTUNE_AMPLIFIER_2017_DIR";
        private const string _vtune18Envvar = "VTUNE_AMPLIFIER_2018_DIR";
        private const string _vtuneExeBasename = "amplxe-cl";

        public static string VTunePath()
        {
            string envvarval = "dummyval";
            if (RuntimeEnvironment.OperatingSystemPlatform == Platform.Windows ||
                 RuntimeEnvironment.OperatingSystemPlatform == Platform.Linux) {
                envvarval = Environment.GetEnvironmentVariable(_vtune17Envvar) ??
                    Environment.GetEnvironmentVariable(_vtune18Envvar) ?? throw new VTuneNotInstalledException();
            } else {
                throw new Exception("Only Linux and Windows are supported at this time");
            }

            string fname = "";
            if (RuntimeEnvironment.OperatingSystemPlatform == Platform.Windows) {
                fname = Path.Combine("bin32", _vtuneExeBasename + ".exe");
            } else {
                string vtuneDir = (Environment.Is64BitOperatingSystem) ? "bin64" : "bin32";
                fname = Path.Combine(vtuneDir, _vtuneExeBasename);
            }
            fname = Path.Combine(envvarval, fname);

            if (File.Exists(fname)) {
                return fname;
            } else {
                throw new Exception($"Could not find {fname}, please check you have installed VTune");
            }
        }

        private readonly string _path;             // vtune path
        private string _baseOutDir = "";           // user data dir
        private readonly string _resultDir = "";   // path of directory to store/retrieve collected results
                                                   // empty if collection has not started

        // VTune organizes its collections in a two-level hierarchy: BaseOutDir/ResultDir
        public string BaseOutDir { get { return _baseOutDir; } }
        public string ResultDir { get { return _resultDir; } }

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

        public static string FreshDirectory(string baseName = "results") {
            string date = DateTime.Now.ToString("yyyyMMdd");
            string outPath = Path.Combine(Path.GetTempPath(), baseName + "_" + date);

            int count = 1;
            while (File.Exists(outPath)) {
                outPath = Path.Combine(Path.GetTempPath(), $"{baseName}_{date} ({count})");
                count++;
            }
            Directory.CreateDirectory(outPath);

            return outPath;
        }

        public static string FreshResultDir(string basedir) {
            const string rdirtemplate = "r_{0:D4}";
            if (basedir == "" || basedir == null || basedir == string.Empty) {
                basedir = Path.GetTempPath();
            }
            if (!Directory.Exists(basedir)) {
                throw new ArgumentException($"Directory {basedir} does not exist");
            }
            int count = 1;
            string candidate = string.Format(rdirtemplate, count);
            while (Directory.Exists(Path.Combine(basedir, candidate))) {
                count++;
                candidate = string.Format(rdirtemplate, count);
            }
            return candidate;
        }

    }

    public class VTuneNotInstalledException : Exception {
        public override string Message {
            get {
                return "Only VTune 2017 or 2018 supported, see https://software.intel.com/intel-vtune-amplifier-xe";
            }
        }
    }

    public abstract class VTuneSpec
    {
        public string UserDataDir { get; set; }
        public string ResultDir { get; set; }

        public string ResultDirCLI {
            get {
                if (ResultDir == null || ResultDir == string.Empty) {
                    if (UserDataDir == string.Empty || UserDataDir == null) {
                        UserDataDir = VTuneInvoker.FreshDirectory();
                    }
                    ResultDir = VTuneInvoker.FreshResultDir(UserDataDir);
                }
                return "-result-dir " + ResultDir;
            }
        }

        public VTuneInvoker Invoker;
        public abstract string FullCLI();
        public string UserDataDirCLI() {
            if (UserDataDir == string.Empty || UserDataDir == null) {
                UserDataDir = VTuneInvoker.FreshDirectory();
            }
            return "-user-data-dir=" + UserDataDir; // should be outputdir in PTVS
        }
    }

    public abstract class VTuneCollectSpec : VTuneSpec {
        public abstract string AnalysisName { get; }
        public string WorkloadSpec { get; set; }
        public string SymbolPath { get; set; }
        public string AnalysisCLI {
            get { return "-collect" + " " + AnalysisName; }
        }

        public string WorkloadCLI {
            get { return " -- " + WorkloadSpec; }
        }
    }

    public class VTuneCollectHotspotsSpec : VTuneCollectSpec {
        public override string AnalysisName { get { return "hotspots"; } }
        public override string FullCLI() {
            // TODO: Make sure that userdatadir exists and workloadspec is not empty
            // (also that things are appropriately quoted)
            StringBuilder sb = new StringBuilder();
            sb.Append(AnalysisCLI);
            sb.Append(" " + UserDataDirCLI());
            sb.Append(" " + ResultDirCLI);

            if (this.SymbolPath != String.Empty) {
                sb.Append($" -search-dir {this.SymbolPath}");
            }

            sb.Append(WorkloadCLI);
            return sb.ToString();
        }
    }

    public abstract class VTuneReportSpec : VTuneSpec {
        virtual public string ReportFileTemplate { get; }
        private string _reportOutFile = null;
        public string ReportOutputFile {
            get { return _reportOutFile; }
            set { _reportOutFile = value; } // should prevent this value from changing once it's been validated
        }

        protected VTuneReportSpec(string reportName) {
            _reportPath = reportName;
        }
        public abstract string ReportName { get; }
        public string ReportNameCLI {
            get { return "-report " + ReportName; }
        }
        private string _reportPath;

        public string ReportCLI {
            get {
                ValidateReportPath();
                return "-format=csv -csv-delimiter=comma -report-output=" + ReportOutputFile;
            }
        }

        private bool ValidateReportPath() {
            if (ReportOutputFile != string.Empty && ReportOutputFile != null && ReportOutputFile.Length != 0) {
                return true;
            }

            // Generate a new name
            int count = 0;
            string candidate = string.Format(ReportFileTemplate, count);
            while (File.Exists(Path.Combine(UserDataDir, candidate))) {
                count++;
                candidate = string.Format(ReportFileTemplate, count);
            }
            _reportOutFile = Path.Combine(UserDataDir, candidate);
            return true;
        }
    }

    public class VTuneReportCallstacksSpec : VTuneReportSpec {
        public override string ReportFileTemplate { get { return "r_stacks_{0:D4}.csv"; } }
        public VTuneReportCallstacksSpec(string reportName = "") : base(reportName) { }
        public override string ReportName { get { return "callstacks"; } }

        public override string FullCLI() {
            StringBuilder sb = new StringBuilder();
            sb.Append(ReportNameCLI + " -call-stack-mode user-plus-one");
            sb.Append(" " + UserDataDirCLI());
            sb.Append(" " + ResultDirCLI);
            sb.Append(" " + ReportCLI);
            return sb.ToString();
        }
    }

    // requires environment variable AMPLXE_EXPERIMENTAL
    public class VTuneCPUUtilizationSpec : VTuneReportSpec {

        public override string ReportFileTemplate { get { return "r_cpu_{0:D4}.csv"; } }
        private Dictionary<string, string> _knobs;
        public VTuneCPUUtilizationSpec(string reportName = "") : base(reportName) {
            _knobs = new Dictionary<string, string>();
            _knobs.Add("column-by", "CPUTime"); // these are case-sensitive
            _knobs.Add("query-type", "overtime");
            _knobs.Add("bin_count", "15");
            // _knobs.Add("group-by", "Process/Thread");
        }

        public override string ReportName { get { return "time"; } }

        public override string FullCLI() {
            StringBuilder sb = new StringBuilder(ReportNameCLI + " ");
            foreach (KeyValuePair<string, string> kv in _knobs) {
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
