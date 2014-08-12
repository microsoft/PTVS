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
    static class Conda {
        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] { 
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        private static bool TryGetCondaFactory(
            IPythonInterpreterFactory target,
            IInterpreterOptionsService service,
            out IPythonInterpreterFactory factory
        ) {
            factory = null;

            var condaMetaPath = CommonUtils.GetAbsoluteDirectoryPath(
                target.Configuration.PrefixPath,
                "conda-meta"
            );

            if (!Directory.Exists(condaMetaPath)) {
                return false;
            }

            string metaFile;
            try {
                metaFile = Directory.EnumerateFiles(condaMetaPath, "*.json").FirstOrDefault();
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                return false;
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
                        return false;
                    }

                    var prefix = Path.GetDirectoryName(Path.GetDirectoryName(pkg));
                    factory = service.Interpreters.FirstOrDefault(
                        f => CommonUtils.IsSameDirectory(f.Configuration.PrefixPath, prefix)
                    );

                    if (factory != null && !factory.FindModules("conda").Any()) {
                        factory = null;
                    }

                    return factory != null;
                }
            }

            if (target.FindModules("conda").Any()) {
                factory = target;
            }
            return factory != null;
        }

        public static bool CanInstall(
            IPythonInterpreterFactory factory,
            IInterpreterOptionsService service
        ) {
            if (!factory.IsRunnable()) {
                return false;
            }

            IPythonInterpreterFactory dummy;
            return TryGetCondaFactory(factory, service, out dummy);
        }

        public static async Task<bool> Install(
            IPythonInterpreterFactory factory,
            IInterpreterOptionsService service,
            string package,
            Redirector output = null
        ) {
            factory.ThrowIfNotRunnable("factory");

            IPythonInterpreterFactory condaFactory;
            if (!TryGetCondaFactory(factory, service, out condaFactory)) {
                throw new InvalidOperationException("Cannot find conda");
            }
            condaFactory.ThrowIfNotRunnable();

            if (output != null) {
                output.WriteLine(SR.GetString(SR.PackageInstalling, package));
                if (PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForPackageInstallation) {
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
                        output.WriteLine(SR.GetString(SR.PackageInstallSucceeded, package));
                    } else {
                        output.WriteLine(SR.GetString(SR.PackageInstallFailedExitCode, package, exitCode));
                    }
                    if (PythonToolsPackage.Instance != null && PythonToolsPackage.Instance.GeneralOptionsPage.ShowOutputWindowForPackageInstallation) {
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
