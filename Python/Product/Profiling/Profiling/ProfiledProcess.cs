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
using System.Reflection;
using System.Windows;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Profiling {
    sealed class ProfiledProcess : IDisposable {
        private readonly string _exe, _args, _dir;
        private readonly ProcessorArchitecture _arch;
        private readonly Process _process;
        private readonly PythonToolsService _pyService;
        private string _pyArch;
        public ProfiledProcess(PythonToolsService pyService, string exe, string args, string dir, Dictionary<string, string> envVars) {

            string pythonInstallDir = Path.GetDirectoryName(PythonToolsInstallPath.GetFile("VsPyProf.dll", typeof(ProfiledProcess).Assembly));

            var arg = ProcessOutput.QuoteSingleArgument(Path.Combine(pythonInstallDir, "getArch.py"));
            getPyArch(exe, arg);

            if (_pyArch.EndsWith("-arm64")) {
                throw new InvalidOperationException(Strings.UnsupportedArchitecture.FormatUI(_pyArch));  
            } else if (_pyArch.EndsWith("-32")) {
                _arch = ProcessorArchitecture.X86;
            } else {
                _arch = ProcessorArchitecture.Amd64;
            }

            dir = PathUtils.TrimEndSeparator(dir);
            if (string.IsNullOrEmpty(dir)) {
                dir = ".";
            }

            _pyService = pyService;
            _exe = exe;
            _args = args;
            _dir = dir;

            ProcessStartInfo processInfo;

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

        public void getPyArch(string exe, string arg) {
            var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = exe,
                    Arguments = arg,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.ErrorDataReceived += Process_OutputDataReceived;
            process.OutputDataReceived += Process_OutputDataReceived;

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.WaitForExit();
        }

        void Process_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            if (!string.IsNullOrEmpty(e.Data)) {
                _pyArch = e.Data;
            }  
        }

        public void Dispose() {
            _process.Dispose();
        }

        public void StartProfiling(string filename) {
            StartPerfMon(filename);

            _process.EnableRaisingEvents = true;
            _process.Exited += (sender, args) => {
                try {
                    // Exited event is fired on a random thread pool thread, we need to handle exceptions.
                    StopPerfMon();
                } catch (InvalidOperationException e) {
                    MessageBox.Show(Strings.UnableToStopPerfMon.FormatUI(e.Message), Strings.ProductTitle);
                }
                var procExited = ProcessExited;
                if (procExited != null) {
                    procExited(this, EventArgs.Empty);
                }
            };

            _process.Start();
        }

        public event EventHandler ProcessExited;

        private void StartPerfMon(string filename) {
            string perfToolsPath = GetPerfToolsPath();

            string perfMonPath = Path.Combine(perfToolsPath, "VSPerfMon.exe");

            if (!File.Exists(perfMonPath)) {
                throw new InvalidOperationException(Strings.CannotLocatePerformanceTools);
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
                    throw new InvalidOperationException(Strings.StartPerfCmdError.FormatUI(
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
                    throw new InvalidOperationException(Strings.StopPerfMonError.FormatUI(
                        string.Join(Environment.NewLine, p.StandardOutputLines),
                        string.Join(Environment.NewLine, p.StandardErrorLines)
                    ));
                }
            }
        }

        private string GetPerfToolsPath() {
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)) {
                if (baseKey == null) {
                    throw new InvalidOperationException(Strings.CannotOpenSystemRegistry);
                }

                using (var key = baseKey.OpenSubKey(@"Software\Microsoft\VisualStudio\VSPerf")) {
                    // ie. CollectionToolsDir2022
                    var path = key?.GetValue("CollectionToolsDir" + AssemblyVersionInfo.Name) as string;

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
                throw new InvalidOperationException(Strings.CannotFindShellFolder);
            }

            try {
                shFolder = Path.GetDirectoryName(Path.GetDirectoryName(shFolder));
            } catch (ArgumentException) {
                throw new InvalidOperationException(Strings.CannotFindShellFolder);
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
