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
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    static class CondaUtils {
        internal static string GetCondaExecutablePath(string prefixPath, bool allowBatch = true) {
            if (!Directory.Exists(prefixPath)) {
                return null;
            }

            var condaExePath = Path.Combine(prefixPath, "scripts", "conda.exe");
            if (File.Exists(condaExePath)) {
                return condaExePath;
            }

            if (allowBatch) {
                var condaBatPath = Path.Combine(prefixPath, "scripts", "conda.bat");
                if (File.Exists(condaBatPath)) {
                    return condaBatPath;
                }
            }

            return null;
        }

        internal static string GetLatestCondaExecutablePath(IEnumerable<IPythonInterpreterFactory> factories) {
            var condaPaths = factories
                .Select(factory => CondaUtils.GetCondaExecutablePath(factory.Configuration.PrefixPath, allowBatch: false))
                .Where(path => !string.IsNullOrEmpty(path))
                .OrderByDescending(path => GetCondaVersion(path));

            return condaPaths.FirstOrDefault();
        }

        private static PackageVersion GetCondaVersion(string exePath) {
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

            return PackageVersion.Empty;
        }
    }
}
