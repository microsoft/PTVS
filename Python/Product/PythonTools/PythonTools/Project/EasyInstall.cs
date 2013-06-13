/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Project {
    static class EasyInstall {
        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] { 
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        // The relative path from PrefixPath, and true if it is a Python script
        // that needs to be run with the interpreter.
        private static readonly KeyValuePair<string, bool>[] EasyInstallLocations = new[] {
            new KeyValuePair<string, bool>(Path.Combine("Scripts", "easy_install-script.py"), true),
            new KeyValuePair<string, bool>("easy_install-script.py", true),
            new KeyValuePair<string, bool>(Path.Combine("Scripts", "easy_install.exe"), false),
            new KeyValuePair<string, bool>("easy_install.exe", false)
        };

        private static string GetEasyInstallPath(IPythonInterpreterFactory factory, out bool isScript) {
            foreach (var path in EasyInstallLocations) {
                string easyInstallPath = Path.Combine(factory.Configuration.PrefixPath, path.Key);
                isScript = path.Value;
                if (File.Exists(easyInstallPath)) {
                    return easyInstallPath;
                }
            }
            isScript = false;
            return null;
        }

        private static Task<int> Run(IPythonInterpreterFactory factory, Redirector output, params string[] cmd) {
            bool isScript;
            var easyInstallPath = GetEasyInstallPath(factory, out isScript);
            if (easyInstallPath == null) {
                var tcs = new TaskCompletionSource<int>();
                tcs.SetException(new FileNotFoundException("Cannot find distribute ('easy_install.exe')"));
                return tcs.Task;
            }

            var outFile = Path.GetTempFileName();
            var errFile = Path.GetTempFileName();
            var psi = new ProcessStartInfo("cmd.exe");
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            psi.WorkingDirectory = factory.Configuration.PrefixPath;

            easyInstallPath = ProcessOutput.QuoteSingleArgument(easyInstallPath);
            if (isScript) {
                easyInstallPath = ProcessOutput.QuoteSingleArgument(factory.Configuration.InterpreterPath) + " " + easyInstallPath;
            }
            psi.Arguments = "/S /C \"" +
                easyInstallPath + " " +
                string.Join(" ", cmd) + 
                " > " + ProcessOutput.QuoteSingleArgument(outFile) +
                " 2> " + ProcessOutput.QuoteSingleArgument(errFile) +
                "\"";

            return Task.Factory.StartNew<int>((Func<int>)(() => {
                try {
                    int exitCode;
                    using (var proc = Process.Start(psi)) {
                        proc.WaitForExit();
                        exitCode = proc.ExitCode;
                    }
                    if (output != null) {
                        var lines = File.ReadAllLines(outFile);
                        foreach (var line in lines) {
                            output.WriteLine(line);
                        }
                        lines = File.ReadAllLines(errFile);
                        foreach (var line in lines) {
                            output.WriteErrorLine(line);
                        }
                    }
                    return exitCode;
                } finally {
                    try {
                        File.Delete(outFile);
                    } catch { }
                    try {
                        File.Delete(errFile);
                    } catch { }
                }
            }));
        }

        public static Task Install(IPythonInterpreterFactory factory, string package, Redirector output = null) {
            return Task.Factory.StartNew((Action)(() => {
                using (var proc = Run(factory, output, "install", package)) {
                    proc.Wait();
                }
            }));
        }

        public static Task<bool> Install(IPythonInterpreterFactory factory,
            string package,
            IServiceProvider site,
            Redirector output = null) {

            Task task;
            bool isScript;
            if (site != null && GetEasyInstallPath(factory, out isScript) == null) {
                task = Pip.QueryInstallPip(factory, site, SR.GetString(SR.InstallEasyInstall), output);
            } else {
                var tcs = new TaskCompletionSource<object>();
                tcs.SetResult(null);
                task = tcs.Task;
            }

            if (output != null) {
                output.WriteLine(SR.GetString(SR.PackageInstalling, package));
                output.Show();
            }
            return Run(factory, output, package).ContinueWith(t => {
                var exitCode = t.Result;

                if (output != null) {
                    if (exitCode == 0) {
                        output.WriteLine(SR.GetString(SR.PackageInstallSucceeded, package));
                    } else {
                        output.WriteLine(SR.GetString(SR.PackageInstallFailedExitCode, package, exitCode));
                    }
                    output.Show();
                }
                return exitCode == 0;
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }
}
