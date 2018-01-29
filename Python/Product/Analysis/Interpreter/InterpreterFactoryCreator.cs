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
using System.IO;
using System.Text;
using Microsoft.PythonTools.Analysis.Infrastructure;

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
        public static IPythonInterpreterFactory CreateInterpreterFactory(
            InterpreterConfiguration configuration,
            InterpreterFactoryCreationOptions options = null
        ) {
            options = options?.Clone() ?? new InterpreterFactoryCreationOptions();

            return new Ast.AstPythonInterpreterFactory(configuration, options);
        }

        /// <summary>
        /// Creates a new interpreter factory with the default database. This
        /// factory is suitable for analysis, but not execution.
        /// </summary>
        public static IPythonInterpreterFactory CreateAnalysisInterpreterFactory(
            Version languageVersion,
            string description = null,
            IEnumerable<string> searchPaths = null
        ) {
            var config = new InterpreterConfiguration(
                "AnalysisOnly|{0}".FormatInvariant(languageVersion),
                description ?? "Analysis Only {0}".FormatUI(languageVersion),
                version: languageVersion
            );
            config.SearchPaths.AddRange(searchPaths.MaybeEnumerate());

            var opts = new InterpreterFactoryCreationOptions {
                WatchFileSystem = false
            };

            return CreateInterpreterFactory(config, opts);
        }
    }
}
