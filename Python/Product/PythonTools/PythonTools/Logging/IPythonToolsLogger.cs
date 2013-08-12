/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

namespace Microsoft.PythonTools.Logging {
    /// <summary>
    /// Provides an interface for logging events and statistics inside of PTVS.
    /// 
    /// Multiple loggers can be created which send stats to different locations.
    /// 
    /// By default there is one logger which shows the stats in 
    /// Tools->Python Tools->Diagnostic Info.
    /// </summary>
    public interface IPythonToolsLogger {
        /// <summary>
        /// Informs the logger of an event.  Unknown events should be ignored.
        /// </summary>
        void LogEvent(PythonLogEvent logEvent, object argument);
    }
}
