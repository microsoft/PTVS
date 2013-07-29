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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    static class VirtualEnv {
        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] { 
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        /// <summary>
        /// Installs virtualenv. If pip is not installed, the returned task will
        /// succeed but error text will be passed to the redirector.
        /// </summary>
        public static Task Install(IPythonInterpreterFactory factory, Redirector output = null) {
            bool elevate = PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ElevatePip;
            return Pip.Install(factory, "virtualenv==1.9.1", elevate, output);
        }

        private static Task ContinueCreate(Task task, IPythonInterpreterFactory factory, string path, Redirector output) {
            return task.ContinueWith(t => {
                path = CommonUtils.TrimEndSeparator(path);
                var name = Path.GetFileName(path);
                var dir = Path.GetDirectoryName(path);
                int? exitCode = null;

                if (output != null) {
                    output.WriteLine(SR.GetString(SR.VirtualEnvCreating, path));
                    if (PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForVirtualEnvCreate) {
                        output.ShowAndActivate();
                    } else {
                        output.Show();
                    }
                }
                using (var proc = ProcessOutput.Run(factory.Configuration.InterpreterPath,
                    new[] { "-m", "virtualenv", "--distribute", name },
                    dir,
                    UnbufferedEnv,
                    false,
                    output)) {
                    proc.Wait();
                    exitCode = proc.ExitCode;

                    if (output != null) {
                        if (exitCode == 0) {
                            output.WriteLine(SR.GetString(SR.VirtualEnvCreationSucceeded, path));
                        } else {
                            output.WriteLine(SR.GetString(SR.VirtualEnvCreationFailedExitCode, path, exitCode ?? -1));
                        }
                        if (PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForVirtualEnvCreate) {
                            output.ShowAndActivate();
                        } else {
                            output.Show();
                        }
                    }
                }

                if (exitCode != 0 || !Directory.Exists(path)) {
                    throw new InvalidOperationException(SR.GetString(SR.VirtualEnvCreationFailed, path));
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        /// <summary>
        /// Creates a virtual environment. If virtualenv is not installed, the
        /// task will succeed but error text will be passed to the redirector.
        /// </summary>
        public static Task Create(IPythonInterpreterFactory factory, string path, Redirector output = null) {
            var tcs = new TaskCompletionSource<object>();
            tcs.SetResult(null);
            return ContinueCreate(tcs.Task, factory, path, output);
        }

        private static Task<Tuple<bool, bool>> FindPipAndVirtualEnv(IPythonInterpreterFactory factory) {
            return Task.Factory.StartNew((Func<Tuple<bool, bool>>)(() => {
                bool hasPip = false, hasVirtualEnv = false;
                foreach (var mp in ModulePath.GetModulesInLib(factory)) {
                    if (!hasPip && mp.ModuleName == "pip") {
                        hasPip = true;
                    }
                    if (!hasVirtualEnv && mp.ModuleName == "virtualenv") {
                        hasVirtualEnv = true;
                    }
                    if (hasPip && hasVirtualEnv) {
                        break;
                    }
                }
                return Tuple.Create(hasPip, hasVirtualEnv);
            }));
        }

        /// <summary>
        /// Creates a virtual environment. If virtualenv or pip are not
        /// installed then they are downloaded and installed automatically.
        /// </summary>
        public static Task CreateAndInstallDependencies(
            IPythonInterpreterFactory factory,
            string path, Redirector output = null) {
            Utilities.ArgumentNotNull("factory", factory);

            var task = FindPipAndVirtualEnv(factory).ContinueWith((Action<Task<Tuple<bool, bool>>>)(t => {
                bool hasPip = t.Result.Item1;
                bool hasVirtualEnv = t.Result.Item2;

                if (!hasVirtualEnv) {
                    if (!hasPip) {
                        bool elevate = PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ElevatePip;
                        Pip.InstallPip(factory, elevate, output).Wait();
                    }
                    Install(factory, output).Wait();
                }
            }));

            return ContinueCreate(task, factory, path, output);
        }

        /// <summary>
        /// Creates a virtual environment. If virtualenv or pip are not
        /// installed then the user is asked whether to install them. If the
        /// user refuses, the returned task will be cancelled.
        /// </summary>
        public static Task QueryCreateAndInstallDependencies(
            IPythonInterpreterFactory factory,
            IServiceProvider site,
            string path,
            Redirector output = null) {

            Utilities.ArgumentNotNull("factory", factory);
            Utilities.ArgumentNotNull("site", site);

            var cts = new CancellationTokenSource();

            var task = FindPipAndVirtualEnv(factory).ContinueWith((Func<Task<Tuple<bool, bool>>, Tuple<bool, bool>>)(t => {
                bool hasPip = t.Result.Item1;
                bool hasVirtualEnv = t.Result.Item2;

                string message;
                if (!hasVirtualEnv) {
                    if (!hasPip) {
                        message = SR.GetString(SR.InstallVirtualEnvAndPip);
                    } else {
                        message = SR.GetString(SR.InstallVirtualEnv);
                    }
                    if (Microsoft.VisualStudio.Shell.VsShellUtilities.ShowMessageBox(site,
                        message,
                        null,
                        OLEMSGICON.OLEMSGICON_QUERY,
                        OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST) == 2) {
                        cts.Cancel();
                    }
                }
                return t.Result;
            }), cts.Token,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.FromCurrentSynchronizationContext()
            ).ContinueWith((Action<Task<Tuple<bool, bool>>>)(t => {
                if (t.IsCanceled) {
                    return;
                }
                
                bool hasPip = t.Result.Item1;
                bool hasVirtualEnv = t.Result.Item2;

                if (!hasVirtualEnv) {
                    if (!hasPip) {
                        bool elevate = PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ElevatePip;
                        Pip.InstallPip(factory, elevate, output).Wait();
                    }
                    Install(factory, output).Wait();
                }
            }));

            return ContinueCreate(task, factory, path, output);
        }

        private static IPythonInterpreterFactory FindBaseInterpreterFromVirtualEnv(
            string libPath,
            IInterpreterOptionsService service) {
            string prefixFile = Path.Combine(libPath, "orig-prefix.txt");
            if (File.Exists(prefixFile)) {
                try {
                    var lines = File.ReadAllLines(prefixFile);
                    if (lines.Length >= 1 && CommonUtils.IsValidPath(lines[0])) {
                        return service.Interpreters.FirstOrDefault(interp =>
                            CommonUtils.IsSamePath(interp.Configuration.PrefixPath, lines[0])
                        );
                    }
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                } catch (System.Security.SecurityException) {
                }
            }
            return null;
        }

        private static string FindFile(string root, string file, int depthLimit = 2) {
            var candidate = Path.Combine(root, file);
            if (File.Exists(candidate)) {
                return candidate;
            }
            candidate = Path.Combine(root, "Scripts", file);
            if (File.Exists(candidate)) {
                return candidate;
            }

            // Do a BFS of the filesystem to ensure we find the match closest to
            // the root directory.
            var dirQueue = new Queue<string>();
            dirQueue.Enqueue(root);
            dirQueue.Enqueue("<EOD>");
            while (dirQueue.Any()) {
                var dir = dirQueue.Dequeue();
                if (dir == "<EOD>") {
                    depthLimit -= 1;
                    if (depthLimit <= 0) {
                        return null;
                    }
                    continue;
                }
                var result = Directory.EnumerateFiles(dir, file, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (result != null) {
                    return result;
                }
                foreach (var subDir in Directory.EnumerateDirectories(dir)) {
                    dirQueue.Enqueue(subDir);
                }
                dirQueue.Enqueue("<EOD>");
            }
            return null;
        }


        public static InterpreterFactoryCreationOptions FindInterpreterOptions(
            string prefixPath,
            IInterpreterOptionsService service,
            IPythonInterpreterFactory baseInterpreter = null) {

            var result = new InterpreterFactoryCreationOptions();

            // Find site.py to find the library
            var libPath = FindFile(prefixPath, "site.py");
            if (!File.Exists(libPath)) {
                return null;
            }
            libPath = Path.GetDirectoryName(libPath);


            if (baseInterpreter == null) {
                baseInterpreter = FindBaseInterpreterFromVirtualEnv(libPath, service);
                if (baseInterpreter == null) {
                    return null;
                }
            }

            // The interpreter name should be the same as the base interpreter.
            var interpExe = Path.GetFileName(baseInterpreter.Configuration.InterpreterPath);
            result.InterpreterPath = FindFile(prefixPath, interpExe);
            interpExe = Path.GetFileName(baseInterpreter.Configuration.WindowsInterpreterPath);
            result.WindowInterpreterPath = FindFile(prefixPath, interpExe);

            result.PrefixPath = prefixPath;
            result.LibraryPath = libPath;
            result.Description = Path.GetFileName(CommonUtils.TrimEndSeparator(prefixPath));

            result.Id = baseInterpreter.Id;
            result.LanguageVersion = baseInterpreter.Configuration.Version;
            result.Architecture = baseInterpreter.Configuration.Architecture;
            result.PathEnvironmentVariableName = baseInterpreter.Configuration.PathEnvironmentVariable;
            result.WatchLibraryForNewModules = true;

            return result;
        }

        // This helper function is not yet needed, but may be useful at some point.

        //public static string FindLibPathFromInterpreter(string interpreterPath) {
        //    using (var output = ProcessOutput.RunHiddenAndCapture(interpreterPath, "-c", "import site; print(site.__file__)")) {
        //        output.Wait();
        //        return output.StandardOutputLines
        //            .Where(CommonUtils.IsValidPath)
        //            .Select(line => Path.GetDirectoryName(line))
        //            .LastOrDefault(dir => Directory.Exists(dir));
        //    }
        //}
    }
}
