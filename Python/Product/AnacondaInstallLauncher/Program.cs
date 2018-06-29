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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Microsoft.PythonTools.AnacondaInstallLauncher {
    class Program {
        private const int ERROR_INVALID_PARAMETER = 87;
        private const int ERROR_INSTALL_CANCEL = 15608;

        static int PrintUsage() {
            var name = Path.GetFileName(typeof(Program).Assembly.Location);
            var spaces = new string(' ', name.Length);
            Console.Error.WriteLine("{0} install [installer] [target directory] [regkey@name=value]", name);
            Console.Error.WriteLine("{0} uninstall [uninstaller] [target directory] [regkey]", spaces);
            Console.Error.WriteLine();
            return ERROR_INVALID_PARAMETER;
        }

        static int Main(string[] args) {
            Program p;

            try {
                p = new Program(args[0], args[1], args[2], args.ElementAtOrDefault(3));
#if DEBUG
            } catch (Exception ex) {
                Console.Error.WriteLine(ex);
                PrintUsage();
                return ex.HResult;
            }
#else
            } catch {
                return PrintUsage();
            }
#endif

            Console.CancelKeyPress += (_, e) => {
                Console.Error.WriteLine("Cancelling wait");
                p.Cancel();
            };

            return p.Go();
        }

        private readonly bool _install;
        private readonly string _installer, _targetDir, _regKey, _regValueName, _regValue;
        private bool _cancel;

        private Program(string command, string installer, string targetDir, string registryValue) {
            _targetDir = GetFullPath(targetDir);
            _installer = GetFullPath(installer);
            if (!File.Exists(_installer)) {
                Console.Error.WriteLine($"Did not find {_installer}");
                throw new FileNotFoundException(_installer);
            }
            _install = command.Equals("install", StringComparison.OrdinalIgnoreCase);

            var m = Regex.Match(registryValue ?? "", @"^(?<key>[^@]+)(@(?<name>.+)=(?<value>.+))?$");
            if (m.Success) {
                _regKey = m.Groups["key"].Value;
                _regValueName = m.Groups["name"].Value;
                _regValue = m.Groups["value"].Value;
            }
        }

        private static string GetFullPath(string path) {
            if (string.IsNullOrEmpty(path)) {
                return null;
            }

            if (Path.IsPathRooted(path)) {
                return path;
            }

            return Path.Combine(
                Path.GetDirectoryName(typeof(Program).Assembly.Location),
                path
            );
        }

        int Go() {
            try {
                if (_install) {
                    return DoInstall();
                } else {
                    return DoUninstall();
                }
            } catch (Exception ex) {
                Console.Error.WriteLine(ex);
                Console.Error.WriteLine(ex.ToString());
                return ex.HResult;
            }
        }

        void Cancel() => _cancel = true;

        Process Run(string filename, string arguments) {
            var psi = new ProcessStartInfo(filename, arguments) {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            Console.Error.WriteLine("Starting:{0}  Filename: {1}{0}  Argument: {2}", Environment.NewLine, psi.FileName, psi.Arguments);
            var p = Process.Start(psi);

            p.OutputDataReceived += (_, e) => { if (e.Data != null) Console.Write(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.Error.Write(e.Data); };

            return p;
        }

        int DoInstall() {
            WaitForUninstall(false);
            if (_cancel) {
                return ERROR_INSTALL_CANCEL;
            }

            if (!string.IsNullOrEmpty(_regKey) && !string.IsNullOrEmpty(_regValueName) && !string.IsNullOrEmpty(_regValue)) {
                Console.Error.WriteLine("Setting {0}@{1} to '{2}'", _regKey, _regValueName, _regValue);
                using (var key = Registry.LocalMachine.CreateSubKey(_regKey)) {
                    key?.SetValue(_regValueName, _regValue);
                }
            }

            var p = Run(_installer,
                // We do not quote the target directory here, because the Anaconda installer requires it to be unquoted
                // (also last on the command line - everything after "/D=" is treated as a directory)
                string.Format(CultureInfo.InvariantCulture, "/InstallationType=AllUsers /RegisterPython=0 /S /D={0}", _targetDir));
            p.WaitForExit();
            return p.ExitCode;
        }

        int DoUninstall() {
            if (!string.IsNullOrEmpty(_regKey)) {
                try {
                    Registry.LocalMachine.DeleteSubKeyTree(_regKey, false);
                } catch (Exception ex) {
                    Console.Error.WriteLine("Error deleting {0}: {1}", _regKey, ex);
                }
            }

            var p = Run(_installer, "/S");

            if (p.WaitForExit(30000)) {
                Console.Error.WriteLine("Process exited: {0}", p.ExitCode);
            } else {
                Console.Error.WriteLine("Process has not exited");
            }

            WaitForUninstall(true);

            return _cancel ? ERROR_INSTALL_CANCEL : p.ExitCode;
        }

        void WaitForUninstall(bool requireWait) {
            try {
                WaitForProcess(requireWait);
            } catch (Exception ex) {
                Console.Error.WriteLine(ex.ToString());
            }
        }

        bool IsDirectoryPresent() {
            if (!Directory.Exists(_targetDir)) {
                Console.Error.WriteLine("Target directory is not present");
                return false;
            }
            try {
                if (!Directory.EnumerateFileSystemEntries(_targetDir).Any()) {
                    Console.Error.WriteLine("Target directory is empty");
                    return false;
                }
            } catch (Exception ex) {
                Console.Error.WriteLine("Error reading target directory");
                Console.Error.WriteLine(ex.ToString());
                return false;
            }

            return true;
        }

        Process[] GetProcesses() {
            return Process.GetProcesses()
                .Where(p => {
                    string n, d;
                    try {
                        n = p.ProcessName;
                        d = p.MainModule.FileVersionInfo.FileDescription;
                    } catch {
                        return false;
                    }
                    return n.StartsWith("Un_") && d.IndexOf("Anaconda", StringComparison.OrdinalIgnoreCase) >= 0;
                }).ToArray();
        }

        void WaitForProcess(bool requireProcess) {
            if (!IsDirectoryPresent()) {
                return;
            }

            var procs = GetProcesses();
            int retries = 30;
            while (requireProcess && procs.Length == 0 && retries-- > 0 && !_cancel) {
                Thread.Sleep(1000);
                procs = GetProcesses();
            }
            Console.Error.WriteLine("Waiting for {0} processes named {1}", procs.Length,
                string.Join(", ", procs.Select(p => { try { return p.ProcessName; } catch { return "<exited>"; } })));

            var tasks = procs.Select(p => Task.Run(() => p.WaitForExit())).ToArray();
            var task = Task.WhenAll(tasks);

            while (!_cancel && IsDirectoryPresent() && !task.Wait(1000)) { }

            Console.Error.WriteLine("Finished waiting");
        }
    }
}
