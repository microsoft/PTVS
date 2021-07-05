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
    /// <summary>
    /// Application telemetry service. In Visual Studio maps to IVsTelemetrySession.
    /// </summary>
    internal interface ITelemetryService {
        /// <summary>
        /// True of user opted in and telemetry is being collected
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Records event with parameters
        /// </summary>
        /// <param name="area">Telemetry area name such as 'Project'.</param>
        /// <param name="eventName">Event name.</param>
        /// <param name="parameters">
        /// Either string/object dictionary or anonymous
        /// collection of string/object pairs.
        /// </param>
        void ReportEvent(string area, string eventName, object parameters = null);

        void ReportFault(Exception ex, string description, bool dumpProcess);
    }
}
