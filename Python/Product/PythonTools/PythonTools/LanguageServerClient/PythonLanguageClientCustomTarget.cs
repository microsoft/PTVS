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
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using Task = System.Threading.Tasks.Task;

using Microsoft.PythonTools.LanguageServerClient.WorkspaceConfiguration;
using Microsoft.PythonTools.LanguageServerClient.FileWatcher;
using Microsoft.PythonTools.LanguageServerClient.WorkspaceFolders;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal class PythonLanguageClientCustomTarget {
        private readonly IServiceProvider _site;
        private readonly IPythonToolsLogger _logger;
        private readonly JoinableTaskContext _joinableTaskContext;

        [Serializable]
        private sealed class PylanceError {
            public string stack { get; set; }
        }

        [Serializable]
        private sealed class PylanceTelemetryEvent {
            public string EventName { get; set; }
            public Dictionary<string, object> Properties { get; set; }
            public Dictionary<string, double> Measurements { get; set; }
            public PylanceError Exception { get; set; }
        }

        private sealed class PylanceException : Exception {
            private readonly string _stackTrace;

            public PylanceException(string message, string stackTrace) : base(message) {
                _stackTrace = stackTrace;
            }

            public override string StackTrace => _stackTrace;
        }

        public PythonLanguageClientCustomTarget(IServiceProvider site, JoinableTaskContext joinableTaskContext) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            _joinableTaskContext = joinableTaskContext;
            _logger = _site.GetService(typeof(IPythonToolsLogger)) as IPythonToolsLogger;
        }

        /// <summary>
        /// Event fired when client/registerCapability didChangeWorkspaceFolders is called
        /// Has to be internal so JsonRpc doesn't register this as a method.
        /// </summary>
        internal event EventHandler WorkspaceFolderChangeRegistered;

        /// <summary>
        /// Event fired when client/registerCapability didChangeWatchedFiles is called.
        /// Has to be internal so JsonRpc doesn't register this as a method.
        /// </summary>
        internal event EventHandler<DidChangeWatchedFilesRegistrationOptions> WatchedFilesRegistered;

        /// <summary>
        /// Event fired when telemetry for analysis complete is sent.
        /// This is used by test code to verify pylance is ready to go.
        /// Has to be internal so JsonRpc doesn't register this as a method.
        /// </summary>
        internal event EventHandler AnalysisComplete;

        /// <summary>
        /// Event fired when pylance sends a workspace/configuration request
        /// </summary>
        internal event AsyncEventHandler<ConfigurationArgs> WorkspaceConfiguration;

        /// <summary>
        /// Event fired when pylance sends a workspace/workspaceFolders request
        /// </summary>
        internal event AsyncEventHandler<WorkspaceFoldersArgs> WorkspaceFolders;

        [JsonRpcMethod("telemetry/event", UseSingleObjectParameterDeserialization = true)]
        public void OnTelemetryEvent(object arg) {
            PylanceTelemetryEvent telemetry;
            try {
                telemetry = Deserialize<PylanceTelemetryEvent>(arg);
            } catch (JsonException) {
                return;
            }

            if (telemetry == null) {
                return;
            }

            Trace.WriteLine(arg);
            if (telemetry.Exception == null) {
                _logger?.LogEvent(telemetry.EventName, telemetry.Properties, telemetry.Measurements);
            } else {
                _logger?.LogFault(new PylanceException(telemetry.EventName, telemetry.Exception.stack), telemetry.EventName, false);
            }

            // Special case language_server/analysis_complete. We need this for testing so we
            // know when it's okay to try to bring up intellisense
            if (telemetry.EventName == "language_server/analysis_complete") {
AnalysisComplete?.Invoke(this, EventArgs.Empty);
            }
        }

        [JsonRpcMethod("python/beginProgress")]
        public void OnBeginProgressAsync() {
        }

        [JsonRpcMethod("python/reportProgress")]
        public async Task OnReportProgressAsync(object arg) {
            if (arg != null) {
                await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

                // TODO: output window as well/instead?
                var msg = arg.ToString();
                var statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
                statusBar?.SetText(msg);
            }
        }

        [JsonRpcMethod("python/endProgress")]
        public async Task OnEndProgressAsync() {
            await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

            // TODO: localize text
            var statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            statusBar?.SetText("Python analysis done");
        }

        [JsonRpcMethod("client/registerCapability", UseSingleObjectParameterDeserialization = true)]
        public void OnRegisterCapability(object arg) {
            var regParams = Deserialize<VisualStudio.LanguageServer.Protocol.RegistrationParams>(arg);
            if (regParams?.Registrations == null) {
                return;
            }

            if (regParams.Registrations.Any(p => p.Method == "workspace/didChangeWorkspaceFolders")) {
                _joinableTaskContext.Factory.RunAsync(async () => this.WorkspaceFolderChangeRegistered.Invoke(this, EventArgs.Empty));
            }
            var watchedFilesReg = regParams.Registrations.FirstOrDefault(p => p.Method == "workspace/didChangeWatchedFiles");
            if (watchedFilesReg?.RegisterOptions is JObject optionsObj) {
                var options = optionsObj.ToObject<DidChangeWatchedFilesRegistrationOptions>();
                if (options != null) {
                    _joinableTaskContext.Factory.RunAsync(async () => this.WatchedFilesRegistered.Invoke(this, options));
                }
            }
        }

        [JsonRpcMethod("workspace/configuration", UseSingleObjectParameterDeserialization = true)]
        public async Task<object> OnWorkspaceConfiguration(object arg) {
            var reqParams = Deserialize<WorkspaceConfiguration.ConfigurationParams>(arg);
            if (this.WorkspaceConfiguration != null && reqParams != null) {
                var eventArgs = new ConfigurationArgs { requestParams = reqParams, requestResult = null };
                await this.WorkspaceConfiguration.InvokeAsync(this, eventArgs);
                return eventArgs.requestResult;
            }
            return null;
        }

        private static T Deserialize<T>(object arg) where T : class {
            if (arg == null) {
                return null;
            }

            // Dev18 supplies System.Text.Json values; older formatters supply JToken.
            return arg is JToken token
                ? token.ToObject<T>()
                : JsonConvert.DeserializeObject<T>(arg.ToString());
        }

        [JsonRpcMethod("workspace/workspaceFolders")]
        public async Task<object> OnWorkspaceFolders() {
            try {
                // Should be no arguments (see request here: https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#workspace_workspaceFolders)
                if (this.WorkspaceFolders != null) {
                    var eventArgs = new WorkspaceFoldersArgs { requestResult = null };
                    await this.WorkspaceFolders.InvokeAsync(this, eventArgs);
                    return eventArgs.requestResult;
                }
                return null;
            } catch {
                return null;
            }
        }
    }

    internal class EmptyEventArgs {
    }
}
