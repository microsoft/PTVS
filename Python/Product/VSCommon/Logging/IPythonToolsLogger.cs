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

namespace Microsoft.PythonTools.Logging {
    /// <summary>
    /// Provides an interface for logging events and statistics inside of PTVS.
    /// 
    /// Multiple loggers can be created which send stats to different locations.
    /// 
    /// By default there is one logger which shows the stats in 
    /// Tools->Python Tools->Diagnostic Info.
    /// </summary>
    interface IPythonToolsLogger {
        /// <summary>
        /// Informs the logger of an event.  Unknown events should be ignored.
        /// </summary>
        void LogEvent(PythonLogEvent logEvent, object argument);

        /// <summary>
        /// Informs the logger of an event.  Unknown events should be ignored.
        /// </summary>
        void LogEvent(string eventName, IReadOnlyDictionary<string, object> properties, IReadOnlyDictionary<string, double> measurements);

        /// <summary>
        /// Reports an exception.
        /// </summary>
        void LogFault(Exception ex, string description, bool dumpProcess);
    }
}
