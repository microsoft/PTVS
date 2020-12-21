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
using System.ComponentModel.Design;
using System.Linq;

namespace Microsoft.PythonTools.Logging {
    /// <summary>
    /// Main entry point for logging events.  A single instance of this logger is created
    /// by our package and can be used to dispatch log events to all installed loggers.
    /// </summary>
    class PythonToolsLogger : IPythonToolsLogger {
        private readonly IPythonToolsLogger[] _loggers;

        public PythonToolsLogger(IPythonToolsLogger[] loggers) {
            _loggers = loggers;
        }

        public void LogEvent(PythonLogEvent logEvent, object data = null) {
            foreach (var logger in _loggers) {
                logger.LogEvent(logEvent, data);
            }
        }

        public void LogFault(Exception ex, string description, bool dumpProcess) {
            foreach (var logger in _loggers) {
                logger.LogFault(ex, description, dumpProcess);
            }
        }

        internal static object CreateService(IServiceContainer container, Type serviceType) {
            if (serviceType.IsEquivalentTo(typeof(IPythonToolsLogger))) {
                var model = container.GetComponentModel();
                return new PythonToolsLogger(model.GetExtensions<IPythonToolsLogger>().ToArray());
            }
            return null;
        }
    }
}
