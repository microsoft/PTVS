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
using Microsoft.PythonTools.LanguageServerClient.StreamHacking;
using Microsoft.PythonTools.Utility;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal sealed class LanguageServer {
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly NodeEnvironmentProvider _nodeEnvironmentProvider;
        private readonly IServiceProvider _site;
        private readonly Func<StreamData, Tuple<StreamData, bool>> _serverSendHandler;
        private readonly Func<StreamData, int> _processOutputReadHandler;

        public LanguageServer(
            IServiceProvider site,
            JoinableTaskContext joinableTaskContext,
            Func<StreamData, Tuple<StreamData, bool>> serverSendHandler,
            Func<StreamData, int> processOutputReadHandler) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));
            _nodeEnvironmentProvider = new NodeEnvironmentProvider(site, joinableTaskContext);
            CancellationFolderName = Guid.NewGuid().ToString().Replace("-", "");
            _serverSendHandler = serverSendHandler;
            _processOutputReadHandler = processOutputReadHandler;
        }

        public string CancellationFolderName { get; }

        public async Task<Connection> ActivateAsync() {
            var nodePath = await _nodeEnvironmentProvider.GetNodeExecutablePath();
            if (!File.Exists(nodePath)) {
                MessageBox.ShowErrorMessage(_site, Strings.LanguageClientNodejsNotFound);
                return null;
            }

            var isDebugging = IsDebugging();
            var debugArgs = isDebugging ? GetDebugArguments() : string.Empty;
            var serverFilePath = isDebugging ? GetDebugServerLocation() : GetServerLocation();
            var debuggerExtra = isDebugging ? "--verbose" : string.Empty;

            if (!File.Exists(serverFilePath)) {
                MessageBox.ShowErrorMessage(_site, Strings.LanguageClientPylanceNotFound);
                return null;
            }

            var serverFolderPath = Path.GetDirectoryName(serverFilePath);

            var info = new ProcessStartInfo {
                FileName = nodePath,
                WorkingDirectory = serverFolderPath,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $"{debugArgs} \"{serverFilePath}\" -- --stdio --cancellationReceive=file:{this.CancellationFolderName} {debuggerExtra}",
            };

            var process = new Process {
                StartInfo = info
            };
            process.ErrorDataReceived += (sender, e) => {
                var outputWindow = OutputWindowRedirector.GetGeneral(_site);
                outputWindow.WriteLine(e.Data);
            };

            if (process.Start()) {
                process.BeginErrorReadLine();
                if (isDebugging) {
                    System.Diagnostics.Debug.WriteLine($"Attach to {process.Id} for pylance debugging");
                    // During debugging give us time to attach
                    await Task.Delay(5000);
                }

                if (!process.HasExited) {
                    // Create a connection where we wrap the stdin stream so that we can intercept all messages
                    return new Connection(
                        new StreamIntercepter(process.StandardOutput.BaseStream, (a) => { return Tuple.Create(a, false); } ,_processOutputReadHandler),
                        new StreamIntercepter(process.StandardInput.BaseStream, _serverSendHandler, (a) => { return a.count; }));
                }
            }
            return null;
        }

        public static bool IsDebugging() {
            // If enabled, we'll use PTVS_PYLANCE_DEBUG_STARTUP_FILE and PTVS_PYLANCE_DEBUG_ARGS
            return IsEnvVarEnabled("PTVS_PYLANCE_DEBUG_ENABLED");
        }

        private static string GetDebugServerLocation() {
            // Use a debug build of Pylance at a specified location.
            var filePath = Environment.GetEnvironmentVariable("PTVS_PYLANCE_DEBUG_STARTUP_FILE");
            return File.Exists(filePath) ? filePath : null;
        }

        private static string GetServerLocation() {
            var filePath = PythonToolsInstallPath.GetFile(@"pylance\dist\pylance-langserver.bundle.js");
            return File.Exists(filePath) ? filePath : null;
        }

        private static string GetDebugArguments() {
            // Example: "--nolazy --inspect=6600"
            return Environment.GetEnvironmentVariable("PTVS_PYLANCE_DEBUG_ARGS") ?? string.Empty;
        }


        private static bool IsEnvVarEnabled(string variable) {
            var val = Environment.GetEnvironmentVariable(variable);
            return !string.IsNullOrEmpty(val) && (val != "0" && string.Compare(val, "false", StringComparison.OrdinalIgnoreCase) != 0);
        }
    }
}
