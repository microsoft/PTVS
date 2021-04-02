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
using Microsoft.Python.Core;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.LanguageServer.Protocol;

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
        /// Event fired when client/registerCapability didChangeWatchedFiles is called.
        /// Has to be internal so JsonRpc doesn't register this as a method.
        /// </summary>
        internal event EventHandler AnalysisComplete;

        [JsonRpcMethod("telemetry/event")]
        public void OnTelemetryEvent(JToken arg) {
            if (!(arg is JObject telemetry)) {
                return;
            }

            Trace.WriteLine(telemetry.ToString());
            try {
                var te = telemetry.ToObject<PylanceTelemetryEvent>();
                if (te == null) {
                    return;
                }

                if (te.Exception == null) {
                    _logger.LogEvent(te.EventName, te.Properties, te.Measurements);
                } else {
                    _logger.LogFault(new PylanceException(te.EventName, te.Exception.stack), te.EventName, false);
                }

                // Special case language_server/analysis_complete. We need this for testing so we 
                // know when it's okay to try to bring up intellisense
                if (te.EventName == "language_server/analysis_complete") {
                    AnalysisComplete.Raise(this, EventArgs.Empty);
                }
            } catch {

            }
        }

        [JsonRpcMethod("python/beginProgress")]
#pragma warning disable IDE0060 // Remove unused parameter
        public void OnBeginProgressAsync(JToken arg) {
#pragma warning restore IDE0060 // Remove unused parameter
        }

        [JsonRpcMethod("python/reportProgress")]
        public async Task OnReportProgressAsync(JToken arg) {
            if (arg != null) {
                await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

                // TODO: output window as well/instead?
                var msg = arg.ToString();
                var statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
                statusBar?.SetText(msg);
            }
        }

        [JsonRpcMethod("python/endProgress")]
        public async Task OnEndProgressAsync(JToken arg) {
            await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

            // TODO: localize text
            var statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            statusBar?.SetText("Python analysis done");
        }

        [JsonRpcMethod("client/registerCapability")]
        public void OnRegisterCapability(JToken arg) {
            var regParams = arg.ToObject<VisualStudio.LanguageServer.Protocol.RegistrationParams>();
            if (regParams.Registrations.Any(p => p.Method == "workspace/didChangeWorkspaceFolders")) {
                _joinableTaskContext.Factory.RunAsync(async () => this.WorkspaceFolderChangeRegistered.Invoke(this, EventArgs.Empty));
            }
            var watchedFilesReg = regParams.Registrations.FirstOrDefault(p => p.Method == "workspace/didChangeWatchedFiles");
            if (watchedFilesReg != null) { 
                var optionsObj = watchedFilesReg.RegisterOptions as JObject;
                var options = optionsObj.ToObject<DidChangeWatchedFilesRegistrationOptions>();
                if (options != null) {
                    _joinableTaskContext.Factory.RunAsync(async () => this.WatchedFilesRegistered.Invoke(this, options));
                }
            }
        }
    }
}
