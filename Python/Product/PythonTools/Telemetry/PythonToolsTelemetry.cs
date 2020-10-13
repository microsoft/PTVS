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
using Microsoft.PythonTools.Common.Telemetry;

namespace Microsoft.PythonTools.Telemetry {
    /// <summary>
    /// Represents telemetry operations in cookiecutter.
    /// </summary>
    internal sealed class PythonToolsTelemetry {
        /// <summary>
        /// Area names show up as part of telemetry event names like:
        ///   VS/Python/[area]/[event]
        /// </summary>
        internal static class TelemetryArea {
            public const string Pylance = "Pylance";
        }

        internal class TemplateEvents {
            public const string Clone = "Clone";
            public const string Load = "Load";
            public const string Run = "Run";
            public const string Delete = "Delete";
            public const string Update = "Update";
            public const string AddToProject = "AddToProject";
        }
    }
}
