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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools.Project;

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
            factory.ThrowIfNotRunnable("factory");

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

        private static async Task<int> ContinueRun(
            IPythonInterpreterFactory factory,
            Redirector output,
            bool elevate,
            params string[] cmd
        ) {
            bool isScript;
            var easyInstallPath = GetEasyInstallPath(factory, out isScript);
            if (easyInstallPath == null) {
                throw new FileNotFoundException("Cannot find setuptools ('easy_install.exe')");
            }

            var args = cmd.ToList();
            args.Insert(0, "--always-copy");
            args.Insert(0, "--always-unzip");
            if (isScript) {
                args.Insert(0, ProcessOutput.QuoteSingleArgument(easyInstallPath));
                easyInstallPath = factory.Configuration.InterpreterPath;
            }
            using (var proc = ProcessOutput.Run(
                easyInstallPath,
                args,
                factory.Configuration.PrefixPath,
                UnbufferedEnv,
                false,
                output,
                false,
                elevate
            )) {
                return await proc;
            }
        }

        public static async Task Install(
            IPythonInterpreterFactory factory,
            string package,
            bool elevate,
            Redirector output = null
        ) {
            factory.ThrowIfNotRunnable("factory");

            await ContinueRun(factory, output, elevate, package);
        }

        public static async Task<bool> Install(
            IServiceProvider provider,
            IPythonInterpreterFactory factory,
            string package,
            IServiceProvider site,
            bool elevate,
            Redirector output = null
        ) {
            factory.ThrowIfNotRunnable("factory");

            bool isScript;
            if (site != null && GetEasyInstallPath(factory, out isScript) == null) {
                await Pip.QueryInstallPip(factory, site, SR.GetString(SR.InstallEasyInstall), elevate, output);
            }

            if (output != null) {
                output.WriteLine(SR.GetString(SR.PackageInstalling, package));
                if (provider.GetPythonToolsService().GeneralOptions.ShowOutputWindowForPackageInstallation) {
                    output.ShowAndActivate();
                } else {
                    output.Show();
                }
            }

            var exitCode = await ContinueRun(factory, output, elevate, package);

            if (output != null) {
                if (exitCode == 0) {
                    output.WriteLine(SR.GetString(SR.PackageInstallSucceeded, package));
                } else {
                    output.WriteLine(SR.GetString(SR.PackageInstallFailedExitCode, package, exitCode));
                }
                if (provider.GetPythonToolsService().GeneralOptions.ShowOutputWindowForPackageInstallation) {
                    output.ShowAndActivate();
                } else {
                    output.Show();
                }
            }
            return exitCode == 0;
        }
    }
}
