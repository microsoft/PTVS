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

            var modules = await factory.FindModulesAsync("pip", "virtualenv", "venv");
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

        public static InterpreterFactoryCreationOptions FindInterpreterOptions(
            string prefixPath,
            IInterpreterOptionsService service,
            IPythonInterpreterFactory baseInterpreter = null
        ) {
            var result = new InterpreterFactoryCreationOptions();

            var libPath = DerivedInterpreterFactory.FindLibPath(prefixPath);

            result.PrefixPath = prefixPath;
            result.LibraryPath = libPath;

            if (baseInterpreter == null) {
                baseInterpreter = DerivedInterpreterFactory.FindBaseInterpreterFromVirtualEnv(
                    prefixPath,
                    libPath,
                    service
                );
            }

            string interpExe, winterpExe;

            if (baseInterpreter != null) {
                // The interpreter name should be the same as the base interpreter.
                interpExe = Path.GetFileName(baseInterpreter.Configuration.InterpreterPath);
                winterpExe = Path.GetFileName(baseInterpreter.Configuration.WindowsInterpreterPath);
                var scripts = new[] { "Scripts", "bin" };
                result.InterpreterPath = CommonUtils.FindFile(prefixPath, interpExe, firstCheck: scripts);
                result.WindowInterpreterPath = CommonUtils.FindFile(prefixPath, winterpExe, firstCheck: scripts);
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
