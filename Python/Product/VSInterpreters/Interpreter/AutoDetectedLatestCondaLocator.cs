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
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Conda locator that searches the registry for Anaconda or Miniconda,
    /// figures out which one has the newest version of conda (if there is
    /// more than one) and returns its path.
    /// This is the lowest priority locator because it is the least predictable.
    /// </summary>
    [Export(typeof(ICondaLocator))]
    [ExportMetadata("Priority", 1000)]
    sealed class AutoDetectedLatestCondaLocator : ICondaLocator {
        private readonly CPythonInterpreterFactoryProvider _globalProvider;
        private readonly IServiceProvider _site;
        private Lazy<string> _latestCondaExe;

        [ImportingConstructor]
        public AutoDetectedLatestCondaLocator(
            [Import] CPythonInterpreterFactoryProvider globalProvider,
            [Import(typeof(SVsServiceProvider), AllowDefault = true)] IServiceProvider site = null
        ) {
            _globalProvider = globalProvider;
            _site = site;

            // This can be slow, if there are 2 or more global conda installations
            // (some conda versions have long startup time), so we only fetch it once.
            _latestCondaExe = new Lazy<string>(() => GetLatestCondaExecutablePath(_site, _globalProvider.GetInterpreterFactories()));
        }

        public string CondaExecutablePath => _latestCondaExe.Value;

        private static string GetLatestCondaExecutablePath(IServiceProvider serviceProvider, IEnumerable<IPythonInterpreterFactory> factories) {
            var condaPaths = factories
                .Select(factory => new {
                    PrefixPath = factory.Configuration.GetPrefixPath(),
                    ExePath = CondaUtils.GetCondaExecutablePath(factory.Configuration.GetPrefixPath(), allowBatch: false)
                })
                .Where(obj => !string.IsNullOrEmpty(obj.ExePath))
                .OrderByDescending(obj => GetCondaVersion(obj.PrefixPath, obj.ExePath));
            return condaPaths.FirstOrDefault()?.ExePath;
        }

        private static PackageVersion GetCondaVersion(string prefixPath, string exePath) {
            // Reading from .version is faster than running conda -V
            var versionFilePath = Path.Combine(prefixPath, "Lib", "site-packages", "conda", ".version");
            if (File.Exists(versionFilePath)) {
                try {
                    var version = File.ReadAllText(versionFilePath).Trim();
                    if (PackageVersion.TryParse(version, out PackageVersion ver)) {
                        return ver;
                    }
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
            }

            if (File.Exists(exePath)) {
                using (var output = ProcessOutput.RunHiddenAndCapture(exePath, "-V")) {
                    output.Wait();
                    if (output.ExitCode == 0) {
                        // Version is currently being printed to stderr, and nothing in stdout
                        foreach (var line in output.StandardErrorLines.Union(output.StandardOutputLines)) {
                            if (!string.IsNullOrEmpty(line) && line.StartsWithOrdinal("conda ")) {
                                var version = line.Substring("conda ".Length);
                                if (PackageVersion.TryParse(version, out PackageVersion ver)) {
                                    return ver;
                                }
                            }
                        }
                    }
                }
            }

            return PackageVersion.Empty;
        }
    }
}
