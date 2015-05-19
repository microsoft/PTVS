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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Interpreter {
    public static class PythonInterpreterFactoryExtensions {
        /// <summary>
        /// Executes the interpreter with the specified arguments. Any output is
        /// captured and returned via the <see cref="ProcessOutput"/> object.
        /// </summary>
        internal static ProcessOutput Run(
            this IPythonInterpreterFactory factory,
            params string[] arguments) {
            return ProcessOutput.RunHiddenAndCapture(factory.Configuration.InterpreterPath, arguments);
        }

        /// <summary>
        /// Determines whether two interpreter factories are equivalent.
        /// </summary>
        public static bool IsEqual(this IPythonInterpreterFactory x, IPythonInterpreterFactory y) {
            if (x == null || y == null) {
                return x == null && y == null;
            }
            if (x.GetType() != y.GetType()) {
                return false;
            }

            return x.Id == y.Id && 
                x.Description == y.Description &&
                x.Configuration.Equals(y.Configuration);
        }

        /// <summary>
        /// Determines whether the interpreter factory contains the specified
        /// modules.
        /// </summary>
        /// <returns>The names of the modules that were found.</returns>
        public static async Task<HashSet<string>> FindModulesAsync(this IPythonInterpreterFactory factory, params string[] moduleNames) {
            var withDb = factory as PythonInterpreterFactoryWithDatabase;
            if (withDb != null && withDb.IsCurrent) {
                var db = withDb.GetCurrentDatabase();
                var set = new HashSet<string>(moduleNames.Where(m => db.GetModule(m) != null));
                return set;
            }

            var expected = new HashSet<string>(moduleNames);

            if (withDb != null) {
                var paths = PythonTypeDatabase.GetCachedDatabaseSearchPaths(withDb.DatabasePath) ??
                    await PythonTypeDatabase.GetUncachedDatabaseSearchPathsAsync(withDb.Configuration.InterpreterPath).ConfigureAwait(false);
                var db = PythonTypeDatabase.GetDatabaseExpectedModules(withDb.Configuration.Version, paths)
                    .SelectMany()
                    .Select(g => g.ModuleName);
                expected.IntersectWith(db);
                return expected;
            }

            return await Task.Run(() => {
                var result = new HashSet<string>();
                foreach (var mp in ModulePath.GetModulesInLib(factory)) {
                    if (expected.Count == 0) {
                        break;
                    }

                    if (expected.Remove(mp.ModuleName)) {
                        result.Add(mp.ModuleName);
                    }
                }
                return result;
            });
        }

        /// <summary>
        /// Generates the completion database and returns a task that will
        /// complete when the database is regenerated.
        /// </summary>
        internal static Task<int> GenerateDatabaseAsync(
            this IPythonInterpreterFactoryWithDatabase factory,
            GenerateDatabaseOptions options
        ) {
            var tcs = new TaskCompletionSource<int>();
            factory.GenerateDatabase(options, tcs.SetResult);
            return tcs.Task;
        }

        /// <summary>
        /// Returns <c>true</c> if the factory should appear in the UI.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool IsUIVisible(this IPythonInterpreterFactory factory) {
            return factory != null &&
                factory.Configuration != null &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.Hidden);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory can ever be the default
        /// interpreter.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool CanBeDefault(this IPythonInterpreterFactory factory) {
            return factory != null &&
                factory.Configuration != null &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.CannotBeDefault);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory can be automatically selected as
        /// the default interpreter.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool CanBeAutoDefault(this IPythonInterpreterFactory factory) {
            return factory != null &&
                factory.Configuration != null &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.CannotBeDefault) &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.CannotBeAutoDefault);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory can be configured.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool CanBeConfigured(this IPythonInterpreterFactory factory) {
            return factory != null &&
                factory.Configuration != null &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.CannotBeConfigured);
        }

    }
}
