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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Utility;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal sealed class PylanceLanguageServer {
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly NodeEnvironmentProvider _nodeEnvironmentProvider;
        private readonly IServiceProvider _site;

        public PylanceLanguageServer(IServiceProvider site, JoinableTaskContext joinableTaskContext) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));
            _nodeEnvironmentProvider = new NodeEnvironmentProvider(site, joinableTaskContext);
            CancellationFolderName = Guid.NewGuid().ToString().Replace("-", "");
        }

        public string CancellationFolderName { get; }

        public async Task<Connection> ActivateAsync() {
            await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

            var nodePath = await _nodeEnvironmentProvider.GetNodeExecutablePath();
            if (!File.Exists(nodePath)) {
                MessageBox.ShowErrorMessage(_site, Strings.LanguageClientNodejsNotFound);
                return null;
            }

            var isDebugging = IsDebugging();
            var debugArgs = isDebugging ? GetDebugArguments() : string.Empty;
            var serverFilePath = isDebugging ? GetDebugServerLocation() : GetServerLocation();

            if (!File.Exists(serverFilePath)) {
                MessageBox.ShowErrorMessage(_site, Strings.LanguageClientPylanceNotFound);
                return null;
            }

            var serverFolderPath = Path.GetDirectoryName(serverFilePath);

            await Task.Yield();

            var info = new ProcessStartInfo {
                FileName = nodePath,
                WorkingDirectory = serverFolderPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $"{debugArgs} \"{serverFilePath}\" -- --stdio --cancellationReceive=file:{this.CancellationFolderName}",
            };

            var process = new Process {
                StartInfo = info
            };

            return process.Start() ? new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream) : null;
        }

        private static string GetDebugServerLocation() {
            // Use a debug build of Pylance at a specified location.
            var filePath = Environment.GetEnvironmentVariable("PTVS_PYLANCE_DEBUG_STARTUP_FILE");
            return File.Exists(filePath) ? filePath : null;
        }

        private static string GetServerLocation() {
            var filePath = PythonToolsInstallPath.GetFile(@"Pylance\package\dist\pylance-langserver.bundle.js");
            return File.Exists(filePath) ? filePath : null;
        }

        private static string GetDebugArguments() {
            // Example: "--nolazy --inspect=6600"
            return Environment.GetEnvironmentVariable("PTVS_PYLANCE_DEBUG_ARGS") ?? string.Empty;
        }

        private static bool IsDebugging() {
            // If enabled, we'll use PTVS_PYLANCE_DEBUG_STARTUP_FILE and PTVS_PYLANCE_DEBUG_ARGS
            return IsEnvVarEnabled("PTVS_PYLANCE_DEBUG_ENABLED");
        }

        private static bool IsEnvVarEnabled(string variable) {
            var val = Environment.GetEnvironmentVariable(variable);
            return !string.IsNullOrEmpty(val) && (val != "0" && string.Compare(val, "false", StringComparison.OrdinalIgnoreCase) != 0);
        }
    }
}
