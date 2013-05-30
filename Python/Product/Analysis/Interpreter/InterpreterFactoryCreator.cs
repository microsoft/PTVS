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

using System;
using System.Reflection;
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
        public static IPythonInterpreterFactory CreateInterpreterFactory(InterpreterFactoryCreationOptions options) {
            return new CPythonInterpreterFactory(
                options.LanguageVersion ?? new Version(2, 7),
                (options.Id == default(Guid)) ? Guid.NewGuid() : options.Id,
                options.Description ?? string.Empty,
                options.InterpreterPath ?? string.Empty,
                options.WindowInterpreterPath ?? string.Empty,
                options.LibraryPath ?? string.Empty,
                options.PathEnvironmentVariableName ?? "PYTHONPATH",
                options.Architecture,
                options.WatchLibraryForNewModules
            );
        }

        /// <summary>
        /// Creates a new interpreter factory with the specified database. This
        /// factory is suitable for analysis, but not execution.
        /// </summary>
        public static IPythonInterpreterFactory CreateAnalysisInterpreterFactory(
            Version languageVersion,
            PythonTypeDatabase database) {
            return new AnalysisOnlyInterpreterFactory(languageVersion, database);
        }

        /// <summary>
        /// Creates a new interpreter factory with the specified database path.
        /// This factory is suitable for analysis, but not execution.
        /// </summary>
        public static IPythonInterpreterFactory CreateAnalysisInterpreterFactory(
            Version languageVersion,
            string databasePath) {
            return new AnalysisOnlyInterpreterFactory(languageVersion, databasePath);
        }

        /// <summary>
        /// Creates a new interpreter factory with the default database. This
        /// factory is suitable for analysis, but not execution.
        /// </summary>
        /// <param name="languageVersion"></param>
        /// <returns></returns>
        public static IPythonInterpreterFactory CreateAnalysisInterpreterFactory(Version languageVersion) {
            return new AnalysisOnlyInterpreterFactory(languageVersion);
        }
    }
}
