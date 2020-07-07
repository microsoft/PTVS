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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.ComponentModelHost;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.Interpreter {
    static class CondaUtils {
        private const string EnvironmentStartMarker = "!!!ENVIRONMENT MARKER!!!";
        private static string PrintEnvironmentCode = $"import os, json; print('{EnvironmentStartMarker}'); print(json.dumps(dict(os.environ)))";

        private static readonly Dictionary<CondaCacheKey, KeyValuePair<string, string>[]> _activationCache = new Dictionary<CondaCacheKey, KeyValuePair<string, string>[]>();
        private static readonly SemaphoreSlim _activationCacheLock = new SemaphoreSlim(1);

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

        internal static string GetRootCondaExecutablePath(IServiceProvider serviceProvider) {
            var componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
            var provider = componentModel.GetService<ICondaLocatorProvider>();
            return provider?.FindLocator()?.CondaExecutablePath;
        }

        /// <summary>
        /// Determine if an environment is created/managed by conda and thus should be activated.
        /// </summary>
        /// <param name="prefixPath">Path to the environment.</param>
        /// <returns><c>true</c> if it is a conda environment.</returns>
        internal static bool IsCondaEnvironment(string prefixPath) {
            if (string.IsNullOrEmpty(prefixPath)) {
                return false;
            }

            return File.Exists(Path.Combine(prefixPath, "python.exe")) &&
                Directory.Exists(Path.Combine(prefixPath, "conda-meta"));
        }

        /// <summary>
        /// Activate the root conda environment, capture and return its environment variables.
        /// </summary>
        /// <param name="condaPath">Path to the root conda environment's conda.exe</param>
        /// <returns>List of environment variables.</returns>
        /// <remarks>Result is cached, it is safe to call multiple times.</remarks>
        internal static Task<IEnumerable<KeyValuePair<string, string>>> GetActivationEnvironmentVariablesForRootAsync(string condaPath) {
            return GetActivationEnvironmentVariablesForPrefixAsync(condaPath, null);
        }

        /// <summary>
        /// Activate the specified conda environment, capture and return its environment variables.
        /// </summary>
        /// <param name="condaPath">Path to the root conda environment's conda.exe</param>
        /// <param name="prefixPath">Path to the conda environment to activate, or <c>null</c> to activate the root environment.</param>
        /// <returns>List of environment variables.</returns>
        /// <remarks>Result is cached, it is safe to call multiple times.</remarks>
        internal static async Task<IEnumerable<KeyValuePair<string, string>>> GetActivationEnvironmentVariablesForPrefixAsync(string condaPath, string prefixPath) {
            using (await _activationCacheLock.LockAsync(CancellationToken.None)) {
                var condaKey = new CondaCacheKey(condaPath, prefixPath);

                if (!_activationCache.TryGetValue(condaKey, out KeyValuePair<string, string>[] activationVariables)) {
                    activationVariables = new KeyValuePair<string, string>[0];

                    var activateBat = Path.Combine(Path.GetDirectoryName(condaPath), "activate.bat");
                    if (File.Exists(activateBat)) {
                        var args = prefixPath != null
                            ? new[] { prefixPath, "&", "python.exe", "-c", PrintEnvironmentCode }
                            : new[] { "&", "python.exe", "-c", PrintEnvironmentCode };

                        using (var proc = ProcessOutput.RunHiddenAndCapture(activateBat, args)) {
                            await proc;
                            if (proc.ExitCode == 0) {
                                activationVariables = ParseEnvironmentVariables(proc).ToArray();
                            }
                        }
                    }

                    _activationCache[condaKey] = activationVariables;
                }

                return activationVariables;
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> ParseEnvironmentVariables(ProcessOutput proc) {
            var lines = proc.StandardOutputLines.ToArray();

            // Protect against potential output coming from the activation batch
            // file by looking for the marker to locate the json output.
            var markerIndex = lines.IndexOf(EnvironmentStartMarker);
            if (markerIndex >= 0 && markerIndex < (lines.Length - 1)) {
                var json = lines[markerIndex + 1];
                if (!string.IsNullOrEmpty(json)) {
                    try {
                        var envs = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        return envs.ToArray();
                    } catch (JsonException) {
                    }
                }
            }

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        class CondaCacheKey {
            public CondaCacheKey(string condaPath, string prefixPath = null) {
                CondaExecutablePath = condaPath ?? throw new ArgumentNullException(nameof(condaPath));
                PrefixPath = prefixPath ?? string.Empty;
            }

            public string CondaExecutablePath { get; }

            public string PrefixPath { get; }

            public override bool Equals(object obj) {
                var other = obj as CondaCacheKey;
                if (other == null) {
                    return false;
                }

                if (!other.CondaExecutablePath.Equals(CondaExecutablePath, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }

                if (!other.PrefixPath.Equals(PrefixPath, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }

                return true;
            }

            public override int GetHashCode() {
                return CondaExecutablePath.GetHashCode() ^ PrefixPath.GetHashCode();
            }
        }
    }
}
