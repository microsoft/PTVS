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

namespace Microsoft.CookiecutterTools.Telemetry {

    internal sealed class VsTelemetryService : TelemetryServiceBase, ITelemetryLog {
        public static readonly string EventNamePrefixString = "VS/Cookiecutter/";
        public static readonly string PropertyNamePrefixString = "VS.Cookiecutter.";

        private static readonly Lazy<VsTelemetryService> _instance = new Lazy<VsTelemetryService>(() => new VsTelemetryService());

        public VsTelemetryService()
            : base(VsTelemetryService.EventNamePrefixString, VsTelemetryService.PropertyNamePrefixString, VsTelemetryRecorder.Current) {
        }

        public static TelemetryServiceBase Current => _instance.Value;

        #region ITelemetryLog
        public string SessionLog {
            get { return (base.TelemetryRecorder as ITelemetryLog)?.SessionLog; }
        }

        public void Reset() {
            (base.TelemetryRecorder as ITelemetryLog)?.Reset();
        }
        #endregion
    }
}
