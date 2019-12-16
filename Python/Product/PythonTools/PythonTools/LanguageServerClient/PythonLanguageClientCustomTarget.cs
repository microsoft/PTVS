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
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal class PythonLanguageClientCustomTarget {
        private readonly IServiceProvider _site;

        public PythonLanguageClientCustomTarget(IServiceProvider site) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
        }

        [JsonRpcMethod("telemetry/event")]
        public void OnTelemetryEvent(JToken arg) {
            var telemetry = arg as JObject;
            if (telemetry != null) {
                Trace.WriteLine(telemetry.ToString());
            }
        }

        [JsonRpcMethod("python/beginProgress")]
        public void OnBeginProgress() {
        }

        [JsonRpcMethod("python/reportProgress")]
        public async Task OnReportProgressAsync(JToken arg) {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var msg = arg.ToString();
            var statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            statusBar?.SetText(msg);
        }

        [JsonRpcMethod("python/endProgress")]
        public async Task OnEndProgressAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            statusBar?.SetText("Python analysis done");
        }

        public string OnCustomRequest(string test) {
            // Example of a request from server. Don't know if we have any of those.
            return string.Empty;
        }
    }
}
