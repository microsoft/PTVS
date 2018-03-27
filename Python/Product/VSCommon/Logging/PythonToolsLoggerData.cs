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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.PythonTools.Logging {
    /// <summary>
    /// Provides a base class for logging complicated event data.
    /// </summary>
    abstract class PythonToolsLoggerData {
        public static IDictionary<string, object> AsDictionary(object obj) {
            IDictionary<string, object> res;

            if (obj == null) {
                return null;
            }

            if ((res = obj as IDictionary<string, object>) != null) {
                return res;
            }

            if (!(obj is PythonToolsLoggerData)) {
                return null;
            }

            res = new Dictionary<string, object>();
            foreach (var propInfo in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                try {
                    var value = propInfo.GetValue(obj);
                    if (propInfo.GetCustomAttributes().OfType<PiiPropertyAttribute>().Any()) {
                        value = new TelemetryPiiProperty(value);
                    }
                    res[propInfo.Name] = value;
                } catch (Exception ex) {
                    Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(PythonToolsLoggerData)));
                }
            }
            return res;
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    sealed class PiiPropertyAttribute : Attribute {
        public PiiPropertyAttribute() { }
    }

    sealed class PackageInfo : PythonToolsLoggerData {
        [PiiProperty]
        public string Name { get; set; }
    }

    sealed class AnalysisInitialize : PythonToolsLoggerData {
        [PiiProperty]
        public string InterpreterId { get; set; }
        public string Architecture { get; set; }
        public string Version { get; set; }
        public string Reason { get; set; }
    }

    static class AnalysisInitializeReasons {
        public const string Project = "Project";
        public const string Interactive = "Interactive";
        public const string Default = "Default";
    }

    sealed class AnalysisInfo : PythonToolsLoggerData {
        [PiiProperty]
        public string InterpreterId { get; set; }
        public int AnalysisSeconds { get; set; }
    }

    sealed class LaunchInfo : PythonToolsLoggerData {
        public bool IsDebug { get; set; }
        public bool IsWeb { get; set; }
        public string Version { get; set; }
    }

    sealed class AnalysisTimingInfo : PythonToolsLoggerData {
        public string RequestName { get; set; }
        public int Milliseconds { get; set; }
        public bool Timeout { get; set; }
    }

    sealed class DebugReplInfo : PythonToolsLoggerData {
        public bool RemoteProcess { get; set; }
        public string Version { get; set; }
    }

    sealed class GetExpressionAtPointInfo : PythonToolsLoggerData {
        public int Milliseconds { get; set; }
        public int PartialAstLength { get; set; }
        public bool Success { get; set; }
        public bool ExpressionFound { get; set; }
    }

    //sealed class UnhandledExceptionInfo : PythonToolsLoggerData {
    //    public string FullName { get; set; }
    //    public string Details { get; set; }
    //    public bool UserNotified { get; set; }
    //}
}
