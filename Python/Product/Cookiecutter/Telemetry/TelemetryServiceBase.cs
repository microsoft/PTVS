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

namespace Microsoft.CookiecutterTools.Telemetry
{
    /// <summary>
    /// Base telemetry service implementation, common to production code and test cases.
    /// </summary>
    internal abstract class TelemetryServiceBase : ITelemetryService, IDisposable
    {
        public string EventNamePrefix { get; private set; }
        public string PropertyNamePrefix { get; private set; }

        /// <summary>
        /// Current active telemetry writer. Inside Visual Studio it 
        /// uses IVsTelemetryService, in unit or component tests
        /// recorder is a simple string container or a disk file.
        /// </summary>
        public ITelemetryRecorder TelemetryRecorder { get; internal set; }

        protected TelemetryServiceBase(string eventNamePrefix, string propertyNamePrefix, ITelemetryRecorder telemetryRecorder)
        {
            this.TelemetryRecorder = telemetryRecorder;
            this.EventNamePrefix = eventNamePrefix;
            this.PropertyNamePrefix = propertyNamePrefix;
        }

        #region ITelemetryService
        /// <summary>
        /// True of user opted in and telemetry is being collected
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                return this.TelemetryRecorder?.IsEnabled == true;
            }
        }

        public bool CanCollectPrivateInformation
        {
            get
            {
                return (this.TelemetryRecorder?.IsEnabled == true && this.TelemetryRecorder?.CanCollectPrivateInformation == true);
            }
        }

        /// <summary>
        /// Records event with parameters
        /// </summary>
        /// <param name="area">Telemetry area name such as 'Toolbox'.</param>
        /// <param name="eventName">Event name.</param>
        /// <param name="parameters">
        /// Either string/object dictionary or anonymous
        /// collection of string/object pairs.
        /// </param>
        public void ReportEvent(string area, string eventName, object parameters = null)
        {
            if (string.IsNullOrEmpty(area))
            {
                throw new ArgumentException(nameof(area));
            }

            if (string.IsNullOrEmpty(eventName))
            {
                throw new ArgumentException(nameof(eventName));
            }

            string completeEventName = MakeEventName(area, eventName);
            if (parameters == null)
            {
                this.TelemetryRecorder.RecordEvent(completeEventName);
            }
            else if (parameters is string)
            {
                this.TelemetryRecorder.RecordEvent(completeEventName, parameters as string);
            }
            else
            {
                IDictionary<string, object> dict = DictionaryExtension.FromAnonymousObject(parameters);
                IDictionary<string, object> dictWithPrefix = new Dictionary<string, object>();

                foreach (KeyValuePair<string, object> kvp in dict)
                {
                    if (string.IsNullOrEmpty(kvp.Key))
                    {
                        throw new ArgumentException("parameterName");
                    }

                    dictWithPrefix[this.PropertyNamePrefix + area.ToString() + "." + kvp.Key] = kvp.Value ?? string.Empty;
                }
                this.TelemetryRecorder.RecordEvent(completeEventName, dictWithPrefix);
            }
        }

        public void ReportFault(Exception ex, string description, bool dumpProcess)
        {
            var completeEventName = this.EventNamePrefix + "UnhandledException";
            this.TelemetryRecorder.RecordFault(completeEventName, ex, description, dumpProcess);
        }

        #endregion

        private string MakeEventName(string area, string eventName)
        {
            return this.EventNamePrefix + area + "/" + eventName;
        }

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(true);
            TelemetryRecorder?.Dispose();
            TelemetryRecorder = null;
        }
    }
}
