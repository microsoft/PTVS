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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools.Environments {
    static class VirtualEnv {
        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] {
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        /// <summary>
        /// Installs virtualenv. If pip is not installed, the returned task will
        /// succeed but error text will be passed to the redirector.
        /// </summary>
        public static Task<bool> Install(IServiceProvider provider, IPythonInterpreterFactory factory) {
            if (provider == null) {
                throw new ArgumentNullException(nameof(provider));
            }

            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }

            var ui = new VsPackageManagerUI(provider);
            var interpreterOpts = provider.GetComponentModel().GetService<IInterpreterOptionsService>();
            var pm = interpreterOpts?.GetPackageManagers(factory).FirstOrDefault(p => p.UniqueKey == "pip");
            if (factory.Configuration.Version < new Version(2, 5)) {
                ui.OnErrorTextReceived(null, "Python versions earlier than 2.5 are not supported by PTVS.\n");
                throw new OperationCanceledException();
            } else if (pm == null) {
                ui.OnErrorTextReceived(null, Strings.PackageManagementNotSupported_Package.FormatUI("virtualenv"));
                throw new OperationCanceledException();
            } else if (factory.Configuration.Version == new Version(2, 5)) {
                return pm.InstallAsync(PackageSpec.FromArguments("https://go.microsoft.com/fwlink/?LinkID=317970"), ui, CancellationToken.None);
            } else {
                return pm.InstallAsync(PackageSpec.FromArguments("https://go.microsoft.com/fwlink/?LinkID=317969"), ui, CancellationToken.None);
            }
        }

        public static async Task<IPythonInterpreterFactory> CreateAndAddFactory(
            IServiceProvider site,
            IInterpreterRegistryService registry,
            IInterpreterOptionsService options,
            PythonProjectNode project,
            IPythonWorkspaceContext workspace,
            string path,
            IPythonInterpreterFactory baseInterp,
            bool registerAsCustomEnv,
            string customEnvName,
            bool preferVEnv = false
        ) {
            if (site == null) {
                throw new ArgumentNullException(nameof(site));
            }

            if (registry == null) {
                throw new ArgumentNullException(nameof(registry));
            }

            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrEmpty(path)) {
                throw new ArgumentNullException(nameof(path));
            }

            if (baseInterp == null) {
                throw new ArgumentNullException(nameof(baseInterp));
            }

            if (preferVEnv) {
                await CreateWithVEnv(site, baseInterp, path);
            } else {
                await CreateWithVirtualEnv(site, baseInterp, path);
            }

            if (registerAsCustomEnv) {
                GetVirtualEnvConfig(path, baseInterp, out string interpExe, out string winterpExe, out string pathVar);

                var factory = await CustomEnv.CreateCustomEnv(
                    registry,
                    options,
                    path,
                    interpExe,
                    winterpExe,
                    pathVar,
                    baseInterp.Configuration.Architecture,
                    baseInterp.Configuration.Version,
                    customEnvName
                );

                if (factory != null) {
                    if (project != null) {
                        project.AddInterpreter(factory.Configuration.Id);
                    } else if (workspace != null) {
                        await workspace.SetInterpreterFactoryAsync(factory);
                    }
                }

                return factory;
            } else {
                if (project != null) {
                    return project.AddVirtualEnvironment(registry, path, baseInterp);
                } else if (workspace != null) {
                    // In workspaces, always store the path to the virtual env's python.exe
                    GetVirtualEnvConfig(path, baseInterp, out string interpExe, out string winterpExe, out string pathVar);

                    var workspaceFactoryProvider = site.GetComponentModel().GetService<WorkspaceInterpreterFactoryProvider>();
                    using (workspaceFactoryProvider?.SuppressDiscoverFactories(forceDiscoveryOnDispose: true)) {
                        var relativeInterpExe = PathUtils.GetRelativeFilePath(workspace.Location, interpExe);
                        await workspace.SetInterpreterAsync(relativeInterpExe);
                    }

                    var factory = workspaceFactoryProvider?
                        .GetInterpreterFactories()
                        .FirstOrDefault(f => PathUtils.IsSamePath(f.Configuration.InterpreterPath, interpExe));
                    return factory;
                } else {
                    return null;
                }
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

            var workspaceFactoryProvider = provider.GetComponentModel().GetService<WorkspaceInterpreterFactoryProvider>();
            using (workspaceFactoryProvider?.SuppressDiscoverFactories(forceDiscoveryOnDispose: true)) {
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
        }

        /// <summary>
        /// Creates a virtual environment. If virtualenv is not installed, the
        /// task will succeed but error text will be passed to the redirector.
        /// </summary>
        public static Task Create(IServiceProvider provider, IPythonInterpreterFactory factory, string path, Redirector output = null) {
            if (provider == null) {
                throw new ArgumentNullException(nameof(provider));
            }

            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }

            if (string.IsNullOrEmpty(path)) {
                throw new ArgumentNullException(nameof(path));
            }

            factory.ThrowIfNotRunnable(nameof(factory));
            return ContinueCreate(provider, factory, path, false, output);
        }

        /// <summary>
        /// Creates a virtual environment using venv. If venv is not available,
        /// the task will succeed but error text will be passed to the
        /// redirector.
        /// </summary>
        public static Task CreateWithVEnv(
            IServiceProvider provider,
            IPythonInterpreterFactory factory,
            string path
        ) {
            if (provider == null) {
                throw new ArgumentNullException(nameof(provider));
            }

            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }

            if (string.IsNullOrEmpty(path)) {
                throw new ArgumentNullException(nameof(path));
            }

            factory.ThrowIfNotRunnable();
            return ContinueCreate(provider, factory, path, true, OutputWindowRedirector.GetGeneral(provider));
        }

        /// <summary>
        /// Creates a virtual environment using virtualenv. If virtualenv or pip
        /// are not installed then they are downloaded and installed automatically.
        /// </summary>
        public static async Task CreateWithVirtualEnv(
            IServiceProvider provider,
            IPythonInterpreterFactory factory,
            string path
        ) {
            if (provider == null) {
                throw new ArgumentNullException(nameof(provider));
            }

            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }

            if (string.IsNullOrEmpty(path)) {
                throw new ArgumentNullException(nameof(path));
            }

            factory.ThrowIfNotRunnable(nameof(factory));

            var cancel = CancellationToken.None;
            var ui = new VsPackageManagerUI(provider);
            var interpreterOpts = provider.GetComponentModel().GetService<IInterpreterOptionsService>();
            var pm = interpreterOpts?.GetPackageManagers(factory).FirstOrDefault(p => p.UniqueKey == "pip");
            if (pm == null) {
                throw new InvalidOperationException(Strings.PackageManagementNotSupported);
            }
            if (!pm.IsReady) {
                await pm.PrepareAsync(ui, cancel);
                if (!pm.IsReady) {
                    throw new InvalidOperationException(Strings.VirtualEnvCreationFailed.FormatUI(path));
                }
            }

            bool hasVirtualEnv = (await factory.HasModuleAsync("venv", interpreterOpts)) ||
                (await factory.HasModuleAsync("virtualenv", interpreterOpts));

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
            IInterpreterRegistryService registry,
            IPythonInterpreterFactory baseInterpreter = null
        ) {
            if (string.IsNullOrEmpty(prefixPath)) {
                throw new ArgumentNullException(nameof(prefixPath));
            }

            var libPath = FindLibPath(prefixPath);

            if (baseInterpreter == null) {
                if (registry == null) {
                    throw new ArgumentNullException(nameof(registry));
                }

                baseInterpreter = FindBaseInterpreterFromVirtualEnv(
                    prefixPath,
                    libPath,
                    registry
                );

                if (baseInterpreter == null) {
                    return null;
                }
            }

            // The interpreter name should be the same as the base interpreter.
            GetVirtualEnvConfig(prefixPath, baseInterpreter, out string interpExe, out string winterpExe, out string pathVar);
            string description = PathUtils.GetFileOrDirectoryName(prefixPath);

            return new VisualStudioInterpreterConfiguration(
                id ?? baseInterpreter.Configuration.Id,
                baseInterpreter == null ? description : string.Format("{0} ({1})", description, baseInterpreter.Configuration.Description),
                prefixPath,
                interpExe,
                winterpExe,
                pathVar,
                baseInterpreter.Configuration.Architecture,
                baseInterpreter.Configuration.Version,
                InterpreterUIMode.CannotBeDefault | InterpreterUIMode.CannotBeConfigured
            );
        }

        private static IPythonInterpreterFactory FindBaseInterpreterFromVirtualEnv(
            string prefixPath,
            string libPath,
            IInterpreterRegistryService service
        ) {
            IPythonInterpreterFactory match = null;
            string basePath = PathUtils.TrimEndSeparator(GetOrigPrefixPath(prefixPath, libPath));

            if (Directory.Exists(basePath)) {
                match = service.Interpreters.FirstOrDefault(interp =>
                    PathUtils.IsSamePath(PathUtils.TrimEndSeparator(interp.Configuration.GetPrefixPath()), basePath));
            }

            // Special case, we may have the store installed interpreter. In this situation both
            // paths end with the same entry. Note, os.path.realpath can't seem to say these are the same anymore. 
            if (match == null && Directory.Exists(basePath) && basePath.Contains("PythonSoftwareFoundation.Python")) {
                var baseDir = Path.GetFileName(basePath);
                var baseEnd = baseDir.Substring(Math.Max(0, baseDir.LastIndexOf('_')));
                match = service.Interpreters.FirstOrDefault(interp => {
                    var interpDir = Path.GetFileName(PathUtils.TrimEndSeparator(interp.Configuration.GetPrefixPath()));
                    var interpEnd = interpDir.Substring(Math.Max(0, interpDir.LastIndexOf('_')));
                    return baseEnd == interpEnd;
                });
            }
            return match;
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

        private static void GetVirtualEnvConfig(
            string prefixPath,
            IPythonInterpreterFactory baseInterpreter,
            out string interpExe,
            out string winterpExe,
            out string pathVar
        ) {
            interpExe = Path.GetFileName(baseInterpreter.Configuration.InterpreterPath);
            winterpExe = Path.GetFileName(baseInterpreter.Configuration.GetWindowsInterpreterPath());
            var scripts = new[] { "Scripts", "bin" };

            // new versions of python >= 3.8 from the windows store have a different executable name (ie. python3.8.exe) in the 
            // registry InstallPath, compared to what is in the venv folder (python.exe) so search for both if interpExe is not found.
            interpExe = PathUtils.FindFile(prefixPath, interpExe, firstCheck: scripts) ?? PathUtils.FindFile(prefixPath, "python.exe", firstCheck: scripts);
            winterpExe = PathUtils.FindFile(prefixPath, winterpExe, firstCheck: scripts) ?? PathUtils.FindFile(prefixPath, "pythonw.exe", firstCheck: scripts);
            pathVar = baseInterpreter.Configuration.PathEnvironmentVariable;
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

        internal static bool IsPythonVirtualEnv(string prefixPath) {
            if (string.IsNullOrEmpty(prefixPath)) {
                return false;
            }

            string libPath = FindLibPath(prefixPath);
            if (libPath == null) {
                return false;
            }

            //pyenv.cfg detects python 3 and orig-prefix.txt detects python 2
            return (File.Exists(Path.Combine(prefixPath, "pyvenv.cfg")) || File.Exists(Path.Combine(libPath, "orig-prefix.txt")));
        }
    }
}
