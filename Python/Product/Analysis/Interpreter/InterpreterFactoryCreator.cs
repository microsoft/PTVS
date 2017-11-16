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
using System.Text;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter.Default;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Provides a factory for creating a Python interpreter factory based on an
    /// executable file and cached completion database.
    /// </summary>
    public static class InterpreterFactoryCreator {
        /// <summary>
        /// Creates a new interpreter factory with the specified options. This
        /// interpreter always includes a cached completion database.
        /// </summary>
        public static IPythonInterpreterFactory CreateInterpreterFactory(InterpreterConfiguration configuration, InterpreterFactoryCreationOptions options = null) {
            options = options?.Clone() ?? new InterpreterFactoryCreationOptions();

            if (string.IsNullOrEmpty(options.DatabasePath)) {
                options.DatabasePath = Path.Combine(
                    PythonTypeDatabase.CompletionDatabasePath,
                    GetRelativePathForConfigurationId(configuration.Id)
                );
            }

            if (options.NoDatabase) {
                // Use the (currenty experimental) non-database backed factory
                var fact = new Ast.AstPythonInterpreterFactory(configuration, options);
                return fact;
            } else {
                // Use the database-backed factory
                var fact = new CPythonInterpreterFactory(configuration, options);
                if (options.WatchFileSystem) {
                    fact.BeginRefreshIsCurrent();
                }
                return fact;
            }
        }

        /// <summary>
        /// Returns a relative path string based on the provided ID. There is no
        /// guarantee that the path is human readable or that it is used by all
        /// components.
        /// </summary>
        public static string GetRelativePathForConfigurationId(string id) {
            var subpath = id.Replace('|', '\\');
            if (!PathUtils.IsValidPath(subpath)) {
                subpath = Convert.ToBase64String(new UTF8Encoding(false).GetBytes(id));
            }
            return subpath;
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
