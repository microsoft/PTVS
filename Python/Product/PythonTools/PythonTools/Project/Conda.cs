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
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Project {
    static class Conda {
        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] { 
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        private static async Task<IPythonInterpreterFactory> TryGetCondaFactoryAsync(
            IPythonInterpreterFactory target,
            IInterpreterOptionsService service
        ) {
            var condaMetaPath = PathUtils.GetAbsoluteDirectoryPath(
                target.Configuration.PrefixPath,
                "conda-meta"
            );

            if (!Directory.Exists(condaMetaPath)) {
                return null;
            }

            string metaFile;
            try {
                metaFile = Directory.EnumerateFiles(condaMetaPath, "*.json").FirstOrDefault();
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                return null;
            }

            if (!string.IsNullOrEmpty(metaFile)) {
                string text = string.Empty;
                try {
                    text = File.ReadAllText(metaFile);
                } catch (Exception ex) {
                    if (ex.IsCriticalException()) {
                        throw;
                    }
                }

                var m = Regex.Match(text, @"\{[^{]+link.+?\{.+?""source""\s*:\s*""(.+?)""", RegexOptions.Singleline);
                if (m.Success) {
                    var pkg = m.Groups[1].Value;
                    if (!Directory.Exists(pkg)) {
                        return null;
                    }

                    var prefix = Path.GetDirectoryName(Path.GetDirectoryName(pkg));
                    var factory = service.Interpreters.FirstOrDefault(
                        f => PathUtils.IsSameDirectory(f.Configuration.PrefixPath, prefix)
                    );

                    if (factory != null && !(await factory.FindModulesAsync("conda")).Any()) {
                        factory = null;
                    }

                    return factory;
                }
            }

            if ((await target.FindModulesAsync("conda")).Any()) {
                return target;
            }
            return null;
        }

        public static bool CanInstall(
            IPythonInterpreterFactory factory,
            IInterpreterOptionsService service
        ) {
            if (!factory.IsRunnable()) {
                return false;
            }

            return TryGetCondaFactoryAsync(factory, service).WaitAndUnwrapExceptions() != null;
        }

        public static async Task<bool> Install(
            IServiceProvider provider,
            IPythonInterpreterFactory factory,
            IInterpreterOptionsService service,
            string package,
            Redirector output = null
        ) {
            factory.ThrowIfNotRunnable("factory");

            var condaFactory = await TryGetCondaFactoryAsync(factory, service); ;
            if (condaFactory == null) {
                throw new InvalidOperationException("Cannot find conda");
            }
            condaFactory.ThrowIfNotRunnable();

            if (output != null) {
                output.WriteLine(Strings.PackageInstalling.FormatUI(package));
                if (provider.GetPythonToolsService().GeneralOptions.ShowOutputWindowForPackageInstallation) {
                    output.ShowAndActivate();
                } else {
                    output.Show();
                }
            }

            using (var proc = ProcessOutput.Run(
                condaFactory.Configuration.InterpreterPath,
                new[] { "-m", "conda", "install", "--yes", "-n", factory.Configuration.PrefixPath, package },
                factory.Configuration.PrefixPath,
                UnbufferedEnv,
                false,
                output
            )) {
                var exitCode = await proc;
                if (output != null) {
                    if (exitCode == 0) {
                        output.WriteLine(Strings.PackageInstallSucceeded.FormatUI(package));
                    } else {
                        output.WriteLine(Strings.PackageInstallFailedExitCode.FormatUI(package, exitCode));
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
}
