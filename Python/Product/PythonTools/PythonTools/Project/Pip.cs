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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Project {
    static class Pip {
        private static readonly Regex PackageNameRegex = new Regex(
            "^(?<name>[a-z0-9_]+)(-.+)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] { 
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        // The relative path from PrefixPath, and true if it is a Python script
        // that needs to be run with the interpreter.
        private static readonly KeyValuePair<string, bool>[] PipLocations = new[] {
            new KeyValuePair<string, bool>(Path.Combine("Scripts", "pip-script.py"), true),
            new KeyValuePair<string, bool>("pip-script.py", true),
            new KeyValuePair<string, bool>(Path.Combine("Scripts", "pip.exe"), false),
            new KeyValuePair<string, bool>("pip.exe", false)
        };

        private static ProcessOutput Run(IPythonInterpreterFactory factory, Redirector output, bool elevate, params string[] cmd) {
            var args = cmd.AsEnumerable();
            bool isScript = false;
            string pipPath = null;
            foreach (var path in PipLocations) {
                pipPath = Path.Combine(factory.Configuration.PrefixPath, path.Key);
                isScript = path.Value;
                if (File.Exists(pipPath)) {
                    break;
                }
                pipPath = null;
            }

            if (string.IsNullOrEmpty(pipPath)) {
                args = new[] { "-m", "pip" }.Concat(args);
                isScript = true;
            } else if (isScript) {
                args = new[] { ProcessOutput.QuoteSingleArgument(pipPath) }.Concat(args);
                pipPath = factory.Configuration.InterpreterPath;
            }

            return ProcessOutput.Run(pipPath,
                args,
                factory.Configuration.PrefixPath,
                UnbufferedEnv,
                false,
                output,
                quoteArgs: false,
                elevate: elevate);
        }

        public static Task<HashSet<string>> Freeze(IPythonInterpreterFactory factory) {
            return Task.Factory.StartNew<HashSet<string>>((Func<HashSet<string>>)(() => {
                var lines = new HashSet<string>();
                using (var proc = Run(factory, null, false, "--version")) {
                    proc.Wait();
                    if (proc.ExitCode == 0) {
                        lines.UnionWith(proc.StandardOutputLines
                            .Select(line => Regex.Match(line, "pip (?<version>[0-9.]+)"))
                            .Where(match => match.Success && match.Groups["version"].Success)
                            .Select(match => "pip==" + match.Groups["version"].Value));
                    }
                }

                using (var proc = Run(factory, null, false, "freeze")) {
                    proc.Wait();
                    if (proc.ExitCode == 0) {
                        lines.UnionWith(proc.StandardOutputLines);
                        return lines;
                    }
                }

                // Pip failed, so clear out any entries that may have appeared
                lines.Clear();

                try {
                    var packagesPath = Path.Combine(factory.Configuration.LibraryPath, "site-packages");
                    lines.UnionWith(Directory.EnumerateDirectories(packagesPath)
                        .Select(name => Path.GetFileName(name))
                        .Select(name => PackageNameRegex.Match(name))
                        .Where(m => m.Success)
                        .Select(m => m.Groups["name"].Value));
                } catch {
                    lines.Clear();
                }

                return lines;
            }));
        }

        /// <summary>
        /// Returns true if installing a package will be secure.
        /// 
        /// This returns false for Python 2.5 and earlier because it does not
        /// include the required SSL support by default. No detection is done to
        /// determine whether the support has been added separately.
        /// </summary>
        public static bool IsSecureInstall(IPythonInterpreterFactory factory) {
            return factory.Configuration.Version > new Version(2, 5);
        }

        private static string GetInsecureArg(IPythonInterpreterFactory factory,
            Redirector output = null) {
            if (!IsSecureInstall(factory)) {
                // Python 2.5 does not include ssl, and so the --insecure
                // option is required to use pip.
                if (output != null) {
                    output.WriteErrorLine("Using '--insecure' option for Python 2.5.");
                }
                return "--insecure";
            }
            return null;
        }

        public static Task Install(IPythonInterpreterFactory factory,
            string package,
            bool elevate,
            Redirector output = null) {
            return Task.Factory.StartNew((Action)(() => {
                using (var proc = Run(factory, output, elevate, "install", GetInsecureArg(factory, output), package)) {
                    proc.Wait();
                }
            }));
        }

        public static Task<bool> Install(IPythonInterpreterFactory factory,
            string package,
            IServiceProvider site,
            bool elevate,
            Redirector output = null) {

            Task task;
            if (site != null && !ModulePath.GetModulesInLib(factory).Any(mp => mp.ModuleName == "pip")) {
                task = QueryInstallPip(factory, site, SR.GetString(SR.InstallPip), elevate, output);
            } else {
                var tcs = new TaskCompletionSource<object>();
                tcs.SetResult(null);
                task = tcs.Task;
            }
            return task.ContinueWith(t => {
                if (output != null) {
                    output.WriteLine(SR.GetString(SR.PackageInstalling, package));
                    if (PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForPackageInstallation) {
                        output.ShowAndActivate();
                    } else {
                        output.Show();
                    }
                }
                using (var proc = Run(factory, output, elevate, "install", GetInsecureArg(factory, output), package)) {
                    proc.Wait();

                    if (output != null) {
                        if (proc.ExitCode == 0) {
                            output.WriteLine(SR.GetString(SR.PackageInstallSucceeded, package));
                        } else {
                            output.WriteLine(SR.GetString(SR.PackageInstallFailedExitCode, package, proc.ExitCode ?? -1));
                        }
                        if (PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForPackageInstallation) {
                            output.ShowAndActivate();
                        } else {
                            output.Show();
                        }
                    }
                    return proc.ExitCode == 0;
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        public static Task<bool> Uninstall(IPythonInterpreterFactory factory, string package, bool elevate, Redirector output = null) {
            return Task.Factory.StartNew((Func<bool>)(() => {
                if (output != null) {
                    output.WriteLine(SR.GetString(SR.PackageUninstalling, package));
                    if (PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForPackageInstallation) {
                        output.ShowAndActivate();
                    } else {
                        output.Show();
                    }
                }
                using (var proc = Run(factory, output, elevate, "uninstall", "-y", package)) {
                    proc.Wait();

                    if (output != null) {
                        if (proc.ExitCode == 0) {
                            output.WriteLine(SR.GetString(SR.PackageUninstallSucceeded, package));
                        } else {
                            output.WriteLine(SR.GetString(SR.PackageUninstallFailedExitCode, package, proc.ExitCode ?? -1));
                        }
                        if (PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForPackageInstallation) {
                            output.ShowAndActivate();
                        } else {
                            output.Show();
                        }
                    }
                    return proc.ExitCode == 0;
                }
            }));
        }

        public static Task InstallPip(IPythonInterpreterFactory factory, bool elevate, Redirector output = null) {
            var pipDownloaderPath = Path.Combine(PythonToolsPackage.GetPythonToolsInstallPath(), "pip_downloader.py");

            return Task.Factory.StartNew((Action)(() => {
                if (output != null) {
                    output.WriteLine(SR.GetString(SR.PipInstalling));
                    if (PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForPackageInstallation) {
                        output.ShowAndActivate();
                    } else {
                        output.Show();
                    }
                }
                using (var proc = ProcessOutput.Run(factory.Configuration.InterpreterPath,
                    new [] { pipDownloaderPath },
                    factory.Configuration.PrefixPath,
                    null,
                    false,
                    output,
                    elevate: PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ElevatePip)
                ) {
                    proc.Wait();
                    if (output != null) {
                        if (proc.ExitCode == 0) {
                            output.WriteLine(SR.GetString(SR.PipInstallSucceeded));
                        } else {
                            output.WriteLine(SR.GetString(SR.PipInstallFailedExitCode, proc.ExitCode ?? -1));
                        }
                        if (PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForPackageInstallation) {
                            output.ShowAndActivate();
                        } else {
                            output.Show();
                        }
                    }
                }
            }));
        }

        public static Task QueryInstall(IPythonInterpreterFactory factory,
            string package,
            IServiceProvider site,
            string message,
            bool elevate,
            Redirector output = null) {
            if (Microsoft.VisualStudio.Shell.VsShellUtilities.ShowMessageBox(site,
                message,
                null,
                OLEMSGICON.OLEMSGICON_QUERY,
                OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST) == 2) {
                var tcs = new TaskCompletionSource<object>();
                tcs.SetCanceled();
                return tcs.Task;
            }

            return Install(factory, package, elevate, output);
        }

        public static Task QueryInstallPip(IPythonInterpreterFactory factory,
            IServiceProvider site,
            string message,
            bool elevate,
            Redirector output = null) {
            if (Microsoft.VisualStudio.Shell.VsShellUtilities.ShowMessageBox(site,
                message,
                null,
                OLEMSGICON.OLEMSGICON_QUERY,
                OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST) == 2) {
                var tcs = new TaskCompletionSource<object>();
                tcs.SetCanceled();
                return tcs.Task;
            }

            return InstallPip(factory, elevate, output);
        }
    }
}
