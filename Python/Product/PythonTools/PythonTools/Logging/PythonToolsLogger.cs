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
    /// Main entry point for logging events.  A single instance of this logger is created
    /// by our package and can be used to dispatch log events to all installed loggers.
    /// </summary>
    class PythonToolsLogger {
        private readonly IPythonToolsLogger[] _loggers;

        public PythonToolsLogger(IPythonToolsLogger[] loggers) {
            _loggers = loggers;
        }
        
        public void LogEvent(PythonLogEvent logEvent, object data = null) {
            foreach (var logger in _loggers) {
                logger.LogEvent(logEvent, data);
            }
        }
    }
}
