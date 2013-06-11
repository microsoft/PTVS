using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell.Interop;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Microsoft.PythonTools.Project {
    static class Pip {
        private static readonly Regex PackageNameRegex = new Regex(
            "^(?<name>[a-z0-9_]+)(-.+)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] { 
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        private static ProcessOutput Run(IPythonInterpreterFactory factory, Redirector output, params string[] cmd) {
            string pipPath = Path.Combine(factory.Configuration.PrefixPath, "Scripts", "pip.exe");
            if (!File.Exists(pipPath)) {
                pipPath = Path.Combine(factory.Configuration.PrefixPath, "pip.exe");
                if (!File.Exists(pipPath)) {
                    pipPath = null;
                }
            }

            if (!string.IsNullOrEmpty(pipPath)) {
                return ProcessOutput.Run(pipPath, cmd, null, UnbufferedEnv, false, output);
            } else {
                return ProcessOutput.Run(factory.Configuration.InterpreterPath,
                    new[] { "-m", "pip" }.Concat(cmd),
                    null,
                    UnbufferedEnv,
                    false,
                    output);
            }
        }

        private static ProcessOutput Run(IPythonInterpreterFactory factory, params string[] cmd) {
            return Run(factory, null, cmd);
        }

        public static Task<HashSet<string>> Freeze(IPythonInterpreterFactory factory) {
            return Task.Factory.StartNew<HashSet<string>>((Func<HashSet<string>>)(() => {
                using (var proc = Run(factory, "freeze")) {
                    proc.Wait();
                    if (proc.ExitCode == 0) {
                        return new HashSet<string>(proc.StandardOutputLines);
                    }
                }

                try {
                    var packagesPath = Path.Combine(factory.Configuration.LibraryPath, "site-packages");
                    return new HashSet<string>(Directory.EnumerateDirectories(packagesPath)
                        .Select(name => Path.GetFileName(name))
                        .Select(name => PackageNameRegex.Match(name))
                        .Where(m => m.Success)
                        .Select(m => m.Groups["name"].Value));
                } catch (ArgumentException) {
                } catch (IOException) {
                }

                return new HashSet<string>();
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
            if (site != null && !ModulePath.GetModulesInLib(factory).Any(mp => mp.ModuleName == "pip")) {
                task = QueryInstallPip(factory, site, SR.GetString(SR.InstallPip), output);
            } else {
                var tcs = new TaskCompletionSource<object>();
                tcs.SetResult(null);
                task = tcs.Task;
            }
            return task.ContinueWith(t => {
                using (var proc = Run(factory, output, "install", package)) {
                    proc.Wait();
                    return proc.ExitCode == 0;
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        public static Task<bool> Uninstall(IPythonInterpreterFactory factory, string package, Redirector output = null) {
            return Task.Factory.StartNew((Func<bool>)(() => {
                using (var proc = Run(factory, output, "uninstall", "-y", package)) {
                    proc.Wait();
                    return proc.ExitCode == 0;
                }
            }));
        }

        public static Task InstallPip(IPythonInterpreterFactory factory, Redirector output = null) {
            var pipDownloaderPath = Path.Combine(PythonToolsPackage.GetPythonToolsInstallPath(), "pip_downloader.py");

            // TODO: Handle elevation
            return Task.Factory.StartNew((Action)(() => {
                using (var proc = ProcessOutput.Run(factory.Configuration.InterpreterPath,
                    new [] { pipDownloaderPath },
                    factory.Configuration.PrefixPath,
                    null,
                    false,
                    output)
                ) {
                    proc.Wait();
                }
            }));
        }

        public static Task QueryInstall(IPythonInterpreterFactory factory,
            string package,
            IServiceProvider site,
            string message,
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

            return Install(factory, package, output);
        }

        public static Task QueryInstallPip(IPythonInterpreterFactory factory,
            IServiceProvider site,
            string message,
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

            return InstallPip(factory, output);
        }
    }
}
