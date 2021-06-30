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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;

namespace Microsoft.PythonTools.Debugger.VCLauncher {
    [ExportDebugger("PythonDebugLaunchProvider")]
    [AppliesTo(ProjectCapabilities.VisualC)]
    internal partial class PythonDebugLaunchProvider : DebugLaunchProviderBase {
        private readonly Dictionary<DebugLaunchOptions, DebugLaunchSettings> _launchSettings;

        [ImportingConstructor]
        public PythonDebugLaunchProvider(ConfiguredProject configuredProject)
            : base(configuredProject) {
            _launchSettings = new Dictionary<DebugLaunchOptions, DebugLaunchSettings>();
        }


        private async Task<DebugLaunchSettings> CreateLaunchSettingsAsync(DebugLaunchOptions options) {
            var settings = new DebugLaunchSettings(options) {
                LaunchOperation = DebugLaunchOperation.CreateProcess,
                LaunchDebugEngineGuid = DkmEngineId.NativeEng,
                Project = VsHierarchy
            };
            settings.AdditionalDebugEngines.Add(DebugEngine.AD7Engine.DebugEngineGuid);

            var props = await new Rules.RuleProperties(ConfiguredProject).GetPythonDebugLaunchProviderPropertiesAsync().ConfigureAwait(false);

            settings.Executable = await props.LocalDebuggerCommand.GetEvaluatedValueAtEndAsync().ConfigureAwait(false);
            settings.Arguments = await props.LocalDebuggerCommandArguments.GetEvaluatedValueAtEndAsync().ConfigureAwait(false);
            settings.CurrentDirectory = await props.LocalDebuggerWorkingDirectory.GetEvaluatedValueAtEndAsync().ConfigureAwait(false);

            var envString = await props.LocalDebuggerEnvironment.GetEvaluatedValueAtEndAsync().ConfigureAwait(false);

            if (!string.IsNullOrEmpty(envString)) {
                var mergeEnv = await props.LocalDebuggerMergeEnvironment.GetEvaluatedValueAtEndAsync().ConfigureAwait(false);
                if ("true".Equals(mergeEnv ?? "", StringComparison.OrdinalIgnoreCase)) {
                    FillFromCurrentEnvironment(settings.Environment);
                }
                ParseEnvironment(settings.Environment, envString);
            }

            return settings;
        }

        private static void FillFromCurrentEnvironment(IDictionary<string, string> env) {
            foreach (DictionaryEntry kv in Environment.GetEnvironmentVariables()) {
                var k = kv.Key as string;
                var v = kv.Value as string;
                if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(v)) {
                    continue;
                }
                env[k] = v;
            }
        }

        private static void ParseEnvironment(IDictionary<string, string> env, string envString) {
            foreach (var kv in envString.Split('\n')) {
                int i = kv.IndexOf('=');
                if (i < 1 || i >= kv.Length - 1) {
                    continue;
                }
                var k = kv.Substring(0, i);
                var v = kv.Substring(i + 1);
                if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(v)) {
                    continue;
                }

                v = Regex.Replace(v, "%([\\w_]+)%", m => {
                    if (!m.Groups[1].Success) {
                        return m.Value;
                    }
                    string newV;
                    if (env.TryGetValue(m.Groups[1].Value, out newV)) {
                        return newV;
                    }
                    return m.Value;
                });

                env[k] = v;
            }
        }

        public async override Task<bool> CanLaunchAsync(DebugLaunchOptions launchOptions) {
            return await CreateLaunchSettingsAsync(launchOptions) != null;
        }

        public async override Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(DebugLaunchOptions launchOptions) {
            var result = new List<IDebugLaunchSettings>();

            result.Add(await CreateLaunchSettingsAsync(launchOptions));

            return result;
        }

    }
}
