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

namespace Microsoft.PythonTools.Logging
{
    /// <summary>
    /// Write errors and failures to the activity log.
    /// </summary>
    [Export(typeof(IPythonToolsLogger))]
    class ActivityLogLogger : IPythonToolsLogger
    {
        public void LogEvent(PythonLogEvent logEvent, object argument)
        {
            switch (logEvent)
            {
                case PythonLogEvent.AnalysisExitedAbnormally:
                case PythonLogEvent.AnalysisOperationFailed:
                    ActivityLog.TryLogError("Python", "[{0}] {1}: {2}".FormatInvariant(DateTime.Now, logEvent, argument as string ?? ""));
                    break;
            }
        }

        public void LogFault(Exception ex, string description, bool dumpProcess)
        {
            ActivityLog.TryLogError("Python", ex.ToString());
        }
    }
}
