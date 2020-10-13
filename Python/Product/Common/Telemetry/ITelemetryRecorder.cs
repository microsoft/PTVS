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

namespace Microsoft.PythonTools.Common.Telemetry {
    /// <summary>
    /// Represents object that records telemetry events and is called by
    /// the telemetry service. In Visual Studio environment maps to IVsTelemetryService
    /// whereas in tests can be replaced by an object that writes events to a string.
    /// </summary>
    public interface ITelemetryRecorder : IDisposable {
        /// <summary>
        /// True if telemetry is actually being recorded
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Indicates if telemetry can collect private information
        /// </summary>
        bool CanCollectPrivateInformation { get; }

        /// <summary>
        /// Records event with parameters. Parameters are
        /// a collection of string/object pairs.
        /// </summary>
        void RecordEvent(string eventName, IReadOnlyDictionary<string, string> parameters = null);

        /// <summary>
        /// Records a fault event.
        /// </summary>
        void RecordFault(string eventName, Exception ex, string description, bool dumpProcess);
    }
}
