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
using Microsoft.PythonTools.Interpreter;
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
            if (factory.Configuration.Version < new Version(2, 5)) {
                if (output != null) {
                    output.WriteErrorLine("Python versions earlier than 2.5 are not supported by PTVS.");
                }
                var tcs = new TaskCompletionSource<object>();
                tcs.SetCanceled();
                return tcs.Task;
            } else if (factory.Configuration.Version == new Version(2, 5)) {
                return Pip.Install(factory, "https://go.microsoft.com/fwlink/?LinkID=317970", elevate, output);
            } else {
                return Pip.Install(factory, "https://go.microsoft.com/fwlink/?LinkID=317969", elevate, output);
            }
        }

        private static Task ContinueCreate(Task task, IPythonInterpreterFactory factory, string path, bool useVEnv, Redirector output) {
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
                    new[] { "-m", useVEnv ? "venv" : "virtualenv", name },
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
            return ContinueCreate(tcs.Task, factory, path, false, output);
        }

        /// <summary>
        /// Creates a virtual environment using venv. If venv is not available,
        /// the task will succeed but error text will be passed to the
        /// redirector.
        /// </summary>
        public static Task CreateWithVEnv(IPythonInterpreterFactory factory, string path, Redirector output = null) {
            var tcs = new TaskCompletionSource<object>();
            tcs.SetResult(null);
            return ContinueCreate(tcs.Task, factory, path, true, output);
        }

        internal static Task<HashSet<string>> FindPipAndVirtualEnv(IPythonInterpreterFactory factory) {
            return Task.Factory.StartNew((Func<HashSet<string>>)(
                () => factory.FindModules("pip", "virtualenv", "venv")
            ));
        }

        /// <summary>
        /// Creates a virtual environment. If virtualenv or pip are not
        /// installed then they are downloaded and installed automatically.
        /// </summary>
        public static Task CreateAndInstallDependencies(
            IPythonInterpreterFactory factory,
            string path, Redirector output = null) {
            Utilities.ArgumentNotNull("factory", factory);

            var task = FindPipAndVirtualEnv(factory).ContinueWith(t => {
                bool hasPip = t.Result.Contains("pip");
                bool hasVirtualEnv = t.Result.Contains("virtualenv") || t.Result.Contains("venv");

                if (!hasVirtualEnv) {
                    if (!hasPip) {
                        bool elevate = PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ElevatePip;
                        Pip.InstallPip(factory, elevate, output).Wait();
                    }
                    Install(factory, output).Wait();
                }
            });

            return ContinueCreate(task, factory, path, false, output);
        }

        private static IPythonInterpreterFactory FindBaseInterpreterFromVirtualEnv(
            string prefixPath,
            string libPath,
            IInterpreterOptionsService service) {
            string basePath = null;
            
            var cfgFile = Path.Combine(prefixPath, "pyvenv.cfg");
            if (File.Exists(cfgFile)) {
                try {
                    var lines = File.ReadAllLines(cfgFile);
                    basePath = lines
                        .Select(line => Regex.Match(line, "^home *= *(?<path>.+)$", RegexOptions.IgnoreCase))
                        .Where(m => m != null && m.Success)
                        .Select(m => m.Groups["path"])
                        .Where(g => g != null && g.Success)
                        .Select(g => g.Value)
                        .FirstOrDefault(CommonUtils.IsValidPath);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                } catch (System.Security.SecurityException) {
                }
            }

            var prefixFile = Path.Combine(libPath, "orig-prefix.txt");
            if (basePath == null && File.Exists(prefixFile)) {
                try {
                    var lines = File.ReadAllLines(prefixFile);
                    basePath = lines.FirstOrDefault(CommonUtils.IsValidPath);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                } catch (System.Security.SecurityException) {
                }
            }

            if (Directory.Exists(basePath)) {
                return service.Interpreters.FirstOrDefault(interp =>
                    CommonUtils.IsSamePath(interp.Configuration.PrefixPath, basePath)
                );
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
                // Python 3.3 venv does not add site.py, but always puts the
                // library in prefixPath\Lib
                libPath = Path.Combine(prefixPath, "Lib");
                if (!Directory.Exists(libPath)) {
                    return null;
                }
            } else {
                libPath = Path.GetDirectoryName(libPath);
            }


            if (baseInterpreter == null) {
                baseInterpreter = FindBaseInterpreterFromVirtualEnv(prefixPath, libPath, service);
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
