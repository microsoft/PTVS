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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Project {
    static class VirtualEnv {
        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] { 
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        /// <summary>
        /// Installs virtualenv. If pip is not installed, the returned task will
        /// succeed but error text will be passed to the redirector.
        /// </summary>
        public static Task<bool> Install(IServiceProvider provider, IPythonInterpreterFactory factory) {
            var ui = new VsPackageManagerUI(provider);
            if (factory.Configuration.Version < new Version(2, 5)) {
                ui.OnErrorTextReceived(null, "Python versions earlier than 2.5 are not supported by PTVS.\n");
                throw new OperationCanceledException();
            } else if (factory.PackageManager == null) {
                ui.OnErrorTextReceived(null, Strings.PackageManagementNotSupported_Package.FormatUI("virtualenv"));
                throw new OperationCanceledException();
            } else if (factory.Configuration.Version == new Version(2, 5)) {
                return factory.PackageManager.InstallAsync(PackageSpec.FromArguments("https://go.microsoft.com/fwlink/?LinkID=317970"), ui, CancellationToken.None);
            } else {
                return factory.PackageManager.InstallAsync(PackageSpec.FromArguments("https://go.microsoft.com/fwlink/?LinkID=317969"), ui, CancellationToken.None);
            }
        }

        private static async Task ContinueCreate(IServiceProvider provider, IPythonInterpreterFactory factory, string path, bool useVEnv, Redirector output) {
            path = PathUtils.TrimEndSeparator(path);
            var name = Path.GetFileName(path);
            var dir = Path.GetDirectoryName(path);

            if (output != null) {
                output.WriteLine(Strings.VirtualEnvCreating.FormatUI(path));
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
                        output.WriteLine(Strings.VirtualEnvCreationSucceeded.FormatUI(path));
                    } else {
                        output.WriteLine(Strings.VirtualEnvCreationFailedExitCode.FormatUI(path, exitCode));
                    }
                    if (provider.GetPythonToolsService().GeneralOptions.ShowOutputWindowForVirtualEnvCreate) {
                        output.ShowAndActivate();
                    } else {
                        output.Show();
                    }
                }

                if (exitCode != 0 || !Directory.Exists(path)) {
                    throw new InvalidOperationException(Strings.VirtualEnvCreationFailed.FormatUI(path));
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
        public static Task CreateWithVEnv(IServiceProvider provider, IPythonInterpreterFactory factory, string path) {
            factory.ThrowIfNotRunnable();
            return ContinueCreate(provider, factory, path, true, OutputWindowRedirector.GetGeneral(provider));
        }

        /// <summary>
        /// Creates a virtual environment. If virtualenv or pip are not
        /// installed then they are downloaded and installed automatically.
        /// </summary>
        public static async Task CreateAndInstallDependencies(
            IServiceProvider provider,
            IPythonInterpreterFactory factory,
            string path
        ) {
            factory.ThrowIfNotRunnable("factory");

            var cancel = CancellationToken.None;
            var ui = new VsPackageManagerUI(provider);
            var pm = factory.PackageManager;
            if (pm == null) {
                throw new InvalidOperationException(Strings.PackageManagementNotSupported);
            }
            if (!pm.IsReady) {
                await pm.PrepareAsync(ui, cancel);
                if (!pm.IsReady) {
                    throw new InvalidOperationException(Strings.VirtualEnvCreationFailed.FormatUI(path));
                }
            }

            var modules = await factory.FindModulesAsync("virtualenv", "venv");
            bool hasVirtualEnv = modules.Contains("virtualenv") || modules.Contains("venv");

            if (!hasVirtualEnv) {
                if (!await Install(provider, factory)) {
                    throw new InvalidOperationException(Strings.VirtualEnvCreationFailed.FormatUI(path));
                }
            }

            await ContinueCreate(provider, factory, path, false, PackageManagerUIRedirector.Get(pm, ui));
        }

        public static InterpreterConfiguration FindInterpreterConfiguration(
            string id,
            string prefixPath,
            IInterpreterRegistryService service,
            IPythonInterpreterFactory baseInterpreter = null
        ) {

            var libPath = FindLibPath(prefixPath);

            if (baseInterpreter == null) {
                baseInterpreter = FindBaseInterpreterFromVirtualEnv(
                    prefixPath,
                    libPath,
                    service
                );

                if (baseInterpreter == null) {
                    return null;
                }
            }

            // The interpreter name should be the same as the base interpreter.
            string interpExe = Path.GetFileName(baseInterpreter.Configuration.InterpreterPath);
            string winterpExe = Path.GetFileName(baseInterpreter.Configuration.WindowsInterpreterPath);
            var scripts = new[] { "Scripts", "bin" };
            interpExe = PathUtils.FindFile(prefixPath, interpExe, firstCheck: scripts);
            winterpExe = PathUtils.FindFile(prefixPath, winterpExe, firstCheck: scripts);
            string pathVar = baseInterpreter.Configuration.PathEnvironmentVariable;
            string description = PathUtils.GetFileOrDirectoryName(prefixPath);

            return new InterpreterConfiguration(
                id ?? baseInterpreter.Configuration.Id,
                baseInterpreter == null ? description : string.Format("{0} ({1})", description, baseInterpreter.Configuration.Description),
                prefixPath,
                interpExe,
                winterpExe,
                pathVar,
                baseInterpreter.Configuration.Architecture,
                baseInterpreter.Configuration.Version,
                InterpreterUIMode.CannotBeDefault | InterpreterUIMode.CannotBeConfigured | InterpreterUIMode.SupportsDatabase
            );
        }
        public static IPythonInterpreterFactory FindBaseInterpreterFromVirtualEnv(
            string prefixPath,
            string libPath,
            IInterpreterRegistryService service
        ) {
            string basePath = PathUtils.TrimEndSeparator(GetOrigPrefixPath(prefixPath, libPath));

            if (Directory.Exists(basePath)) {
                return service.Interpreters.FirstOrDefault(interp =>
                    PathUtils.IsSamePath(PathUtils.TrimEndSeparator(interp.Configuration.PrefixPath), basePath)
                );
            }
            return null;
        }

        public static string GetOrigPrefixPath(string prefixPath, string libPath = null) {
            string basePath = null;

            if (!Directory.Exists(prefixPath)) {
                return null;
            }

            var cfgFile = Path.Combine(prefixPath, "pyvenv.cfg");
            if (File.Exists(cfgFile)) {
                try {
                    var lines = File.ReadAllLines(cfgFile);
                    basePath = lines
                        .Select(line => Regex.Match(line, @"^home\s*=\s*(?<path>.+)$", RegexOptions.IgnoreCase))
                        .Where(m => m != null && m.Success)
                        .Select(m => m.Groups["path"])
                        .Where(g => g != null && g.Success)
                        .Select(g => g.Value)
                        .FirstOrDefault(PathUtils.IsValidPath);
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
                    basePath = lines.FirstOrDefault(PathUtils.IsValidPath);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                } catch (System.Security.SecurityException) {
                }
            }
            return basePath;
        }

        private static string FindLibPath(string prefixPath) {
            // Find site.py to find the library
            var libPath = PathUtils.FindFile(prefixPath, "site.py", depthLimit: 1, firstCheck: new[] { "Lib" });
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
    }
}
