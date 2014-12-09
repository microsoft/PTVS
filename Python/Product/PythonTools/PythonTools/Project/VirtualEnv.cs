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
using System.Reflection;
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
        public static Task<bool> Install(IServiceProvider provider, IPythonInterpreterFactory factory, Redirector output = null) {
            bool elevate = provider.GetPythonToolsService().GeneralOptions.ElevatePip;
            if (factory.Configuration.Version < new Version(2, 5)) {
                if (output != null) {
                    output.WriteErrorLine("Python versions earlier than 2.5 are not supported by PTVS.");
                }
                throw new OperationCanceledException();
            } else if (factory.Configuration.Version == new Version(2, 5)) {
                return Pip.Install(provider, factory, "https://go.microsoft.com/fwlink/?LinkID=317970", elevate, output);
            } else {
                return Pip.Install(provider, factory, "https://go.microsoft.com/fwlink/?LinkID=317969", elevate, output);
            }
        }

        private static async Task ContinueCreate(IServiceProvider provider, IPythonInterpreterFactory factory, string path, bool useVEnv, Redirector output) {
            path = CommonUtils.TrimEndSeparator(path);
            var name = Path.GetFileName(path);
            var dir = Path.GetDirectoryName(path);

            if (output != null) {
                output.WriteLine(SR.GetString(SR.VirtualEnvCreating, path));
                if (provider.GetPythonToolsService().GeneralOptions.ShowOutputWindowForVirtualEnvCreate) {
                    output.ShowAndActivate();
                } else {
                    output.Show();
                }
            }

            // Ensure the target directory exists.
            Directory.CreateDirectory(dir);

            using (var proc = ProcessOutput.Run(
                factory.Configuration.InterpreterPath,
                new[] { "-m", useVEnv ? "venv" : "virtualenv", name },
                dir,
                UnbufferedEnv,
                false,
                output
            )) {
                var exitCode = await proc;

                if (output != null) {
                    if (exitCode == 0) {
                        output.WriteLine(SR.GetString(SR.VirtualEnvCreationSucceeded, path));
                    } else {
                        output.WriteLine(SR.GetString(SR.VirtualEnvCreationFailedExitCode, path, exitCode));
                    }
                    if (provider.GetPythonToolsService().GeneralOptions.ShowOutputWindowForVirtualEnvCreate) {
                        output.ShowAndActivate();
                    } else {
                        output.Show();
                    }
                }

                if (exitCode != 0 || !Directory.Exists(path)) {
                    throw new InvalidOperationException(SR.GetString(SR.VirtualEnvCreationFailed, path));
                }
            }
        }

        /// <summary>
        /// Creates a virtual environment. If virtualenv is not installed, the
        /// task will succeed but error text will be passed to the redirector.
        /// </summary>
        public static Task Create(IServiceProvider provider, IPythonInterpreterFactory factory, string path, Redirector output = null) {
            factory.ThrowIfNotRunnable();
            return ContinueCreate(provider, factory, path, false, output);
        }

        /// <summary>
        /// Creates a virtual environment using venv. If venv is not available,
        /// the task will succeed but error text will be passed to the
        /// redirector.
        /// </summary>
        public static Task CreateWithVEnv(IServiceProvider provider, IPythonInterpreterFactory factory, string path, Redirector output = null) {
            factory.ThrowIfNotRunnable();
            return ContinueCreate(provider, factory, path, true, output);
        }

        internal static Task<HashSet<string>> FindPipAndVirtualEnv(IPythonInterpreterFactory factory) {
            return Task.Run(() => factory.FindModules("pip", "virtualenv", "venv"));
        }

        /// <summary>
        /// Creates a virtual environment. If virtualenv or pip are not
        /// installed then they are downloaded and installed automatically.
        /// </summary>
        public static async Task CreateAndInstallDependencies(
            IServiceProvider provider,
            IPythonInterpreterFactory factory,
            string path,
            Redirector output = null
        ) {
            factory.ThrowIfNotRunnable("factory");

            var modules = await FindPipAndVirtualEnv(factory);
            bool hasPip = modules.Contains("pip");
            bool hasVirtualEnv = modules.Contains("virtualenv") || modules.Contains("venv");

            if (!hasVirtualEnv) {
                if (!hasPip) {
                    bool elevate = provider.GetPythonToolsService().GeneralOptions.ElevatePip;
                    await Pip.InstallPip(provider, factory, elevate, output);
                }
                if (!await Install(provider, factory, output)) {
                    throw new InvalidOperationException(SR.GetString(SR.VirtualEnvCreationFailed, path));
                }
            }

            await ContinueCreate(provider, factory, path, false, output);
        }

        private static IPythonInterpreterFactory FindBaseInterpreterFromVirtualEnv(
            string prefixPath,
            string libPath,
            IInterpreterOptionsService service
        ) {
            string basePath = GetOrigPrefixPath(prefixPath, libPath);

            if (Directory.Exists(basePath)) {
                return service.Interpreters.FirstOrDefault(interp =>
                    CommonUtils.IsSamePath(interp.Configuration.PrefixPath, basePath)
                );
            }
            return null;
        }

        internal static string GetOrigPrefixPath(string prefixPath, string libPath = null) {
            string basePath = null;

            if (!Directory.Exists(prefixPath)) {
                return null;
            }

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

            if (string.IsNullOrEmpty(libPath)) {
                libPath = FindLibPath(prefixPath);
            }

            if (!Directory.Exists(libPath)) {
                return null;
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
            return basePath;
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

        internal static string FindLibPath(string prefixPath) {
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
            return libPath;
        }

        public static InterpreterFactoryCreationOptions FindInterpreterOptions(
            string prefixPath,
            IInterpreterOptionsService service,
            IPythonInterpreterFactory baseInterpreter = null
        ) {
            var result = new InterpreterFactoryCreationOptions();

            var libPath = FindLibPath(prefixPath);

            result.PrefixPath = prefixPath;
            result.LibraryPath = libPath;

            if (baseInterpreter == null) {
                baseInterpreter = FindBaseInterpreterFromVirtualEnv(prefixPath, libPath, service);
            }

            string interpExe, winterpExe;

            if (baseInterpreter != null) {
                // The interpreter name should be the same as the base interpreter.
                interpExe = Path.GetFileName(baseInterpreter.Configuration.InterpreterPath);
                winterpExe = Path.GetFileName(baseInterpreter.Configuration.WindowsInterpreterPath);
                result.InterpreterPath = FindFile(prefixPath, interpExe);
                result.WindowInterpreterPath = FindFile(prefixPath, winterpExe);
                result.PathEnvironmentVariableName = baseInterpreter.Configuration.PathEnvironmentVariable;
            } else {
                result.InterpreterPath = string.Empty;
                result.WindowInterpreterPath = string.Empty;
                result.PathEnvironmentVariableName = string.Empty;
            }

            if (baseInterpreter != null) {
                result.Description = string.Format(
                    "{0} ({1})",
                    CommonUtils.GetFileOrDirectoryName(prefixPath),
                    baseInterpreter.Description
                );

                result.Id = baseInterpreter.Id;
                result.LanguageVersion = baseInterpreter.Configuration.Version;
                result.Architecture = baseInterpreter.Configuration.Architecture;
                result.WatchLibraryForNewModules = true;
            } else {
                result.Description = CommonUtils.GetFileOrDirectoryName(prefixPath);

                result.Id = Guid.Empty;
                result.LanguageVersion = new Version(0, 0);
                result.Architecture = ProcessorArchitecture.None;
                result.WatchLibraryForNewModules = false;
            }

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
