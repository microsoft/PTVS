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
            IInterpreterRegistryService service,
            IInterpreterOptionsService optionsService
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
                metaFile = PathUtils.EnumerateFiles(condaMetaPath, "*.json", recurse: false).FirstOrDefault();
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
                    var config = service.Configurations.FirstOrDefault(
                        f => PathUtils.IsSameDirectory(f.PrefixPath, prefix)
                    );

                    IPythonInterpreterFactory factory = null;
                    if (config != null) {
                        factory = service.FindInterpreter(config.Id);
                    }

                    if (factory != null && !await factory.HasModuleAsync("conda", optionsService)) {
                        factory = null;
                    }

                    return factory;
                }
            }

            if (await target.HasModuleAsync("conda", optionsService)) {
                return target;
            }
            return null;
        }

        public static bool CanInstall(
            IPythonInterpreterFactory factory,
            IInterpreterRegistryService service, 
            IInterpreterOptionsService optionsService
        ) {
            if (!factory.IsRunnable()) {
                return false;
            }

            return TryGetCondaFactoryAsync(factory, service, optionsService).WaitAndUnwrapExceptions() != null;
        }

        public static async Task<bool> Install(
            IServiceProvider provider,
            IPythonInterpreterFactory factory,
            IInterpreterRegistryService service,
            IInterpreterOptionsService optionsService,
            string package,
            Redirector output = null
        ) {
            factory.ThrowIfNotRunnable("factory");

            var condaFactory = await TryGetCondaFactoryAsync(factory, service, optionsService);
            if (condaFactory == null) {
                throw new InvalidOperationException(Strings.CannotFindConda);
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
