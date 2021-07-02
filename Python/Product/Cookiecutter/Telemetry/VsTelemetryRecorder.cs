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
    /// Implements telemetry recording in Visual Studio environment
    /// </summary>
    internal sealed class VsTelemetryRecorder : ITelemetryRecorder
    {
        private readonly TelemetrySession _session;
        private static readonly Lazy<VsTelemetryRecorder> _instance = new Lazy<VsTelemetryRecorder>(() => new VsTelemetryRecorder());

        private VsTelemetryRecorder()
        {
            _session = TelemetryService.DefaultSession;
        }

        public static ITelemetryRecorder Current => _instance.Value;

        #region ITelemetryRecorder
        /// <summary>
        /// True if telemetry is actually being recorder
        /// </summary>
        public bool IsEnabled => _session.IsOptedIn;
        public bool CanCollectPrivateInformation => _session.CanCollectPrivateInformation;

        /// <summary>
        /// Records event with parameters
        /// </summary>
        public void RecordEvent(string eventName, object parameters = null)
        {
            if (this.IsEnabled)
            {
                TelemetryEvent telemetryEvent = new TelemetryEvent(eventName);
                if (parameters != null)
                {
                    var stringParameter = parameters as string;
                    if (stringParameter != null)
                    {
                        telemetryEvent.Properties["Value"] = stringParameter;
                    }
                    else
                    {
                        IDictionary<string, object> dict = DictionaryExtension.FromAnonymousObject(parameters);
                        foreach (KeyValuePair<string, object> kvp in dict)
                        {
                            telemetryEvent.Properties[kvp.Key] = kvp.Value;
                        }
                    }
                }
                _session.PostEvent(telemetryEvent);
            }
        }

        public void RecordFault(string eventName, Exception ex, string description, bool dumpProcess)
        {
            if (this.IsEnabled)
            {
                var fault = new FaultEvent(
                    eventName,
                    !string.IsNullOrEmpty(description) ? description : "Unhandled exception in Cookiecutter extension.",
                    ex
                );

                if (dumpProcess)
                {
                    fault.AddProcessDump(Process.GetCurrentProcess().Id);
                    fault.IsIncludedInWatsonSample = true;
                }
                else
                {
                    fault.IsIncludedInWatsonSample = false;
                }

                _session.PostEvent(fault);
            }
        }

        #endregion

        public void Dispose() { }
    }
}
