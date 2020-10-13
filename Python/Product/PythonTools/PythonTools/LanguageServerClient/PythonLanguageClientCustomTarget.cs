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
using System.Diagnostics;
using Microsoft.PythonTools.Telemetry;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal class PythonLanguageClientCustomTarget {
        private readonly IServiceProvider _site;
        private readonly JoinableTaskContext _joinableTaskContext;

        [Serializable]
        private sealed class PylanceTelemetryEvent {
            public string EventName { get; set; }
            public Dictionary<string, string> Properties { get; set; }
            public Dictionary<string, string> Measurements { get; set; }
        }

        public PythonLanguageClientCustomTarget(IServiceProvider site, JoinableTaskContext joinableTaskContext) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            _joinableTaskContext = joinableTaskContext;
        }

        [JsonRpcMethod("telemetry/event")]
        public void OnTelemetryEvent(JToken arg) {
            if (arg is JObject telemetry) {
                Trace.WriteLine(telemetry.ToString());
                var te = telemetry.ToObject<PylanceTelemetryEvent>();
                if (te != null) {
                    VsTelemetryService.Current.ReportEvent(PythonToolsTelemetry.TelemetryArea.Pylance, te.EventName, te.Properties);
                }
            }
        }

        [JsonRpcMethod("python/beginProgress")]
        public void OnBeginProgress() {
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
        public async Task OnEndProgressAsync() {
            await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

            // TODO: localize text
            var statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            statusBar?.SetText("Python analysis done");
        }
    }
}
