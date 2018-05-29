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
using System.Threading;

namespace Microsoft.PythonTools.AnacondaInstallLauncher {
    class Program {
        static int PrintUsage() {
            var name = Path.GetFileName(typeof(Program).Assembly.Location);
            var spaces = new string(' ', name.Length);
            Console.Error.WriteLine("{0} install [installer] [target directory]", name);
            Console.Error.WriteLine("{0} uninstall [uninstaller] [target directory]", spaces);
            Console.Error.WriteLine();
            return 1;
        }

        static int Main(string[] args) {
            Program p;

            try {
                p = new Program(args[0], args[1], args[2]);
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

            return p.Go();
        }

        private readonly bool _install;
        private readonly string _installer, _targetDir;
        private DateTime _stopAt;

        private Program(string command, string installer, string targetDir) {
            _targetDir = Path.GetFullPath(targetDir);
            _installer = Path.GetFullPath(installer);
            if (!File.Exists(_installer)) {
                Console.Error.WriteLine($"Did not find {_installer}");
                throw new FileNotFoundException(_installer);
            }
            _stopAt = DateTime.UtcNow.AddMinutes(20);
            _install = command.Equals("install", StringComparison.OrdinalIgnoreCase);
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

        int DoInstall() {
            WaitForUninstall(false);

            var psi = new ProcessStartInfo(
                _installer,
                string.Format(CultureInfo.InvariantCulture, "/InstallationType=AllUsers /RegisterPython=0 /S /D={0}", _targetDir)
            );
            Console.Error.WriteLine("Starting:{0}  Filename: {1}{0}  Argument: {2}", Environment.NewLine, psi.FileName, psi.Arguments);
            var p = Process.Start(psi);
            p.WaitForExit();
            Console.Error.WriteLine("Process exited: {0}", p.ExitCode);
            return p.ExitCode;
        }

        int DoUninstall() {
            var psi = new ProcessStartInfo(_installer, "/S");
            Console.Error.WriteLine("Starting:{0}  Filename: {1}{0}  Argument: {2}", Environment.NewLine, psi.FileName, psi.Arguments);
            var p = Process.Start(psi);

            p.WaitForExit(30000);

            WaitForUninstall(true);
            return 0;
        }

        void WaitForUninstall(bool requireWait) {
            try {
                _stopAt = DateTime.UtcNow.AddMinutes(20);
                WaitForProcess("Un_A", requireWait);
            } catch (Exception ex) {
                Console.Error.WriteLine(ex.ToString());
            }
        }

        bool KeepWaiting {
            get {
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

                if (DateTime.UtcNow >= _stopAt) {
                    Console.Error.WriteLine("Timeout has expired");
                    return false;
                }

                return true;
            }
        }

        void WaitForProcess(string name, bool requireProcess) {
            if (!KeepWaiting) {
                return;
            }

            var procs = Process.GetProcessesByName(name) ?? new Process[0];
            int retries = 30;
            while (requireProcess && procs.Length == 0 && retries-- > 0) {
                Thread.Sleep(1000);
                procs = Process.GetProcessesByName(name) ?? new Process[0];
            }
            Console.Error.WriteLine("Waiting for {0} processes named {1}", procs.Length, name);

            bool any = true;
            while (any && KeepWaiting) {
                Thread.Sleep(1000);
                any = false;
                foreach (var p in procs) {
                    try {
                        if (!p.HasExited) {
                            any = true;
                        }
                    } catch (Exception ex) {
                        Console.Error.WriteLine("Error {0} waiting for process: {1}", ex.GetType().Name, ex.Message);
                    }
                }
            }

            Console.Error.WriteLine("Finished waiting");
        }
    }
}
