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
using System.Windows;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal class PythonLanguageServerPylance : PythonLanguageServer {
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly NodeEnvironmentProvider _nodeEnvironmentProvider;

        public PythonLanguageServerPylance(IServiceProvider site, JoinableTaskContext joinableTaskContext) {
            _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));
            _nodeEnvironmentProvider = new NodeEnvironmentProvider(site, joinableTaskContext);
        }

        public async override Task<Connection> ActivateAsync() {
            await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

            var nodePath = await _nodeEnvironmentProvider.GetNodeExecutablePath();
            if (!File.Exists(nodePath)) {
                MessageBox.Show(Strings.LanguageClientNodejsNotFound, Strings.ProductTitle);
                return null;
            }

            var isDebugging = IsDebugging();
            var debugArgs = isDebugging ? GetDebugArguments(): string.Empty;
            var serverFilePath = isDebugging ? GetDebugServerLocation() : GetServerLocation();

            if (!File.Exists(serverFilePath)) {
                MessageBox.Show(Strings.LanguageClientPylanceNotFound, Strings.ProductTitle);
                return null;
            }

            var serverFolderPath = Path.GetDirectoryName(serverFilePath);

            await Task.Yield();

            var cancelFile = Guid.NewGuid().ToString().Replace("-", "");
            var info = new ProcessStartInfo {
                FileName = nodePath,
                WorkingDirectory = serverFolderPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $"{debugArgs} \"{serverFilePath}\" -- --stdio --cancellationReceive=file:{cancelFile}",
            };

            var process = new Process {
                StartInfo = info
            };

            if (process.Start()) {
                return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
            }

            return null;
        }

        private static string GetDebugServerLocation() {
            // Later, Pylance will be bundled in VS and we'll get its install location.
            // For now, location is retrieved via environment variable.
            // For debugging, use absolute path to: client\server\server.js
            var filePath = Environment.GetEnvironmentVariable("PTVS_PYLANCE_DEBUG_STARTUP_FILE");
            return File.Exists(filePath) ? filePath : null;
        }

        private static string GetServerLocation() {
            // Later, Pylance will be bundled in VS and we'll get its install location.
            // For now, location is retrieved via environment variable.
            // For release, use absolute path to: extension\server\server.bundle.js
            var filePath = Environment.GetEnvironmentVariable("PTVS_PYLANCE_STARTUP_FILE");
            return File.Exists(filePath) ? filePath : null;
        }

        private static string GetDebugArguments() {
            // Example: "--nolazy --inspect=6600"
            return Environment.GetEnvironmentVariable("PTVS_PYLANCE_DEBUG_ARGS") ?? string.Empty;
        }

        private static bool IsDebugging() {
            // If enabled, use PTVS_PYLANCE_DEBUG_STARTUP_FILE and PTVS_PYLANCE_DEBUG_ARGS
            // If disabled, use PTVS_PYLANCE_STARTUP_FILE
            return IsEnvVarEnabled("PTVS_PYLANCE_DEBUG_ENABLED");
        }

        private static bool IsEnvVarEnabled(string variable) {
            var val = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(val)) {
                return false;
            }

            return val != "0" && string.Compare(val, "false", StringComparison.OrdinalIgnoreCase) != 0;
        }
    }
}
