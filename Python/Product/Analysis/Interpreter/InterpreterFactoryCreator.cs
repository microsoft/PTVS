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
using System.IO;
using System.Reflection;
using Microsoft.PythonTools.Interpreter.Default;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Provides a factory for creating a Python interpreter factory based on an
    /// executable file and cached completion database.
    /// </summary>
    public static class InterpreterFactoryCreator {
        public static readonly Guid AnalysisOnlyFactoryGuid = new Guid("{3E2D2684-58F3-47E2-B121-58A602EA2382}");

        /// <summary>
        /// Creates a new interpreter factory with the specified options. This
        /// interpreter always includes a cached completion database.
        /// </summary>
        public static PythonInterpreterFactoryWithDatabase CreateInterpreterFactory(InterpreterFactoryCreationOptions options) {
            var ver = options.LanguageVersion ?? new Version(2, 7);
            var description = options.Description ?? string.Format("Unknown Python {0}", ver);
            var prefixPath = options.PrefixPath;
            if (string.IsNullOrEmpty(prefixPath) && !string.IsNullOrEmpty(options.InterpreterPath)) {
                prefixPath = Path.GetDirectoryName(options.InterpreterPath);
            }

            return new CPythonInterpreterFactory(
                ver,
                options.Id,
                description,
                prefixPath ?? string.Empty,
                options.InterpreterPath ?? string.Empty,
                options.WindowInterpreterPath ?? string.Empty,
                options.LibraryPath ?? string.Empty,
                options.PathEnvironmentVariableName ?? "PYTHONPATH",
                options.Architecture,
                options.WatchLibraryForNewModules
            );
        }

        public static PythonInterpreterFactoryWithDatabase CreateInterpreterFactory(InterpreterConfiguration configuration, InterpreterFactoryCreationOptions options) {
            return new CPythonInterpreterFactory(configuration, options);
        }

        /// <summary>
        /// Creates a new interpreter factory with the specified database. This
        /// factory is suitable for analysis, but not execution.
        /// </summary>
        public static PythonInterpreterFactoryWithDatabase CreateAnalysisInterpreterFactory(
            Version languageVersion,
            PythonTypeDatabase database) {
            return new AnalysisOnlyInterpreterFactory(languageVersion, database);
        }

        /// <summary>
        /// Creates a new interpreter factory with the specified database path.
        /// This factory is suitable for analysis, but not execution.
        /// </summary>
        public static PythonInterpreterFactoryWithDatabase CreateAnalysisInterpreterFactory(
            Version languageVersion,
            string description,
            params string[] databasePaths) {
            return new AnalysisOnlyInterpreterFactory(languageVersion, databasePaths);
        }

        /// <summary>
        /// Creates a new interpreter factory with the default database. This
        /// factory is suitable for analysis, but not execution.
        /// </summary>
        public static PythonInterpreterFactoryWithDatabase CreateAnalysisInterpreterFactory(
            Version languageVersion,
            string description = null) {
            return new AnalysisOnlyInterpreterFactory(languageVersion, description);
        }
    }
}
