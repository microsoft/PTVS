// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
// Copyright(c) 2016 Intel Corporation
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
using System.Reflection;
using System.Windows;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Profiling {
    sealed class ProfiledProcess : IDisposable {
        private readonly string _exe, _args, _dir;
        private readonly ProcessorArchitecture _arch;
        private readonly Process _process;
        private readonly PythonToolsService _pyService;
        private readonly bool _useVTune;

        // Python mixed mode profiling is only available on VTune 2017 or later
        private static readonly string _vtunePath = "C:\\Program Files (x86)\\IntelSWTools\\VTune Amplifier XE 2017";
        private static readonly string _vtuneCl = _vtunePath + "\\bin32\\amplxe-cl.exe";
        private static readonly string[] _vtuneCollectOptions =  {"-collect hotspots", "-d 5", "-user-data-dir="};
        private static readonly string[] _vtuneReportOptions = {"-report hotspots", "-r r000hs",  "-user-data-dir="}; // TODO: Check for latest run

        public ProfiledProcess(PythonToolsService pyService, string exe, string args, string dir, Dictionary<string, string> envVars, bool useVTune) {
            var arch = NativeMethods.GetBinaryType(exe);
            if (arch != ProcessorArchitecture.X86 && arch != ProcessorArchitecture.Amd64) {
                throw new InvalidOperationException(String.Format("Unsupported architecture: {0}", arch));
            }

            dir = PathUtils.TrimEndSeparator(dir);
            if (string.IsNullOrEmpty(dir)) {
                dir = ".";
            }

            _pyService = pyService;
            _exe = exe;
            _args = args;
            _dir = dir;
            _arch = arch;
            _useVTune = useVTune;

            ProcessStartInfo processInfo;
            string pythonInstallDir = Path.GetDirectoryName(PythonToolsInstallPath.GetFile("VsPyProf.dll", typeof(ProfiledProcess).Assembly));
            
            string dll = _arch == ProcessorArchitecture.Amd64 ? "VsPyProf.dll" : "VsPyProfX86.dll";
            string arguments = string.Join(" ",
                ProcessOutput.QuoteSingleArgument(Path.Combine(pythonInstallDir, "proflaun.py")),
                ProcessOutput.QuoteSingleArgument(Path.Combine(pythonInstallDir, dll)),
                ProcessOutput.QuoteSingleArgument(dir),
                _args
            );

            processInfo = new ProcessStartInfo(_exe, arguments);
            if (_pyService.DebuggerOptions.WaitOnNormalExit) {
                processInfo.EnvironmentVariables["VSPYPROF_WAIT_ON_NORMAL_EXIT"] = "1";
            }
            if (_pyService.DebuggerOptions.WaitOnAbnormalExit) {
                processInfo.EnvironmentVariables["VSPYPROF_WAIT_ON_ABNORMAL_EXIT"] = "1";
            }
            
            processInfo.CreateNoWindow = false;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = false;
            processInfo.WorkingDirectory = _dir;

            if (envVars != null) {
                foreach (var keyValue in envVars) {
                    processInfo.EnvironmentVariables[keyValue.Key] = keyValue.Value;
                }
            }

            _process = new Process();
            _process.StartInfo = processInfo;
        }

        public void Dispose() {
            _process.Dispose();
        }

        public bool StartProfiling(string filename) {
            string outPath = "";
            if (_useVTune) {
                string[] opts = new string[_vtuneCollectOptions.Length + 3];
                _vtuneCollectOptions.CopyTo(opts, 0);
                outPath = ProcessOutput.QuoteSingleArgument(filename);
                try
                {
                    Directory.CreateDirectory(outPath);
                }
                catch (Exception e)
                {
                    MessageBox.Show(String.Format("Unable to create VTune output directory: {0}", e.Message), "Python Tools for Visual Studio");
                    return false;
                }
                string[] addtlOpts = { outPath, _exe, ProcessOutput.QuoteSingleArgument(_args.Trim('"')) };
                addtlOpts.CopyTo(opts, _vtuneCollectOptions.Length);

                ProcessStartInfo processInfo = new ProcessStartInfo(_vtuneCl, string.Join(" ", opts));
                _process.StartInfo = processInfo;
            } else {
                StartPerfMon(filename);
            }
            
            _process.EnableRaisingEvents = true;
            _process.Exited += (sender, args) => {
                if (!_useVTune) {
                    try {
                        // Exited event is fired on a random thread pool thread, we need to handle exceptions.
                        StopPerfMon();
                    } catch (InvalidOperationException e) {
                        MessageBox.Show(String.Format("Unable to stop performance monitor: {0}", e.Message), "Python Tools for Visual Studio");
                    }
                }
                else
                {
                    string[] reportAddtlOpts = { outPath, "-csv-delimiter=\",\"", "-format=csv", "-report-output=" + outPath + "\\report.csv" };
                    string[] reportOpts = new string[_vtuneReportOptions.Length + reportAddtlOpts.Length];
                    _vtuneReportOptions.CopyTo(reportOpts, 0);
                    reportAddtlOpts.CopyTo(reportOpts, _vtuneReportOptions.Length);
                    using (var p = ProcessOutput.RunHiddenAndCapture(_vtuneCl, reportOpts))
                    {
                        p.Wait();
                        if (p.ExitCode != 0)
                        {
                            throw new InvalidOperationException("Starting VTune report failed{0}{0}Output:{0}{1}{0}{0}Error:{0}{2}".FormatUI(
                            Environment.NewLine,
                            string.Join(Environment.NewLine, p.StandardOutputLines),
                            string.Join(Environment.NewLine, p.StandardErrorLines)
                            ));
                        }
                    };

                    try
                    {
                        VTuneCSVToHTML(outPath + "\\report.csv");
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException("Failed writing VTune report{0}{0}Error:{0}{1}".FormatUI(
                        Environment.NewLine,
                        string.Join(Environment.NewLine, e.Message)
                        ));
                    }
                }
                var procExited = ProcessExited;
                if (procExited != null) {
                    procExited(this, EventArgs.Empty);
                }
            };

            return _process.Start();            
        }

        public event EventHandler ProcessExited;

        private string VTuneCSVToHTML(string csvpath)
        {
            IEnumerable<string> records = File.ReadLines(csvpath);

            using (StreamWriter outs = new StreamWriter(csvpath + ".html"))
            {
                outs.WriteLine(@"<!doctype html>
<html>
<head>
<title>VTune report</title>
<style>
body { font-family: sans-serif; }
table, td { border: 1px solid black; }
th { background-color: gray; }
</style>
</head>
<body><table>
	    ");
                foreach (var ri in records.Select((v, i) => new { i, v }))
                {
                    outs.WriteLine("<tr>");
                    foreach (string f in ri.v.Split(','))
                    {
                        string ft = f.Trim('"');
                        if (0 == ri.i)
                        {
                            outs.WriteLine("<th>" + ft + "</th>");
                        }
                        else
                        {
                            outs.WriteLine("<td>" + ft + "</td>");
                        }
                    }
                    outs.WriteLine("</tr>");
                }
                outs.WriteLine("</table></body>");
                outs.WriteLine("</html>");
            }

            return csvpath + ".html";
        }

        private void StartPerfMon(string filename) {
            string perfToolsPath = GetPerfToolsPath();

            string perfMonPath = Path.Combine(perfToolsPath, "VSPerfMon.exe");

            if (!File.Exists(perfMonPath)) {
                throw new InvalidOperationException("Cannot locate performance tools.");
            }

            var psi = new ProcessStartInfo(perfMonPath, "/trace /output:" + ProcessOutput.QuoteSingleArgument(filename));
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            Process.Start(psi).Dispose();

            string perfCmdPath = Path.Combine(perfToolsPath, "VSPerfCmd.exe");
            using (var p = ProcessOutput.RunHiddenAndCapture(perfCmdPath, "/waitstart")) {
                p.Wait();
                if (p.ExitCode != 0) {
                    throw new InvalidOperationException("Starting perf cmd failed{0}{0}Output:{0}{1}{0}{0}Error:{0}{2}".FormatUI(
                        Environment.NewLine,
                        string.Join(Environment.NewLine, p.StandardOutputLines),
                        string.Join(Environment.NewLine, p.StandardErrorLines)
                    ));
                }
            }
        }

        private void StopPerfMon() {
            string perfToolsPath = GetPerfToolsPath();

            string perfMonPath = Path.Combine(perfToolsPath, "VSPerfCmd.exe");
            using (var p = ProcessOutput.RunHiddenAndCapture(perfMonPath, "/shutdown")) {
                p.Wait();
                if (p.ExitCode != 0) {
                    throw new InvalidOperationException("Shutting down perf cmd failed{0}{0}Output:{0}{1}{0}{0}Error:{0}{2}".FormatUI(
                        Environment.NewLine,
                        string.Join(Environment.NewLine, p.StandardOutputLines),
                        string.Join(Environment.NewLine, p.StandardErrorLines)
                    ));
                }
            }
        }

        private string GetPerfToolsPath() {
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)) {
                if (baseKey == null) {
                    throw new InvalidOperationException("Cannot open system registry");
                }

                using (var key = baseKey.OpenSubKey(@"Software\Microsoft\VisualStudio\VSPerf")) {
                    var path = key?.GetValue("CollectionToolsDir") as string;
                    if (!string.IsNullOrEmpty(path)) {
                        if (_arch == ProcessorArchitecture.Amd64) {
                            path = PathUtils.GetAbsoluteDirectoryPath(path, "x64");
                        }
                        if (Directory.Exists(path)) {
                            return path;
                        }
                    }
                }

                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\RemoteTools\{0}\DiagnosticsHub".FormatInvariant(AssemblyVersionInfo.VSVersion))) {
                    var path = PathUtils.GetParent(key?.GetValue("VSPerfPath") as string);
                    if (!string.IsNullOrEmpty(path)) {
                        if (_arch == ProcessorArchitecture.Amd64) {
                            path = PathUtils.GetAbsoluteDirectoryPath(path, "x64");
                        }
                        if (Directory.Exists(path)) {
                            return path;
                        }
                    }
                }
            }

            Debug.Fail("Registry search for Perfomance Tools failed - falling back on old path");

            string shFolder;
            if (!_pyService.Site.TryGetShellProperty(__VSSPROPID.VSSPROPID_InstallDirectory, out shFolder)) {
                throw new InvalidOperationException("Cannot find shell folder for Visual Studio");
            }

            try {
                shFolder = Path.GetDirectoryName(Path.GetDirectoryName(shFolder));
            } catch (ArgumentException) {
                throw new InvalidOperationException("Cannot find shell folder for Visual Studio");
            }

            string perfToolsPath;
            if (_arch == ProcessorArchitecture.Amd64) {
                perfToolsPath = @"Team Tools\Performance Tools\x64";
            } else {
                perfToolsPath = @"Team Tools\Performance Tools\";
            }
            perfToolsPath = Path.Combine(shFolder, perfToolsPath);
            return perfToolsPath;
        }


        internal void StopProfiling() {
            _process.Kill();
        }
    }
}
