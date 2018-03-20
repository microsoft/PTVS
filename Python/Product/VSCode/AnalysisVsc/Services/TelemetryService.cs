// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using Microsoft.PythonTools.VsCode.Core.Shell;
using StreamJsonRpc;

namespace Microsoft.PythonTools.VsCode.Services {
    public sealed class TelemetryService : ITelemetryService {
        private readonly JsonRpc _rpc;
        public TelemetryService(JsonRpc rpc) {
            _rpc = rpc;
        }
        public Task SendTelemetry(object o) => _rpc.InvokeAsync("telemetry/event", o);
    }
}
