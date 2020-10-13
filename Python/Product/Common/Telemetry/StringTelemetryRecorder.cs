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
using System.Text;

namespace Microsoft.PythonTools.Common.Telemetry {
    /// <summary>
    /// Records telemetry events into a string. The resulting log can be used
    /// for testing or for submitting telemetry as a file rather than via
    /// VS telemetry Web service.
    /// </summary>
    public sealed class StringTelemetryRecorder : ITelemetryRecorder, ITelemetryLog, IDisposable {
        private readonly StringBuilder _stringBuilder = new StringBuilder();

        #region ITelemetryRecorder
        public bool IsEnabled => true;

        public bool CanCollectPrivateInformation => true;

        public void RecordEvent(string eventName, IReadOnlyDictionary<string, string> parameters = null) {
            _stringBuilder.AppendLine(eventName);
            if (parameters != null) {
                WriteDictionary(parameters);
            }
        }

        public void RecordFault(string eventName, Exception ex, string description, bool dumpProcess) {
        }

        #endregion

        #region ITelemetryLog
        public void Reset() => _stringBuilder.Clear();
        public string SessionLog => _stringBuilder.ToString();
        #endregion

        public void Dispose() {
        }

        private void WriteDictionary(IReadOnlyDictionary<string, string> dict) {
            foreach (var kvp in dict) {
                WriteProperty(kvp.Key, kvp.Value);
            }
        }

        private void WriteProperty(string name, object value) {
            _stringBuilder.Append('\t');
            _stringBuilder.Append(name);
            _stringBuilder.Append(" : ");
            _stringBuilder.AppendLine(value.ToString());
        }
    }
}
