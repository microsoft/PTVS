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

    internal static class PylanceImportFormat {
        public const string Absolute = "absolute";
        public const string Relative = "relative";
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
                public string[] typeshedPaths { get; set; }

                /// <summary>
                /// Path to directory containing custom type stub files.
                /// </summary>
                public string stubPath { get; set; }

                /// <summary>
                /// Allows a user to override the severity levels for individual diagnostics.
                /// Typically specified in mspythonconfig.json.
                /// </summary>
                public Dictionary<string, string> diagnosticSeverityOverrides { get; set; }

                /// <summary>
                /// Analyzes and reports errors on only open files or the entire workspace.
                /// "enum": ["openFilesOnly", "workspace"]
                /// </summary>
                public string diagnosticMode { get; set; }

                /// <summary>
                /// Specifies the level of logging for the Output panel.
                /// "enum": ["Error", "Warning", "Information", "Trace"]
                /// </summary>
                public string logLevel { get; set; }

                /// <summary>
                /// Automatically add common search paths like 'src'.
                /// </summary>
                public bool? autoSearchPaths { get; set; }

                /// <summary>
                /// Defines the default rule set for type checking.
                /// </summary>
                public string typeCheckingMode { get; set; }

                /// <summary>
                /// Use library implementations to extract type information when type stub is not present.
                /// </summary>
                public bool? useLibraryCodeForTypes { get; set; }

                /// <summary>
                /// Additional import search resolution paths.
                /// </summary>
                public string[] extraPaths { get; set; }

                /// <summary>
                /// Automatically add brackets for functions.
                /// </summary>
                public bool completeFunctionParens { get; set; }

                /// <summary>
                /// Offer auto-import completions.
                /// </summary>
                public bool autoImportCompletions { get; set; }

                /// <summary>
                /// Index installed third party libraries and user files for language features such as auto-import, add import, workspace symbols and etc.
                /// </summary>
                public bool? indexing { get; set; }

                /// <summary>
                /// Allow using '.', '(' as commit characters when applicable.
                /// </summary>
                public bool? extraCommitChars { get; set; }

                public PythonAnalysisInlayHintsSettings inlayHints { get; set; }

                public string importFormat { get; set; }

                /// <summary>
                /// Tokens that identify comments that should show up in the task list pane
                /// </summary>
                public TaskListToken[] taskListTokens { get; set; }

                public class PythonAnalysisInlayHintsSettings {

                    /// <summary>
                    /// Enable/disable inlay hints for variable types:\n```python\nfoo ' :list[str] ' = [\"a\"]\n \n```\n
                    /// </summary>
                    public bool variableTypes { get; set; }

                    /// <summary>
                    /// Enable/disable inlay hints for function return types:\n```python\ndef foo(x:int) ' -> int ':\n\treturn x\n```\n"
                    /// </summary>
                    public bool functionReturnTypes { get; set; }
                }

                public class TaskListToken {

                    /// <summary>
                    /// The text of the token.
                    /// </summary>
                    public string text { get; set; }

                    /// <summary>
                    /// The priority of the token.
                    /// This comes from the CommentTaskPriority enum in Microsoft.VisualStudio.Shell
                    /// </summary>
                    public string priority { get; set; }
                }

            }
            /// <summary>
            /// Analysis settings.
            /// </summary>
            public PythonAnalysisSettings analysis { get; set; }

            /// <summary>
            /// Path to Python, you can use a custom version of Python.
            /// </summary>
            public string pythonPath { get; set; }

            /// <summary>
            /// Path to folder with a list of Virtual Environments.
            /// </summary>
            public string venvPath { get; set; }
        }
        /// <summary>
        /// Python section.
        /// </summary>
        public PythonSettings python { get; set; }
    }
}
