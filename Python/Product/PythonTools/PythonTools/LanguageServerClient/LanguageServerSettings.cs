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

namespace Microsoft.PythonTools.LanguageServerClient {
    internal static class PylanceDiagnosticMode {
        public const string OpenFilesOnly = "openFilesOnly";
        public const string Workspace = "workspace";
    }

    internal static class PylanceLogLevel {
        public const string Error = "Error";
        public const string Warning = "Warning";
        public const string Information = "Information";
        public const string Trace = "Trace";
    }

    internal static class PylanceTypeCheckingMode {
        public const string Off = "off";
        public const string Basic = "basic";
        public const string Strict = "strict";
    }

    [Serializable]
    internal sealed class LanguageServerSettings {
        [Serializable]
        public class PythonSettings {
            /// <summary>
            /// Python settings. Match [python] section in Pylance.
            /// </summary>
            [Serializable]
            public class PythonAnalysisSettings {
                /// <summary>
                /// Paths to look for typeshed modules.
                /// </summary>
                public string[] typeshedPaths;

                /// <summary>
                /// Path to directory containing custom type stub files.
                /// </summary>
                public string stubPath;

                /// <summary>
                /// Allows a user to override the severity levels for individual diagnostics.
                /// Typically specified in mspythonconfig.json.
                /// </summary>
                public Dictionary<string, string> diagnosticSeverityOverrides;

                /// <summary>
                /// Analyzes and reports errors on only open files or the entire workspace.
                /// "enum": ["openFilesOnly", "workspace"]
                /// </summary>
                public string diagnosticMode;

                /// <summary>
                /// Specifies the level of logging for the Output panel.
                /// "enum": ["Error", "Warning", "Information", "Trace"]
                /// </summary>
                public string logLevel;

                /// <summary>
                /// Automatically add common search paths like 'src'.
                /// </summary>
                public bool? autoSearchPaths;

                /// <summary>
                /// Defines the default rule set for type checking.
                /// </summary>
                public string typeCheckingMode;

                /// <summary>
                /// Use library implementations to extract type information when type stub is not present.
                /// </summary>
                public bool? useLibraryCodeForTypes;

                /// <summary>
                /// Additional import search resolution paths.
                /// </summary>
                public string[] extraPaths;

                /// <summary>
                /// Automatically add brackets for functions.
                /// </summary>
                public bool completeFunctionParens;

                /// <summary>
                /// Offer auto-import completions.
                /// </summary>
                public bool autoImportCompletions;
            }
            /// <summary>
            /// Analysis settings.
            /// </summary>
            public PythonAnalysisSettings analysis;

            /// <summary>
            /// Path to Python, you can use a custom version of Python.
            /// </summary>
            public string pythonPath;

            /// <summary>
            /// Path to folder with a list of Virtual Environments.
            /// </summary>
            public string venvPath;
        }
        /// <summary>
        /// Python section.
        /// </summary>
        public PythonSettings python;
    }
}
